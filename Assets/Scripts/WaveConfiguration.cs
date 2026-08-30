using System;
using System.Collections.Generic;
using RobotArena.Session;

[Serializable]
public sealed class WaveConfiguration
{
    public float spawnDuration = 60f;
    public float spawnInterval = 2f;
    public int maximumLiveBots = 5;
    public float fireIntervalMultiplier = 1f;
    public float aimConeMultiplier = 1f;

    public WaveSchedule ToSchedule()
    {
        return new WaveSchedule(
            spawnDuration,
            spawnInterval,
            maximumLiveBots,
            fireIntervalMultiplier,
            aimConeMultiplier);
    }

    public static List<WaveConfiguration> CreateDefaults()
    {
        return new List<WaveConfiguration>
        {
            Create(60f, 2.0f, 5, 1.00f, 1.00f),
            Create(60f, 1.8f, 6, 0.95f, 0.95f),
            Create(60f, 1.6f, 7, 0.90f, 0.90f),
            Create(60f, 1.4f, 8, 0.85f, 0.85f)
        };
    }

    private static WaveConfiguration Create(
        float duration,
        float interval,
        int maximumBots,
        float fireMultiplier,
        float aimMultiplier)
    {
        return new WaveConfiguration
        {
            spawnDuration = duration,
            spawnInterval = interval,
            maximumLiveBots = maximumBots,
            fireIntervalMultiplier = fireMultiplier,
            aimConeMultiplier = aimMultiplier
        };
    }
}
