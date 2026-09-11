using System;
using NUnit.Framework;
using RobotArena.Platform;

namespace RobotArena.Session.Tests
{
    public sealed class PlatformServicesAdapterTests
    {
        [Test]
        public void Exposes_backend_snapshot_and_forwards_pause_events()
        {
            var backend = new FakeBackend(
                PlatformServicesStatus.Ready,
                sdkInitialized: true,
                environment: "production",
                language: "ru");
            var adapter = new PlatformServicesAdapter(backend);
            bool? pauseState = null;
            adapter.PlatformPauseChanged += isPaused => pauseState = isPaused;

            backend.PublishPause(true);

            Assert.That(adapter.Current.Status, Is.EqualTo(PlatformServicesStatus.Ready));
            Assert.That(adapter.Current.Environment, Is.EqualTo("production"));
            Assert.That(adapter.Current.Language, Is.EqualTo("ru"));
            Assert.That(pauseState, Is.True);
        }

        [Test]
        public void Marks_interactive_ready_once_after_sdk_becomes_ready()
        {
            var backend = new FakeBackend(
                PlatformServicesStatus.WaitingForSdk,
                sdkInitialized: false,
                environment: string.Empty,
                language: string.Empty);
            var adapter = new PlatformServicesAdapter(backend);

            adapter.MarkInteractiveReady();
            backend.PublishSnapshot(backend.Snapshot.WithSdkReady("production", "en", true));
            adapter.MarkInteractiveReady();

            Assert.That(backend.MarkGameReadyCallCount, Is.EqualTo(1));
            Assert.That(
                adapter.Current.GameReadyStatus,
                Is.EqualTo(PlatformGameReadyStatus.Requested));
            Assert.That(adapter.Current.GameReady, Is.False);
            Assert.That(
                adapter.Current.LoadingApiStatus,
                Is.EqualTo(PlatformCapabilityStatus.Available));
        }

        [Test]
        public void Keeps_capabilities_unknown_until_backend_reports_a_result()
        {
            var backend = new FakeBackend(
                PlatformServicesStatus.WaitingForSdk,
                sdkInitialized: false,
                environment: string.Empty,
                language: string.Empty);
            var adapter = new PlatformServicesAdapter(backend);

            backend.PublishSnapshot(backend.Snapshot.WithSdkReady("production", "en"));

            Assert.That(adapter.Current.LoadingApiStatus, Is.EqualTo(PlatformCapabilityStatus.Unknown));
            Assert.That(adapter.Current.PlayerDataStatus, Is.EqualTo(PlatformCapabilityStatus.Unavailable));

            backend.PublishSnapshot(backend.Snapshot.WithCapabilities(
                PlatformCapabilityStatus.Available,
                PlatformCapabilityStatus.Available,
                PlatformCapabilityStatus.Unavailable,
                PlatformCapabilityStatus.Available,
                PlatformCapabilityStatus.Unavailable));

            Assert.That(adapter.Current.LoadingApiStatus, Is.EqualTo(PlatformCapabilityStatus.Available));
            Assert.That(adapter.Current.SupportsLeaderboard, Is.True);
            Assert.That(adapter.Current.SupportsPlayerData, Is.False);
        }

        [Test]
        public void Does_not_report_game_ready_success_when_loading_api_is_unavailable()
        {
            var backend = new FakeBackend(
                PlatformServicesStatus.Ready,
                sdkInitialized: true,
                environment: "production",
                language: "en");
            var adapter = new PlatformServicesAdapter(backend);

            backend.PublishSnapshot(backend.Snapshot.WithSdkReady("production", "en", false));
            adapter.MarkInteractiveReady();

            Assert.That(backend.MarkGameReadyCallCount, Is.Zero);
            Assert.That(
                adapter.Current.GameReadyStatus,
                Is.EqualTo(PlatformGameReadyStatus.NotRequested));
            Assert.That(adapter.Current.GameReady, Is.False);
        }

        [Test]
        public void Does_not_request_game_ready_after_backend_reports_failure()
        {
            var backend = new FakeBackend(
                PlatformServicesStatus.Ready,
                sdkInitialized: true,
                environment: "production",
                language: "en");
            var adapter = new PlatformServicesAdapter(backend);

            backend.PublishSnapshot(backend.Snapshot.WithGameReadyFailed("Loading API unavailable."));
            adapter.MarkInteractiveReady();

            Assert.That(backend.MarkGameReadyCallCount, Is.Zero);
            Assert.That(adapter.Current.GameReadyStatus, Is.EqualTo(PlatformGameReadyStatus.Failed));
            Assert.That(adapter.Current.FailureReason, Is.EqualTo("Loading API unavailable."));
        }

        [Test]
        public void Detaches_from_backend_when_disposed()
        {
            var backend = new FakeBackend(
                PlatformServicesStatus.Ready,
                sdkInitialized: true,
                environment: "production",
                language: "en");
            var adapter = new PlatformServicesAdapter(backend);
            int snapshotCount = 0;
            adapter.SnapshotChanged += _ => snapshotCount++;

            adapter.Dispose();
            backend.PublishSnapshot(backend.Snapshot.WithSdkReady("production", "ru"));

            Assert.That(snapshotCount, Is.Zero);
        }

