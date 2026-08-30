using System;

namespace RobotArena.Session
{
    public sealed class SessionResultTracker
    {
        private readonly ISessionBestTimeStore bestTimeStore;

        public SessionResultTracker(ISessionBestTimeStore bestTimeStore)
        {
            this.bestTimeStore = bestTimeStore;
            if (bestTimeStore != null &&
                bestTimeStore.TryLoadBestTime(out float bestTime) &&
                SessionResult.IsValidActiveTime(bestTime))
            {
                BestTime = bestTime;
            }
        }

        public SessionResult? Result { get; private set; }
        public float? BestTime { get; private set; }

        public void BeginSession()
        {
            Result = null;
        }

        public void Complete(SessionResult result)
        {
            if (Result.HasValue)
            {
                throw new InvalidOperationException("A session result has already been completed.");
            }

            Result = result;

            if (result.Outcome == SessionOutcome.Won &&
                (!BestTime.HasValue || result.ActiveTime < BestTime.Value))
            {
                BestTime = result.ActiveTime;
                bestTimeStore?.SaveBestTime(result.ActiveTime);
            }
        }
    }
}
