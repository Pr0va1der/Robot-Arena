using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotArena.PlayerWeapon.Tests
{
    public sealed class LaserPointerTests
    {
        [UnityTest]
        public IEnumerator Aim_ray_skips_owner_but_visual_beam_starts_outside_owner()
        {
            GameObject owner = new GameObject("LaserOwner");
            GameObject target = new GameObject("LaserTarget");

            try
            {
                BoxCollider ownerCollider = owner.AddComponent<BoxCollider>();
                ownerCollider.size = new Vector3(2f, 2f, 2f);

                GameObject barrelObject = new GameObject("Barrel");
                barrelObject.transform.SetParent(owner.transform);
                barrelObject.transform.localPosition = new Vector3(0f, 0f, -2f);

                GameObject visualPointObject = new GameObject("VisualEmission");
                visualPointObject.transform.SetParent(owner.transform);
                visualPointObject.transform.localPosition = Vector3.zero;

                GameObject laserObject = new GameObject("Laser");
                laserObject.transform.SetParent(owner.transform);
                LineRenderer lineRenderer = laserObject.AddComponent<LineRenderer>();
                Component laser = laserObject.AddComponent(
                    PlayerWeaponTestReflection.FindRuntimeType("LaserPointer"));
                SetField(laser, "barrel", barrelObject.transform);
                SetField(laser, "visualEmissionPoint", visualPointObject.transform);
                SetField(laser, "ownerRoot", owner.transform);
                SetField(laser, "maxDistance", 20f);
                SetField(laser, "hitLayers", (LayerMask)(~0));
                SetField(laser, "localAimAxis", Vector3.forward);

                target.transform.position = new Vector3(0f, 0f, 5f);
                BoxCollider targetCollider = target.AddComponent<BoxCollider>();
                targetCollider.size = Vector3.one;

                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();

                bool refreshed = (bool)laser.GetType().GetMethod("RefreshAim").Invoke(laser, null);
                Vector3 aimTarget = GetProperty<Vector3>(laser, "AimTarget");
                Vector3 lineStart = lineRenderer.GetPosition(0);

                Assert.That(refreshed, Is.True);
                Assert.That(aimTarget.z, Is.EqualTo(4.5f).Within(0.05f));
                Assert.That(lineStart.z, Is.GreaterThan(1f));
            }
            finally
            {
                UnityEngine.Object.Destroy(owner);
                UnityEngine.Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator No_external_hit_uses_max_distance_from_visual_start()
        {
            GameObject owner = new GameObject("LaserOwner");

            try
            {
                BoxCollider ownerCollider = owner.AddComponent<BoxCollider>();
                ownerCollider.size = new Vector3(2f, 2f, 2f);

                GameObject barrelObject = new GameObject("Barrel");
                barrelObject.transform.SetParent(owner.transform);
                barrelObject.transform.localPosition = new Vector3(0f, 0f, -2f);

                GameObject visualPointObject = new GameObject("VisualEmission");
                visualPointObject.transform.SetParent(owner.transform);
                visualPointObject.transform.localPosition = Vector3.zero;

                GameObject laserObject = new GameObject("Laser");
                laserObject.transform.SetParent(owner.transform);
                LineRenderer lineRenderer = laserObject.AddComponent<LineRenderer>();
                Component laser = laserObject.AddComponent(
                    PlayerWeaponTestReflection.FindRuntimeType("LaserPointer"));
                SetField(laser, "barrel", barrelObject.transform);
                SetField(laser, "visualEmissionPoint", visualPointObject.transform);
                SetField(laser, "ownerRoot", owner.transform);
                SetField(laser, "maxDistance", 5f);
                SetField(laser, "hitLayers", (LayerMask)(~0));
                SetField(laser, "localAimAxis", Vector3.forward);

                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();

                bool refreshed = (bool)laser.GetType().GetMethod("RefreshAim").Invoke(laser, null);
                Vector3 lineStart = lineRenderer.GetPosition(0);
                Vector3 lineEnd = lineRenderer.GetPosition(1);

                Assert.That(refreshed, Is.False);
                Assert.That(lineEnd.z - lineStart.z, Is.EqualTo(5f).Within(0.05f));
            }
            finally
            {
                UnityEngine.Object.Destroy(owner);
            }
        }

        [UnityTest]
        public IEnumerator Beam_begins_at_first_owner_exit_even_when_emission_point_is_ahead()
        {
            GameObject owner = new GameObject("LaserOwner");

            try
            {
                BoxCollider ownerCollider = owner.AddComponent<BoxCollider>();
                ownerCollider.size = new Vector3(2f, 2f, 2f);

                GameObject barrelObject = new GameObject("Barrel");
                barrelObject.transform.SetParent(owner.transform);
                barrelObject.transform.localPosition = new Vector3(0f, 0f, -2f);

                GameObject visualPointObject = new GameObject("VisualEmission");
                visualPointObject.transform.SetParent(owner.transform);
                visualPointObject.transform.localPosition = new Vector3(0f, 0f, 2f);

                GameObject laserObject = new GameObject("Laser");
                laserObject.transform.SetParent(owner.transform);
                LineRenderer lineRenderer = laserObject.AddComponent<LineRenderer>();
                Component laser = laserObject.AddComponent(
                    PlayerWeaponTestReflection.FindRuntimeType("LaserPointer"));
                SetField(laser, "barrel", barrelObject.transform);
                SetField(laser, "visualEmissionPoint", visualPointObject.transform);
                SetField(laser, "ownerRoot", owner.transform);
                SetField(laser, "maxDistance", 5f);
                SetField(laser, "hitLayers", (LayerMask)(~0));
                SetField(laser, "localAimAxis", Vector3.forward);

                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();

                laser.GetType().GetMethod("RefreshAim").Invoke(laser, null);
                Vector3 lineStart = lineRenderer.GetPosition(0);

                Assert.That(lineStart.z, Is.GreaterThan(1f));
                Assert.That(lineStart.z, Is.LessThan(1.1f));
            }
            finally
            {
                UnityEngine.Object.Destroy(owner);
            }
        }

        private static T GetProperty<T>(Component component, string propertyName)
        {
            PropertyInfo property = component.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            return (T)property.GetValue(component, null);
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
