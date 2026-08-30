using System;

namespace RobotArena.Session
{
    public readonly struct SessionResult : IEquatable<SessionResult>
    {
        public SessionResult(SessionOutcome outcome, int reachedWave, float activeTime)
        {
            if (outcome != SessionOutcome.Won && outcome != SessionOutcome.Lost)
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            if (reachedWave <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reachedWave));
            }

            if (!IsValidActiveTime(activeTime))
            {
                throw new ArgumentOutOfRangeException(nameof(activeTime));
            }

            Outcome = outcome;
            ReachedWave = reachedWave;
            ActiveTime = activeTime;
        }

        public SessionOutcome Outcome { get; }
        public int ReachedWave { get; }
        public float ActiveTime { get; }

        internal static bool IsValidActiveTime(float activeTime)
        {
            return activeTime >= 0f &&
                   !float.IsNaN(activeTime) &&
                   !float.IsInfinity(activeTime);
        }

        public bool Equals(SessionResult other)
        {
            return Outcome == other.Outcome &&
                   ReachedWave == other.ReachedWave &&
                   ActiveTime.Equals(other.ActiveTime);
        }

        public override bool Equals(object obj)
        {
            return obj is SessionResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Outcome;
                hash = (hash * 397) ^ ReachedWave;
                hash = (hash * 397) ^ ActiveTime.GetHashCode();
                return hash;
            }
        }
    }
}
