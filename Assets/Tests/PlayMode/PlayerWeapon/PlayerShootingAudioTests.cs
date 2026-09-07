using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RobotArena.PlayerWeapon;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotArena.PlayerWeapon.Tests
{
    public sealed class PlayerShootingAudioTests
    {
        [UnityTest]
        public IEnumerator Successful_volley_and_ultimate_each_request_one_distinct_sfx()
        {
            var createdObjects = new List<GameObject>();
            try
            {
                Type playerShootingType = PlayerWeaponTestReflection.FindRuntimeType("PlayerShooting");
                Type laserPointerType = PlayerWeaponTestReflection.FindRuntimeType("LaserPointer");
                Assert.That(playerShootingType, Is.Not.Null);
                Assert.That(laserPointerType, Is.Not.Null);

                GameObject player = new GameObject("AudioPlayer");
                createdObjects.Add(player);

                GameObject leftBarrel = CreateChild(player, "AudioLeftBarrel", new Vector3(-0.75f, 0f, 0f));
                GameObject rightBarrel = CreateChild(player, "AudioRightBarrel", new Vector3(0.75f, 0f, 0f));
                GameObject aimBarrel = CreateChild(player, "AudioAimBarrel", Vector3.zero);
                Component laserPointer = aimBarrel.AddComponent(laserPointerType);
                PlayerWeaponTestReflection.SetField(laserPointer, "barrel", aimBarrel.transform);
                PlayerWeaponTestReflection.SetField(laserPointer, "localAimAxis", Vector3.forward);
                PlayerWeaponTestReflection.SetField(laserPointer, "maxDistance", 20f);

                GameObject bulletPrefab = new GameObject("AudioBullet");
                bulletPrefab.AddComponent<Rigidbody>();
                createdObjects.Add(bulletPrefab);

                GameObject physicsBody = new GameObject("AudioPhysicsBody");
                physicsBody.AddComponent<Rigidbody>();
                createdObjects.Add(physicsBody);

                GameObject ultimateEffectPrefab = new GameObject("AudioUltimateEffect");
                createdObjects.Add(ultimateEffectPrefab);

                Component shooting = player.AddComponent(playerShootingType);
                var recorder = new RecordingPlayerWeaponAudio();
                PlayerWeaponTestReflection.SetField(shooting, "bulletPrefab", bulletPrefab);
                PlayerWeaponTestReflection.SetField(shooting, "firePointLeft", leftBarrel.transform);
                PlayerWeaponTestReflection.SetField(shooting, "firePointRight", rightBarrel.transform);
                PlayerWeaponTestReflection.SetField(shooting, "laserPointer", laserPointer);
                PlayerWeaponTestReflection.SetField(shooting, "physicsBody", physicsBody);
                PlayerWeaponTestReflection.SetField(shooting, "cameraTransform", player.transform);
                PlayerWeaponTestReflection.SetField(shooting, "ultimateEffectPrefab", ultimateEffectPrefab);
                PlayerWeaponTestReflection.SetField(shooting, "ultiEffectLifetime", 0f);
                PlayerWeaponTestReflection.SetField(shooting, "audioPlayback", recorder);

                yield return null;

                MethodInfo tryStartUltimate = playerShootingType.GetMethod(
                    "TryStartUltimate",
                    BindingFlags.Instance | BindingFlags.Public);
                MethodInfo tryFireVolley = playerShootingType.GetMethod(
                    "TryFireVolley",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(tryStartUltimate, Is.Not.Null);
                Assert.That(tryFireVolley, Is.Not.Null);

                Assert.That((bool)tryStartUltimate.Invoke(shooting, null), Is.True);
                Assert.That((bool)tryStartUltimate.Invoke(shooting, null), Is.False);
                Assert.That((bool)tryFireVolley.Invoke(shooting, null), Is.True);

                Assert.That(recorder.Cues, Is.EqualTo(new[]
                {
                    PlayerWeaponAudioCue.Ultimate,
                    PlayerWeaponAudioCue.Volley
                }));
                Assert.That(recorder.Count(PlayerWeaponAudioCue.Ultimate), Is.EqualTo(1));
                Assert.That(recorder.Count(PlayerWeaponAudioCue.Volley), Is.EqualTo(1));

                yield return null;
            }
            finally
            {
                DestroyObjectsByName("AudioBullet(Clone)");
                DestroyObjectsByName("AudioUltimateEffect(Clone)");
                foreach (GameObject createdObject in createdObjects)
                {
                    if (createdObject != null)
                    {
                        UnityEngine.Object.DestroyImmediate(createdObject);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator Rejected_volley_does_not_request_sfx()
        {
            var createdObjects = new List<GameObject>();
            try
            {
                Type playerShootingType = PlayerWeaponTestReflection.FindRuntimeType("PlayerShooting");
                Assert.That(playerShootingType, Is.Not.Null);

                GameObject player = new GameObject("RejectedAudioPlayer");
                createdObjects.Add(player);
                GameObject physicsBody = new GameObject("RejectedAudioPhysicsBody");
                physicsBody.AddComponent<Rigidbody>();
                createdObjects.Add(physicsBody);

                Component shooting = player.AddComponent(playerShootingType);
                var recorder = new RecordingPlayerWeaponAudio();
                PlayerWeaponTestReflection.SetField(shooting, "physicsBody", physicsBody);
                PlayerWeaponTestReflection.SetField(shooting, "audioPlayback", recorder);

                yield return null;

                MethodInfo tryFireVolley = playerShootingType.GetMethod(
                    "TryFireVolley",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(tryFireVolley, Is.Not.Null);
                LogAssert.Expect(
                    LogType.Error,
                    "PlayerShooting requires a bullet, left and right barrels, and a valid laser direction for a normal volley.");
                Assert.That((bool)tryFireVolley.Invoke(shooting, null), Is.False);
                Assert.That(recorder.Cues, Is.Empty);
            }
            finally
            {
                foreach (GameObject createdObject in createdObjects)
                {
                    if (createdObject != null)
                    {
                        UnityEngine.Object.DestroyImmediate(createdObject);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator Incomplete_ultimate_configuration_does_not_request_sfx()
        {
            var createdObjects = new List<GameObject>();
            try
            {
                Type playerShootingType = PlayerWeaponTestReflection.FindRuntimeType("PlayerShooting");
                Assert.That(playerShootingType, Is.Not.Null);

                GameObject player = new GameObject("IncompleteAudioPlayer");
                createdObjects.Add(player);
                GameObject physicsBody = new GameObject("IncompleteAudioPhysicsBody");
                physicsBody.AddComponent<Rigidbody>();
                createdObjects.Add(physicsBody);
                GameObject ultimateEffectPrefab = new GameObject("IncompleteAudioUltimateEffect");
                createdObjects.Add(ultimateEffectPrefab);

                Component shooting = player.AddComponent(playerShootingType);
                var recorder = new RecordingPlayerWeaponAudio();
                PlayerWeaponTestReflection.SetField(shooting, "physicsBody", physicsBody);
                PlayerWeaponTestReflection.SetField(shooting, "ultimateEffectPrefab", ultimateEffectPrefab);
                PlayerWeaponTestReflection.SetField(shooting, "audioPlayback", recorder);

                yield return null;

                MethodInfo tryStartUltimate = playerShootingType.GetMethod(
                    "TryStartUltimate",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(tryStartUltimate, Is.Not.Null);
                Assert.That((bool)tryStartUltimate.Invoke(shooting, null), Is.True);
                Assert.That(recorder.Cues, Is.Empty);
            }
            finally
            {
                DestroyObjectsByName("IncompleteAudioUltimateEffect(Clone)");
                foreach (GameObject createdObject in createdObjects)
                {
                    if (createdObject != null)
                    {
                        UnityEngine.Object.DestroyImmediate(createdObject);
                    }
                }
            }
        }

        private static GameObject CreateChild(GameObject parent, string name, Vector3 position)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            child.transform.position = position;
            return child;
        }

        private static void DestroyObjectsByName(string name)
        {
            GameObject[] objects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            foreach (GameObject current in objects)
            {
                if (current.name == name)
                {
                    UnityEngine.Object.DestroyImmediate(current);
                }
            }
        }

        private sealed class RecordingPlayerWeaponAudio : IPlayerWeaponAudio
        {
            public List<PlayerWeaponAudioCue> Cues { get; } = new List<PlayerWeaponAudioCue>();

            public void Play(PlayerWeaponAudioCue cue)
            {
                Cues.Add(cue);
            }

            public int Count(PlayerWeaponAudioCue cue)
            {
                int count = 0;
                foreach (PlayerWeaponAudioCue current in Cues)
                {
                    if (current == cue)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

    }
}
