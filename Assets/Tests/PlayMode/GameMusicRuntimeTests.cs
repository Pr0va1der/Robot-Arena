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
        [UnityTest]
        public IEnumerator Runtime_creation_returns_one_persistent_owner()
        {
            Type runtimeType = Type.GetType("GameMusicRuntime, Assembly-CSharp", true);
            MethodInfo getOrCreate = runtimeType.GetMethod(
                "GetOrCreate",
                BindingFlags.Public | BindingFlags.Static);

            Component first = (Component)getOrCreate.Invoke(null, null);
            yield return null;
            Component second = (Component)getOrCreate.Invoke(null, null);

            Assert.That(second, Is.SameAs(first));
            Assert.That(UnityEngine.Object.FindObjectsOfType(runtimeType).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Scene_duplicate_is_destroyed_before_it_can_become_an_owner()
        {
            Type runtimeType = Type.GetType("GameMusicRuntime, Assembly-CSharp", true);
            Component owner = (Component)runtimeType
                .GetMethod("GetOrCreate", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            GameObject duplicateObject = new GameObject("DuplicateGameMusicRuntime");
            duplicateObject.AddComponent(runtimeType);

            yield return null;

            Assert.That(UnityEngine.Object.FindObjectsOfType(runtimeType).Length, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectOfType(runtimeType), Is.SameAs(owner));
        }
    }
}
