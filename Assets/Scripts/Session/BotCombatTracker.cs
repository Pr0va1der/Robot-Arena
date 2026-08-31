using System;
using System.Collections.Generic;

namespace RobotArena.Session
{
    public sealed class BotCombatTracker
    {
        private readonly HashSet<BotId> combatBots = new HashSet<BotId>();

        public event Action<bool> CombatPresenceChanged;

        public bool AnyBotInCombat => combatBots.Count > 0;
        public int ActiveBotCount => combatBots.Count;

        public bool EnterCombat(BotId bot)
        {
            if (!combatBots.Add(bot))
            {
                return false;
            }

            if (combatBots.Count == 1)
            {
                CombatPresenceChanged?.Invoke(true);
            }

            return true;
        }

        public bool ExitCombat(BotId bot)
        {
            if (!combatBots.Remove(bot))
            {
                return false;
            }

            if (combatBots.Count == 0)
            {
                CombatPresenceChanged?.Invoke(false);
            }

            return true;
        }

        public void Clear()
        {
            if (combatBots.Count == 0)
            {
                return;
            }

            combatBots.Clear();
            CombatPresenceChanged?.Invoke(false);
        }
    }
}
