using System;

namespace RobotArena.Platform
{
    public enum PlatformServicesStatus
    {
        Unavailable,
        WaitingForSdk,
        Ready,
        TimedOut,
        Failed
    }

    public enum PlatformGameReadyStatus
    {
        NotRequested,
        Requested,
        Confirmed,
        Unavailable,
        Failed
    }

    public enum PlatformCapabilityStatus
    {
        Unknown,
        Available,
        Unavailable
    }

    public sealed class PlatformServicesSnapshot
    {
            public PlatformServicesSnapshot(
                PlatformServicesStatus status,
                bool sdkDetected,
                bool sdkInitialized,
                string environment,
            string language,
            bool hasLoadingApi,
            bool supportsPause,
            bool supportsPlayerData,
            bool supportsLeaderboard,
            bool supportsFullscreenAds,
            bool gameReady,
            string failureReason)
            : this(
                status,
                sdkDetected,
                sdkInitialized,
                environment,
                language,
                hasLoadingApi
                    ? PlatformCapabilityStatus.Available
                    : PlatformCapabilityStatus.Unavailable,
                supportsPause
                    ? PlatformCapabilityStatus.Available
                    : PlatformCapabilityStatus.Unavailable,
                supportsPlayerData
                    ? PlatformCapabilityStatus.Available
                    : PlatformCapabilityStatus.Unavailable,
                supportsLeaderboard
                    ? PlatformCapabilityStatus.Available
                    : PlatformCapabilityStatus.Unavailable,
                supportsFullscreenAds
                    ? PlatformCapabilityStatus.Available
                    : PlatformCapabilityStatus.Unavailable,
                gameReady ? PlatformGameReadyStatus.Confirmed : PlatformGameReadyStatus.NotRequested,
                failureReason)
        {
        }

        public PlatformServicesSnapshot(
            PlatformServicesStatus status,
            bool sdkDetected,
            bool sdkInitialized,
            string environment,
            string language,
            PlatformCapabilityStatus loadingApiStatus,
            bool supportsPause,
            bool supportsPlayerData,
            bool supportsLeaderboard,
            bool supportsFullscreenAds,
            PlatformGameReadyStatus gameReadyStatus,
            string failureReason)
            : this(
                status,
                sdkDetected,
                sdkInitialized,
                environment,
                language,
                loadingApiStatus,
                supportsPause
                    ? PlatformCapabilityStatus.Available
                    : PlatformCapabilityStatus.Unavailable,
                supportsPlayerData
                    ? PlatformCapabilityStatus.Available
                    : PlatformCapabilityStatus.Unavailable,
                supportsLeaderboard
                    ? PlatformCapabilityStatus.Available
                    : PlatformCapabilityStatus.Unavailable,
                supportsFullscreenAds
                    ? PlatformCapabilityStatus.Available
                    : PlatformCapabilityStatus.Unavailable,
                gameReadyStatus,
                failureReason)
        {
        }

        public PlatformServicesSnapshot(
            PlatformServicesStatus status,
            bool sdkDetected,
            bool sdkInitialized,
            string environment,
            string language,
            PlatformCapabilityStatus loadingApiStatus,
            PlatformCapabilityStatus pauseStatus,
            PlatformCapabilityStatus playerDataStatus,
            PlatformCapabilityStatus leaderboardStatus,
            PlatformCapabilityStatus fullscreenAdsStatus,
            PlatformGameReadyStatus gameReadyStatus,
            string failureReason)
        {
            Status = status;
            SdkDetected = sdkDetected;
            SdkInitialized = sdkInitialized;
            Environment = environment ?? string.Empty;
            Language = language ?? string.Empty;
            LoadingApiStatus = loadingApiStatus;
            PauseStatus = pauseStatus;
            PlayerDataStatus = playerDataStatus;
            LeaderboardStatus = leaderboardStatus;
            FullscreenAdsStatus = fullscreenAdsStatus;
            GameReadyStatus = gameReadyStatus;
            FailureReason = failureReason ?? string.Empty;
        }

        public PlatformServicesStatus Status { get; }
        public bool SdkDetected { get; }
        public bool SdkInitialized { get; }
        public string Environment { get; }
        public string Language { get; }
        public PlatformCapabilityStatus LoadingApiStatus { get; }
        public bool HasLoadingApi => LoadingApiStatus == PlatformCapabilityStatus.Available;
        public PlatformCapabilityStatus PauseStatus { get; }
        public bool SupportsPause => PauseStatus == PlatformCapabilityStatus.Available;
        public PlatformCapabilityStatus PlayerDataStatus { get; }
        public bool SupportsPlayerData => PlayerDataStatus == PlatformCapabilityStatus.Available;
        public PlatformCapabilityStatus LeaderboardStatus { get; }
        public bool SupportsLeaderboard => LeaderboardStatus == PlatformCapabilityStatus.Available;
        public PlatformCapabilityStatus FullscreenAdsStatus { get; }
        public bool SupportsFullscreenAds => FullscreenAdsStatus == PlatformCapabilityStatus.Available;
        public PlatformGameReadyStatus GameReadyStatus { get; }
        public bool GameReady => GameReadyStatus == PlatformGameReadyStatus.Confirmed;
        public string FailureReason { get; }

        public PlatformServicesSnapshot WithGameReadyRequested()
        {
            return new PlatformServicesSnapshot(
                Status,
                SdkDetected,
                SdkInitialized,
                Environment,
                Language,
                LoadingApiStatus,
                PauseStatus,
                PlayerDataStatus,
                LeaderboardStatus,
                FullscreenAdsStatus,
                PlatformGameReadyStatus.Requested,
                FailureReason);
        }

