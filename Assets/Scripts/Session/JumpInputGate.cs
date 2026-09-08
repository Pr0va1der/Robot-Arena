namespace RobotArena.Session
{
    /// <summary>
    /// Converts a held jump action into one press per release cycle.
    /// </summary>
    public sealed class JumpInputGate
    {
        private bool wasHeld;
        private bool pendingPress;

        public void Sample(bool isHeld, bool isPaused)
        {
            bool wasPressed = isHeld && !wasHeld;
            wasHeld = isHeld;

            if (isPaused)
            {
                pendingPress = false;
                return;
            }

            if (wasPressed)
            {
                pendingPress = true;
            }
        }

        public bool Consume()
        {
            bool wasPressed = pendingPress;
            pendingPress = false;
            return wasPressed;
        }

        public bool Consume(bool isHeld, bool isPaused)
        {
            Sample(isHeld, isPaused);
            return Consume();
        }
    }
}