        [Test]
        public void Keeps_guest_mode_when_backend_is_unavailable()
        {
            var backend = new FakeBackend(
                PlatformServicesStatus.Unavailable,
                sdkInitialized: false,
                environment: string.Empty,
                language: string.Empty);
            var adapter = new PlatformServicesAdapter(backend);

            adapter.MarkInteractiveReady();

            Assert.That(adapter.Current.Status, Is.EqualTo(PlatformServicesStatus.Unavailable));
            Assert.That(backend.MarkGameReadyCallCount, Is.Zero);
        }

        [Test]
        public void Preserves_terminal_guest_states_without_requesting_game_ready()
        {
            foreach (PlatformServicesStatus terminalStatus in new[]
                     {
                         PlatformServicesStatus.TimedOut,
                         PlatformServicesStatus.Failed,
                         PlatformServicesStatus.Unavailable
                     })
            {
                var backend = new FakeBackend(
                    PlatformServicesStatus.WaitingForSdk,
                    sdkInitialized: false,
                    environment: string.Empty,
                    language: string.Empty);
                var adapter = new PlatformServicesAdapter(backend);

                backend.PublishSnapshot(new PlatformServicesSnapshot(
                    terminalStatus,
                    sdkDetected: false,
                    sdkInitialized: false,
                    environment: string.Empty,
                    language: string.Empty,
                    loadingApiStatus: PlatformCapabilityStatus.Unknown,
                    pauseStatus: PlatformCapabilityStatus.Unavailable,
                    playerDataStatus: PlatformCapabilityStatus.Unavailable,
                    leaderboardStatus: PlatformCapabilityStatus.Unavailable,
                    fullscreenAdsStatus: PlatformCapabilityStatus.Unavailable,
                    gameReadyStatus: PlatformGameReadyStatus.NotRequested,
                    failureReason: "synthetic terminal state"));

                adapter.MarkInteractiveReady();

                Assert.That(adapter.Current.Status, Is.EqualTo(terminalStatus));
                Assert.That(backend.MarkGameReadyCallCount, Is.Zero);
            }
        }

        [Test]
        public void Forwards_repeated_platform_pause_cycles_in_order()
        {
            var backend = new FakeBackend(
                PlatformServicesStatus.Ready,
                sdkInitialized: true,
                environment: "production",
                language: "en");
            var adapter = new PlatformServicesAdapter(backend);
            var pauseStates = new System.Collections.Generic.List<bool>();
            adapter.PlatformPauseChanged += pauseStates.Add;

            backend.PublishPause(true);
            backend.PublishPause(false);
            backend.PublishPause(true);
            backend.PublishPause(false);

            Assert.That(pauseStates, Is.EqualTo(new[] { true, false, true, false }));
        }

        [Test]
        public void Maps_platform_pause_to_pause_coordinator_source()
        {
            var backend = new FakeBackend(
                PlatformServicesStatus.Ready,
                sdkInitialized: true,
                environment: "production",
                language: "en");
            var adapter = new PlatformServicesAdapter(backend);
            var pauseCoordinator = new PauseCoordinator();
            adapter.PlatformPauseChanged += isPaused =>
                pauseCoordinator.SetSource(PauseSource.Platform, isPaused);

            backend.PublishPause(true);
            Assert.That(pauseCoordinator.ActiveSources, Is.EqualTo(PauseSource.Platform));

            backend.PublishPause(false);
            Assert.That(pauseCoordinator.ActiveSources, Is.EqualTo(PauseSource.None));
        }

        private sealed class FakeBackend : IPlatformServicesBackend
        {
            public FakeBackend(
                PlatformServicesStatus status,
                bool sdkInitialized,
                string environment,
                string language)
            {
                Snapshot = new PlatformServicesSnapshot(
                    status,
                    sdkDetected: sdkInitialized,
                    sdkInitialized,
                    environment,
                    language,
                    hasLoadingApi: sdkInitialized,
                    supportsPause: sdkInitialized,
                    supportsPlayerData: false,
                    supportsLeaderboard: false,
                    supportsFullscreenAds: false,
                    gameReady: false,
                    failureReason: string.Empty);
            }

            public event Action<PlatformServicesSnapshot> SnapshotChanged;
            public event Action<bool> PlatformPauseChanged;

            public PlatformServicesSnapshot Snapshot { get; private set; }

            public int MarkGameReadyCallCount { get; private set; }

            public void MarkGameReady()
            {
                MarkGameReadyCallCount++;
                Snapshot = Snapshot.LoadingApiStatus == PlatformCapabilityStatus.Unavailable
                    ? Snapshot.WithGameReadyUnavailable("Loading API unavailable.")
                    : Snapshot.WithGameReadyRequested();
                SnapshotChanged?.Invoke(Snapshot);
            }

            public void Tick(float unscaledTime)
            {
            }

            public void PublishPause(bool isPaused)
            {
                PlatformPauseChanged?.Invoke(isPaused);
            }

            public void PublishSnapshot(PlatformServicesSnapshot snapshot)
            {
                Snapshot = snapshot;
                SnapshotChanged?.Invoke(snapshot);
            }

            public void Dispose()
            {
                SnapshotChanged = null;
                PlatformPauseChanged = null;
            }
        }
    }
}
