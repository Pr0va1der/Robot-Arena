using System;

namespace RobotArena.Session
{
    public sealed class MusicStateMachine
    {
        public const float CalmReturnDelay = 2f;

        private bool combatPresence;
        private bool terminal;
        private MusicMode terminalMode;
        private float quietElapsed;
        private bool waitingForCalm;

        public event Action<MusicCue> CueRequested;

        public MusicMode Mode { get; private set; } = MusicMode.Silent;
        public bool AudioPermissionGranted { get; private set; }
        public bool IsCalmIntroPlaying { get; private set; }
        public bool CombatPresence => combatPresence;

        public void StartCalm()
        {
            terminal = false;
            terminalMode = MusicMode.Silent;
            waitingForCalm = false;
            quietElapsed = 0f;
            IsCalmIntroPlaying = false;

            if (AudioPermissionGranted)
            {
                BeginCalmIntro();
            }
            else
            {
                Mode = MusicMode.Silent;
            }
        }

        public void RegisterAudioGesture()
        {
            if (AudioPermissionGranted)
            {
                return;
            }

            AudioPermissionGranted = true;
            if (terminal)
            {
                if (terminalMode == MusicMode.Death)
                {
                    EnterMode(MusicMode.Death, MusicCue.Death);
                }

                return;
            }

            BeginCalmIntro();
        }

        public void SetCombatPresence(bool isPresent)
        {
            combatPresence = isPresent;

            if (!AudioPermissionGranted || terminal)
            {
                return;
            }

            if (isPresent)
            {
                bool reenteredDuringCalmDebounce = waitingForCalm;
                waitingForCalm = false;
                quietElapsed = 0f;
                if (IsCalmIntroPlaying)
                {
                    return;
                }

                if (Mode != MusicMode.Combat || reenteredDuringCalmDebounce)
                {
                    EnterMode(MusicMode.Combat, MusicCue.CombatIntro);
                }

                return;
            }

            if (Mode == MusicMode.Combat)
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

            if (isPaused ||
                !AudioPermissionGranted ||
                !waitingForCalm ||
                combatPresence ||
                terminal ||
                IsCalmIntroPlaying)
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
            BeginCalmIntro();
        }

        public void CompleteCalmIntro()
        {
            if (!IsCalmIntroPlaying)
            {
                return;
            }

            IsCalmIntroPlaying = false;
            if (terminal)
            {
                return;
            }

            if (combatPresence)
            {
                EnterMode(MusicMode.Combat, MusicCue.CombatIntro);
            }
            else
            {
                Mode = MusicMode.Calm;
            }
        }

        public void CompleteDeath()
        {
            if (terminal)
            {
                return;
            }

            terminal = true;
            terminalMode = MusicMode.Death;
            IsCalmIntroPlaying = false;
            waitingForCalm = false;
            quietElapsed = 0f;
            if (AudioPermissionGranted)
            {
                EnterMode(MusicMode.Death, MusicCue.Death);
            }
            else
            {
                Mode = MusicMode.Silent;
            }
        }

        public void CompleteDeathPlayback()
        {
            if (!terminal || terminalMode != MusicMode.Death || Mode != MusicMode.Death)
            {
                return;
            }

            EnterMode(MusicMode.Silent, MusicCue.Silent);
        }

        public void CompleteVictory()
        {
            if (terminal)
            {
                return;
            }

            terminal = true;
            terminalMode = MusicMode.Silent;
            IsCalmIntroPlaying = false;
            waitingForCalm = false;
            quietElapsed = 0f;
            if (AudioPermissionGranted)
            {
                EnterMode(MusicMode.Silent, MusicCue.Silent);
            }
            else
            {
                Mode = MusicMode.Silent;
            }
        }

        private void BeginCalmIntro()
        {
            if (!AudioPermissionGranted || terminal)
            {
                return;
            }

            IsCalmIntroPlaying = true;
            EnterMode(MusicMode.Calm, MusicCue.CalmIntro);
        }

        private void EnterMode(MusicMode mode, MusicCue cue)
        {
            Mode = mode;
            CueRequested?.Invoke(cue);
        }
    }
}
