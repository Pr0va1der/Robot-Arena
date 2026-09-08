namespace RobotArena.Session
{
    /// <summary>
    /// Converts a held jump action into one press per release cycle.
    /// </summary>
    public sealed class JumpInputGate
    {
        private bool wasHeld;

        public bool Consume(bool isHeld, bool isPaused)
        {
            bool wasPressed = isHeld && !wasHeld;
            wasHeld = isHeld;

            return !isPaused && wasPressed;
        }
    }
}
