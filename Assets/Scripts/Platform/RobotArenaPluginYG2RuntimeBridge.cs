using System;
using UnityEngine;

namespace RobotArena.Platform
{
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
