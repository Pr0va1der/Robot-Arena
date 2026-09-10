using System;

namespace RobotArena.Platform
{
    public sealed class RobotArenaPlatformProbeBackend : IPlatformServicesBackend
    {
        private readonly RobotArenaPlatformProbe probe;

        public RobotArenaPlatformProbeBackend(RobotArenaPlatformProbe probe)
        {
            this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
            this.probe.ReportChanged += OnReportChanged;
            this.probe.PlatformPauseChanged += OnPlatformPauseChanged;
        }

        public event Action<PlatformServicesSnapshot> SnapshotChanged;

        public event Action<bool> PlatformPauseChanged;

        public PlatformServicesSnapshot Snapshot => ToSnapshot(probe.Current);

        public void MarkGameReady()
        {
            RobotArenaPlatformProbe.MarkInteractiveReady();
        }

        public void Tick(float unscaledTime)
        {
        }

        public void Dispose()
        {
            probe.ReportChanged -= OnReportChanged;
            probe.PlatformPauseChanged -= OnPlatformPauseChanged;
            SnapshotChanged = null;
            PlatformPauseChanged = null;
        }

        private void OnReportChanged(RobotArenaPlatformProbeReport report)
        {
            SnapshotChanged?.Invoke(ToSnapshot(report));
        }

        private void OnPlatformPauseChanged(bool isPaused)
        {
            PlatformPauseChanged?.Invoke(isPaused);
        }

        private static PlatformServicesSnapshot ToSnapshot(RobotArenaPlatformProbeReport report)
        {
            if (report == null)
            {
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
                    failureReason: string.Empty);
            }

            return new PlatformServicesSnapshot(
                ConvertStatus(report.Status),
                report.SdkDetected,
                report.SdkInitialized,
                report.Environment,
                report.Language,
                report.HasLoadingApi,
                report.SupportsPause,
                report.SupportsPlayerData,
                report.SupportsLeaderboard,
                report.SupportsFullscreenAds,
                report.GameReady,
                report.FailureReason);
        }

        private static PlatformServicesStatus ConvertStatus(RobotArenaPlatformProbeStatus status)
        {
            switch (status)
            {
                case RobotArenaPlatformProbeStatus.WaitingForSdk:
                    return PlatformServicesStatus.WaitingForSdk;
                case RobotArenaPlatformProbeStatus.SdkReady:
                    return PlatformServicesStatus.Ready;
                case RobotArenaPlatformProbeStatus.TimedOut:
                    return PlatformServicesStatus.TimedOut;
                case RobotArenaPlatformProbeStatus.Failed:
                    return PlatformServicesStatus.Failed;
                default:
                    return PlatformServicesStatus.Unavailable;
            }
        }
    }
}
