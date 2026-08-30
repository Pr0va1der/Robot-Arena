using System;
using System.Collections.Generic;

namespace RobotArena.Session
{
    public sealed class SessionPlan
    {
        private readonly WaveSchedule[] waves;

        public SessionPlan(
            IReadOnlyList<WaveSchedule> waves,
            float intermissionDuration,
            float healthRestoreFraction)
        {
            if (waves == null)
            {
                throw new ArgumentNullException(nameof(waves));
            }

            if (waves.Count == 0)
            {
                throw new ArgumentException("A session needs at least one wave.", nameof(waves));
            }

            if (intermissionDuration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(intermissionDuration));
            }

            if (healthRestoreFraction < 0f || healthRestoreFraction > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(healthRestoreFraction));
            }

            this.waves = new WaveSchedule[waves.Count];
            for (int index = 0; index < waves.Count; index++)
            {
                this.waves[index] = waves[index];
            }

            IntermissionDuration = intermissionDuration;
            HealthRestoreFraction = healthRestoreFraction;
        }

        public IReadOnlyList<WaveSchedule> Waves => waves;
        public float IntermissionDuration { get; }
        public float HealthRestoreFraction { get; }
    }
}
