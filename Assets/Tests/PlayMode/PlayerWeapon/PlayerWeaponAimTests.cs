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

                yield return WaitForRotation(
                    fixture.WeaponObject,
                    Quaternion.Euler(-110f, 35f, 0f));
            }
        }

        [UnityTest]
        public IEnumerator Weapon_clamps_camera_vertical_angle_and_recovers_from_the_limits()
        {
            using (AimFixture fixture = new AimFixture("LimitAimCamera", "LimitAimWeapon"))
            {
                fixture.CameraObject.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
                yield return WaitForRotation(fixture.WeaponObject, Quaternion.Euler(-135f, 0f, 0f));

                fixture.CameraObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
                yield return WaitForRotation(fixture.WeaponObject, Quaternion.Euler(-110f, 0f, 0f));

                fixture.CameraObject.transform.rotation = Quaternion.Euler(-70f, 0f, 0f);
                yield return WaitForRotation(fixture.WeaponObject, Quaternion.Euler(-45f, 0f, 0f));

                fixture.CameraObject.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
                yield return WaitForRotation(fixture.WeaponObject, Quaternion.Euler(-70f, 0f, 0f));
            }
        }

        [UnityTest]
        public IEnumerator Weapon_converges_smoothly_to_the_camera_angle()
        {
            using (AimFixture fixture = new AimFixture("SmoothAimCamera", "SmoothAimWeapon"))
            {
                Quaternion expectedRotation = Quaternion.Euler(-110f, 35f, 0f);
                fixture.CameraObject.transform.rotation = Quaternion.Euler(20f, 35f, 0f);

                float initialDistance = Quaternion.Angle(
                    fixture.WeaponObject.transform.rotation,
                    expectedRotation);

                yield return null;

                float firstFrameDistance = Quaternion.Angle(
                    fixture.WeaponObject.transform.rotation,
                    expectedRotation);
                Assert.That(firstFrameDistance, Is.GreaterThan(1f));
                Assert.That(firstFrameDistance, Is.LessThan(initialDistance));

                yield return WaitForRotation(fixture.WeaponObject, expectedRotation);
            }
        }

        [UnityTest]
        public IEnumerator Weapon_does_not_drift_when_the_camera_is_stationary()
        {
            using (AimFixture fixture = new AimFixture("StationaryAimCamera", "StationaryAimWeapon"))
            {
                fixture.CameraObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
                yield return WaitForRotation(
                    fixture.WeaponObject,
                    Quaternion.Euler(-110f, 0f, 0f));
                Quaternion settledRotation = fixture.WeaponObject.transform.rotation;

                yield return new WaitForSecondsRealtime(0.25f);

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

        private static void AssertWeaponRotation(
            GameObject weaponObject,
            Quaternion expectedRotation,
            float tolerance = 1f)
        {
            Assert.That(
                Quaternion.Angle(weaponObject.transform.rotation, expectedRotation),
                Is.LessThan(tolerance));
        }

        private static IEnumerator WaitForRotation(
            GameObject weaponObject,
            Quaternion expectedRotation,
            float tolerance = 1f,
            float timeoutSeconds = 1f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Quaternion.Angle(weaponObject.transform.rotation, expectedRotation) >= tolerance &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            AssertWeaponRotation(weaponObject, expectedRotation, tolerance);
        }

        private sealed class AimFixture : IDisposable
        {
            private readonly float previousTimeScale;
            private GameObject cameraObject;
            private GameObject weaponObject;

            public AimFixture(string cameraName, string weaponName)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;

                try
                {
                    cameraObject = new GameObject(cameraName);
                    weaponObject = new GameObject(weaponName);
                    AddGunRotation(weaponObject, cameraObject.transform);
                }
                catch
                {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    UnityEngine.Object.DestroyImmediate(weaponObject);
                    Time.timeScale = previousTimeScale;
                    throw;
                }
            }

            public GameObject CameraObject => cameraObject;
            public GameObject WeaponObject => weaponObject;

            public void Dispose()
            {
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.DestroyImmediate(CameraObject);
                UnityEngine.Object.DestroyImmediate(WeaponObject);
            }
        }

    }
}
