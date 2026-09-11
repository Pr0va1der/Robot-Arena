using System;
using UnityEngine;

#if ROBOTARENA_PLUGINYG2 && !YandexGamesPlatform_yg
#error ROBOTARENA_PLUGINYG2 requires the official PluginYG2 YandexGamesPlatform_yg integration.
#endif

namespace RobotArena.Platform
{
    public sealed class RobotArenaPluginYG2Backend : IPlatformServicesBackend
    {
        private const float SdkInitializationTimeoutSeconds = 8f;

        private readonly IRobotArenaPluginYG2RuntimeSource runtimeSource;
        private PlatformServicesSnapshot snapshot;
        private float sdkWaitStartedAt;
        private bool sdkFallbackPublished;
        private bool? lastPlatformPauseState;
        private RobotArenaPluginYG2RuntimeState runtimeState;
        private bool disposed;

        public RobotArenaPluginYG2Backend()
            : this(CreateProductionRuntimeSource(), Time.unscaledTime)
        {
        }

        public RobotArenaPluginYG2Backend(IRobotArenaPluginYG2RuntimeSource runtimeSource)
            : this(runtimeSource, Time.unscaledTime)
        {
        }

        public RobotArenaPluginYG2Backend(
            IRobotArenaPluginYG2RuntimeSource runtimeSource,
            float initialUnscaledTime)
        {
            this.runtimeSource = runtimeSource ?? throw new ArgumentNullException(nameof(runtimeSource));
            snapshot = CreateInitialSnapshot(runtimeSource.IsAvailable);
            sdkWaitStartedAt = initialUnscaledTime;

            this.runtimeSource.StateChanged += OnRuntimeStateChanged;
            this.runtimeSource.PlatformPauseChanged += OnPlatformPauseChanged;
            this.runtimeSource.SdkDataReady += OnSdkData;

            if (this.runtimeSource.IsSdkReady)
            {
                PublishSdkReady();
            }
        }

        public event Action<PlatformServicesSnapshot> SnapshotChanged;

        public event Action<bool> PlatformPauseChanged;

        public PlatformServicesSnapshot Snapshot => snapshot;

        public void MarkGameReady()
        {
            if (disposed || snapshot.GameReadyStatus != PlatformGameReadyStatus.NotRequested)
            {
                return;
            }

            if (snapshot.LoadingApiStatus == PlatformCapabilityStatus.Unavailable)
            {
                const string reason = "PluginYG2 Loading API is unavailable; Game Ready was not sent.";
                PublishSnapshot(snapshot.WithGameReadyUnavailable(reason));
                Debug.LogWarning("[RobotArena.Platform] " + reason);
                return;
            }

            Debug.Log("[RobotArena.Platform] PluginYG2 Game Ready requested");
            try
            {
                runtimeSource.SendGameReady();
                PublishSnapshot(snapshot.WithGameReadyRequested());
                ApplyGameReadyOutcome(runtimeState);
            }
            catch (Exception exception)
            {
                PublishSnapshot(snapshot.WithGameReadyFailed(exception.Message));
                Debug.LogError(
                    "[RobotArena.Platform] PluginYG2 Game Ready failed; reason="
                    + exception.Message);
            }
        }

        public void Tick(float unscaledTime)
        {
            if (disposed || sdkFallbackPublished || snapshot.Status != PlatformServicesStatus.WaitingForSdk)
            {
                return;
            }

            if (unscaledTime - sdkWaitStartedAt < SdkInitializationTimeoutSeconds)
            {
                return;
            }

            PublishUnavailable(
                PlatformServicesStatus.TimedOut,
                "PluginYG2 SDK initialization timed out after "
                + SdkInitializationTimeoutSeconds
                + " seconds.");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            runtimeSource.StateChanged -= OnRuntimeStateChanged;
            runtimeSource.PlatformPauseChanged -= OnPlatformPauseChanged;
            runtimeSource.SdkDataReady -= OnSdkData;
            runtimeSource.Dispose();
            SnapshotChanged = null;
            PlatformPauseChanged = null;
        }

