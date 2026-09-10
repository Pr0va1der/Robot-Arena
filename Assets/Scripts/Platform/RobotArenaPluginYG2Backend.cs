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

        public RobotArenaPluginYG2Backend()
        {
            snapshot = CreateWaitingSnapshot();
            sdkWaitStartedAt = Time.unscaledTime;

#if YandexGamesPlatform_yg
            YG.YG2.onGetSDKData += OnSdkData;
            YG.YG2.onPauseGame += OnPauseGame;

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

            Debug.Log("[RobotArena.Platform] PluginYG2 Game Ready requested");
            try
            {
                YG.YG2.GameReadyAPI();
                PublishSnapshot(snapshot.WithGameReadyRequested());
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
#if YandexGamesPlatform_yg
            YG.YG2.onGetSDKData -= OnSdkData;
            YG.YG2.onPauseGame -= OnPauseGame;
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

        private void OnPauseGame(bool isPaused)
        {
            if (snapshot.Status != PlatformServicesStatus.Ready)
            {
                return;
            }

            Debug.Log("[RobotArena.Platform] PluginYG2 platform pause=" + isPaused);
            PlatformPauseChanged?.Invoke(isPaused);
        }

        private void PublishSdkReady()
        {
            string environment = YG.YG2.envir.appID;
            if (string.IsNullOrEmpty(environment))
            {
                PublishUnavailable("PluginYG2 did not receive Yandex environment data.");
                return;
            }

            string language = YG.YG2.envir.language;
            Debug.Log(
                "[RobotArena.Platform] PluginYG2 SDK ready; appId="
                + environment
                + "; language="
                + language);
            snapshot = new PlatformServicesSnapshot(
                PlatformServicesStatus.Ready,
                sdkDetected: true,
                sdkInitialized: true,
                environment,
                language,
                loadingApiStatus: PlatformCapabilityStatus.Unknown,
                supportsPause: true,
                supportsPlayerData: false,
                supportsLeaderboard: false,
                supportsFullscreenAds: false,
                snapshot.GameReadyStatus,
                failureReason: string.Empty);
            SnapshotChanged?.Invoke(snapshot);
            Debug.Log(
                "[RobotArena.Platform] PluginYG2 Loading API capability=unconfirmed; "
                + "the official Game Ready call will report its own outcome.");
        }

        private void PublishUnavailable(string reason)
        {
            PublishUnavailable(PlatformServicesStatus.Unavailable, reason);
        }

        private void PublishUnavailable(PlatformServicesStatus status, string reason)
        {
            Debug.LogWarning("[RobotArena.Platform] PluginYG2 SDK unavailable; reason=" + reason);
            snapshot = new PlatformServicesSnapshot(
                status,
                sdkDetected: false,
                sdkInitialized: false,
                environment: string.Empty,
                language: string.Empty,
                loadingApiStatus: PlatformCapabilityStatus.Unknown,
                supportsPause: false,
                supportsPlayerData: false,
                supportsLeaderboard: false,
                supportsFullscreenAds: false,
                gameReadyStatus: PlatformGameReadyStatus.NotRequested,
                failureReason: reason);
            SnapshotChanged?.Invoke(snapshot);
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
                supportsPause: false,
                supportsPlayerData: false,
                supportsLeaderboard: false,
                supportsFullscreenAds: false,
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
                supportsPause: false,
                supportsPlayerData: false,
                supportsLeaderboard: false,
                supportsFullscreenAds: false,
                gameReadyStatus: PlatformGameReadyStatus.NotRequested,
                failureReason: "PluginYG2 Yandex platform symbols are not enabled.");
#endif
        }
    }
}
