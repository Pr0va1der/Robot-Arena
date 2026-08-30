using System;
using System.Collections.Generic;

namespace RobotArena.Session
{
    public sealed class SessionOrchestrator
    {
        private readonly HashSet<BotId> liveBots = new HashSet<BotId>();
        private readonly WaveSchedule schedule;
        private readonly ISessionBotFactory botFactory;
        private float spawnElapsed;
        private float timeUntilNextSpawn;

        public SessionOrchestrator(WaveSchedule schedule, ISessionBotFactory botFactory)
        {
            this.schedule = schedule;
            this.botFactory = botFactory ?? throw new ArgumentNullException(nameof(botFactory));
        }

        public event Action<SessionState> StateChanged;

        public SessionState State { get; private set; } = SessionState.NotStarted;
        public int LiveBotCount => liveBots.Count;
        public float SpawnTimeRemaining => Math.Max(0f, schedule.SpawnDuration - spawnElapsed);

        public void StartSession(IEnumerable<BotId> initialBots)
        {
            if (initialBots == null)
            {
                throw new ArgumentNullException(nameof(initialBots));
            }

            liveBots.Clear();
            foreach (BotId bot in initialBots)
            {
                liveBots.Add(bot);
            }

            spawnElapsed = 0f;
            timeUntilNextSpawn = 0f;
            SetState(SessionState.Spawning);
        }

        public void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            if (State != SessionState.Spawning)
            {
                return;
            }

            spawnElapsed += elapsedSeconds;
            if (spawnElapsed >= schedule.SpawnDuration)
            {
                CompleteSpawning();
                return;
            }

            timeUntilNextSpawn -= elapsedSeconds;
            if (timeUntilNextSpawn <= 0f && liveBots.Count < schedule.MaximumLiveBots)
            {
                if (botFactory.TryCreateBot(out BotId bot))
                {
                    liveBots.Add(bot);
                }

                timeUntilNextSpawn = schedule.SpawnInterval;
            }
        }

        public bool RegisterBot(BotId bot)
        {
            if (State != SessionState.Spawning && State != SessionState.Clearing)
            {
                return false;
            }

            return liveBots.Add(bot);
        }

        public bool RemoveBot(BotId bot)
        {
            if (!liveBots.Remove(bot))
            {
                return false;
            }

            if (State == SessionState.Clearing && liveBots.Count == 0)
            {
                SetState(SessionState.Won);
            }

            return true;
        }

        public void DefeatPlayer()
        {
            if (State == SessionState.Spawning || State == SessionState.Clearing)
            {
                SetState(SessionState.Lost);
            }
        }

        private void CompleteSpawning()
        {
            SetState(liveBots.Count == 0 ? SessionState.Won : SessionState.Clearing);
        }

        private void SetState(SessionState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
