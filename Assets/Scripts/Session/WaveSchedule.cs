using System;

namespace RobotArena.Session
{
    public readonly struct WaveSchedule
    {
        public WaveSchedule(float spawnDuration, float spawnInterval, int maximumLiveBots)
        {
            if (spawnDuration <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnDuration));
            }

            if (spawnInterval <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnInterval));
            }

            if (maximumLiveBots <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumLiveBots));
            }

            SpawnDuration = spawnDuration;
            SpawnInterval = spawnInterval;
            MaximumLiveBots = maximumLiveBots;
        }

        public float SpawnDuration { get; }
        public float SpawnInterval { get; }
        public int MaximumLiveBots { get; }
    }
}
