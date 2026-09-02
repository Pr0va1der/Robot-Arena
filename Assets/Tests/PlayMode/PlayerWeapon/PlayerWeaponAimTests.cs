using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RobotArena.PlayerWeapon.Tests
{
    public sealed class GunRotationTests
    {
        private Scene integrationScene;
        private Scene previousActiveScene;
        private bool integrationSceneLoadedByTest;

        [UnityTest]
        public IEnumerator Weapon_and_laser_follow_camera_orbit_upward()
        {
            using (AimFixture fixture = new AimFixture("AimCamera", "AimWeapon"))
            {
                fixture.CameraObject.transform.rotation = Quaternion.Euler(20f, 35f, 0f);

                yield return WaitForRotation(
                    fixture.WeaponObject,
                    Quaternion.Euler(-70f, 35f, 0f));
                yield return WaitForAimDirection(
                    fixture.LaserPointer,
                    Quaternion.Euler(-70f, 35f, 0f) * Vector3.up);

                Assert.That(GetAimDirection(fixture.LaserPointer).y, Is.GreaterThan(0.25f));
            }
        }

        [UnityTest]
        public IEnumerator Weapon_and_laser_follow_camera_orbit_downward()
        {
            using (AimFixture fixture = new AimFixture("DownAimCamera", "DownAimWeapon"))
            {
                fixture.CameraObject.transform.rotation = Quaternion.Euler(-20f, 35f, 0f);

                yield return WaitForRotation(
                    fixture.WeaponObject,
                    Quaternion.Euler(-110f, 35f, 0f));
                yield return WaitForAimDirection(
                    fixture.LaserPointer,
                    Quaternion.Euler(-110f, 35f, 0f) * Vector3.up);

                Assert.That(GetAimDirection(fixture.LaserPointer).y, Is.LessThan(-0.25f));
            }
        }

        [UnityTest]
        public IEnumerator Weapon_clamps_camera_vertical_angle_and_recovers_from_the_limits()
        {
            using (AimFixture fixture = new AimFixture("LimitAimCamera", "LimitAimWeapon"))
            {
                fixture.CameraObject.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
                yield return WaitForRotation(fixture.WeaponObject, Quaternion.Euler(-45f, 0f, 0f));
                yield return null;
                Assert.That(GetAimDirection(fixture.LaserPointer).y, Is.GreaterThan(0.65f));

                fixture.CameraObject.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
                yield return WaitForRotation(fixture.WeaponObject, Quaternion.Euler(-70f, 0f, 0f));
                yield return null;
                Assert.That(GetAimDirection(fixture.LaserPointer).y, Is.GreaterThan(0.25f));

                fixture.CameraObject.transform.rotation = Quaternion.Euler(-70f, 0f, 0f);
                yield return WaitForRotation(fixture.WeaponObject, Quaternion.Euler(-135f, 0f, 0f));
                yield return null;
                Assert.That(GetAimDirection(fixture.LaserPointer).y, Is.LessThan(-0.65f));

                fixture.CameraObject.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
                yield return WaitForRotation(fixture.WeaponObject, Quaternion.Euler(-110f, 0f, 0f));
                yield return null;
                Assert.That(GetAimDirection(fixture.LaserPointer).y, Is.LessThan(-0.25f));
            }
        }

        [UnityTest]
        public IEnumerator Weapon_converges_smoothly_to_the_camera_angle()
        {
            using (AimFixture fixture = new AimFixture("SmoothAimCamera", "SmoothAimWeapon"))
            {
                Quaternion expectedRotation = Quaternion.Euler(-70f, 35f, 0f);
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
                    Quaternion.Euler(-70f, 0f, 0f));
                yield return null;
                Vector3 settledAimDirection = GetAimDirection(fixture.LaserPointer);

                yield return new WaitForSecondsRealtime(0.25f);

                Assert.That(
                    Vector3.Angle(GetAimDirection(fixture.LaserPointer), settledAimDirection),
                    Is.LessThan(0.1f));
            }
        }

        [UnityTest]
        public IEnumerator Neutral_camera_keeps_the_line_of_fire_level()
        {
            using (AimFixture fixture = new AimFixture("NeutralAimCamera", "NeutralAimWeapon"))
            {
                fixture.CameraObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

                yield return WaitForRotation(
                    fixture.WeaponObject,
                    Quaternion.Euler(-90f, 0f, 0f));
                yield return WaitForAimDirection(
                    fixture.LaserPointer,
                    Vector3.back);

                Assert.That(GetAimDirection(fixture.LaserPointer).y, Is.EqualTo(0f).Within(0.01f));
            }
        }

        [UnityTest]
        public IEnumerator SampleScene_Cinemachine_final_camera_drives_the_weapon_and_laser()
        {
            previousActiveScene = SceneManager.GetActiveScene();
            integrationScene = SceneManager.GetSceneByName("SampleScene");

            if (!integrationScene.IsValid() || !integrationScene.isLoaded)
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
                    "SampleScene",
                    LoadSceneMode.Additive);
                Assert.That(loadOperation, Is.Not.Null);

                while (!loadOperation.isDone)
                {
                    yield return null;
                }

                integrationScene = SceneManager.GetSceneByName("SampleScene");
                integrationSceneLoadedByTest = true;
            }

            Assert.That(integrationScene.IsValid() && integrationScene.isLoaded, Is.True);
            SceneManager.SetActiveScene(integrationScene);
            yield return null;

            Type gunRotationType = PlayerWeaponTestReflection.FindRuntimeType("GunRotation");
            Type laserPointerType = PlayerWeaponTestReflection.FindRuntimeType("LaserPointer");
            Type freeLookType = PlayerWeaponTestReflection.FindRuntimeType("Cinemachine.CinemachineFreeLook");
            Assert.That(gunRotationType, Is.Not.Null);
            Assert.That(laserPointerType, Is.Not.Null);
            Assert.That(freeLookType, Is.Not.Null);

            Component gunRotation = FindSceneComponent(integrationScene, gunRotationType);
            Component laserPointer = FindSceneComponent(integrationScene, laserPointerType);
            Component freeLook = FindSceneComponent(integrationScene, freeLookType);
            Camera sceneCamera = (Camera)FindSceneComponent(integrationScene, typeof(Camera));
            Assert.That(gunRotation, Is.Not.Null);
            Assert.That(laserPointer, Is.Not.Null);
            Assert.That(freeLook, Is.Not.Null);
            Assert.That(sceneCamera, Is.Not.Null);

            FieldInfo cameraTransformField = gunRotationType.GetField(
                "cameraTransform",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(cameraTransformField, Is.Not.Null);
            Assert.That(
                cameraTransformField.GetValue(gunRotation),
                Is.EqualTo(sceneCamera.transform));

            SetCinemachineYAxisValue(freeLook, 0.9f);
            yield return WaitForAimToFollowCamera(sceneCamera, laserPointer);

            Vector3 upperCameraDirection = sceneCamera.transform.forward;
            Vector3 upperAimDirection = GetAimDirection(laserPointer);
            Assert.That(upperCameraDirection.y, Is.LessThan(0f));
            Assert.That(upperAimDirection.y, Is.GreaterThan(0.05f));

            SetCinemachineYAxisValue(freeLook, 0.1f);
            yield return WaitForAimToFollowCamera(sceneCamera, laserPointer);

            Vector3 lowerCameraDirection = sceneCamera.transform.forward;
            Vector3 lowerAimDirection = GetAimDirection(laserPointer);
            Assert.That(lowerCameraDirection.y, Is.GreaterThan(upperCameraDirection.y));
            Assert.That(lowerAimDirection.y, Is.LessThan(upperAimDirection.y));
        }

        [UnityTearDown]
        public IEnumerator RestoreIntegrationScene()
        {
            if (!integrationScene.IsValid())
            {
                yield break;
            }

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }

            if (!integrationSceneLoadedByTest || !integrationScene.isLoaded)
            {
                yield break;
            }

            AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(integrationScene);
            if (unloadOperation != null)
            {
                while (!unloadOperation.isDone)
                {
                    yield return null;
                }
            }

            integrationScene = default;
            integrationSceneLoadedByTest = false;
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

        private static Component AddLaserPointer(GameObject weaponObject)
        {
            Type laserPointerType = PlayerWeaponTestReflection.FindRuntimeType("LaserPointer");
            Assert.That(laserPointerType, Is.Not.Null);

            GameObject laserObject = new GameObject("AimLaser");
            laserObject.transform.SetParent(weaponObject.transform);
            laserObject.AddComponent<LineRenderer>();

            Component laserPointer = laserObject.AddComponent(laserPointerType);
            PlayerWeaponTestReflection.SetField(laserPointer, "barrel", weaponObject.transform);
            PlayerWeaponTestReflection.SetField(laserPointer, "maxDistance", 100f);
            PlayerWeaponTestReflection.SetField(laserPointer, "localAimAxis", Vector3.up);
            return laserPointer;
        }

        private static Vector3 GetAimDirection(Component laserPointer)
        {
            PropertyInfo aimDirection = laserPointer.GetType().GetProperty(
                "AimDirection",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(aimDirection, Is.Not.Null);
            return (Vector3)aimDirection.GetValue(laserPointer, null);
        }

        private static IEnumerator WaitForAimDirection(
            Component laserPointer,
            Vector3 expectedDirection,
            float tolerance = 1f,
            float timeoutSeconds = 1f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Vector3.Angle(GetAimDirection(laserPointer), expectedDirection) >= tolerance &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Vector3.Angle(GetAimDirection(laserPointer), expectedDirection), Is.LessThan(tolerance));
        }

        private static IEnumerator WaitForAimToFollowCamera(
            Camera sceneCamera,
            Component laserPointer,
            float tolerance = 1f,
            float timeoutSeconds = 2f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                Vector3 aimDirection = GetAimDirection(laserPointer);
                if (aimDirection.sqrMagnitude > 0.0001f &&
                    Vector3.Angle(aimDirection, -sceneCamera.transform.forward) < tolerance)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(
                Vector3.Angle(GetAimDirection(laserPointer), -sceneCamera.transform.forward),
                Is.LessThan(tolerance));
        }

        private static Component FindSceneComponent(Scene scene, Type componentType)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Component[] components = root.GetComponentsInChildren(componentType, true);
                if (components.Length > 0)
                {
                    return components[0];
                }
            }

            return null;
        }

        private static void SetCinemachineYAxisValue(Component freeLook, float value)
        {
            FieldInfo yAxisField = freeLook.GetType().GetField(
                "m_YAxis",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(yAxisField, Is.Not.Null);

            object yAxis = yAxisField.GetValue(freeLook);
            FieldInfo valueField = yAxis.GetType().GetField(
                "Value",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(valueField, Is.Not.Null);
            valueField.SetValue(yAxis, value);
            yAxisField.SetValue(freeLook, yAxis);
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
            private Component laserPointer;

            public AimFixture(string cameraName, string weaponName)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 1f;

                try
                {
                    cameraObject = new GameObject(cameraName);
                    weaponObject = new GameObject(weaponName);
                    AddGunRotation(weaponObject, cameraObject.transform);
                    laserPointer = AddLaserPointer(weaponObject);
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
            public Component LaserPointer => laserPointer;

            public void Dispose()
            {
                Time.timeScale = previousTimeScale;
                UnityEngine.Object.DestroyImmediate(CameraObject);
                UnityEngine.Object.DestroyImmediate(WeaponObject);
            }
        }

    }
}
