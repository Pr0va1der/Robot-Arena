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

        private PlatformServicesSnapshot snapshot;
        private float sdkWaitStartedAt;
        private bool sdkFallbackPublished;
        private bool? lastPlatformPauseState;
        private RobotArenaPluginYG2RuntimeState runtimeState;
        private bool disposed;

        public RobotArenaPluginYG2Backend()
        {
            snapshot = CreateWaitingSnapshot();
            sdkWaitStartedAt = Time.unscaledTime;

#if YandexGamesPlatform_yg
            RobotArenaPluginYG2RuntimeChannel.StateChanged += OnRuntimeStateChanged;
            RobotArenaPluginYG2RuntimeChannel.PlatformPauseChanged += OnPlatformPauseChanged;
            YG.YG2.onGetSDKData += OnSdkData;

            if (YG.YG2.isSDKEnabled)
            {
                PublishSdkReady();
            }
#endif
        }

        public event Action<PlatformServicesSnapshot> SnapshotChanged;

        public event Action<bool> PlatformPauseChanged;

        public PlatformServicesSnapshot Snapshot => snapshot;

        public void MarkGameReady()
        {
#if YandexGamesPlatform_yg
            if (snapshot.GameReadyStatus != PlatformGameReadyStatus.NotRequested)
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
                YG.YG2.GameReadyAPI();
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
#endif
        }

        public void Tick(float unscaledTime)
        {
#if YandexGamesPlatform_yg
            if (sdkFallbackPublished || snapshot.Status != PlatformServicesStatus.WaitingForSdk)
            {
                return;
            }

            if (unscaledTime - sdkWaitStartedAt < SdkInitializationTimeoutSeconds)
            {
                return;
            }

            sdkFallbackPublished = true;
            PublishUnavailable(
                PlatformServicesStatus.TimedOut,
                "PluginYG2 SDK initialization timed out after "
                + SdkInitializationTimeoutSeconds
                + " seconds.");
#endif
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
#if YandexGamesPlatform_yg
            RobotArenaPluginYG2RuntimeChannel.StateChanged -= OnRuntimeStateChanged;
            RobotArenaPluginYG2RuntimeChannel.PlatformPauseChanged -= OnPlatformPauseChanged;
            YG.YG2.onGetSDKData -= OnSdkData;
#endif
            SnapshotChanged = null;
            PlatformPauseChanged = null;
        }

#if YandexGamesPlatform_yg
        private void OnSdkData()
        {
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
            if (state == null)
            {
                return;
            }

            runtimeState = state;
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

        private void OnPlatformPauseChanged(bool isPaused)
        {
            if (snapshot.Status != PlatformServicesStatus.Ready ||
                (lastPlatformPauseState.HasValue && lastPlatformPauseState.Value == isPaused))
            {
                return;
            }

            lastPlatformPauseState = isPaused;
            Debug.Log("[RobotArena.Platform] PluginYG2 platform pause=" + isPaused);
            PlatformPauseChanged?.Invoke(isPaused);
        }

        private void PublishSdkReady()
        {
            string environment = YG.YG2.envir.appID;
            if (string.IsNullOrEmpty(environment))
            {
                Debug.LogWarning(
                    "[RobotArena.Platform] PluginYG2 SDK callback arrived before environment data; "
                    + "continuing to wait for EnvirData.");
                return;
            }

            string language = YG.YG2.envir.language;
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

        private void PublishUnavailable(string reason)
        {
            PublishUnavailable(PlatformServicesStatus.Unavailable, reason);
        }

        private void PublishUnavailable(PlatformServicesStatus status, string reason)
        {
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
#endif

        private void PublishSnapshot(PlatformServicesSnapshot nextSnapshot)
        {
            snapshot = nextSnapshot;
            SnapshotChanged?.Invoke(snapshot);
        }

        private static PlatformServicesSnapshot CreateWaitingSnapshot()
        {
#if YandexGamesPlatform_yg
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
#else
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
#endif
        }
    }
}
