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
        private readonly SessionResultTracker resultTracker;
        private readonly PauseCoordinator pauseCoordinator;
        private int currentWaveIndex;
        private float spawnElapsed;
        private float timeUntilNextSpawn;
        private float intermissionElapsed;

        public SessionOrchestrator(
            SessionPlan plan,
            ISessionBotFactory botFactory,
            IPlayerRecovery playerRecovery)
            : this(plan, botFactory, playerRecovery, null, null)
        {
        }

        public SessionOrchestrator(
            SessionPlan plan,
            ISessionBotFactory botFactory,
            IPlayerRecovery playerRecovery,
            ISessionBestTimeStore bestTimeStore)
            : this(plan, botFactory, playerRecovery, bestTimeStore, null)
        {
        }

        public SessionOrchestrator(
            SessionPlan plan,
            ISessionBotFactory botFactory,
            IPlayerRecovery playerRecovery,
            ISessionBestTimeStore bestTimeStore,
            PauseCoordinator pauseCoordinator)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.botFactory = botFactory ?? throw new ArgumentNullException(nameof(botFactory));
            this.playerRecovery = playerRecovery ?? throw new ArgumentNullException(nameof(playerRecovery));
            resultTracker = new SessionResultTracker(bestTimeStore);
            this.pauseCoordinator = pauseCoordinator ?? new PauseCoordinator();
        }

        public event Action<SessionState> StateChanged;

        public SessionState State { get; private set; } = SessionState.NotStarted;
        public int LiveBotCount => liveBots.Count;
        public int CurrentWaveNumber => currentWaveIndex + 1;
        public int TotalWaves => plan.Waves.Count;
        public WaveSchedule CurrentWave => plan.Waves[currentWaveIndex];
        public float ActiveTime { get; private set; }
        public SessionResult? Result => resultTracker.Result;
        public float? BestTime => resultTracker.BestTime;
        public float SpawnTimeRemaining => Math.Max(0f, CurrentWave.SpawnDuration - spawnElapsed);
        public float IntermissionTimeRemaining => Math.Max(0f, plan.IntermissionDuration - intermissionElapsed);

        public void StartSession(IEnumerable<BotId> initialBots)
        {
            if (initialBots == null)
            {
                throw new ArgumentNullException(nameof(initialBots));
            }

            resultTracker.BeginSession();
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

            if (pauseCoordinator.IsPaused)
            {
                return;
            }

            if (elapsedSeconds == 0f)
            {
                if (State == SessionState.Spawning)
                {
                    AdvanceSpawning(0f);
                }
                else if (State == SessionState.Intermission && plan.IntermissionDuration == 0f)
                {
                    AdvanceIntermission(0f);
                }

                return;
            }

            float remainingSeconds = elapsedSeconds;
            while (remainingSeconds > 0f)
            {
                if (State == SessionState.Spawning)
                {
                    float timeUntilSpawnEnds = CurrentWave.SpawnDuration - spawnElapsed;
                    if (timeUntilSpawnEnds <= 0f)
                    {
                        SessionState stateBeforeTransition = State;
                        AdvanceSpawning(0f);
                        if (State == stateBeforeTransition)
                        {
                            break;
                        }

                        continue;
                    }

                    float activeStep = Math.Min(remainingSeconds, timeUntilSpawnEnds);
                    ActiveTime += activeStep;
                    AdvanceSpawning(activeStep);
                    remainingSeconds -= activeStep;
                }
                else if (State == SessionState.Clearing)
                {
                    ActiveTime += remainingSeconds;
                    remainingSeconds = 0f;
                }
                else if (State == SessionState.Intermission)
                {
                    float timeUntilIntermissionEnds = plan.IntermissionDuration - intermissionElapsed;
                    if (timeUntilIntermissionEnds <= 0f)
                    {
                        SessionState stateBeforeTransition = State;
                        AdvanceIntermission(0f);
                        if (State == stateBeforeTransition)
                        {
                            break;
                        }

                        continue;
                    }

                    float intermissionStep = Math.Min(remainingSeconds, timeUntilIntermissionEnds);
                    AdvanceIntermission(intermissionStep);
                    remainingSeconds -= intermissionStep;
                }
                else
                {
                    remainingSeconds = 0f;
                }
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
                CompleteSession(SessionOutcome.Lost, SessionState.Lost);
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
            intermissionElapsed = Math.Min(
                plan.IntermissionDuration,
                intermissionElapsed + elapsedSeconds);
            if (intermissionElapsed < plan.IntermissionDuration)
            {
                return;
            }

            currentWaveIndex++;
            BeginSpawning();
        }

        private void CompleteWave()
        {
            if (currentWaveIndex == plan.Waves.Count - 1)
            {
                CompleteSession(SessionOutcome.Won, SessionState.Won);
                return;
            }

            playerRecovery.RestoreHealthFraction(plan.HealthRestoreFraction);
            intermissionElapsed = 0f;
            SetState(SessionState.Intermission);
        }

        private void CompleteSession(SessionOutcome outcome, SessionState state)
        {
            var result = new SessionResult(outcome, CurrentWaveNumber, ActiveTime);
            resultTracker.Complete(result);
            SetState(state);
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
