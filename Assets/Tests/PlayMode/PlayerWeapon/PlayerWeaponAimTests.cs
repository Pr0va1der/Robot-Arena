using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotArena.PlayerWeapon.Tests
{
    public sealed class GunRotationTests
    {
        [UnityTest]
        public IEnumerator Weapon_follows_the_final_camera_vertical_angle()
        {
            using (AimFixture fixture = new AimFixture("AimCamera", "AimWeapon"))
            {
                fixture.CameraObject.transform.rotation = Quaternion.Euler(20f, 35f, 0f);

                yield return WaitForFrames(60);

                AssertWeaponRotation(fixture.WeaponObject, Quaternion.Euler(-110f, 35f, 0f));
            }
        }

        [UnityTest]
        public IEnumerator Weapon_clamps_camera_vertical_angle_and_recovers_from_the_limits()
        {
            using (AimFixture fixture = new AimFixture("LimitAimCamera", "LimitAimWeapon"))
            {
                fixture.CameraObject.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
                yield return WaitForFrames(60);
                AssertWeaponRotation(fixture.WeaponObject, Quaternion.Euler(-135f, 0f, 0f));

                fixture.CameraObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
                yield return WaitForFrames(60);
                AssertWeaponRotation(fixture.WeaponObject, Quaternion.Euler(-110f, 0f, 0f));

                fixture.CameraObject.transform.rotation = Quaternion.Euler(-70f, 0f, 0f);
                yield return WaitForFrames(60);
                AssertWeaponRotation(fixture.WeaponObject, Quaternion.Euler(-45f, 0f, 0f));

                fixture.CameraObject.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
                yield return WaitForFrames(60);
                AssertWeaponRotation(fixture.WeaponObject, Quaternion.Euler(-70f, 0f, 0f));
            }
        }

        [UnityTest]
        public IEnumerator Weapon_does_not_drift_when_the_camera_is_stationary()
        {
            using (AimFixture fixture = new AimFixture("StationaryAimCamera", "StationaryAimWeapon"))
            {
                fixture.CameraObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
                yield return WaitForFrames(60);
                Quaternion settledRotation = fixture.WeaponObject.transform.rotation;

                yield return WaitForFrames(60);

                Assert.That(
                    Quaternion.Angle(fixture.WeaponObject.transform.rotation, settledRotation),
                    Is.LessThan(0.1f));
            }
        }

        private static void AddGunRotation(GameObject weaponObject, Transform cameraTransform)
        {
            Type gunRotationType = PlayerWeaponTestReflection.FindRuntimeType("GunRotation");
            Assert.That(gunRotationType, Is.Not.Null);

            Component gunRotation = weaponObject.AddComponent(gunRotationType);
            PlayerWeaponTestReflection.SetField(gunRotation, "cameraTransform", cameraTransform);
            PlayerWeaponTestReflection.SetField(gunRotation, "rotationSpeed", 10f);
            PlayerWeaponTestReflection.SetField(gunRotation, "minElevation", -45f);
            PlayerWeaponTestReflection.SetField(gunRotation, "maxElevation", 45f);
        }

        private static void AssertWeaponRotation(GameObject weaponObject, Quaternion expectedRotation)
        {
            Assert.That(
                Quaternion.Angle(weaponObject.transform.rotation, expectedRotation),
                Is.LessThan(1f));
        }

        private static IEnumerator WaitForFrames(int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return null;
            }
        }

        private sealed class AimFixture : IDisposable
        {
            private readonly float previousTimeScale;

            public AimFixture(string cameraName, string weaponName)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;
                CameraObject = new GameObject(cameraName);
                WeaponObject = new GameObject(weaponName);
                AddGunRotation(WeaponObject, CameraObject.transform);
            }

            public GameObject CameraObject { get; }
            public GameObject WeaponObject { get; }

            public void Dispose()
            {
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.DestroyImmediate(CameraObject);
                UnityEngine.Object.DestroyImmediate(WeaponObject);
            }
        }

    }
}
