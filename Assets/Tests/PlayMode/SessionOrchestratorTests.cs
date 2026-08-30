using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotArena.Session.Tests
{
    public class SessionOrchestratorTests
    {
        [Test]
        public void Session_wins_after_four_cleared_waves_with_three_full_intermissions()
        {
            var factory = new FakeBotFactory(
                new BotId(1),
                new BotId(2),
                new BotId(3),
                new BotId(4));
            var recovery = new FakePlayerRecovery();
            var session = new SessionOrchestrator(
                new SessionPlan(
                    new[]
                    {
                        new WaveSchedule(1f, 0.25f, 2),
                        new WaveSchedule(1f, 0.20f, 3),
                        new WaveSchedule(1f, 0.15f, 4),
                        new WaveSchedule(1f, 0.10f, 5)
                    },
                    5f,
                    0.20f),
                factory,
                recovery);

            session.StartSession(Array.Empty<BotId>());

            for (int waveNumber = 1; waveNumber <= 4; waveNumber++)
            {
                Assert.That(session.CurrentWaveNumber, Is.EqualTo(waveNumber));
                Assert.That(session.State, Is.EqualTo(SessionState.Spawning));

                session.Advance(0f);
                session.Advance(1f);
                Assert.That(session.State, Is.EqualTo(SessionState.Clearing));
                session.RemoveBot(new BotId(waveNumber));

                if (waveNumber < 4)
                {
                    Assert.That(session.State, Is.EqualTo(SessionState.Intermission));
                    float activeTimeBeforeIntermission = session.ActiveTime;
                    session.Advance(4.99f);
                    Assert.That(session.State, Is.EqualTo(SessionState.Intermission));
                    session.Advance(0.01f);
                    Assert.That(session.ActiveTime, Is.EqualTo(activeTimeBeforeIntermission));
                }
            }

            Assert.That(session.State, Is.EqualTo(SessionState.Won));
            Assert.That(recovery.RestoredFractions, Is.EqualTo(new[] { 0.20f, 0.20f, 0.20f }));
        }

        [Test]
        public void Each_wave_uses_its_configured_spawn_and_shooting_difficulty()
        {
            var waves = new[]
            {
                new WaveSchedule(1f, 0.25f, 2, 1.00f, 1.00f),
                new WaveSchedule(1f, 0.20f, 3, 0.95f, 0.95f),
                new WaveSchedule(1f, 0.15f, 4, 0.90f, 0.90f),
                new WaveSchedule(1f, 0.10f, 5, 0.85f, 0.85f)
            };
            var factory = new RecordingBotFactory();
            var session = new SessionOrchestrator(
                new SessionPlan(waves, 5f, 0.20f),
                factory,
                new FakePlayerRecovery());

            session.StartSession(Array.Empty<BotId>());
            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                session.Advance(0f);
                session.Advance(1f);
                session.RemoveBot(new BotId(waveIndex + 1));
                if (waveIndex < waves.Length - 1)
                {
                    session.Advance(5f);
                }
            }

            Assert.That(factory.MaximumLiveBots, Is.EqualTo(new[] { 2, 3, 4, 5 }));
            Assert.That(factory.SpawnIntervals, Is.EqualTo(new[] { 0.25f, 0.20f, 0.15f, 0.10f }));
            Assert.That(factory.FireIntervalMultipliers, Is.EqualTo(new[] { 1.00f, 0.95f, 0.90f, 0.85f }));
            Assert.That(factory.AimConeMultipliers, Is.EqualTo(new[] { 1.00f, 0.95f, 0.90f, 0.85f }));
        }

        [Test]
        public void Time_past_the_intermission_boundary_advances_the_next_wave()
        {
            var session = new SessionOrchestrator(
                new SessionPlan(
                    new[]
                    {
                        new WaveSchedule(1f, 0.25f, 2),
                        new WaveSchedule(1f, 0.25f, 2)
                    },
                    5f,
                    0.20f),
                new FakeBotFactory(new BotId(1), new BotId(2)),
                new FakePlayerRecovery());
            session.StartSession(Array.Empty<BotId>());
            session.Advance(0f);
            session.Advance(1f);
            session.RemoveBot(new BotId(1));
            float activeTimeBeforeIntermission = session.ActiveTime;

            session.Advance(5.2f);

            Assert.That(session.State, Is.EqualTo(SessionState.Spawning));
            Assert.That(session.CurrentWaveNumber, Is.EqualTo(2));
            Assert.That(session.ActiveTime, Is.EqualTo(activeTimeBeforeIntermission + 0.2f).Within(0.0001f));
            Assert.That(session.SpawnTimeRemaining, Is.EqualTo(0.8f).Within(0.0001f));
        }

        [Test]
        public void Session_waits_for_spawn_end_and_all_registered_bots_before_victory()
        {
            var factory = new FakeBotFactory(new BotId(102));
            var session = CreateSession(factory);
            var placedBot = new BotId(101);

            session.StartSession(new[] { placedBot });
            session.Advance(0f);
            Assert.That(session.LiveBotCount, Is.EqualTo(2));

            session.Advance(1f);
            Assert.That(session.State, Is.EqualTo(SessionState.Clearing));
            session.RemoveBot(placedBot);
            Assert.That(session.State, Is.EqualTo(SessionState.Clearing));

            session.RemoveBot(new BotId(102));
            Assert.That(session.State, Is.EqualTo(SessionState.Won));
        }

        [Test]
        public void Unregistered_and_duplicate_bots_cannot_change_session_progress()
        {
            var session = CreateSession(new FakeBotFactory());
            var registeredBot = new BotId(201);

            session.StartSession(new[] { registeredBot });
            session.Advance(1f);

            Assert.That(session.RemoveBot(new BotId(999)), Is.False);
            Assert.That(session.LiveBotCount, Is.EqualTo(1));
            Assert.That(session.RemoveBot(registeredBot), Is.True);
            Assert.That(session.RemoveBot(registeredBot), Is.False);
        }

        [Test]
        public void Player_death_is_terminal()
        {
            var session = CreateSession(new FakeBotFactory());
            var bot = new BotId(301);

            session.StartSession(new[] { bot });
            session.DefeatPlayer();
            session.Advance(1f);
            session.RemoveBot(bot);

            Assert.That(session.State, Is.EqualTo(SessionState.Lost));
        }

        [UnityTest]
        public IEnumerator Disabling_a_live_bot_does_not_unregister_it_but_destroying_it_does()
        {
            var registry = new FakeRegistry();
            var botObject = new GameObject("Registered bot");
            SessionBotRegistration registration = botObject.AddComponent<SessionBotRegistration>();
            registration.Connect(registry);
            BotId registeredId = registration.Id;

            registration.enabled = false;
            yield return null;
            Assert.That(registry.UnregisteredBots, Is.Empty);

            UnityEngine.Object.Destroy(botObject);
            yield return null;
            Assert.That(registry.UnregisteredBots, Is.EqualTo(new[] { registeredId }));
        }

        [UnityTest]
        public IEnumerator Destroying_an_unregistered_damageable_does_not_reduce_live_bots()
        {
            Type managerType = Type.GetType("BotSpawnManager, Assembly-CSharp", true);
            Type healthType = Type.GetType("RobotHealth, Assembly-CSharp", true);
            var managerObject = new GameObject("Session manager");
            Component manager = managerObject.AddComponent(managerType);
            var spawnPoints = new GameObject("Spawn points");
            managerType.GetField("spawnPointsRoot").SetValue(manager, spawnPoints.transform);

            var botObject = new GameObject("Placed bot");
            botObject.AddComponent<SessionBotRegistration>();
            yield return null;
            Assert.That(
                managerType.GetProperty("TotalWaves").GetValue(manager),
                Is.EqualTo(4));
            Assert.That(ReadLiveBotCount(managerType, manager), Is.EqualTo(1));

            var foreignObject = new GameObject("Unregistered damageable");
            Component foreignHealth = foreignObject.AddComponent(healthType);
            yield return null;
            healthType.GetMethod("TakeDamage").Invoke(foreignHealth, new object[] { 1000f });
            yield return null;

            Assert.That(ReadLiveBotCount(managerType, manager), Is.EqualTo(1));
            UnityEngine.Object.Destroy(botObject);
            yield return null;
            Assert.That(ReadLiveBotCount(managerType, manager), Is.Zero);

            UnityEngine.Object.Destroy(managerObject);
            UnityEngine.Object.Destroy(spawnPoints);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Damaging_the_player_transitions_the_session_manager_to_lost()
        {
            Type managerType = Type.GetType("BotSpawnManager, Assembly-CSharp", true);
            Type playerHealthType = Type.GetType("PlayerHP, Assembly-CSharp", true);
            var managerObject = new GameObject("Session manager");
            Component manager = managerObject.AddComponent(managerType);
            var spawnPoints = new GameObject("Spawn points");
            managerType.GetField("spawnPointsRoot").SetValue(manager, spawnPoints.transform);
            var playerObject = new GameObject("Player");
            Component playerHealth = playerObject.AddComponent(playerHealthType);

            yield return null;
            playerHealthType.GetMethod("TakeDamage").Invoke(playerHealth, new object[] { 1000f });

            Assert.That(
                managerType.GetProperty("State").GetValue(manager),
                Is.EqualTo(SessionState.Lost));

            UnityEngine.Object.Destroy(playerObject);
            UnityEngine.Object.Destroy(managerObject);
            UnityEngine.Object.Destroy(spawnPoints);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_recovery_restores_twenty_percent_and_caps_at_maximum_health()
        {
            Type managerType = Type.GetType("BotSpawnManager, Assembly-CSharp", true);
            Type playerHealthType = Type.GetType("PlayerHP, Assembly-CSharp", true);
            var managerObject = new GameObject("Session manager");
            Component manager = managerObject.AddComponent(managerType);
            var spawnPoints = new GameObject("Spawn points");
            managerType.GetField("spawnPointsRoot").SetValue(manager, spawnPoints.transform);
            var playerObject = new GameObject("Player");
            Component playerHealth = playerObject.AddComponent(playerHealthType);
            playerHealthType.GetField("maxHealth").SetValue(playerHealth, 100f);
            float observedHealth = -1f;
            Action<float, float> observeHealth = (current, maximum) => observedHealth = current;
            playerHealthType.GetEvent("OnHealthChanged").AddEventHandler(playerHealth, observeHealth);

            yield return null;
            playerHealthType.GetMethod("TakeDamage").Invoke(playerHealth, new object[] { 50f });
            managerType.GetMethod("RestoreHealthFraction").Invoke(manager, new object[] { 0.20f });
            Assert.That(observedHealth, Is.EqualTo(70f));

            managerType.GetMethod("RestoreHealthFraction").Invoke(manager, new object[] { 0.20f });
            managerType.GetMethod("RestoreHealthFraction").Invoke(manager, new object[] { 0.20f });
            Assert.That(observedHealth, Is.EqualTo(100f));

            playerHealthType.GetEvent("OnHealthChanged").RemoveEventHandler(playerHealth, observeHealth);
            UnityEngine.Object.Destroy(playerObject);
            UnityEngine.Object.Destroy(managerObject);
            UnityEngine.Object.Destroy(spawnPoints);
            yield return null;
        }

        private static SessionOrchestrator CreateSession(ISessionBotFactory factory)
        {
            return new SessionOrchestrator(
                new SessionPlan(new[] { new WaveSchedule(1f, 0.25f, 5) }, 5f, 0.20f),
                factory,
                new FakePlayerRecovery());
        }

        private static int ReadLiveBotCount(Type managerType, Component manager)
        {
            return (int)managerType.GetProperty("LiveBotCount").GetValue(manager);
        }

        private sealed class FakeBotFactory : ISessionBotFactory
        {
            private readonly Queue<BotId> bots;

            public FakeBotFactory(params BotId[] bots)
            {
                this.bots = new Queue<BotId>(bots);
            }

            public bool TryCreateBot(WaveSchedule wave, out BotId bot)
            {
                if (bots.Count == 0)
                {
                    bot = default;
                    return false;
                }

                bot = bots.Dequeue();
                return true;
            }
        }

        private sealed class FakeRegistry : ISessionBotRegistry
        {
            public List<BotId> UnregisteredBots { get; } = new List<BotId>();

            public bool RegisterBot(SessionBotRegistration bot)
            {
                return true;
            }

            public bool UnregisterBot(SessionBotRegistration bot)
            {
                UnregisteredBots.Add(bot.Id);
                return true;
            }
        }

        private sealed class RecordingBotFactory : ISessionBotFactory
        {
            private int nextBotId = 1;

            public List<int> MaximumLiveBots { get; } = new List<int>();
            public List<float> SpawnIntervals { get; } = new List<float>();
            public List<float> FireIntervalMultipliers { get; } = new List<float>();
            public List<float> AimConeMultipliers { get; } = new List<float>();

            public bool TryCreateBot(WaveSchedule wave, out BotId bot)
            {
                MaximumLiveBots.Add(wave.MaximumLiveBots);
                SpawnIntervals.Add(wave.SpawnInterval);
                FireIntervalMultipliers.Add(wave.FireIntervalMultiplier);
                AimConeMultipliers.Add(wave.AimConeMultiplier);
                bot = new BotId(nextBotId++);
                return true;
            }
        }

        private sealed class FakePlayerRecovery : IPlayerRecovery
        {
            public List<float> RestoredFractions { get; } = new List<float>();

            public void RestoreHealthFraction(float fraction)
            {
                RestoredFractions.Add(fraction);
            }
        }
    }
}
