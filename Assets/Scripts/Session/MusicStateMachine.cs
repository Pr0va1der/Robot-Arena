using System;

namespace RobotArena.Session
{
    public sealed class MusicStateMachine
    {
        public const float CalmReturnDelay = 2f;

        private bool combatPresence;
        private bool terminal;
        private float quietElapsed;
        private bool waitingForCalm;

        public event Action<MusicCue> CueRequested;

        public MusicMode Mode { get; private set; } = MusicMode.Silent;

        public void StartCalm()
        {
            combatPresence = false;
            terminal = false;
            waitingForCalm = false;
            quietElapsed = 0f;
            EnterMode(MusicMode.Calm, MusicCue.CalmIntro);
        }

        public void SetCombatPresence(bool isPresent)
        {
            combatPresence = isPresent;

            if (isPresent)
            {
                waitingForCalm = false;
                quietElapsed = 0f;
                if (!terminal && Mode != MusicMode.Combat)
                {
                    EnterMode(MusicMode.Combat, MusicCue.CombatIntro);
                }

                return;
            }

            if (Mode == MusicMode.Combat && !terminal)
            {
                waitingForCalm = true;
                quietElapsed = 0f;
            }
        }

        public void Advance(float elapsedSeconds, bool isPaused)
        {
            if (elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            if (isPaused || !waitingForCalm || combatPresence || terminal)
            {
                return;
            }

            quietElapsed += elapsedSeconds;
            if (quietElapsed < CalmReturnDelay)
            {
                return;
            }

            waitingForCalm = false;
            quietElapsed = 0f;
            EnterMode(MusicMode.Calm, MusicCue.CalmIntro);
        }

        public void CompleteDeath()
        {
            if (terminal)
            {
                return;
            }

            terminal = true;
            waitingForCalm = false;
            quietElapsed = 0f;
            EnterMode(MusicMode.Death, MusicCue.Death);
        }

        public void CompleteVictory()
        {
            if (terminal)
            {
                return;
            }

            terminal = true;
            waitingForCalm = false;
            quietElapsed = 0f;
            EnterMode(MusicMode.Silent, MusicCue.Silent);
        }

        private void EnterMode(MusicMode mode, MusicCue cue)
        {
            Mode = mode;
            CueRequested?.Invoke(cue);
        }
    }
}
