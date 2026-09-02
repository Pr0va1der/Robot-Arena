using NUnit.Framework;
using UnityEngine;

namespace RobotArena.PlayerWeapon.Tests
{
    public sealed class PlayerWeaponAimTests
    {
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
