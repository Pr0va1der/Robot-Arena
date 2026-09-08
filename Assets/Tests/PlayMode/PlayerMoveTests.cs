using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotArena.Session.Tests
{
    public sealed class PlayerMoveTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator DestroyCreatedObjects()
        {
            for (int i = 0; i < createdObjects.Count; i++)
            {
                if (createdObjects[i] != null)
                {
                    UnityEngine.Object.Destroy(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Jump_uses_velocity_change_instead_of_rigidbody_mass()
        {
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            GameObject ground = new GameObject("Ground");
            createdObjects.Add(ground);
            GameObject lightPlayer = null;
            GameObject heavyPlayer = null;

            try
            {
                ground.tag = "Ground";
                ground.AddComponent<BoxCollider>().size = new Vector3(20f, 1f, 20f);
                ground.transform.position = new Vector3(1000f, -0.5f, 0f);

                lightPlayer = CreatePlayer("LightPlayer", 998f, 1f);
                heavyPlayer = CreatePlayer("HeavyPlayer", 1002f, 1000f);
                createdObjects.Add(lightPlayer);
                createdObjects.Add(heavyPlayer);

                yield return null;

                Type playerMoveType = Type.GetType("PlayerMove, Assembly-CSharp", true);
                MethodInfo tryJump = playerMoveType.GetMethod("TryJump", BindingFlags.Instance | BindingFlags.Public);
                FieldInfo groundedField = playerMoveType.GetField(
                    "_isGrounded",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Component lightMove = lightPlayer.GetComponent(playerMoveType);
                Component heavyMove = heavyPlayer.GetComponent(playerMoveType);

                // The collision callback owns this state in production. Set the
                // observed grounded seam here so this test remains independent
                // from any pause/tutorial state in the integration scene.
                groundedField.SetValue(lightMove, true);
                groundedField.SetValue(heavyMove, true);

                Assert.That((bool)tryJump.Invoke(lightMove, null), Is.True);
                Assert.That((bool)tryJump.Invoke(heavyMove, null), Is.True);

                Rigidbody lightBody = lightPlayer.GetComponent<Rigidbody>();
                Rigidbody heavyBody = heavyPlayer.GetComponent<Rigidbody>();
                Assert.That(lightBody.velocity.y, Is.EqualTo(8f).Within(0.01f));
                Assert.That(heavyBody.velocity.y, Is.EqualTo(8f).Within(0.01f));
                Assert.That((bool)tryJump.Invoke(lightMove, null), Is.False);
                Assert.That((bool)tryJump.Invoke(heavyMove, null), Is.False);
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
        }

        private static GameObject CreatePlayer(string name, float x, float mass)
        {
            GameObject player = new GameObject(name);
            player.transform.position = new Vector3(x, 2f, 0f);
            player.AddComponent<BoxCollider>().size = Vector3.one;
            player.AddComponent<Rigidbody>().mass = mass;
            Type playerMoveType = Type.GetType("PlayerMove, Assembly-CSharp", true);
            Component playerMove = player.AddComponent(playerMoveType);
            FieldInfo jumpSpeed = playerMoveType.GetField("JumpSpeed", BindingFlags.Instance | BindingFlags.Public);
            jumpSpeed.SetValue(playerMove, 8f);
            return player;
        }
    }
}
