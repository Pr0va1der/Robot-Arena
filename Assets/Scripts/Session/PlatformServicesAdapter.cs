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
        {
            Status = status;
            SdkDetected = sdkDetected;
            SdkInitialized = sdkInitialized;
            Environment = environment ?? string.Empty;
            Language = language ?? string.Empty;
            HasLoadingApi = hasLoadingApi;
            SupportsPause = supportsPause;
            SupportsPlayerData = supportsPlayerData;
            SupportsLeaderboard = supportsLeaderboard;
            SupportsFullscreenAds = supportsFullscreenAds;
            GameReady = gameReady;
            FailureReason = failureReason ?? string.Empty;
        }

        public PlatformServicesStatus Status { get; }
        public bool SdkDetected { get; }
        public bool SdkInitialized { get; }
        public string Environment { get; }
        public string Language { get; }
        public bool HasLoadingApi { get; }
        public bool SupportsPause { get; }
        public bool SupportsPlayerData { get; }
        public bool SupportsLeaderboard { get; }
        public bool SupportsFullscreenAds { get; }
        public bool GameReady { get; }
        public string FailureReason { get; }

        public PlatformServicesSnapshot WithGameReady()
        {
            return new PlatformServicesSnapshot(
                Status,
                SdkDetected,
                SdkInitialized,
                Environment,
                Language,
                HasLoadingApi,
                SupportsPause,
                SupportsPlayerData,
                SupportsLeaderboard,
                SupportsFullscreenAds,
                gameReady: true,
                FailureReason);
        }

        public PlatformServicesSnapshot WithSdkReady(string environment, string language)
        {
            return new PlatformServicesSnapshot(
                PlatformServicesStatus.Ready,
                sdkDetected: true,
                sdkInitialized: true,
                environment,
                language,
                hasLoadingApi: true,
                supportsPause: true,
                SupportsPlayerData,
                SupportsLeaderboard,
                SupportsFullscreenAds,
                GameReady,
                failureReason: string.Empty);
        }
    }

    public interface IPlatformServicesBackend
    {
        PlatformServicesSnapshot Snapshot { get; }

        event Action<PlatformServicesSnapshot> SnapshotChanged;

        event Action<bool> PlatformPauseChanged;

        void MarkGameReady();
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
                snapshot.GameReady)
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
