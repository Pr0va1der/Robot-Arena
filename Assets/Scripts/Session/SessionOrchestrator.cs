using System;
using System.Collections.Generic;

namespace RobotArena.Session
{
    public sealed class SessionOrchestrator
    {
        private readonly HashSet<BotId> liveBots = new HashSet<BotId>();
        private readonly SessionPlan plan;
        private readonly ISessionBotFactory botFactory;
        private readonly IPlayerRecovery playerRecovery;
        private int currentWaveIndex;
        private float spawnElapsed;
        private float timeUntilNextSpawn;
        private float intermissionElapsed;

        public SessionOrchestrator(
            SessionPlan plan,
            ISessionBotFactory botFactory,
            IPlayerRecovery playerRecovery)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.botFactory = botFactory ?? throw new ArgumentNullException(nameof(botFactory));
            this.playerRecovery = playerRecovery ?? throw new ArgumentNullException(nameof(playerRecovery));
        }

        public event Action<SessionState> StateChanged;

        public SessionState State { get; private set; } = SessionState.NotStarted;
        public int LiveBotCount => liveBots.Count;
        public int CurrentWaveNumber => currentWaveIndex + 1;
        public int TotalWaves => plan.Waves.Count;
        public WaveSchedule CurrentWave => plan.Waves[currentWaveIndex];
        public float ActiveTime { get; private set; }
        public float SpawnTimeRemaining => Math.Max(0f, CurrentWave.SpawnDuration - spawnElapsed);
        public float IntermissionTimeRemaining => Math.Max(0f, plan.IntermissionDuration - intermissionElapsed);

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

            currentWaveIndex = 0;
            ActiveTime = 0f;
            BeginSpawning();
        }

        public void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            if (State == SessionState.Spawning)
            {
                ActiveTime += elapsedSeconds;
                AdvanceSpawning(elapsedSeconds);
            }
            else if (State == SessionState.Clearing)
            {
                ActiveTime += elapsedSeconds;
            }
            else if (State == SessionState.Intermission)
            {
                AdvanceIntermission(elapsedSeconds);
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
                CompleteWave();
            }

            return true;
        }

        public void DefeatPlayer()
        {
            if (State == SessionState.Spawning ||
                State == SessionState.Clearing ||
                State == SessionState.Intermission)
            {
                SetState(SessionState.Lost);
            }
        }

        private void AdvanceSpawning(float elapsedSeconds)
        {
            spawnElapsed += elapsedSeconds;
            if (spawnElapsed >= CurrentWave.SpawnDuration)
            {
                if (liveBots.Count == 0)
                {
                    CompleteWave();
                }
                else
                {
                    SetState(SessionState.Clearing);
                }

                return;
            }

            timeUntilNextSpawn -= elapsedSeconds;
            if (timeUntilNextSpawn <= 0f && liveBots.Count < CurrentWave.MaximumLiveBots)
            {
                if (botFactory.TryCreateBot(CurrentWave, out BotId bot))
                {
                    liveBots.Add(bot);
                }

                timeUntilNextSpawn = CurrentWave.SpawnInterval;
            }
        }

        private void AdvanceIntermission(float elapsedSeconds)
        {
            float totalIntermissionElapsed = intermissionElapsed + elapsedSeconds;
            if (totalIntermissionElapsed < plan.IntermissionDuration)
            {
                intermissionElapsed = totalIntermissionElapsed;
                return;
            }

            intermissionElapsed = plan.IntermissionDuration;
            currentWaveIndex++;
            BeginSpawning();

            float nextWaveElapsed = totalIntermissionElapsed - plan.IntermissionDuration;
            if (nextWaveElapsed > 0f)
            {
                Advance(nextWaveElapsed);
            }
        }

        private void CompleteWave()
        {
            if (currentWaveIndex == plan.Waves.Count - 1)
            {
                SetState(SessionState.Won);
                return;
            }

            playerRecovery.RestoreHealthFraction(plan.HealthRestoreFraction);
            intermissionElapsed = 0f;
            SetState(SessionState.Intermission);
        }

        private void BeginSpawning()
        {
            spawnElapsed = 0f;
            timeUntilNextSpawn = 0f;
            SetState(SessionState.Spawning);
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
