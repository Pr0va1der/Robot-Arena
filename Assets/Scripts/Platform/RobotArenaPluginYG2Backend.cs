using System;

namespace RobotArena.Platform
{
    public sealed class RobotArenaPluginYG2Backend : IPlatformServicesBackend
    {
        private PlatformServicesSnapshot snapshot;

        public RobotArenaPluginYG2Backend()
        {
            snapshot = CreateWaitingSnapshot();

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
            YG.YG2.GameReadyAPI();
            PublishSnapshot(snapshot.WithGameReady());
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
            PublishSdkReady();
        }

        private void OnPauseGame(bool isPaused)
        {
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
            snapshot = new PlatformServicesSnapshot(
                PlatformServicesStatus.Ready,
                sdkDetected: true,
                sdkInitialized: true,
                environment,
                language,
                hasLoadingApi: true,
                supportsPause: true,
                supportsPlayerData: false,
                supportsLeaderboard: false,
                supportsFullscreenAds: false,
                snapshot.GameReady,
                failureReason: string.Empty);
            SnapshotChanged?.Invoke(snapshot);
        }

        private void PublishUnavailable(string reason)
        {
            snapshot = new PlatformServicesSnapshot(
                PlatformServicesStatus.Unavailable,
                sdkDetected: false,
                sdkInitialized: false,
                environment: string.Empty,
                language: string.Empty,
                hasLoadingApi: false,
                supportsPause: false,
                supportsPlayerData: false,
                supportsLeaderboard: false,
                supportsFullscreenAds: false,
                gameReady: false,
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
                hasLoadingApi: false,
                supportsPause: false,
                supportsPlayerData: false,
                supportsLeaderboard: false,
                supportsFullscreenAds: false,
                gameReady: false,
                failureReason: string.Empty);
#else
            return new PlatformServicesSnapshot(
                PlatformServicesStatus.Unavailable,
                sdkDetected: false,
                sdkInitialized: false,
                environment: string.Empty,
                language: string.Empty,
                hasLoadingApi: false,
                supportsPause: false,
                supportsPlayerData: false,
                supportsLeaderboard: false,
                supportsFullscreenAds: false,
                gameReady: false,
                failureReason: "PluginYG2 Yandex platform symbols are not enabled.");
#endif
        }
    }
}