        private static IRobotArenaPluginYG2RuntimeSource CreateProductionRuntimeSource()
        {
#if YandexGamesPlatform_yg
            return new RobotArenaPluginYG2RuntimeSource();
#else
            return new RobotArenaPluginYG2UnavailableRuntimeSource();
#endif
        }

        private void OnSdkData()
        {
            if (disposed)
            {
                return;
            }

            if (sdkFallbackPublished)
            {
                Debug.LogWarning(
                    "[RobotArena.Platform] PluginYG2 SDK became ready after guest fallback; "
                    + "keeping the guest session deterministic.");
                return;
            }

            PublishSdkReady();
        }

        private void OnRuntimeStateChanged(RobotArenaPluginYG2RuntimeState state)
        {
            if (disposed || state == null)
            {
                return;
            }

            string initState = (state.initState ?? string.Empty).Trim().ToLowerInvariant();
            if (sdkFallbackPublished)
            {
                if (initState == "ready")
                {
                    Debug.LogWarning(
                        "[RobotArena.Platform] PluginYG2 SDK became ready after guest fallback; "
                        + "keeping the guest session deterministic.");
                }

                return;
            }

            runtimeState = state;
            ApplyGameReadyOutcome(state);
            switch (initState)
            {
                case "ready":
                    PublishSdkReady();
                    break;
                case "timeout":
                    if (snapshot.Status == PlatformServicesStatus.Ready)
                    {
                        break;
                    }

                    PublishUnavailable(
                        PlatformServicesStatus.TimedOut,
                        GetRuntimeFailureReason(
                            state,
                            "PluginYG2 SDK initialization timed out."));
                    break;
                case "failed":
                    if (snapshot.Status == PlatformServicesStatus.Ready)
                    {
                        break;
                    }

                    PublishUnavailable(
                        PlatformServicesStatus.Failed,
                        GetRuntimeFailureReason(state, "PluginYG2 SDK initialization failed."));
                    break;
                case "local":
                    if (snapshot.Status == PlatformServicesStatus.Ready)
                    {
                        break;
                    }

                    PublishUnavailable(
                        PlatformServicesStatus.Unavailable,
                        "PluginYG2 local host mode is using the guest path.");
                    break;
            }
        }

        private void OnPlatformPauseChanged(RobotArenaPluginYG2PlatformPauseEvent pauseEvent)
        {
            if (disposed ||
                pauseEvent == null ||
                !pauseEvent.IsYandexLifecycle ||
                snapshot.Status != PlatformServicesStatus.Ready ||
                (lastPlatformPauseState.HasValue && lastPlatformPauseState.Value == pauseEvent.IsPaused))
            {
                return;
            }

            lastPlatformPauseState = pauseEvent.IsPaused;
            Debug.Log("[RobotArena.Platform] PluginYG2 platform pause=" + pauseEvent.IsPaused);
            PlatformPauseChanged?.Invoke(pauseEvent.IsPaused);
        }

