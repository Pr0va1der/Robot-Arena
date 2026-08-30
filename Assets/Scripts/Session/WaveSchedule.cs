using System;

namespace RobotArena.Session
{
    public readonly struct WaveSchedule
    {
        public WaveSchedule(float spawnDuration, float spawnInterval, int maximumLiveBots)
            : this(spawnDuration, spawnInterval, maximumLiveBots, 1f, 1f)
        {
        }

        public WaveSchedule(
            float spawnDuration,
            float spawnInterval,
            int maximumLiveBots,
            float fireIntervalMultiplier,
            float aimConeMultiplier)
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

            if (fireIntervalMultiplier <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fireIntervalMultiplier));
            }

            if (aimConeMultiplier <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(aimConeMultiplier));
            }

            SpawnDuration = spawnDuration;
            SpawnInterval = spawnInterval;
            MaximumLiveBots = maximumLiveBots;
            FireIntervalMultiplier = fireIntervalMultiplier;
            AimConeMultiplier = aimConeMultiplier;
        }

        public float SpawnDuration { get; }
        public float SpawnInterval { get; }
        public int MaximumLiveBots { get; }
        public float FireIntervalMultiplier { get; }
        public float AimConeMultiplier { get; }
    }
}
