using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotArena.Session.Tests
{
    public sealed class BotNavigationTests
    {
        private Scene previousScene;

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            previousScene = SceneManager.GetActiveScene();
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestorePreviousScene()
        {
            if (previousScene.IsValid() && !string.IsNullOrEmpty(previousScene.path))
            {
                yield return SceneManager.LoadSceneAsync(previousScene.path);
            }
        }

        [UnityTest]
        public IEnumerator Placed_bots_start_on_the_loaded_nav_mesh()
        {
            AssertAllSceneAgentsAreOnNavMesh();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Wave_bots_are_created_on_the_loaded_nav_mesh()
        {
            Type managerType = Type.GetType("BotSpawnManager, Assembly-CSharp", true);
            Component manager = (Component)FindSceneObject(managerType);
            MethodInfo createBot = managerType.GetMethod("TryCreateBot");
            Assert.That(createBot, Is.Not.Null);

            object[] arguments =
            {
                new WaveSchedule(60f, 2f, 5),
                null
            };
            bool created = (bool)createBot.Invoke(manager, arguments);

            Assert.That(created, Is.True);
            yield return null;
            AssertAllSceneAgentsAreOnNavMesh();
        }

        private static void AssertAllSceneAgentsAreOnNavMesh()
        {
            Type agentType = Type.GetType("UnityEngine.AI.NavMeshAgent, UnityEngine.AIModule", true);
            PropertyInfo isOnNavMesh = agentType.GetProperty("isOnNavMesh");
            Assert.That(isOnNavMesh, Is.Not.Null);

            UnityEngine.Object[] agents = UnityEngine.Object.FindObjectsOfType(agentType);
            Assert.That(agents.Length, Is.GreaterThanOrEqualTo(11));
            foreach (UnityEngine.Object agent in agents)
            {
                bool registered = (bool)isOnNavMesh.GetValue(agent);
                Assert.That(registered, Is.True, agent.name + " must be registered on NavMesh before combat.");
            }
        }

        private static UnityEngine.Object FindSceneObject(Type componentType)
        {
            UnityEngine.Object[] objects = UnityEngine.Object.FindObjectsOfType(componentType);
            Assert.That(objects.Length, Is.EqualTo(1));
            return objects[0];
        }
    }
}
