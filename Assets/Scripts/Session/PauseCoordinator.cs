using System;

namespace RobotArena.Session
{
    public sealed class PauseCoordinator
    {
        private const PauseSource KnownSources =
            PauseSource.User |
            PauseSource.Focus |
            PauseSource.Platform |
            PauseSource.Advertisement |
            PauseSource.Result |
            PauseSource.Tutorial;
        private const PauseSource SystemSources =
            PauseSource.Focus |
            PauseSource.Platform |
            PauseSource.Advertisement;
        private const PauseSource NonAudioSources =
            PauseSource.Result |
            PauseSource.Tutorial;

        private PauseSource activeSources;
        private bool requiresPointerLockClick;

        public event Action PauseStateChanged;

        public bool IsPaused => activeSources != PauseSource.None;
        public bool IsGameplayPaused => IsPaused || requiresPointerLockClick;
        public bool IsWaitingForResume => !IsPaused && requiresPointerLockClick;
        public bool IsAudioPaused => (activeSources & ~NonAudioSources) != PauseSource.None;
        public PauseSource ActiveSources => activeSources;
        public bool RequiresPointerLockClick => requiresPointerLockClick;

        public void RequirePointerLockClick()
        {
            if (requiresPointerLockClick)
            {
                return;
            }

            PauseState previousState = CaptureState();
            requiresPointerLockClick = true;
            NotifyIfStateChanged(previousState);
        }

        public void ClearResumeRequirement()
        {
            if (!requiresPointerLockClick)
            {
                return;
            }

            PauseState previousState = CaptureState();
            requiresPointerLockClick = false;
            NotifyIfStateChanged(previousState);
        }

        public void SetSource(PauseSource source, bool isActive)
        {
            ValidateSource(source);

            PauseState previousState = CaptureState();

            if (isActive && (source & SystemSources) != PauseSource.None)
            {
                requiresPointerLockClick = true;
            }

            if (isActive && (source & PauseSource.Result) != PauseSource.None)
            {
                requiresPointerLockClick = false;
            }

            if (isActive)
            {
                activeSources |= source;
            }
            else
            {
                activeSources &= ~source;
            }

            NotifyIfStateChanged(previousState);
        }

        public bool IsSourceActive(PauseSource source)
        {
            ValidateSource(source);
            return (activeSources & source) == source;
        }

        public bool TryConsumePointerLockRequest()
        {
            if (IsPaused || !requiresPointerLockClick)
            {
                return false;
            }

            PauseState previousState = CaptureState();
            requiresPointerLockClick = false;
            NotifyIfStateChanged(previousState);
            return true;
        }

        private PauseState CaptureState()
        {
            return new PauseState(IsPaused, IsGameplayPaused, IsAudioPaused);
        }

        private void NotifyIfStateChanged(PauseState previousState)
        {
            if (previousState.HasChanged(this))
            {
                PauseStateChanged?.Invoke();
            }
        }

        private static void ValidateSource(PauseSource source)
        {
            if (source == PauseSource.None || (source & ~KnownSources) != PauseSource.None)
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }
        }

        private readonly struct PauseState
        {
            public PauseState(bool isPaused, bool isGameplayPaused, bool isAudioPaused)
            {
                IsPaused = isPaused;
                IsGameplayPaused = isGameplayPaused;
                IsAudioPaused = isAudioPaused;
            }

            private bool IsPaused { get; }
            private bool IsGameplayPaused { get; }
            private bool IsAudioPaused { get; }

            public bool HasChanged(PauseCoordinator current)
            {
                return IsPaused != current.IsPaused ||
                       IsGameplayPaused != current.IsGameplayPaused ||
                       IsAudioPaused != current.IsAudioPaused;
            }
        }
    }
}