        public PlatformServicesSnapshot WithGameReadyConfirmed()
        {
            return new PlatformServicesSnapshot(
                Status,
                SdkDetected,
                SdkInitialized,
                Environment,
                Language,
                LoadingApiStatus,
                PauseStatus,
                PlayerDataStatus,
                LeaderboardStatus,
                FullscreenAdsStatus,
                PlatformGameReadyStatus.Confirmed,
                FailureReason);
        }

        public PlatformServicesSnapshot WithGameReadyFailed(string reason)
        {
            return new PlatformServicesSnapshot(
                Status,
                SdkDetected,
                SdkInitialized,
                Environment,
                Language,
                LoadingApiStatus,
                PauseStatus,
                PlayerDataStatus,
                LeaderboardStatus,
                FullscreenAdsStatus,
                PlatformGameReadyStatus.Failed,
                reason);
        }

        public PlatformServicesSnapshot WithGameReadyUnavailable(string reason)
        {
            return new PlatformServicesSnapshot(
                Status,
                SdkDetected,
                SdkInitialized,
                Environment,
                Language,
                LoadingApiStatus,
                PauseStatus,
                PlayerDataStatus,
                LeaderboardStatus,
                FullscreenAdsStatus,
                PlatformGameReadyStatus.Unavailable,
                reason);
        }

        public PlatformServicesSnapshot WithCapabilities(
            PlatformCapabilityStatus loadingApiStatus,
            PlatformCapabilityStatus pauseStatus,
            PlatformCapabilityStatus playerDataStatus,
            PlatformCapabilityStatus leaderboardStatus,
            PlatformCapabilityStatus fullscreenAdsStatus)
        {
            return new PlatformServicesSnapshot(
                Status,
                SdkDetected,
                SdkInitialized,
                Environment,
                Language,
                loadingApiStatus,
                pauseStatus,
                playerDataStatus,
                leaderboardStatus,
                fullscreenAdsStatus,
                GameReadyStatus,
                FailureReason);
        }

        public PlatformServicesSnapshot WithSdkReady(string environment, string language)
        {
            return WithSdkReady(
                environment,
                language,
                PlatformCapabilityStatus.Unknown);
        }

        public PlatformServicesSnapshot WithSdkReady(
            string environment,
            string language,
            bool hasLoadingApi)
        {
            return WithSdkReady(
                environment,
                language,
                hasLoadingApi
                    ? PlatformCapabilityStatus.Available
                    : PlatformCapabilityStatus.Unavailable);
        }

        public PlatformServicesSnapshot WithSdkReady(
            string environment,
            string language,
            PlatformCapabilityStatus loadingApiStatus)
        {
            return new PlatformServicesSnapshot(
                PlatformServicesStatus.Ready,
                sdkDetected: true,
                sdkInitialized: true,
                environment,
                language,
                loadingApiStatus,
                pauseStatus: PlatformCapabilityStatus.Available,
                playerDataStatus: PlayerDataStatus,
                leaderboardStatus: LeaderboardStatus,
                fullscreenAdsStatus: FullscreenAdsStatus,
                gameReadyStatus: GameReadyStatus,
                failureReason: string.Empty);
        }
    }

    public interface IPlatformServicesBackend
    {
        PlatformServicesSnapshot Snapshot { get; }

        event Action<PlatformServicesSnapshot> SnapshotChanged;

        event Action<bool> PlatformPauseChanged;

        void MarkGameReady();

        void Tick(float unscaledTime);

        void Dispose();
    }

    public sealed class PlatformServicesAdapter : IDisposable
    {
        private readonly IPlatformServicesBackend backend;
        private bool interactiveReady;
        private bool gameReadyRequested;
        private bool disposed;

        public PlatformServicesAdapter(IPlatformServicesBackend backend)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            this.backend.SnapshotChanged += OnBackendSnapshotChanged;
            this.backend.PlatformPauseChanged += OnBackendPauseChanged;
        }

        public event Action<PlatformServicesSnapshot> SnapshotChanged;

        public event Action<bool> PlatformPauseChanged;

        public PlatformServicesSnapshot Current => backend.Snapshot;

        public void MarkInteractiveReady()
        {
            ThrowIfDisposed();
            interactiveReady = true;
            TryMarkGameReady();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            backend.SnapshotChanged -= OnBackendSnapshotChanged;
            backend.PlatformPauseChanged -= OnBackendPauseChanged;
            SnapshotChanged = null;
            PlatformPauseChanged = null;
        }

        private void OnBackendSnapshotChanged(PlatformServicesSnapshot snapshot)
        {
            SnapshotChanged?.Invoke(snapshot);
            TryMarkGameReady();
        }

        private void OnBackendPauseChanged(bool isPaused)
        {
            PlatformPauseChanged?.Invoke(isPaused);
        }

        private void TryMarkGameReady()
        {
            PlatformServicesSnapshot snapshot = backend.Snapshot;
            if (!interactiveReady ||
                gameReadyRequested ||
                snapshot == null ||
                snapshot.Status != PlatformServicesStatus.Ready ||
                !snapshot.SdkInitialized ||
                snapshot.LoadingApiStatus != PlatformCapabilityStatus.Available ||
                snapshot.GameReadyStatus != PlatformGameReadyStatus.NotRequested)
            {
                return;
            }

            gameReadyRequested = true;
            backend.MarkGameReady();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(PlatformServicesAdapter));
            }
        }
    }
}