        private void PublishSdkReady()
        {
            string environment = runtimeSource.Environment;
            if (string.IsNullOrEmpty(environment))
            {
                Debug.LogWarning(
                    "[RobotArena.Platform] PluginYG2 SDK callback arrived before environment data; "
                    + "continuing to wait for EnvirData.");
                return;
            }

            string language = runtimeSource.Language;
            PlatformCapabilityStatus loadingApiStatus = ParseCapability(
                runtimeState == null ? null : runtimeState.loadingApi);
            PlatformCapabilityStatus playerDataStatus = ParseCapability(
                runtimeState == null ? null : runtimeState.playerData);
            PlatformCapabilityStatus leaderboardStatus = ParseCapability(
                runtimeState == null ? null : runtimeState.leaderboard);
            PlatformCapabilityStatus fullscreenAdsStatus = ParseCapability(
                runtimeState == null ? null : runtimeState.fullscreenAds);
            bool alreadyReady = snapshot.Status == PlatformServicesStatus.Ready;
            if (alreadyReady &&
                string.Equals(snapshot.Environment, environment, StringComparison.Ordinal) &&
                string.Equals(snapshot.Language, language, StringComparison.Ordinal) &&
                snapshot.LoadingApiStatus == loadingApiStatus &&
                snapshot.PlayerDataStatus == playerDataStatus &&
                snapshot.LeaderboardStatus == leaderboardStatus &&
                snapshot.FullscreenAdsStatus == fullscreenAdsStatus)
            {
                return;
            }

            if (!alreadyReady)
            {
                Debug.Log(
                    "[RobotArena.Platform] PluginYG2 SDK ready; appId="
                    + environment
                    + "; language="
                    + language);
            }

            snapshot = new PlatformServicesSnapshot(
                PlatformServicesStatus.Ready,
                sdkDetected: true,
                sdkInitialized: true,
                environment: environment,
                language: language,
                loadingApiStatus: loadingApiStatus,
                pauseStatus: PlatformCapabilityStatus.Available,
                playerDataStatus: playerDataStatus,
                leaderboardStatus: leaderboardStatus,
                fullscreenAdsStatus: fullscreenAdsStatus,
                gameReadyStatus: snapshot.GameReadyStatus,
                failureReason: string.Empty);
            if (loadingApiStatus == PlatformCapabilityStatus.Unavailable &&
                snapshot.GameReadyStatus == PlatformGameReadyStatus.NotRequested)
            {
                snapshot = snapshot.WithGameReadyUnavailable(
                    "PluginYG2 Loading API is unavailable; Game Ready was not sent.");
            }
            else if (loadingApiStatus == PlatformCapabilityStatus.Unavailable &&
                snapshot.GameReadyStatus == PlatformGameReadyStatus.Requested)
            {
                snapshot = snapshot.WithGameReadyUnavailable(
                    "PluginYG2 Loading API became unavailable after Game Ready was requested.");
            }

            SnapshotChanged?.Invoke(snapshot);
            Debug.Log(
                "[RobotArena.Platform] PluginYG2 capability report="
                + "loadingApi=" + loadingApiStatus
                + "; playerData=" + playerDataStatus
                + "; leaderboard=" + leaderboardStatus
                + "; fullscreenAds=" + fullscreenAdsStatus);
        }

        private void PublishUnavailable(PlatformServicesStatus status, string reason)
        {
            if (disposed || sdkFallbackPublished)
            {
                return;
            }

            sdkFallbackPublished = true;
            Debug.LogWarning("[RobotArena.Platform] PluginYG2 SDK unavailable; reason=" + reason);
            snapshot = new PlatformServicesSnapshot(
                status,
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
                failureReason: reason);
            SnapshotChanged?.Invoke(snapshot);
        }

        private static string GetRuntimeFailureReason(
            RobotArenaPluginYG2RuntimeState state,
            string fallback)
        {
            return string.IsNullOrEmpty(state.failureReason) ? fallback : state.failureReason;
        }

        private void ApplyGameReadyOutcome(RobotArenaPluginYG2RuntimeState state)
        {
            if (state == null || snapshot.GameReadyStatus != PlatformGameReadyStatus.Requested)
            {
                return;
            }

            string outcome = (state.gameReadyOutcome ?? string.Empty).Trim().ToLowerInvariant();
            if (outcome == "confirmed")
            {
                PublishSnapshot(snapshot.WithGameReadyConfirmed());
                Debug.Log("[RobotArena.Platform] PluginYG2 Game Ready confirmed");
            }
            else if (outcome == "failed")
            {
                string reason = string.IsNullOrEmpty(state.gameReadyFailureReason)
                    ? "PluginYG2 Loading API.ready() failed."
                    : state.gameReadyFailureReason;
                PublishSnapshot(snapshot.WithGameReadyFailed(reason));
                Debug.LogError(
                    "[RobotArena.Platform] PluginYG2 Game Ready failed; reason=" + reason);
            }
        }

