using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RobotArena.Platform;
using UnityEngine;
using UnityEngine.TestTools;

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
            source.PublishState(CreateReadyState());

            Assert.That(backend.Snapshot.Status, Is.EqualTo(PlatformServicesStatus.Ready));
            Assert.That(
                backend.Snapshot.LoadingApiStatus,
                Is.EqualTo(PlatformCapabilityStatus.Available));
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
            source.PublishPause(CreatePlatformPause(true));

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

            source.PublishPause(CreatePlatformPause(true));
            source.PublishPause(CreatePlatformPause(true));
            source.PublishPause(CreatePlatformPause(false));
            backend.Dispose();
            source.PublishPause(CreatePlatformPause(true));
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
        public void Ignores_non_yandex_pause_origin()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var pauseStates = new List<bool>();
            backend.PlatformPauseChanged += pauseStates.Add;
            source.PublishState(CreateReadyState());

            source.PublishPause(
                new RobotArenaPluginYG2PlatformPauseEvent("plugin-internal", true));

            Assert.That(pauseStates, Is.Empty);

            backend.Dispose();
        }

        [Test]
        public void Runtime_pause_channel_updates_and_requires_yandex_origin_token()
        {
            Type channelType = typeof(RobotArenaPluginYG2PlatformPauseEvent).Assembly.GetType(
                "RobotArena.Platform.RobotArenaPluginYG2RuntimeChannel");
            Assert.That(channelType, Is.Not.Null);

            MethodInfo publishState = channelType.GetMethod(
                "PublishStateJson",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo publishPause = channelType.GetMethod(
                "PublishYandexLifecyclePause",
                BindingFlags.Static | BindingFlags.NonPublic);
            EventInfo pauseEvent = channelType.GetEvent(
                "PlatformPauseChanged",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(publishState, Is.Not.Null);
            Assert.That(publishPause, Is.Not.Null);
            Assert.That(pauseEvent, Is.Not.Null);

            var received = new List<RobotArenaPluginYG2PlatformPauseEvent>();
            Action<RobotArenaPluginYG2PlatformPauseEvent> handler = received.Add;
            pauseEvent.GetAddMethod(nonPublic: true).Invoke(null, new object[] { handler });
            try
            {
                publishState.Invoke(
                    null,
                    new object[]
                    {
                        "{\"initState\":\"failed\",\"lifecycleToken\":\"controlled-token\"}"
                    });
                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("\\[RobotArena\\.PluginYG2\\.Transport\\] invalid platform pause payload:.*"));
                publishPause.Invoke(
                    null,
                    new object[]
                    {
                        "{\"source\":\"yandex-lifecycle\",\"state\":\"paused\","
                        + "\"token\":\"wrong-token\"}"
                    });
                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("\\[RobotArena\\.PluginYG2\\.Transport\\] invalid platform pause payload:.*"));
                publishPause.Invoke(
                    null,
                    new object[]
                    {
                        "{\"source\":\"plugin-internal\",\"state\":\"paused\","
                        + "\"token\":\"controlled-token\"}"
                    });
                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("\\[RobotArena\\.PluginYG2\\.Transport\\] invalid platform pause payload:.*"));
                publishPause.Invoke(null, new object[] { "malformed" });
                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("\\[RobotArena\\.PluginYG2\\.Transport\\] invalid platform pause payload:.*"));
                publishPause.Invoke(
                    null,
                    new object[]
                    {
                        "{\"source\":\"yandex-lifecycle\",\"state\":\"paused\"}"
                    });

                Assert.That(received, Is.Empty);

                publishState.Invoke(
                    null,
                    new object[]
                    {
                        "{\"initState\":\"failed\",\"lifecycleToken\":\"updated-token\"}"
                    });
                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("\\[RobotArena\\.PluginYG2\\.Transport\\] invalid platform pause payload:.*"));
                publishPause.Invoke(
                    null,
                    new object[]
                    {
                        "{\"source\":\"yandex-lifecycle\",\"state\":\"paused\","
                        + "\"token\":\"controlled-token\"}"
                    });

                publishPause.Invoke(
                    null,
                    new object[]
                    {
                        "{\"source\":\"yandex-lifecycle\",\"state\":\"paused\","
                        + "\"token\":\"updated-token\"}"
                    });

                Assert.That(received, Has.Count.EqualTo(1));
                Assert.That(received[0].IsYandexLifecycle, Is.True);
                Assert.That(received[0].IsPaused, Is.True);
            }
            finally
            {
                pauseEvent.GetRemoveMethod(nonPublic: true).Invoke(null, new object[] { handler });
            }
        }

        [Test]
        public void Sends_game_ready_through_the_adapter_and_consumes_backend_outcome()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var adapter = new PlatformServicesAdapter(backend);
            adapter.MarkInteractiveReady();

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

        [Test]
        public void Sends_game_ready_when_sdk_is_ready_before_interactive_menu()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var adapter = new PlatformServicesAdapter(backend);

            source.PublishState(CreateReadyState());
            Assert.That(source.GameReadyCallCount, Is.Zero);

            adapter.MarkInteractiveReady();

            Assert.That(source.GameReadyCallCount, Is.EqualTo(1));
            Assert.That(
                adapter.Current.GameReadyStatus,
                Is.EqualTo(PlatformGameReadyStatus.Requested));

            adapter.Dispose();
            backend.Dispose();
        }

        [Test]
        public void Sends_game_ready_when_interactive_menu_is_ready_before_sdk()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var adapter = new PlatformServicesAdapter(backend);

            adapter.MarkInteractiveReady();
            Assert.That(source.GameReadyCallCount, Is.Zero);

            source.PublishState(CreateReadyState());

            Assert.That(source.GameReadyCallCount, Is.EqualTo(1));
            Assert.That(
                adapter.Current.GameReadyStatus,
                Is.EqualTo(PlatformGameReadyStatus.Requested));

            adapter.Dispose();
            backend.Dispose();
        }

        [Test]
        public void Does_not_send_game_ready_when_loading_api_is_unavailable()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var adapter = new PlatformServicesAdapter(backend);
            adapter.MarkInteractiveReady();

            source.PublishState(new RobotArenaPluginYG2RuntimeState
            {
                initState = "ready",
                loadingApi = "unavailable"
            });

            Assert.That(source.GameReadyCallCount, Is.Zero);
            Assert.That(
                adapter.Current.GameReadyStatus,
                Is.EqualTo(PlatformGameReadyStatus.Unavailable));

            adapter.Dispose();
            backend.Dispose();
        }

        [Test]
        public void Converts_synchronous_game_ready_failure_to_failed_status()
        {
            var source = new ControlledRuntimeSource
            {
                ThrowOnGameReady = true
            };
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var adapter = new PlatformServicesAdapter(backend);
            adapter.MarkInteractiveReady();

            LogAssert.Expect(
                LogType.Error,
                "[RobotArena.Platform] PluginYG2 Game Ready failed; reason=controlled Game Ready failure");
            source.PublishState(CreateReadyState());

            Assert.That(source.GameReadyCallCount, Is.EqualTo(1));
            Assert.That(
                adapter.Current.GameReadyStatus,
                Is.EqualTo(PlatformGameReadyStatus.Failed));
            Assert.That(adapter.Current.FailureReason, Does.Contain("controlled Game Ready failure"));

            adapter.Dispose();
            backend.Dispose();
        }

        [Test]
        public void Converts_rejected_game_ready_outcome_to_failed_status_through_adapter()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var adapter = new PlatformServicesAdapter(backend);
            adapter.MarkInteractiveReady();

            LogAssert.Expect(
                LogType.Error,
                "[RobotArena.Platform] PluginYG2 Game Ready failed; reason=controlled Game Ready rejection");
            source.PublishState(new RobotArenaPluginYG2RuntimeState
            {
                initState = "ready",
                loadingApi = "available",
                gameReadyOutcome = "failed",
                gameReadyFailureReason = "controlled Game Ready rejection"
            });

            Assert.That(source.GameReadyCallCount, Is.EqualTo(1));
            Assert.That(
                adapter.Current.GameReadyStatus,
                Is.EqualTo(PlatformGameReadyStatus.Failed));
            Assert.That(
                adapter.Current.FailureReason,
                Is.EqualTo("controlled Game Ready rejection"));

            adapter.Dispose();
            backend.Dispose();
        }

        [Test]
        public void Keeps_void_game_ready_outcome_requested_and_does_not_retry()
        {
            var source = new ControlledRuntimeSource();
            var backend = new RobotArenaPluginYG2Backend(source, 0f);
            var adapter = new PlatformServicesAdapter(backend);
            adapter.MarkInteractiveReady();

            source.PublishState(CreateReadyState());
            adapter.MarkInteractiveReady();
            source.PublishState(CreateReadyState());

            Assert.That(source.GameReadyCallCount, Is.EqualTo(1));
            Assert.That(
                adapter.Current.GameReadyStatus,
                Is.EqualTo(PlatformGameReadyStatus.Requested));

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

        private static RobotArenaPluginYG2PlatformPauseEvent CreatePlatformPause(bool paused)
        {
            return new RobotArenaPluginYG2PlatformPauseEvent(
                RobotArenaPluginYG2PlatformPauseEvent.YandexLifecycleSource,
                paused);
        }

        private sealed class ControlledRuntimeSource : IRobotArenaPluginYG2RuntimeSource
        {
            public event Action SdkDataReady;
            public event Action<RobotArenaPluginYG2RuntimeState> StateChanged;
            public event Action<RobotArenaPluginYG2PlatformPauseEvent> PlatformPauseChanged;

            public bool IsAvailable => true;
            public bool IsSdkReady => false;
            public string Environment { get; set; } = "production";
            public string Language { get; set; } = "en";
            public int GameReadyCallCount { get; private set; }
            public int DisposeCallCount { get; private set; }
            public bool ThrowOnGameReady { get; set; }

            public void PublishSdkData()
            {
                SdkDataReady?.Invoke();
            }

            public void PublishState(RobotArenaPluginYG2RuntimeState state)
            {
                StateChanged?.Invoke(state);
            }

            public void PublishPause(RobotArenaPluginYG2PlatformPauseEvent pauseEvent)
            {
                PlatformPauseChanged?.Invoke(pauseEvent);
            }

            public void SendGameReady()
            {
                GameReadyCallCount++;
                if (ThrowOnGameReady)
                {
                    throw new InvalidOperationException("controlled Game Ready failure");
                }
            }

            public void Dispose()
            {
                DisposeCallCount++;
            }
        }
    }
}
