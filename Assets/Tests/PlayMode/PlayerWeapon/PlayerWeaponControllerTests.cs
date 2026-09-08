using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RobotArena.PlayerWeapon.Tests
{
    public sealed class PlayerWeaponControllerTests
    {
        [Test]
        public void HeldFireStartsImmediatelyAndRepeatsAfterEachRecoilCycle()
        {
            var controller = new PlayerWeaponController(0.25f);

            Assert.That(controller.Tick(0f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.FireVolley));
            Assert.That(controller.Tick(0.249f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.None));
            Assert.That(controller.Tick(0.25f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.FireVolley));
            Assert.That(controller.Tick(0.5f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.FireVolley));
        }

        [Test]
        public void ReleasingFireDuringRecoilPreventsTheNextVolley()
        {
            var controller = new PlayerWeaponController(0.25f);

            Assert.That(controller.Tick(0f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.FireVolley));
            Assert.That(controller.Tick(0.1f, fireHeld: false, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.None));
            Assert.That(controller.Tick(0.25f, fireHeld: false, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.None));
        }

        [Test]
        public void UltimatePressedDuringRecoilRunsBeforeTheNextHeldFireVolley()
        {
            var controller = new PlayerWeaponController(0.25f);

            Assert.That(controller.Tick(0f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.FireVolley));
            Assert.That(controller.Tick(0.1f, fireHeld: true, ultimatePressed: true, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.None));
            Assert.That(controller.Tick(0.25f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.StartUltimate));
            Assert.That(controller.Tick(0.3f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.None));

            controller.CompleteUltimate();

            Assert.That(controller.Tick(0.5f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.FireVolley));
        }

        [Test]
        public void ChangingRecoilDurationChangesTheNextReadyMoment()
        {
            var controller = new PlayerWeaponController(0.25f);
            controller.RecoilDuration = 0.4f;

            Assert.That(controller.Tick(0f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.FireVolley));

            Assert.That(controller.Tick(0.25f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.None));
            Assert.That(controller.Tick(0.4f, fireHeld: true, ultimatePressed: false, ultimateReady: true),
                Is.EqualTo(PlayerWeaponCommand.FireVolley));
        }

        [Test]
        public void Invalid_volley_configuration_does_not_start_recoil_but_keeps_ultimate_available()
        {
            var controller = new PlayerWeaponController(0.25f);

            Assert.That(controller.Tick(0f, fireHeld: true, ultimatePressed: false, ultimateReady: true, fireVolleyAvailable: false),
                Is.EqualTo(PlayerWeaponCommand.None));
            Assert.That(controller.IsRecoilActive, Is.False);

            Assert.That(controller.Tick(0.1f, fireHeld: false, ultimatePressed: true, ultimateReady: true, fireVolleyAvailable: false),
                Is.EqualTo(PlayerWeaponCommand.StartUltimate));
        }
    }

    public sealed class PlayerShootingVolleyTests
    {
        [UnityTest]
        public IEnumerator TryFireVolley_spawns_two_parallel_projectiles_from_both_barrels()
        {
            var createdObjects = new List<GameObject>();
            try
            {
                Type playerShootingType = PlayerWeaponTestReflection.FindRuntimeType("PlayerShooting");
                Type laserPointerType = PlayerWeaponTestReflection.FindRuntimeType("LaserPointer");
                Assert.That(playerShootingType, Is.Not.Null);
                Assert.That(laserPointerType, Is.Not.Null);

                GameObject player = new GameObject("VolleyPlayer");
                createdObjects.Add(player);

                GameObject leftBarrel = new GameObject("LeftBarrel");
                leftBarrel.transform.SetParent(player.transform);
                leftBarrel.transform.position = new Vector3(-0.75f, 0f, 0f);

                GameObject rightBarrel = new GameObject("RightBarrel");
                rightBarrel.transform.SetParent(player.transform);
                rightBarrel.transform.position = new Vector3(0.75f, 0f, 0f);

                GameObject aimBarrel = new GameObject("AimBarrel");
                aimBarrel.transform.SetParent(player.transform);
                aimBarrel.transform.position = Vector3.zero;
                Component laserPointer = aimBarrel.AddComponent(laserPointerType);
                PlayerWeaponTestReflection.SetField(laserPointer, "barrel", aimBarrel.transform);
                PlayerWeaponTestReflection.SetField(laserPointer, "localAimAxis", Vector3.forward);
                PlayerWeaponTestReflection.SetField(laserPointer, "maxDistance", 20f);
                PlayerWeaponTestReflection.SetField(laserPointer, "hitLayers", (LayerMask)(1 << 8));

                GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name = "LaserHitTarget";
                target.layer = 8;
                target.transform.position = new Vector3(0f, 0f, 10f);
                target.transform.localScale = new Vector3(4f, 4f, 0.2f);
                createdObjects.Add(target);

                GameObject bulletPrefab = new GameObject("VolleyBullet");
                bulletPrefab.AddComponent<Rigidbody>();
                createdObjects.Add(bulletPrefab);

                GameObject physicsBody = new GameObject("PhysicsBody");
                physicsBody.AddComponent<Rigidbody>();
                createdObjects.Add(physicsBody);

                Component shooting = player.AddComponent(playerShootingType);
                PlayerWeaponTestReflection.SetField(shooting, "bulletPrefab", bulletPrefab);
                PlayerWeaponTestReflection.SetField(shooting, "firePointLeft", leftBarrel.transform);
                PlayerWeaponTestReflection.SetField(shooting, "firePointRight", rightBarrel.transform);
                PlayerWeaponTestReflection.SetField(shooting, "laserPointer", laserPointer);
                PlayerWeaponTestReflection.SetField(shooting, "physicsBody", physicsBody);
                PlayerWeaponTestReflection.SetField(shooting, "shootForce", 10f);

                MethodInfo tryFireVolley = playerShootingType.GetMethod(
                    "TryFireVolley",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(tryFireVolley, Is.Not.Null);
                Assert.That((bool)tryFireVolley.Invoke(shooting, null), Is.True);

                var spawned = new List<Rigidbody>();
                Rigidbody[] bodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                foreach (Rigidbody body in bodies)
                {
                    if (body.gameObject != bulletPrefab &&
                        body.gameObject.name.StartsWith("VolleyBullet", StringComparison.Ordinal))
                    {
                        spawned.Add(body);
                    }
                }

                Assert.That(spawned.Count, Is.EqualTo(2));

                Rigidbody leftBullet = spawned[0].position.x < 0f ? spawned[0] : spawned[1];
                Rigidbody rightBullet = spawned[0].position.x < 0f ? spawned[1] : spawned[0];
                Assert.That(Vector3.Distance(leftBullet.position, leftBarrel.transform.position), Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(rightBullet.position, rightBarrel.transform.position), Is.LessThan(0.0001f));
                Assert.That(leftBullet.useGravity, Is.False);
                Assert.That(rightBullet.useGravity, Is.False);
                Assert.That(Vector3.Angle(leftBullet.transform.forward, rightBullet.transform.forward), Is.LessThan(0.01f));

                PropertyInfo aimDirection = laserPointerType.GetProperty(
                    "AimDirection",
                    BindingFlags.Instance | BindingFlags.Public);
                Vector3 expectedDirection = (Vector3)aimDirection.GetValue(laserPointer, null);
                Assert.That(Vector3.Angle(leftBullet.transform.forward, expectedDirection), Is.LessThan(0.01f));
                Assert.That(Vector3.Angle(rightBullet.transform.forward, expectedDirection), Is.LessThan(0.01f));

                Vector3 leftHitDirection = (target.transform.position - leftBullet.position).normalized;
                Vector3 rightHitDirection = (target.transform.position - rightBullet.position).normalized;
                Assert.That(Vector3.Angle(leftBullet.transform.forward, leftHitDirection), Is.GreaterThan(0.1f));
                Assert.That(Vector3.Angle(rightBullet.transform.forward, rightHitDirection), Is.GreaterThan(0.1f));

                yield return null;
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

    }

    public sealed class PauseMenuLifecycleTests
    {
        [UnityTest]
        public IEnumerator Keyboard_resume_keeps_the_resume_gate_until_the_pointer_gesture()
        {
            GameObject canvasObject = CreatePauseMenu(out Component pauseMenu);
            try
            {
                yield return null;

                Invoke(pauseMenu, "SetTutorialMode", false);
                Type pauseSourceType = PlayerWeaponTestReflection.FindRuntimeType("RobotArena.Session.PauseSource");
                object userSource = Enum.Parse(pauseSourceType, "User");
                Invoke(pauseMenu, "SetPauseSource", userSource, true);

                Invoke(pauseMenu, "Resume");

                Assert.That(GetBool(pauseMenu, "IsPaused"), Is.False);
                Assert.That(GetBool(pauseMenu, "RequiresPointerLockClick"), Is.True);
                Assert.That(GetBool(pauseMenu, "IsGameplayPaused"), Is.True);

                Assert.That((bool)Invoke(pauseMenu, "ResumeFromPointerGesture"), Is.True);
                Assert.That(GetBool(pauseMenu, "IsGameplayPaused"), Is.False);
                Assert.That(GetStaticBool("PauseMenu", "PointerLockGestureConsumed"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        [UnityTest]
        public IEnumerator Completing_tutorial_persists_completion_and_leaves_one_resume_gate()
        {
            Type settingsRuntimeType = PlayerWeaponTestReflection.FindRuntimeType("GameSettingsRuntime");
            object settingsRuntime = settingsRuntimeType.GetMethod("GetOrCreate").Invoke(null, null);
            PropertyInfo currentProperty = settingsRuntimeType.GetProperty("Current");
            FieldInfo currentField = settingsRuntimeType.GetField(
                "<Current>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object previousSettings = currentProperty.GetValue(settingsRuntime);
            const string tutorialKey = "RobotArena.Settings.TutorialCompleted";
            bool hadTutorialKey = PlayerPrefs.HasKey(tutorialKey);
            int previousTutorialValue = hadTutorialKey ? PlayerPrefs.GetInt(tutorialKey) : 0;

            try
            {
                PlayerPrefs.SetInt(tutorialKey, 0);
                PlayerPrefs.Save();
                Type storeType = PlayerWeaponTestReflection.FindRuntimeType("RobotArena.Session.PlayerPrefsPlayerSettingsStore");
                object store = Activator.CreateInstance(storeType);
                object incompleteSettings = storeType.GetMethod("Load").Invoke(store, null);
                currentField.SetValue(settingsRuntime, incompleteSettings);

                GameObject canvasObject = CreatePauseMenu(out Component pauseMenu);
                try
                {
                    yield return null;

                    Component desktopUi = canvasObject.GetComponent(PlayerWeaponTestReflection.FindRuntimeType("DesktopArenaUi"));
                    Assert.That(desktopUi, Is.Not.Null);
                    Invoke(desktopUi, "StartTutorial");

                    object currentSettings = currentProperty.GetValue(settingsRuntime);
                    PropertyInfo completedProperty = currentSettings.GetType().GetProperty("HasCompletedTutorial");
                    Assert.That((bool)completedProperty.GetValue(currentSettings), Is.True);
                    Assert.That(GetBool(pauseMenu, "IsTutorialMode"), Is.False);
                    Assert.That(GetBool(pauseMenu, "RequiresPointerLockClick"), Is.True);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(canvasObject);
                }
            }
            finally
            {
                currentField.SetValue(settingsRuntime, previousSettings);
                if (hadTutorialKey)
                {
                    PlayerPrefs.SetInt(tutorialKey, previousTutorialValue);
                }
                else
                {
                    PlayerPrefs.DeleteKey(tutorialKey);
                }

                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator Result_mode_clears_a_pending_resume_gate_and_stops_gameplay()
        {
            GameObject canvasObject = CreatePauseMenu(out Component pauseMenu);
            try
            {
                yield return null;

                Invoke(pauseMenu, "SetResultMode", true);

                Assert.That(GetBool(pauseMenu, "IsResultMode"), Is.True);
                Assert.That(GetBool(pauseMenu, "IsPaused"), Is.True);
                Assert.That(GetBool(pauseMenu, "RequiresPointerLockClick"), Is.False);
                Assert.That(GetBool(pauseMenu, "IsAudioPaused"), Is.False);

                Invoke(pauseMenu, "SetResultMode", false);

                Assert.That(GetBool(pauseMenu, "IsResultMode"), Is.False);
                Assert.That(GetBool(pauseMenu, "IsGameplayPaused"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static GameObject CreatePauseMenu(out Component pauseMenu)
        {
            GameObject canvasObject = new GameObject("PauseMenuLifecycleCanvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            Type pauseMenuType = PlayerWeaponTestReflection.FindRuntimeType("PauseMenu");
            pauseMenu = canvasObject.AddComponent(pauseMenuType);
            return canvasObject;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, arguments);
        }

        private static bool GetBool(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (bool)property.GetValue(target);
        }

        private static bool GetStaticBool(string typeName, string propertyName)
        {
            Type type = PlayerWeaponTestReflection.FindRuntimeType(typeName);
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (bool)property.GetValue(null);
        }

    }

    internal static class PlayerWeaponTestReflection
    {
        public static Type FindRuntimeType(string typeName)
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

        public static void SetField(Component component, string fieldName, object value)
        {
            FieldInfo field = component.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(component, value);
        }
    }
}
