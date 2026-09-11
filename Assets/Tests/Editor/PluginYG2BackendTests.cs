using System;
using System.Collections.Generic;
using NUnit.Framework;
using RobotArena.Platform;

namespace RobotArena.Session.Tests
{
    public sealed class PluginYG2BackendTests
    {
        [Test]
        public void Publishes_one_ready_snapshot_for_duplicate_ready_and_sdk_callbacks()
        {
            var source = new ControlledRuntimeSource
            {
                Environment = "production",
                Language = "ru"
            };
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var snapshots = new List<PlatformServicesSnapshot>();
            backend.SnapshotChanged += snapshots.Add;

            source.PublishState(CreateReadyState());
            source.PublishState(CreateReadyState());
            source.PublishSdkData();

            Assert.That(snapshots, Has.Count.EqualTo(1));
            Assert.That(backend.Snapshot.Status, Is.EqualTo(PlatformServicesStatus.Ready));
            Assert.That(backend.Snapshot.Environment, Is.EqualTo("production"));
            Assert.That(backend.Snapshot.Language, Is.EqualTo("ru"));
            Assert.That(
                backend.Snapshot.LoadingApiStatus,
                Is.EqualTo(PlatformCapabilityStatus.Available));

            backend.Dispose();
        }

        [Test]
        public void Keeps_sdk_callback_first_then_ready_state_idempotent()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var snapshots = new List<PlatformServicesSnapshot>();
            backend.SnapshotChanged += snapshots.Add;

            source.PublishSdkData();
            source.PublishState(new RobotArenaPluginYG2RuntimeState
            {
                initState = "ready"
            });

            Assert.That(backend.Snapshot.Status, Is.EqualTo(PlatformServicesStatus.Ready));
            Assert.That(snapshots, Has.Count.EqualTo(1));

            backend.Dispose();
        }

        [Test]
        public void Keeps_timeout_terminal_when_ready_and_pause_callbacks_arrive_late()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var pauseStates = new List<bool>();
            backend.PlatformPauseChanged += pauseStates.Add;

            backend.Tick(8.1f);
            source.PublishState(CreateReadyState());
            source.PublishSdkData();
            source.PublishPause(true);

            Assert.That(backend.Snapshot.Status, Is.EqualTo(PlatformServicesStatus.TimedOut));
            Assert.That(pauseStates, Is.Empty);

            backend.Dispose();
        }

        [TestCase("failed", PlatformServicesStatus.Failed)]
        [TestCase("local", PlatformServicesStatus.Unavailable)]
        public void Keeps_non_ready_terminal_state_when_ready_callback_arrives_late(
            string terminalRuntimeState,
            PlatformServicesStatus expectedStatus)
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);

            source.PublishState(new RobotArenaPluginYG2RuntimeState
            {
                initState = terminalRuntimeState,
                failureReason = "controlled terminal state"
            });
            source.PublishState(CreateReadyState());
            source.PublishSdkData();

            Assert.That(backend.Snapshot.Status, Is.EqualTo(expectedStatus));
            Assert.That(backend.Snapshot.FailureReason, Is.EqualTo(
                terminalRuntimeState == "local"
                    ? "PluginYG2 local host mode is using the guest path."
                    : "controlled terminal state"));

            backend.Dispose();
        }

        [Test]
        public void Forwards_only_real_platform_pause_transitions_and_unsubscribes_on_dispose()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var snapshots = new List<PlatformServicesSnapshot>();
            var pauseStates = new List<bool>();
            backend.SnapshotChanged += snapshots.Add;
            backend.PlatformPauseChanged += pauseStates.Add;
            source.PublishState(CreateReadyState());

            source.PublishPause(true);
            source.PublishPause(true);
            source.PublishPause(false);
            backend.Dispose();
            source.PublishPause(true);
            source.PublishState(CreateReadyState());
            source.PublishSdkData();

            Assert.That(pauseStates, Is.EqualTo(new[] { true, false }));
            Assert.That(snapshots, Has.Count.EqualTo(1));
            Assert.That(source.DisposeCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Keeps_first_terminal_failure_when_duplicate_terminal_callbacks_arrive()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var snapshots = new List<PlatformServicesSnapshot>();
            backend.SnapshotChanged += snapshots.Add;

            source.PublishState(new RobotArenaPluginYG2RuntimeState
            {
                initState = "failed",
                failureReason = "first failure"
            });
            source.PublishState(new RobotArenaPluginYG2RuntimeState
            {
                initState = "failed",
                failureReason = "second failure"
            });
            source.PublishSdkData();

            Assert.That(backend.Snapshot.Status, Is.EqualTo(PlatformServicesStatus.Failed));
            Assert.That(backend.Snapshot.FailureReason, Is.EqualTo("first failure"));
            Assert.That(snapshots, Has.Count.EqualTo(1));

            backend.Dispose();
        }

        [Test]
        public void Sends_game_ready_through_the_adapter_and_consumes_backend_outcome()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var adapter = new PlatformServicesAdapter(backend);
            adapter.MarkInteractiveReady();
            backend.MarkGameReady();

            source.PublishState(new RobotArenaPluginYG2RuntimeState
            {
                initState = "ready",
                loadingApi = "available",
                gameReadyOutcome = "confirmed"
            });

            Assert.That(source.GameReadyCallCount, Is.EqualTo(1));
            Assert.That(
                adapter.Current.GameReadyStatus,
                Is.EqualTo(PlatformGameReadyStatus.Confirmed));
            Assert.That(adapter.Current.GameReady, Is.True);

            adapter.Dispose();
            backend.Dispose();
        }

        private static RobotArenaPluginYG2RuntimeState CreateReadyState()
        {
            return new RobotArenaPluginYG2RuntimeState
            {
                initState = "ready",
                loadingApi = "available",
                playerData = "unavailable",
                leaderboard = "available",
                fullscreenAds = "unavailable"
            };
        }

        private sealed class ControlledRuntimeSource : IRobotArenaPluginYG2RuntimeSource
        {
            public event Action SdkDataReady;
            public event Action<RobotArenaPluginYG2RuntimeState> StateChanged;
            public event Action<bool> PlatformPauseChanged;

            public bool IsAvailable => true;
            public bool IsSdkReady => false;
            public string Environment { get; set; } = "production";
            public string Language { get; set; } = "en";
            public int GameReadyCallCount { get; private set; }
            public int DisposeCallCount { get; private set; }

            public void PublishSdkData()
            {
                SdkDataReady?.Invoke();
            }

            public void PublishState(RobotArenaPluginYG2RuntimeState state)
            {
                StateChanged?.Invoke(state);
            }

            public void PublishPause(bool isPaused)
            {
                PlatformPauseChanged?.Invoke(isPaused);
            }

            public void SendGameReady()
            {
                GameReadyCallCount++;
            }

            public void Dispose()
            {
                DisposeCallCount++;
            }
        }
    }
}
