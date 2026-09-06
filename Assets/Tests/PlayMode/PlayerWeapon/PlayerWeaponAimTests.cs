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

        [Test]
        public void Shooting_runs_after_camera_and_laser_pose_updates()
        {
            Assert.That(GetExecutionOrder("GunRotation"), Is.LessThan(GetExecutionOrder("LaserPointer")));
            Assert.That(GetExecutionOrder("LaserPointer"), Is.LessThan(GetExecutionOrder("PlayerShooting")));
        }

        [Test]
        public void Turret_rotation_uses_the_camera_forward_heading()
        {
            Vector3 cameraForward = (Quaternion.Euler(18f, 127f, 23f) * Vector3.forward).normalized;
            Quaternion turretRotation = PlayerWeaponAim.TurretRotation(cameraForward, -45f, 45f);

            Assert.That(
                Vector3.Angle(turretRotation * Vector3.up, -cameraForward),
                Is.LessThan(0.01f));
        }

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
        public IEnumerator Weapon_follows_camera_without_gameplay_lag()
        {
            using (AimFixture fixture = new AimFixture("SmoothAimCamera", "SmoothAimWeapon"))
            {
                Quaternion expectedRotation = Quaternion.Euler(-70f, 35f, 0f);
                fixture.CameraObject.transform.rotation = Quaternion.Euler(20f, 35f, 0f);

                yield return null;

                AssertWeaponRotation(fixture.WeaponObject, expectedRotation);
                yield return WaitForAimDirection(
                    fixture.LaserPointer,
                    expectedRotation * Vector3.up);
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
            yield return LoadIntegrationScene();

            Type gunRotationType = PlayerWeaponTestReflection.FindRuntimeType("GunRotation");
            Type laserPointerType = PlayerWeaponTestReflection.FindRuntimeType("LaserPointer");
            Type freeLookType = PlayerWeaponTestReflection.FindRuntimeType("Cinemachine.CinemachineFreeLook");
            Type shoulderCameraRigType = PlayerWeaponTestReflection.FindRuntimeType("ShoulderCameraRig");
            Type cameraColliderType = PlayerWeaponTestReflection.FindRuntimeType("Cinemachine.CinemachineCollider");
            Type crosshairType = PlayerWeaponTestReflection.FindRuntimeType("DesktopCrosshair");
            Assert.That(gunRotationType, Is.Not.Null);
            Assert.That(laserPointerType, Is.Not.Null);
            Assert.That(freeLookType, Is.Not.Null);
            Assert.That(shoulderCameraRigType, Is.Not.Null);
            Assert.That(cameraColliderType, Is.Not.Null);
            Assert.That(crosshairType, Is.Not.Null);

            Component gunRotation = FindSceneComponent(integrationScene, gunRotationType);
            Component laserPointer = FindSceneComponent(integrationScene, laserPointerType);
            Component freeLook = FindSceneComponent(integrationScene, freeLookType);
            Component shoulderCameraRig = FindSceneComponent(integrationScene, shoulderCameraRigType);
            Component cameraCollider = FindSceneComponent(integrationScene, cameraColliderType);
            Component crosshair = FindSceneComponent(integrationScene, crosshairType);
            Camera sceneCamera = (Camera)FindSceneComponent(integrationScene, typeof(Camera));
            Assert.That(gunRotation, Is.Not.Null);
            Assert.That(laserPointer, Is.Not.Null);
            Assert.That(freeLook, Is.Not.Null);
            Assert.That(shoulderCameraRig, Is.Not.Null);
            Assert.That(cameraCollider, Is.Not.Null);
            Assert.That(crosshair, Is.Not.Null);
            Assert.That(sceneCamera, Is.Not.Null);

            yield return null;
            PropertyInfo lookAtProperty = freeLookType.GetProperty(
                "LookAt",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(lookAtProperty, Is.Not.Null);
            Transform lookAt = lookAtProperty.GetValue(freeLook, null) as Transform;
            Assert.That(lookAt, Is.Not.Null);

            RectTransform crosshairRect = crosshair.GetComponent<RectTransform>();
            Assert.That(crosshairRect, Is.Not.Null);
            Assert.That(crosshairRect.anchorMin.x, Is.EqualTo(sceneCamera.rect.center.x).Within(0.01f));
            Assert.That(crosshairRect.anchorMin.y, Is.EqualTo(sceneCamera.rect.center.y).Within(0.01f));
            AssertLookAtScreenX(sceneCamera, lookAt, 0.375f);

            Vector3 cameraToLookAt = (lookAt.position - sceneCamera.transform.position).normalized;
            Assert.That(
                Vector3.Dot(cameraToLookAt, sceneCamera.transform.right),
                Is.LessThan(-0.02f),
                "The look-at target should remain left of the camera in the right-shoulder composition.");

            Assert.That(GetFloatField(shoulderCameraRig, "shoulderOffset"), Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(GetFloatField(shoulderCameraRig, "targetScreenX"), Is.EqualTo(0.375f).Within(0.001f));

            FieldInfo cameraTransformField = gunRotationType.GetField(
                "cameraTransform",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(cameraTransformField, Is.Not.Null);
            Assert.That(
                cameraTransformField.GetValue(gunRotation),
                Is.EqualTo(sceneCamera.transform));

            SetCinemachineYAxisValue(freeLook, 0.9f);
            yield return WaitForAimToFollowCamera(sceneCamera, laserPointer);
            AssertLookAtScreenX(sceneCamera, lookAt, 0.375f);

            Vector3 upperCameraDirection = sceneCamera.transform.forward;
            Vector3 upperAimDirection = GetAimDirection(laserPointer);
            Assert.That(upperCameraDirection.y, Is.LessThan(0f));
            Assert.That(upperAimDirection.y, Is.GreaterThan(0.05f));

            SetCinemachineYAxisValue(freeLook, 0.1f);
            yield return WaitForAimToFollowCamera(sceneCamera, laserPointer);
            AssertLookAtScreenX(sceneCamera, lookAt, 0.375f);

            Vector3 lowerCameraDirection = sceneCamera.transform.forward;
            Vector3 lowerAimDirection = GetAimDirection(laserPointer);
            Assert.That(lowerCameraDirection.y, Is.GreaterThan(upperCameraDirection.y));
            Assert.That(lowerAimDirection.y, Is.LessThan(upperAimDirection.y));
        }

        [UnityTest]
        public IEnumerator SampleScene_camera_support_point_does_not_follow_turret_rotation()
        {
            yield return LoadIntegrationScene();

            Type freeLookType = PlayerWeaponTestReflection.FindRuntimeType("Cinemachine.CinemachineFreeLook");
            Type gunRotationType = PlayerWeaponTestReflection.FindRuntimeType("GunRotation");
            Type laserPointerType = PlayerWeaponTestReflection.FindRuntimeType("LaserPointer");
            Assert.That(freeLookType, Is.Not.Null);
            Assert.That(gunRotationType, Is.Not.Null);
            Assert.That(laserPointerType, Is.Not.Null);

            Component freeLook = FindSceneComponent(integrationScene, freeLookType);
            Component gunRotation = FindSceneComponent(integrationScene, gunRotationType);
            Component laserPointer = FindSceneComponent(integrationScene, laserPointerType);
            Camera sceneCamera = (Camera)FindSceneComponent(integrationScene, typeof(Camera));
            Assert.That(freeLook, Is.Not.Null);
            Assert.That(gunRotation, Is.Not.Null);
            Assert.That(laserPointer, Is.Not.Null);
            Assert.That(sceneCamera, Is.Not.Null);

            PropertyInfo lookAtProperty = freeLookType.GetProperty(
                "LookAt",
                BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo followProperty = freeLookType.GetProperty(
                "Follow",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(lookAtProperty, Is.Not.Null);
            Assert.That(followProperty, Is.Not.Null);
            Transform lookAt = lookAtProperty.GetValue(freeLook, null) as Transform;
            Assert.That(lookAt, Is.Not.Null);
            Assert.That(
                lookAt.parent,
                Is.Null,
                "The camera support point must not inherit the turret hierarchy.");
            Assert.That(
                Quaternion.Angle(lookAt.rotation, Quaternion.identity),
                Is.LessThan(0.001f),
                "The camera support point must keep a neutral world rotation.");
            Assert.That(
                followProperty.GetValue(freeLook, null),
                Is.SameAs(lookAt),
                "The FreeLook body and aim must both use the camera support point.");

            FieldInfo targetField = gunRotationType.GetField(
                "target",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(targetField, Is.Not.Null);
            Transform chassis = targetField.GetValue(gunRotation) as Transform;
            Assert.That(chassis, Is.Not.Null);

            Rigidbody chassisBody = chassis.GetComponent<Rigidbody>();
            Assert.That(chassisBody, Is.Not.Null);

            Vector3 initialOffset = lookAt.position - chassis.position;
            Vector3 originalChassisPosition = chassis.position;
            Quaternion originalChassisRotation = chassis.rotation;
            Quaternion originalTurretRotation = gunRotation.transform.rotation;
            bool originalIsKinematic = chassisBody.isKinematic;
            RigidbodyInterpolation originalInterpolation = chassisBody.interpolation;
            Vector3 originalVelocity = chassisBody.velocity;
            Vector3 originalAngularVelocity = chassisBody.angularVelocity;
            string originalXAxisInput = GetCinemachineAxisInput(freeLook, "m_XAxis");
            string originalYAxisInput = GetCinemachineAxisInput(freeLook, "m_YAxis");

            try
            {
                SetCinemachineAxisInput(freeLook, "m_XAxis", string.Empty);
                SetCinemachineAxisInput(freeLook, "m_YAxis", string.Empty);

                chassisBody.rotation = originalChassisRotation * Quaternion.Euler(0f, 45f, 0f);
                gunRotation.transform.rotation = originalTurretRotation * Quaternion.Euler(0f, 90f, 0f);
                Assert.That(
                    Vector3.Distance(lookAt.position - chassis.position, initialOffset),
                    Is.LessThan(0.001f),
                    "Rotating the turret must not move the camera's support point.");
                Assert.That(
                    Quaternion.Angle(lookAt.rotation, Quaternion.identity),
                    Is.LessThan(0.001f),
                    "Rotating the chassis or turret must not rotate the camera support point.");

                chassisBody.rotation = originalChassisRotation;
                gunRotation.transform.rotation = originalTurretRotation;
                chassisBody.isKinematic = true;
                chassisBody.interpolation = RigidbodyInterpolation.Interpolate;
                yield return WaitForAimToFollowCamera(sceneCamera, laserPointer);

                Vector3 previousCameraForward = sceneCamera.transform.forward;
                bool chassisMoved = false;

                for (int frame = 0; frame < 8; frame++)
                {
                    Vector3 nextPosition = chassisBody.position + new Vector3(0.08f, 0f, -0.04f);
                    chassisBody.MovePosition(nextPosition);
                    yield return new WaitForFixedUpdate();
                    yield return null;

                    chassisMoved |= Vector3.Distance(chassis.position, originalChassisPosition) > 0.01f;

                    Assert.That(
                        Vector3.Distance(lookAt.position - chassis.position, initialOffset),
                        Is.LessThan(0.001f),
                        "The camera support point must keep a stable chassis-relative offset over consecutive frames.");
                    Assert.That(
                        Quaternion.Angle(lookAt.rotation, Quaternion.identity),
                        Is.LessThan(0.001f),
                        "The camera support point must not inherit chassis or weapon rotation during movement.");
                    Assert.That(
                        Vector3.Distance(gunRotation.transform.position, chassis.position),
                        Is.LessThan(0.001f),
                        "The turret must remain attached to the moving chassis while following the camera.");
                    Assert.That(
                        Vector3.Angle(GetAimDirection(laserPointer), -sceneCamera.transform.forward),
                        Is.LessThan(1f),
                        "The turret and fire line must follow the final camera direction on every rendered frame.");
                    Assert.That(
                        Vector3.Angle(previousCameraForward, sceneCamera.transform.forward),
                        Is.LessThan(5f),
                        "A fixed camera input must not produce a frame-sized direction jump during chassis motion.");

                    previousCameraForward = sceneCamera.transform.forward;
                }

                Assert.That(
                    chassisMoved,
                    Is.True,
                    "The regression must observe actual Rigidbody movement across rendered frames.");
            }
            finally
            {
                SetCinemachineAxisInput(freeLook, "m_XAxis", originalXAxisInput);
                SetCinemachineAxisInput(freeLook, "m_YAxis", originalYAxisInput);
                chassisBody.isKinematic = originalIsKinematic;
                chassisBody.interpolation = originalInterpolation;
                chassisBody.position = originalChassisPosition;
                chassisBody.rotation = originalChassisRotation;
                chassisBody.velocity = originalVelocity;
                chassisBody.angularVelocity = originalAngularVelocity;
                gunRotation.transform.rotation = originalTurretRotation;
            }
        }

        [UnityTest]
        public IEnumerator SampleScene_final_camera_keeps_weapon_aligned_during_motion_and_collision()
        {
            yield return LoadIntegrationScene();

            Type freeLookType = PlayerWeaponTestReflection.FindRuntimeType("Cinemachine.CinemachineFreeLook");
            Type gunRotationType = PlayerWeaponTestReflection.FindRuntimeType("GunRotation");
            Type laserPointerType = PlayerWeaponTestReflection.FindRuntimeType("LaserPointer");
            Type crosshairType = PlayerWeaponTestReflection.FindRuntimeType("DesktopCrosshair");
            Assert.That(freeLookType, Is.Not.Null);
            Assert.That(gunRotationType, Is.Not.Null);
            Assert.That(laserPointerType, Is.Not.Null);
            Assert.That(crosshairType, Is.Not.Null);

            Component freeLook = FindSceneComponent(integrationScene, freeLookType);
            Component gunRotation = FindSceneComponent(integrationScene, gunRotationType);
            Component laserPointer = FindSceneComponent(integrationScene, laserPointerType);
            Component crosshair = FindSceneComponent(integrationScene, crosshairType);
            Camera sceneCamera = (Camera)FindSceneComponent(integrationScene, typeof(Camera));
            Assert.That(freeLook, Is.Not.Null);
            Assert.That(gunRotation, Is.Not.Null);
            Assert.That(laserPointer, Is.Not.Null);
            Assert.That(crosshair, Is.Not.Null);
            Assert.That(sceneCamera, Is.Not.Null);

            RectTransform crosshairRect = crosshair.GetComponent<RectTransform>();
            Assert.That(crosshairRect, Is.Not.Null);

            PropertyInfo lookAtProperty = freeLookType.GetProperty(
                "LookAt",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(lookAtProperty, Is.Not.Null);
            Transform lookAt = lookAtProperty.GetValue(freeLook, null) as Transform;
            Assert.That(lookAt, Is.Not.Null);

            FieldInfo targetField = gunRotationType.GetField(
                "target",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(targetField, Is.Not.Null);
            Transform chassis = targetField.GetValue(gunRotation) as Transform;
            Assert.That(chassis, Is.Not.Null);

            Rigidbody chassisBody = chassis.GetComponent<Rigidbody>();
            Assert.That(chassisBody, Is.Not.Null);

            float originalXAxis = GetCinemachineAxisValue(freeLook, "m_XAxis");
            float originalYAxis = GetCinemachineAxisValue(freeLook, "m_YAxis");
            string originalXAxisInput = GetCinemachineAxisInput(freeLook, "m_XAxis");
            string originalYAxisInput = GetCinemachineAxisInput(freeLook, "m_YAxis");
            bool originalIsKinematic = chassisBody.isKinematic;
            RigidbodyInterpolation originalInterpolation = chassisBody.interpolation;
            Vector3 originalPosition = chassisBody.position;
            Quaternion originalRotation = chassisBody.rotation;
            Vector3 originalVelocity = chassisBody.velocity;
            Vector3 originalAngularVelocity = chassisBody.angularVelocity;
            GameObject obstacle = null;

            try
            {
                SetCinemachineAxisInput(freeLook, "m_XAxis", string.Empty);
                SetCinemachineAxisInput(freeLook, "m_YAxis", string.Empty);
                chassisBody.isKinematic = true;
                chassisBody.interpolation = RigidbodyInterpolation.Interpolate;
                chassisBody.velocity = Vector3.zero;
                chassisBody.angularVelocity = Vector3.zero;

                yield return WaitForAimToFollowCamera(sceneCamera, laserPointer);

                for (int frame = 0; frame < 8; frame++)
                {
                    SetCinemachineAxisValue(freeLook, "m_XAxis", originalXAxis + frame * 12f);
                    SetCinemachineAxisValue(freeLook, "m_YAxis", Mathf.Lerp(0.25f, 0.75f, frame / 7f));
                    chassisBody.MovePosition(chassisBody.position + new Vector3(0.08f, 0f, -0.04f));

                    yield return new WaitForFixedUpdate();
                    yield return null;

                    Assert.That(
                        Vector3.Angle(GetAimDirection(laserPointer), -sceneCamera.transform.forward),
                        Is.LessThan(1f),
                        "The line of fire must follow the final camera orientation while movement and orbit input occur together.");
                    Assert.That(
                        Vector3.Angle(gunRotation.transform.up, -sceneCamera.transform.forward),
                        Is.LessThan(1f),
                        "The player turret must consume the current frame's final camera orientation.");
                    Assert.That(
                        crosshairRect.anchorMin.x,
                        Is.EqualTo(sceneCamera.rect.center.x).Within(0.01f));
                    Assert.That(
                        crosshairRect.anchorMin.y,
                        Is.EqualTo(sceneCamera.rect.center.y).Within(0.01f));
                }

                Vector3 unobstructedCameraPosition = sceneCamera.transform.position;
                Vector3 cameraOrbitOffset = unobstructedCameraPosition - lookAt.position;
                Assert.That(cameraOrbitOffset.sqrMagnitude, Is.GreaterThan(1f));

                obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = "CameraAimCollisionProbe";
                obstacle.transform.position = unobstructedCameraPosition - cameraOrbitOffset.normalized * 0.3f;
                obstacle.transform.rotation = Quaternion.LookRotation(cameraOrbitOffset.normalized);
                obstacle.transform.localScale = new Vector3(3f, 3f, 0.25f);

                Vector3 previousCollisionForward = sceneCamera.transform.forward;
                for (int frame = 0; frame < 4; frame++)
                {
                    yield return null;

                    Assert.That(
                        Vector3.Angle(previousCollisionForward, sceneCamera.transform.forward),
                        Is.LessThan(5f),
                        "A stationary collision correction must not introduce a frame-sized camera oscillation.");
                    Assert.That(
                        Vector3.Angle(GetAimDirection(laserPointer), -sceneCamera.transform.forward),
                        Is.LessThan(1f),
                        "The line of fire must follow the collision-corrected final camera orientation.");
                    Assert.That(
                        Vector3.Angle(gunRotation.transform.up, -sceneCamera.transform.forward),
                        Is.LessThan(1f),
                        "The player turret must follow the collision-corrected final camera orientation without a frame of lag.");

                    previousCollisionForward = sceneCamera.transform.forward;
                }

                Assert.That(
                    Vector3.Distance(sceneCamera.transform.position, lookAt.position),
                    Is.LessThan(Vector3.Distance(unobstructedCameraPosition, lookAt.position) - 0.05f),
                    "Camera collision handling must move the final camera toward the target when an obstacle blocks the orbit.");
            }
            finally
            {
                if (obstacle != null)
                {
                    UnityEngine.Object.DestroyImmediate(obstacle);
                }

                SetCinemachineAxisInput(freeLook, "m_XAxis", originalXAxisInput);
                SetCinemachineAxisInput(freeLook, "m_YAxis", originalYAxisInput);
                SetCinemachineAxisValue(freeLook, "m_XAxis", originalXAxis);
                SetCinemachineAxisValue(freeLook, "m_YAxis", originalYAxis);
                chassisBody.isKinematic = originalIsKinematic;
                chassisBody.interpolation = originalInterpolation;
                chassisBody.position = originalPosition;
                chassisBody.rotation = originalRotation;
                chassisBody.velocity = originalVelocity;
                chassisBody.angularVelocity = originalAngularVelocity;
            }
        }

        [UnityTest]
        public IEnumerator SampleScene_vertical_motion_and_position_jumps_keep_support_point_and_weapon_in_sync()
        {
            yield return LoadIntegrationScene();

            Type freeLookType = PlayerWeaponTestReflection.FindRuntimeType("Cinemachine.CinemachineFreeLook");
            Type stableCameraTargetType = PlayerWeaponTestReflection.FindRuntimeType("StableCameraTarget");
            Type gunRotationType = PlayerWeaponTestReflection.FindRuntimeType("GunRotation");
            Type laserPointerType = PlayerWeaponTestReflection.FindRuntimeType("LaserPointer");
            Assert.That(freeLookType, Is.Not.Null);
            Assert.That(stableCameraTargetType, Is.Not.Null);
            Assert.That(gunRotationType, Is.Not.Null);
            Assert.That(laserPointerType, Is.Not.Null);

            Component freeLook = FindSceneComponent(integrationScene, freeLookType);
            Component stableCameraTarget = FindSceneComponent(integrationScene, stableCameraTargetType);
            Component gunRotation = FindSceneComponent(integrationScene, gunRotationType);
            Component laserPointer = FindSceneComponent(integrationScene, laserPointerType);
            Camera sceneCamera = (Camera)FindSceneComponent(integrationScene, typeof(Camera));
            Assert.That(freeLook, Is.Not.Null);
            Assert.That(stableCameraTarget, Is.Not.Null);
            Assert.That(gunRotation, Is.Not.Null);
            Assert.That(laserPointer, Is.Not.Null);
            Assert.That(sceneCamera, Is.Not.Null);

            PropertyInfo lookAtProperty = freeLookType.GetProperty(
                "LookAt",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(lookAtProperty, Is.Not.Null);
            Transform supportPoint = lookAtProperty.GetValue(freeLook, null) as Transform;
            Assert.That(supportPoint, Is.Not.Null);

            FieldInfo targetField = gunRotationType.GetField(
                "target",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(targetField, Is.Not.Null);
            Transform chassis = targetField.GetValue(gunRotation) as Transform;
            Assert.That(chassis, Is.Not.Null);
            Rigidbody chassisBody = chassis.GetComponent<Rigidbody>();
            Assert.That(chassisBody, Is.Not.Null);

            MethodInfo snapMethod = stableCameraTargetType.GetMethod(
                "SnapToFollowTarget",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(snapMethod, Is.Not.Null);

            Vector3 initialOffset = supportPoint.position - chassis.position;
            Vector3 originalPosition = chassisBody.position;
            Quaternion originalRotation = chassisBody.rotation;
            Vector3 originalVelocity = chassisBody.velocity;
            Vector3 originalAngularVelocity = chassisBody.angularVelocity;
            bool originalIsKinematic = chassisBody.isKinematic;
            RigidbodyInterpolation originalInterpolation = chassisBody.interpolation;
            string originalXAxisInput = GetCinemachineAxisInput(freeLook, "m_XAxis");
            string originalYAxisInput = GetCinemachineAxisInput(freeLook, "m_YAxis");

            try
            {
                SetCinemachineAxisInput(freeLook, "m_XAxis", string.Empty);
                SetCinemachineAxisInput(freeLook, "m_YAxis", string.Empty);
                chassisBody.isKinematic = true;
                chassisBody.interpolation = RigidbodyInterpolation.Interpolate;
                chassisBody.velocity = Vector3.zero;
                chassisBody.angularVelocity = Vector3.zero;

                yield return WaitForAimToFollowCamera(sceneCamera, laserPointer);
                Vector3 previousCameraForward = sceneCamera.transform.forward;

                for (int frame = 0; frame < 10; frame++)
                {
                    Vector3 verticalOffset = new Vector3(
                        0.06f,
                        Mathf.Sin(frame * 0.8f) * 0.18f,
                        -0.04f);
                    chassisBody.MovePosition(chassisBody.position + verticalOffset);
                    chassisBody.MoveRotation(Quaternion.Euler(0f, frame * 17f, 0f));

                    yield return new WaitForFixedUpdate();
                    yield return null;

                    AssertSceneAimPose(
                        supportPoint,
                        chassis,
                        gunRotation.transform,
                        laserPointer,
                        sceneCamera,
                        initialOffset,
                        "Vertical chassis movement must keep the support point, turret, and line of fire synchronized.");
                    Assert.That(
                        Vector3.Angle(previousCameraForward, sceneCamera.transform.forward),
                        Is.LessThan(5f),
                        "Vertical movement must not introduce a frame-sized camera direction jump.");
                    previousCameraForward = sceneCamera.transform.forward;
                }

                chassisBody.position = originalPosition + new Vector3(4.5f, 2.25f, -6.5f);
                chassisBody.rotation = Quaternion.Euler(0f, 135f, 0f);
                snapMethod.Invoke(stableCameraTarget, null);

                Assert.That(
                    Vector3.Distance(supportPoint.position - chassis.position, initialOffset),
                    Is.LessThan(0.001f),
                    "An explicit position jump must snap the support point before the next rendered frame.");

                yield return null;

                AssertSceneAimPose(
                    supportPoint,
                    chassis,
                    gunRotation.transform,
                    laserPointer,
                    sceneCamera,
                    initialOffset,
                    "A position jump must not leave the support point or turret catching up over later frames.");
            }
            finally
            {
                SetCinemachineAxisInput(freeLook, "m_XAxis", originalXAxisInput);
                SetCinemachineAxisInput(freeLook, "m_YAxis", originalYAxisInput);
                chassisBody.isKinematic = originalIsKinematic;
                chassisBody.interpolation = originalInterpolation;
                chassisBody.position = originalPosition;
                chassisBody.rotation = originalRotation;
                chassisBody.velocity = originalVelocity;
                chassisBody.angularVelocity = originalAngularVelocity;
            }
        }

        [UnityTest]
        public IEnumerator SampleScene_respawn_updates_rigidbody_support_point_and_turret_without_catch_up()
        {
            yield return LoadIntegrationScene();

            Type freeLookType = PlayerWeaponTestReflection.FindRuntimeType("Cinemachine.CinemachineFreeLook");
            Type stableCameraTargetType = PlayerWeaponTestReflection.FindRuntimeType("StableCameraTarget");
            Type respawnZoneType = PlayerWeaponTestReflection.FindRuntimeType("RespawnZone");
            Type gunRotationType = PlayerWeaponTestReflection.FindRuntimeType("GunRotation");
            Type laserPointerType = PlayerWeaponTestReflection.FindRuntimeType("LaserPointer");
            Assert.That(freeLookType, Is.Not.Null);
            Assert.That(stableCameraTargetType, Is.Not.Null);
            Assert.That(respawnZoneType, Is.Not.Null);
            Assert.That(gunRotationType, Is.Not.Null);
            Assert.That(laserPointerType, Is.Not.Null);

            Component freeLook = FindSceneComponent(integrationScene, freeLookType);
            Component stableCameraTarget = FindSceneComponent(integrationScene, stableCameraTargetType);
            Component respawnZone = FindSceneComponent(integrationScene, respawnZoneType);
            Component gunRotation = FindSceneComponent(integrationScene, gunRotationType);
            Component laserPointer = FindSceneComponent(integrationScene, laserPointerType);
            Camera sceneCamera = (Camera)FindSceneComponent(integrationScene, typeof(Camera));
            Assert.That(freeLook, Is.Not.Null);
            Assert.That(stableCameraTarget, Is.Not.Null);
            Assert.That(respawnZone, Is.Not.Null);
            Assert.That(gunRotation, Is.Not.Null);
            Assert.That(laserPointer, Is.Not.Null);
            Assert.That(sceneCamera, Is.Not.Null);

            PropertyInfo lookAtProperty = freeLookType.GetProperty(
                "LookAt",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(lookAtProperty, Is.Not.Null);
            Transform supportPoint = lookAtProperty.GetValue(freeLook, null) as Transform;
            Assert.That(supportPoint, Is.Not.Null);

            FieldInfo targetField = gunRotationType.GetField(
                "target",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(targetField, Is.Not.Null);
            Transform chassis = targetField.GetValue(gunRotation) as Transform;
            Assert.That(chassis, Is.Not.Null);
            Rigidbody chassisBody = chassis.GetComponent<Rigidbody>();
            Collider chassisCollider = chassis.GetComponent<Collider>();
            Assert.That(chassisBody, Is.Not.Null);
            Assert.That(chassisCollider, Is.Not.Null);

            FieldInfo respawnPointField = respawnZoneType.GetField(
                "respawnPoint",
                BindingFlags.Instance | BindingFlags.Public);
            FieldInfo cameraSupportPointField = respawnZoneType.GetField(
                "cameraSupportPoint",
                BindingFlags.Instance | BindingFlags.Public);
            FieldInfo damageField = respawnZoneType.GetField(
                "damageOnRespawn",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(respawnPointField, Is.Not.Null);
            Assert.That(cameraSupportPointField, Is.Not.Null);
            Assert.That(damageField, Is.Not.Null);
            Assert.That(cameraSupportPointField.GetValue(respawnZone), Is.SameAs(stableCameraTarget));
            Transform respawnPoint = respawnPointField.GetValue(respawnZone) as Transform;
            Assert.That(respawnPoint, Is.Not.Null);

            MethodInfo tryRespawn = respawnZoneType.GetMethod(
                "TryRespawn",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(tryRespawn, Is.Not.Null);

            Vector3 initialOffset = supportPoint.position - chassis.position;
            Vector3 originalPosition = chassisBody.position;
            Quaternion originalRotation = chassisBody.rotation;
            Vector3 originalVelocity = chassisBody.velocity;
            Vector3 originalAngularVelocity = chassisBody.angularVelocity;
            bool originalIsKinematic = chassisBody.isKinematic;
            RigidbodyInterpolation originalInterpolation = chassisBody.interpolation;
            float originalDamage = (float)damageField.GetValue(respawnZone);
            string originalXAxisInput = GetCinemachineAxisInput(freeLook, "m_XAxis");
            string originalYAxisInput = GetCinemachineAxisInput(freeLook, "m_YAxis");

            try
            {
                SetCinemachineAxisInput(freeLook, "m_XAxis", string.Empty);
                SetCinemachineAxisInput(freeLook, "m_YAxis", string.Empty);
                chassisBody.isKinematic = true;
                chassisBody.interpolation = RigidbodyInterpolation.Interpolate;
                chassisBody.velocity = Vector3.zero;
                chassisBody.angularVelocity = Vector3.zero;
                damageField.SetValue(respawnZone, 0f);

                yield return WaitForAimToFollowCamera(sceneCamera, laserPointer);

                Assert.That(
                    (bool)tryRespawn.Invoke(respawnZone, new object[] { chassisCollider }),
                    Is.True,
                    "The scene respawn zone must accept the player collider.");
                Assert.That(
                    Vector3.Distance(chassisBody.position, respawnPoint.position),
                    Is.LessThan(0.001f),
                    "Respawn must move the physics body, not only its rendered Transform.");
                Assert.That(
                    Vector3.Distance(supportPoint.position - chassis.position, initialOffset),
                    Is.LessThan(0.001f),
                    "Respawn must snap the camera support point at the teleport write site.");

                yield return null;

                AssertSceneAimPose(
                    supportPoint,
                    chassis,
                    gunRotation.transform,
                    laserPointer,
                    sceneCamera,
                    initialOffset,
                    "Respawn must leave no support-point or turret catch-up on the rendered frame.");
            }
            finally
            {
                damageField.SetValue(respawnZone, originalDamage);
                SetCinemachineAxisInput(freeLook, "m_XAxis", originalXAxisInput);
                SetCinemachineAxisInput(freeLook, "m_YAxis", originalYAxisInput);
                chassisBody.isKinematic = originalIsKinematic;
                chassisBody.interpolation = originalInterpolation;
                chassisBody.position = originalPosition;
                chassisBody.rotation = originalRotation;
                chassisBody.velocity = originalVelocity;
                chassisBody.angularVelocity = originalAngularVelocity;
            }
        }

        [UnityTest]
        public IEnumerator SampleScene_recoil_and_ultimate_animations_keep_support_point_and_laser_aligned()
        {
            yield return LoadIntegrationScene();

            Type freeLookType = PlayerWeaponTestReflection.FindRuntimeType("Cinemachine.CinemachineFreeLook");
            Type stableCameraTargetType = PlayerWeaponTestReflection.FindRuntimeType("StableCameraTarget");
            Type gunRotationType = PlayerWeaponTestReflection.FindRuntimeType("GunRotation");
            Type laserPointerType = PlayerWeaponTestReflection.FindRuntimeType("LaserPointer");
            Type playerShootingType = PlayerWeaponTestReflection.FindRuntimeType("PlayerShooting");
            Assert.That(freeLookType, Is.Not.Null);
            Assert.That(stableCameraTargetType, Is.Not.Null);
            Assert.That(gunRotationType, Is.Not.Null);
            Assert.That(laserPointerType, Is.Not.Null);
            Assert.That(playerShootingType, Is.Not.Null);

            Component freeLook = FindSceneComponent(integrationScene, freeLookType);
            Component stableCameraTarget = FindSceneComponent(integrationScene, stableCameraTargetType);
            Component gunRotation = FindSceneComponent(integrationScene, gunRotationType);
            Component laserPointer = FindSceneComponent(integrationScene, laserPointerType);
            Component playerShooting = FindSceneComponent(integrationScene, playerShootingType);
            Camera sceneCamera = (Camera)FindSceneComponent(integrationScene, typeof(Camera));
            Assert.That(freeLook, Is.Not.Null);
            Assert.That(stableCameraTarget, Is.Not.Null);
            Assert.That(gunRotation, Is.Not.Null);
            Assert.That(laserPointer, Is.Not.Null);
            Assert.That(playerShooting, Is.Not.Null);
            Assert.That(sceneCamera, Is.Not.Null);

            PropertyInfo lookAtProperty = freeLookType.GetProperty(
                "LookAt",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(lookAtProperty, Is.Not.Null);
            Transform supportPoint = lookAtProperty.GetValue(freeLook, null) as Transform;
            Assert.That(supportPoint, Is.Not.Null);

            FieldInfo targetField = gunRotationType.GetField(
                "target",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(targetField, Is.Not.Null);
            Transform chassis = targetField.GetValue(gunRotation) as Transform;
            Assert.That(chassis, Is.Not.Null);
            Rigidbody chassisBody = chassis.GetComponent<Rigidbody>();
            Assert.That(chassisBody, Is.Not.Null);

            MethodInfo tryFireVolley = playerShootingType.GetMethod(
                "TryFireVolley",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(tryFireVolley, Is.Not.Null);
            MethodInfo tryStartUltimate = playerShootingType.GetMethod(
                "TryStartUltimate",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(tryStartUltimate, Is.Not.Null);

            Vector3 initialOffset = supportPoint.position - chassis.position;
            Vector3 originalPosition = chassisBody.position;
            Quaternion originalRotation = chassisBody.rotation;
            Vector3 originalVelocity = chassisBody.velocity;
            Vector3 originalAngularVelocity = chassisBody.angularVelocity;
            bool originalIsKinematic = chassisBody.isKinematic;
            RigidbodyInterpolation originalInterpolation = chassisBody.interpolation;
            string originalXAxisInput = GetCinemachineAxisInput(freeLook, "m_XAxis");
            string originalYAxisInput = GetCinemachineAxisInput(freeLook, "m_YAxis");

            try
            {
                SetCinemachineAxisInput(freeLook, "m_XAxis", string.Empty);
                SetCinemachineAxisInput(freeLook, "m_YAxis", string.Empty);
                chassisBody.isKinematic = true;
                chassisBody.interpolation = RigidbodyInterpolation.None;
                chassisBody.velocity = Vector3.zero;
                chassisBody.angularVelocity = Vector3.zero;

                yield return WaitForAimToFollowCamera(sceneCamera, laserPointer);

                Assert.That(
                    (bool)tryFireVolley.Invoke(playerShooting, null),
                    Is.True,
                    "The scene must expose a working recoil animation trigger for this regression.");

                for (int frame = 0; frame < 12; frame++)
                {
                    yield return null;
                    AssertSceneAimPose(
                        supportPoint,
                        chassis,
                        gunRotation.transform,
                        laserPointer,
                        sceneCamera,
                        initialOffset,
                        "Recoil animation must not move or rotate the camera support point.");
                }

                yield return new WaitForSecondsRealtime(0.35f);
                Assert.That(
                    (bool)tryStartUltimate.Invoke(playerShooting, null),
                    Is.True,
                    "The scene must expose a working ultimate animation trigger for this regression.");

                for (int frame = 0; frame < 20; frame++)
                {
                    yield return null;
                    AssertSceneAimPose(
                        supportPoint,
                        chassis,
                        gunRotation.transform,
                        laserPointer,
                        sceneCamera,
                        initialOffset,
                        "Ultimate animation must not move or rotate the camera support point.");
                }
            }
            finally
            {
                SetCinemachineAxisInput(freeLook, "m_XAxis", originalXAxisInput);
                SetCinemachineAxisInput(freeLook, "m_YAxis", originalYAxisInput);
                chassisBody.isKinematic = originalIsKinematic;
                chassisBody.interpolation = originalInterpolation;
                chassisBody.position = originalPosition;
                chassisBody.rotation = originalRotation;
                chassisBody.velocity = originalVelocity;
                chassisBody.angularVelocity = originalAngularVelocity;
            }
        }

        private static void AssertSceneAimPose(
            Transform supportPoint,
            Transform chassis,
            Transform turret,
            Component laserPointer,
            Camera sceneCamera,
            Vector3 expectedOffset,
            string message)
        {
            Assert.That(
                Vector3.Distance(supportPoint.position - chassis.position, expectedOffset),
                Is.LessThan(0.001f),
                message);
            Assert.That(
                Quaternion.Angle(supportPoint.rotation, Quaternion.identity),
                Is.LessThan(0.001f),
                message + " The support point must keep a neutral world rotation.");
            Assert.That(
                Vector3.Distance(turret.position, chassis.position),
                Is.LessThan(0.001f),
                message + " The turret must remain attached to the chassis.");
            Assert.That(
                Vector3.Angle(GetAimDirection(laserPointer), -sceneCamera.transform.forward),
                Is.LessThan(1f),
                message + " The line of fire must follow the final camera direction.");
        }

        private IEnumerator LoadIntegrationScene()
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
            SetCinemachineAxisValue(freeLook, "m_YAxis", value);
        }

        private static void SetCinemachineAxisValue(Component freeLook, string axisName, float value)
        {
            FieldInfo axisField = freeLook.GetType().GetField(
                axisName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(axisField, Is.Not.Null);

            object axis = axisField.GetValue(freeLook);
            FieldInfo valueField = axis.GetType().GetField(
                "Value",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(valueField, Is.Not.Null);
            valueField.SetValue(axis, value);
            axisField.SetValue(freeLook, axis);
        }

        private static float GetCinemachineAxisValue(Component freeLook, string axisName)
        {
            FieldInfo axisField = freeLook.GetType().GetField(
                axisName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(axisField, Is.Not.Null);

            object axis = axisField.GetValue(freeLook);
            FieldInfo valueField = axis.GetType().GetField(
                "Value",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(valueField, Is.Not.Null);
            return (float)valueField.GetValue(axis);
        }

        private static string GetCinemachineAxisInput(Component freeLook, string axisName)
        {
            FieldInfo axisField = freeLook.GetType().GetField(
                axisName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(axisField, Is.Not.Null);

            object axis = axisField.GetValue(freeLook);
            FieldInfo inputField = axis.GetType().GetField(
                "m_InputAxisName",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(inputField, Is.Not.Null);
            return (string)inputField.GetValue(axis);
        }

        private static void SetCinemachineAxisInput(
            Component freeLook,
            string axisName,
            string inputAxisName)
        {
            FieldInfo axisField = freeLook.GetType().GetField(
                axisName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(axisField, Is.Not.Null);

            object axis = axisField.GetValue(freeLook);
            FieldInfo inputField = axis.GetType().GetField(
                "m_InputAxisName",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(inputField, Is.Not.Null);
            inputField.SetValue(axis, inputAxisName);
            axisField.SetValue(freeLook, axis);
        }

        private static float GetFloatField(Component component, string fieldName)
        {
            FieldInfo field = component.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (float)field.GetValue(component);
        }

        private static void AssertLookAtScreenX(Camera camera, Transform lookAt, float expected)
        {
            Vector3 viewportPoint = camera.WorldToViewportPoint(lookAt.position);
            Assert.That(viewportPoint.z, Is.GreaterThan(0f));
            Assert.That(viewportPoint.x, Is.EqualTo(expected).Within(0.03f));
        }

        private static int GetExecutionOrder(string typeName)
        {
            Type type = PlayerWeaponTestReflection.FindRuntimeType(typeName);
            Assert.That(type, Is.Not.Null);
            DefaultExecutionOrder order = (DefaultExecutionOrder)Attribute.GetCustomAttribute(
                type,
                typeof(DefaultExecutionOrder));
            Assert.That(order, Is.Not.Null);
            return order.order;
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
