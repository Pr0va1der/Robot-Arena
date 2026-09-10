using System;

namespace RobotArena.Session
{
    public enum MusicPlaybackPhase
    {
        Silent,
        WaitingForIntro,
        Intro,
        Loop
    }

    public sealed class MusicPlaybackSequence
    {
        public MusicPlaybackPhase Phase { get; private set; } = MusicPlaybackPhase.Silent;
        public float IntroRemainingSeconds { get; private set; }

        public void StartIntro(float durationSeconds)
        {
            QueueIntro(durationSeconds);
            MarkIntroStarted();
        }

        public void QueueIntro(float durationSeconds)
        {
            if (durationSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            }

            IntroRemainingSeconds = durationSeconds;
            Phase = durationSeconds == 0f
                ? MusicPlaybackPhase.Loop
                : MusicPlaybackPhase.WaitingForIntro;
        }

        public bool MarkIntroStarted(float actualPositionSeconds = 0f)
        {
            if (Phase != MusicPlaybackPhase.WaitingForIntro)
            {
                return false;
            }

            if (float.IsNaN(actualPositionSeconds) ||
                float.IsInfinity(actualPositionSeconds) ||
                actualPositionSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(actualPositionSeconds));
            }

            Phase = MusicPlaybackPhase.Intro;
            IntroRemainingSeconds = Math.Max(0f, IntroRemainingSeconds - actualPositionSeconds);
            return true;
        }

        public bool Advance(
            float elapsedSeconds,
            bool isPaused,
            bool actualIntroComplete = true)
        {
            if (elapsedSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            }

            if (isPaused || Phase != MusicPlaybackPhase.Intro)
            {
                return false;
            }

            IntroRemainingSeconds = Math.Max(0f, IntroRemainingSeconds - elapsedSeconds);
            if (IntroRemainingSeconds > 0f || !actualIntroComplete)
            {
                return false;
            }

            Phase = MusicPlaybackPhase.Loop;
            return true;
        }

        public void Stop()
        {
            IntroRemainingSeconds = 0f;
            Phase = MusicPlaybackPhase.Silent;
        }
    }
}
