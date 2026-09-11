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

        event Action<bool> PlatformPauseChanged;

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
    }

    internal static class RobotArenaPluginYG2RuntimeChannel
    {
        internal static event Action<RobotArenaPluginYG2RuntimeState> StateChanged;

        internal static event Action<bool> PlatformPauseChanged;

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

        internal static void PublishPlatformPause(string value)
        {
            if (!bool.TryParse(value, out bool isPaused))
            {
                Debug.LogWarning(
                    "[RobotArena.PluginYG2.Transport] invalid platform pause value: "
                    + value);
                return;
            }

            PlatformPauseChanged?.Invoke(isPaused);
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

        public event Action<bool> PlatformPauseChanged
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

        public void RobotArenaPlatformPause(string value)
        {
            RobotArena.Platform.RobotArenaPluginYG2RuntimeChannel.PublishPlatformPause(value);
        }
    }
}
#endif
