using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotArena.Session.Tests
{
    public sealed class GameMusicRuntimeTests
    {
        private Component createdRuntime;

        [UnityTearDown]
        public IEnumerator DestroyCreatedRuntime()
        {
            if (createdRuntime != null)
            {
                UnityEngine.Object.Destroy(createdRuntime.gameObject);
                createdRuntime = null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Runtime_creation_returns_one_persistent_owner()
        {
            Type runtimeType = Type.GetType("GameMusicRuntime, Assembly-CSharp", true);
            MethodInfo getOrCreate = runtimeType.GetMethod(
                "GetOrCreate",
                BindingFlags.Public | BindingFlags.Static);

            Component existing = (Component)UnityEngine.Object.FindObjectOfType(runtimeType);
            Component first = (Component)getOrCreate.Invoke(null, null);
            if (existing == null)
            {
                createdRuntime = first;
            }
            yield return null;
            Component second = (Component)getOrCreate.Invoke(null, null);

            Assert.That(second, Is.SameAs(first));
            Assert.That(UnityEngine.Object.FindObjectsOfType(runtimeType).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Scene_duplicate_is_destroyed_before_it_can_become_an_owner()
        {
            Type runtimeType = Type.GetType("GameMusicRuntime, Assembly-CSharp", true);
            Component existing = (Component)UnityEngine.Object.FindObjectOfType(runtimeType);
            Component owner = (Component)runtimeType
                .GetMethod("GetOrCreate", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            if (existing == null)
            {
                createdRuntime = owner;
            }
            GameObject duplicateObject = new GameObject("DuplicateGameMusicRuntime");
            duplicateObject.AddComponent(runtimeType);

            yield return null;

            Assert.That(UnityEngine.Object.FindObjectsOfType(runtimeType).Length, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectOfType(runtimeType), Is.SameAs(owner));
        }
    }
}
