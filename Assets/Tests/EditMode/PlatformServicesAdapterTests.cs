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
            backend.PublishSnapshot(backend.Snapshot.WithSdkReady("production", "en"));
            adapter.MarkInteractiveReady();

            Assert.That(backend.MarkGameReadyCallCount, Is.EqualTo(1));
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
                Snapshot = Snapshot.WithGameReady();
                SnapshotChanged?.Invoke(Snapshot);
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
        }
    }
}
