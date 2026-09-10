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
                supportsPause,
                supportsPlayerData,
                supportsLeaderboard,
                supportsFullscreenAds,
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
        {
            Status = status;
            SdkDetected = sdkDetected;
            SdkInitialized = sdkInitialized;
            Environment = environment ?? string.Empty;
            Language = language ?? string.Empty;
            LoadingApiStatus = loadingApiStatus;
            SupportsPause = supportsPause;
            SupportsPlayerData = supportsPlayerData;
            SupportsLeaderboard = supportsLeaderboard;
            SupportsFullscreenAds = supportsFullscreenAds;
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
        public bool SupportsPause { get; }
        public bool SupportsPlayerData { get; }
        public bool SupportsLeaderboard { get; }
        public bool SupportsFullscreenAds { get; }
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
                SupportsPause,
                SupportsPlayerData,
                SupportsLeaderboard,
                SupportsFullscreenAds,
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
                SupportsPause,
                SupportsPlayerData,
                SupportsLeaderboard,
                SupportsFullscreenAds,
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
                SupportsPause,
                SupportsPlayerData,
                SupportsLeaderboard,
                SupportsFullscreenAds,
                PlatformGameReadyStatus.Failed,
                reason);
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
                supportsPause: true,
                SupportsPlayerData,
                SupportsLeaderboard,
                SupportsFullscreenAds,
                GameReadyStatus,
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
                !snapshot.SdkInitialized ||
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
