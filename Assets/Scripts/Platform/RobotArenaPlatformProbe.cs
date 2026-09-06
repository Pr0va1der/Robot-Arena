using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace RobotArena.Platform
{
    public enum RobotArenaPlatformProbeStatus
    {
        Unavailable,
        WaitingForSdk,
        SdkReady,
        TimedOut,
        Failed
    }

    [Serializable]
    public sealed class RobotArenaPlatformProbeReport
    {
        public RobotArenaPlatformProbeStatus Status { get; }
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

        public RobotArenaPlatformProbeReport(
            RobotArenaPlatformProbeStatus status,
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
    }

    [Preserve]
    public sealed class RobotArenaPlatformProbe : MonoBehaviour
    {
        private const int ProbeTimeoutMilliseconds = 8000;
        private static RobotArenaPlatformProbe instance;
        private Coroutine titleMenuReadyRoutine;

        [Serializable]
        private sealed class BridgeResult
        {
            public bool sdkDetected = false;
            public bool sdkInitialized = false;
            public string environment = string.Empty;
            public string language = string.Empty;
            public bool hasLoadingApi = false;
            public bool supportsPause = false;
            public bool supportsPlayerData = false;
            public bool supportsLeaderboard = false;
            public bool supportsFullscreenAds = false;
            public bool gameReady = false;
            public bool timedOut = false;
            public string error = string.Empty;
        }

        public static RobotArenaPlatformProbe Instance => instance;

        public RobotArenaPlatformProbeReport Current { get; private set; }

        public event Action<RobotArenaPlatformProbeReport> ReportChanged;

        public event Action<bool> PlatformPauseChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallAtStartup()
        {
            EnsureInstalled();
        }

        public static RobotArenaPlatformProbe EnsureInstalled()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject host = new GameObject(nameof(RobotArenaPlatformProbe));
            DontDestroyOnLoad(host);
            return host.AddComponent<RobotArenaPlatformProbe>();
        }

        public static void MarkInteractiveReady()
        {
            if (instance != null)
            {
                instance.MarkInteractiveReadyInternal();
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            BeginProbe();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                instance = null;
            }
        }

        private void BeginProbe()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SetReport(new RobotArenaPlatformProbeReport(
                RobotArenaPlatformProbeStatus.WaitingForSdk,
                false,
                false,
                string.Empty,
                string.Empty,
                false,
                false,
                false,
                false,
                false,
                false,
                string.Empty));
            RobotArenaPlatformProbe_Begin(gameObject.name, ProbeTimeoutMilliseconds);
#else
            SetReport(new RobotArenaPlatformProbeReport(
                RobotArenaPlatformProbeStatus.Unavailable,
                false,
                false,
                Application.platform.ToString(),
                Application.systemLanguage.ToString(),
                false,
                false,
                false,
                false,
                false,
                false,
                "The Yandex SDK probe is enabled only in a WebGL player."));
#endif
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Title Screen")
            {
                return;
            }

            if (titleMenuReadyRoutine != null)
            {
                StopCoroutine(titleMenuReadyRoutine);
            }

            titleMenuReadyRoutine = StartCoroutine(MarkTitleMenuReadyWhenSdkIsReady());
        }

        private IEnumerator MarkTitleMenuReadyWhenSdkIsReady()
        {
            // Let the title canvas build its interactive controls before reporting Game Ready.
            yield return null;

            float deadline = Time.unscaledTime + ProbeTimeoutMilliseconds / 1000f;
            while (Current != null && !Current.SdkInitialized && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            MarkInteractiveReadyInternal();
            titleMenuReadyRoutine = null;
        }

        private void MarkInteractiveReadyInternal()
        {
            if (Current == null || !Current.SdkInitialized || Current.GameReady)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            RobotArenaPlatformProbe_MarkGameReady(gameObject.name);
#endif
        }

        [Preserve]
        public void OnPlatformProbeResult(string serializedResult)
        {
            if (string.IsNullOrWhiteSpace(serializedResult))
            {
                SetFailure("The JavaScript bridge returned an empty probe result.");
                return;
            }

            BridgeResult result;
            try
            {
                result = JsonUtility.FromJson<BridgeResult>(serializedResult);
            }
            catch (Exception exception)
            {
                SetFailure("The JavaScript bridge returned invalid JSON: " + exception.Message);
                return;
            }

            if (result == null)
            {
                SetFailure("The JavaScript bridge returned no probe result.");
                return;
            }

            RobotArenaPlatformProbeStatus status = result.timedOut
                ? RobotArenaPlatformProbeStatus.TimedOut
                : string.IsNullOrEmpty(result.error)
                    ? RobotArenaPlatformProbeStatus.SdkReady
                    : RobotArenaPlatformProbeStatus.Failed;

            SetReport(new RobotArenaPlatformProbeReport(
                status,
                result.sdkDetected,
                result.sdkInitialized,
                result.environment,
                result.language,
                result.hasLoadingApi,
                result.supportsPause,
                result.supportsPlayerData,
                result.supportsLeaderboard,
                result.supportsFullscreenAds,
                result.gameReady,
                result.error));
        }

        [Preserve]
        public void OnPlatformProbePause(string value)
        {
            bool isPaused = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            PlatformPauseChanged?.Invoke(isPaused);
        }

        [Preserve]
        public void OnPlatformProbeError(string message)
        {
            SetFailure(string.IsNullOrEmpty(message) ? "The JavaScript bridge reported an unknown error." : message);
        }

        private void SetFailure(string reason)
        {
            SetReport(new RobotArenaPlatformProbeReport(
                RobotArenaPlatformProbeStatus.Failed,
                Current != null && Current.SdkDetected,
                Current != null && Current.SdkInitialized,
                Current == null ? string.Empty : Current.Environment,
                Current == null ? string.Empty : Current.Language,
                Current != null && Current.HasLoadingApi,
                Current != null && Current.SupportsPause,
                Current != null && Current.SupportsPlayerData,
                Current != null && Current.SupportsLeaderboard,
                Current != null && Current.SupportsFullscreenAds,
                Current != null && Current.GameReady,
                reason));
        }

        private void SetReport(RobotArenaPlatformProbeReport report)
        {
            Current = report;
            ReportChanged?.Invoke(report);

            if (report.Status == RobotArenaPlatformProbeStatus.Failed ||
                report.Status == RobotArenaPlatformProbeStatus.TimedOut)
            {
                Debug.LogWarning("RobotArena platform probe: " + report.FailureReason);
            }
            else
            {
                Debug.Log("RobotArena platform probe: " + report.Status);
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RobotArenaPlatformProbe_Begin(string hostName, int timeoutMilliseconds);

        [DllImport("__Internal")]
        private static extern void RobotArenaPlatformProbe_MarkGameReady(string hostName);
#endif
    }
}