        private static PlatformCapabilityStatus ParseCapability(string value)
        {
            if (string.Equals(value, "available", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                return PlatformCapabilityStatus.Available;
            }

            if (string.Equals(value, "unavailable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                return PlatformCapabilityStatus.Unavailable;
            }

            return PlatformCapabilityStatus.Unknown;
        }

        private void PublishSnapshot(PlatformServicesSnapshot nextSnapshot)
        {
            if (disposed)
            {
                return;
            }

            snapshot = nextSnapshot;
            SnapshotChanged?.Invoke(snapshot);
        }

        private static PlatformServicesSnapshot CreateInitialSnapshot(bool isAvailable)
        {
            if (isAvailable)
            {
                return new PlatformServicesSnapshot(
                    PlatformServicesStatus.WaitingForSdk,
                    sdkDetected: false,
                    sdkInitialized: false,
                    environment: string.Empty,
                    language: string.Empty,
                    loadingApiStatus: PlatformCapabilityStatus.Unknown,
                    pauseStatus: PlatformCapabilityStatus.Unknown,
                    playerDataStatus: PlatformCapabilityStatus.Unknown,
                    leaderboardStatus: PlatformCapabilityStatus.Unknown,
                    fullscreenAdsStatus: PlatformCapabilityStatus.Unknown,
                    gameReadyStatus: PlatformGameReadyStatus.NotRequested,
                    failureReason: string.Empty);
            }

            return new PlatformServicesSnapshot(
                PlatformServicesStatus.Unavailable,
                sdkDetected: false,
                sdkInitialized: false,
                environment: string.Empty,
                language: string.Empty,
                loadingApiStatus: PlatformCapabilityStatus.Unavailable,
                pauseStatus: PlatformCapabilityStatus.Unavailable,
                playerDataStatus: PlatformCapabilityStatus.Unavailable,
                leaderboardStatus: PlatformCapabilityStatus.Unavailable,
                fullscreenAdsStatus: PlatformCapabilityStatus.Unavailable,
                gameReadyStatus: PlatformGameReadyStatus.NotRequested,
                failureReason: "PluginYG2 Yandex platform symbols are not enabled.");
        }

#if YandexGamesPlatform_yg
        private sealed class RobotArenaPluginYG2RuntimeSource
            : IRobotArenaPluginYG2RuntimeSource
        {
            private bool disposed;

            public RobotArenaPluginYG2RuntimeSource()
            {
                RobotArenaPluginYG2RuntimeChannel.StateChanged += OnRuntimeStateChanged;
                RobotArenaPluginYG2RuntimeChannel.PlatformPauseChanged += OnPlatformPauseChanged;
                YG.YG2.onGetSDKData += OnSdkData;
            }

            public bool IsAvailable => true;

            public bool IsSdkReady => YG.YG2.isSDKEnabled;

            public string Environment => YG.YG2.envir.appID;

            public string Language => YG.YG2.envir.language;

            public event Action SdkDataReady;

            public event Action<RobotArenaPluginYG2RuntimeState> StateChanged;

            public event Action<RobotArenaPluginYG2PlatformPauseEvent> PlatformPauseChanged;

            public void SendGameReady()
            {
                YG.YG2.GameReadyAPI();
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                RobotArenaPluginYG2RuntimeChannel.StateChanged -= OnRuntimeStateChanged;
                RobotArenaPluginYG2RuntimeChannel.PlatformPauseChanged -= OnPlatformPauseChanged;
                YG.YG2.onGetSDKData -= OnSdkData;
                SdkDataReady = null;
                StateChanged = null;
                PlatformPauseChanged = null;
            }

            private void OnSdkData()
            {
                if (!disposed)
                {
                    SdkDataReady?.Invoke();
                }
            }

            private void OnRuntimeStateChanged(RobotArenaPluginYG2RuntimeState state)
            {
                if (!disposed)
                {
                    StateChanged?.Invoke(state);
                }
            }

            private void OnPlatformPauseChanged(RobotArenaPluginYG2PlatformPauseEvent pauseEvent)
            {
                if (!disposed)
                {
                    PlatformPauseChanged?.Invoke(pauseEvent);
                }
            }
        }
#endif
    }
}
