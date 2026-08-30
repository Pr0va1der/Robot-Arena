using UnityEngine;

namespace RobotArena.Session
{
    public sealed class SessionBotRegistration : MonoBehaviour
    {
        private ISessionBotRegistry registry;

        public BotId Id => new BotId(GetInstanceID());

        private void OnDestroy()
        {
            registry?.UnregisterBot(this);
        }

        public void Connect(ISessionBotRegistry botRegistry)
        {
            registry = botRegistry;
            registry?.RegisterBot(this);
        }
    }
}
