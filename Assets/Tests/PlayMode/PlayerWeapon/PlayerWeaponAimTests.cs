using NUnit.Framework;
using UnityEngine;

namespace RobotArena.PlayerWeapon.Tests
{
    public sealed class PlayerWeaponAimTests
    {
        [Test]
        public void Projectile_direction_uses_the_laser_target_for_targets_above_and_below_the_weapon()
        {
            Vector3 origin = new Vector3(0f, 1f, 0f);
            Vector3 targetAbove = new Vector3(4f, 5f, 12f);
            Vector3 targetBelow = new Vector3(4f, -3f, 12f);

            Vector3 directionAbove = PlayerWeaponAim.DirectionToTarget(
                origin,
                targetAbove,
                Vector3.forward);
            Vector3 directionBelow = PlayerWeaponAim.DirectionToTarget(
                origin,
                targetBelow,
                Vector3.forward);

            Assert.That(directionAbove.y, Is.GreaterThan(0f));
            Assert.That(directionBelow.y, Is.LessThan(0f));
            Assert.That(directionAbove, Is.EqualTo((targetAbove - origin).normalized));
            Assert.That(directionBelow, Is.EqualTo((targetBelow - origin).normalized));
        }

        [Test]
        public void Vertical_aim_is_clamped_to_the_configured_elevation_limits()
        {
            float elevation = PlayerWeaponAim.ClampElevation(
                current: 0f,
                lookDelta: 1f,
                lookSpeed: 180f,
                deltaTime: 1f,
                minElevation: -45f,
                maxElevation: 45f);

            Assert.That(elevation, Is.EqualTo(45f));

            elevation = PlayerWeaponAim.ClampElevation(
                current: 0f,
                lookDelta: -1f,
                lookSpeed: 180f,
                deltaTime: 1f,
                minElevation: -45f,
                maxElevation: 45f);

            Assert.That(elevation, Is.EqualTo(-45f));
        }
    }
}
