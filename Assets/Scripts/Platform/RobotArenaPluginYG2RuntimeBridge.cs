using System;
using UnityEngine;

namespace RobotArena.Platform
{
    public interface IRobotArenaPluginYG2RuntimeSource : IDisposable
    {
        bool IsAvailable { get; }

        bool IsSdkReady { get; }

        string Environment { get; }

        string Language { get; }

        event Action SdkDataReady;

        event Action<RobotArenaPluginYG2RuntimeState> StateChanged;

        event Action<RobotArenaPluginYG2PlatformPauseEvent> PlatformPauseChanged;

        void SendGameReady();
    }

    [Serializable]
    public sealed class RobotArenaPluginYG2RuntimeState
    {
        public string initState;
        public string failureReason;
        public string loadingApi;
        public string playerData;
        public string leaderboard;
        public string fullscreenAds;
        public string gameReadyOutcome;
        public string gameReadyFailureReason;
        public string lifecycleToken;
    }

    public sealed class RobotArenaPluginYG2PlatformPauseEvent
    {
        public const string YandexLifecycleSource = "yandex-lifecycle";

        public RobotArenaPluginYG2PlatformPauseEvent(string source, bool paused)
        {
            Source = source ?? string.Empty;
            IsPaused = paused;
        }

        public string Source { get; }

        public bool IsPaused { get; }

        public bool IsYandexLifecycle => string.Equals(
            Source,
            YandexLifecycleSource,
            StringComparison.Ordinal);
    }

    internal static class RobotArenaPluginYG2RuntimeChannel
    {
        internal static event Action<RobotArenaPluginYG2RuntimeState> StateChanged;

        internal static event Action<RobotArenaPluginYG2PlatformPauseEvent> PlatformPauseChanged;

        [Serializable]
        private sealed class PlatformPausePayload
        {
            public string source;
            public string state;
            public string token;
        }

        private static string yandexLifecycleToken;

        internal static void PublishStateJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                RobotArenaPluginYG2RuntimeState state =
                    JsonUtility.FromJson<RobotArenaPluginYG2RuntimeState>(json);
                if (state != null)
                {
                    RegisterYandexLifecycleToken(state.lifecycleToken);
                    StateChanged?.Invoke(state);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[RobotArena.PluginYG2.Transport] invalid lifecycle state: "
                    + exception.Message);
            }
        }

        private static void RegisterYandexLifecycleToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning(
                    "[RobotArena.PluginYG2.Transport] invalid Yandex lifecycle token: empty");
                return;
            }

            if (!string.IsNullOrEmpty(yandexLifecycleToken) &&
                !string.Equals(token, yandexLifecycleToken, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    "[RobotArena.PluginYG2.Transport] invalid Yandex lifecycle token: already registered");
                return;
            }

            yandexLifecycleToken = token;
        }

        internal static void PublishYandexLifecyclePause(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning(
                    "[RobotArena.PluginYG2.Transport] invalid platform pause payload: empty");
                return;
            }

            try
            {
                PlatformPausePayload payload = JsonUtility.FromJson<PlatformPausePayload>(json);
                string source = payload == null ? string.Empty : payload.source;
                string state = payload == null ? string.Empty : payload.state;
                string token = payload == null ? string.Empty : payload.token;
                bool validSource = string.Equals(
                    source,
                    RobotArenaPluginYG2PlatformPauseEvent.YandexLifecycleSource,
                    StringComparison.Ordinal);
                bool validToken = !string.IsNullOrEmpty(yandexLifecycleToken) &&
                                  string.Equals(token, yandexLifecycleToken, StringComparison.Ordinal);
                bool isPaused = string.Equals(state, "paused", StringComparison.Ordinal);
                bool isResumed = string.Equals(state, "resumed", StringComparison.Ordinal);
                if (!validSource || !validToken || (!isPaused && !isResumed))
                {
                    Debug.LogWarning(
                        "[RobotArena.PluginYG2.Transport] invalid platform pause payload: "
                        + json);
                    return;
                }

                PlatformPauseChanged?.Invoke(
                    new RobotArenaPluginYG2PlatformPauseEvent(source, isPaused));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[RobotArena.PluginYG2.Transport] invalid platform pause payload: "
                    + exception.Message);
            }
        }

    }
}

namespace RobotArena.Platform
{
    internal sealed class RobotArenaPluginYG2UnavailableRuntimeSource
        : IRobotArenaPluginYG2RuntimeSource
    {
        public bool IsAvailable => false;

        public bool IsSdkReady => false;

        public string Environment => string.Empty;

        public string Language => string.Empty;

        public event Action SdkDataReady
        {
            add { }
            remove { }
        }

        public event Action<RobotArenaPluginYG2RuntimeState> StateChanged
        {
            add { }
            remove { }
        }

        public event Action<RobotArenaPluginYG2PlatformPauseEvent> PlatformPauseChanged
        {
            add { }
            remove { }
        }

        public void SendGameReady()
        {
        }

        public void Dispose()
        {
        }
    }
}

#if YandexGamesPlatform_yg
namespace YG.Insides
{
    public partial class YGSendMessage
    {
        public void RobotArenaPlatformState(string json)
        {
            RobotArena.Platform.RobotArenaPluginYG2RuntimeChannel.PublishStateJson(json);
        }

        public void RobotArenaYandexLifecyclePause(string json)
        {
            RobotArena.Platform.RobotArenaPluginYG2RuntimeChannel.PublishYandexLifecyclePause(json);
        }
    }
}
#endif
