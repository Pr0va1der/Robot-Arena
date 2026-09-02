using System;
using System.Collections;
using System.Reflection;
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
            GameObject cameraObject = new GameObject("AimCamera");
            GameObject weaponObject = new GameObject("AimWeapon");
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                AddGunRotation(weaponObject, cameraObject.transform);

                cameraObject.transform.rotation = Quaternion.Euler(20f, 35f, 0f);

                yield return WaitForFrames(60);

                AssertWeaponRotation(weaponObject, Quaternion.Euler(-110f, 35f, 0f));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(weaponObject);
            }
        }

        [UnityTest]
        public IEnumerator Weapon_clamps_camera_vertical_angle_and_recovers_from_the_limits()
        {
            GameObject cameraObject = new GameObject("LimitAimCamera");
            GameObject weaponObject = new GameObject("LimitAimWeapon");
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                AddGunRotation(weaponObject, cameraObject.transform);

                cameraObject.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
                yield return WaitForFrames(60);
                AssertWeaponRotation(weaponObject, Quaternion.Euler(-135f, 0f, 0f));

                cameraObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
                yield return WaitForFrames(60);
                AssertWeaponRotation(weaponObject, Quaternion.Euler(-110f, 0f, 0f));

                cameraObject.transform.rotation = Quaternion.Euler(-70f, 0f, 0f);
                yield return WaitForFrames(60);
                AssertWeaponRotation(weaponObject, Quaternion.Euler(-45f, 0f, 0f));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(weaponObject);
            }
        }

        [UnityTest]
        public IEnumerator Weapon_does_not_drift_when_the_camera_is_stationary()
        {
            GameObject cameraObject = new GameObject("StationaryAimCamera");
            GameObject weaponObject = new GameObject("StationaryAimWeapon");
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                AddGunRotation(weaponObject, cameraObject.transform);

                cameraObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
                yield return WaitForFrames(60);
                Quaternion settledRotation = weaponObject.transform.rotation;

                yield return WaitForFrames(60);

                Assert.That(
                    Quaternion.Angle(weaponObject.transform.rotation, settledRotation),
                    Is.LessThan(0.1f));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(weaponObject);
            }
        }

        private static Component AddGunRotation(GameObject weaponObject, Transform cameraTransform)
        {
            Type gunRotationType = FindRuntimeType("GunRotation");
            Assert.That(gunRotationType, Is.Not.Null);

            Component gunRotation = weaponObject.AddComponent(gunRotationType);
            SetField(gunRotation, "cameraTransform", cameraTransform);
            SetField(gunRotation, "rotationSpeed", 10f);
            SetField(gunRotation, "minElevation", -45f);
            SetField(gunRotation, "maxElevation", 45f);
            return gunRotation;
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

        private static Type FindRuntimeType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void SetField(Component component, string fieldName, object value)
        {
            FieldInfo field = component.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(component, value);
        }
    }
}
