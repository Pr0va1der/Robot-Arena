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

        private static SessionOrchestrator CreateSession(ISessionBotFactory factory)
        {
            return new SessionOrchestrator(new WaveSchedule(1f, 0.25f, 5), factory);
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

            public bool TryCreateBot(out BotId bot)
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
    }
}
