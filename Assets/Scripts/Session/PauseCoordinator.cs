using System;

namespace RobotArena.Session
{
    public sealed class PauseCoordinator
    {
        private const PauseSource KnownSources =
            PauseSource.User |
            PauseSource.Focus |
            PauseSource.Platform |
            PauseSource.Advertisement;
        private const PauseSource SystemSources =
            PauseSource.Focus |
            PauseSource.Platform |
            PauseSource.Advertisement;

        private PauseSource activeSources;
        private bool requiresPointerLockClick;

        public event Action<bool> PauseStateChanged;

        public bool IsPaused => activeSources != PauseSource.None;
        public PauseSource ActiveSources => activeSources;
        public bool RequiresPointerLockClick => requiresPointerLockClick;

        public void SetSource(PauseSource source, bool isActive)
        {
            ValidateSource(source);

            if (isActive && (source & SystemSources) != PauseSource.None)
            {
                requiresPointerLockClick = true;
            }

            PauseSource previousSources = activeSources;
            if (isActive)
            {
                activeSources |= source;
            }
            else
            {
                activeSources &= ~source;
            }

            if (previousSources != activeSources)
            {
                PauseStateChanged?.Invoke(IsPaused);
            }
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

            requiresPointerLockClick = false;
            return true;
        }

        private static void ValidateSource(PauseSource source)
        {
            if (source == PauseSource.None || (source & ~KnownSources) != PauseSource.None)
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }
        }
    }
}
