using NUnit.Framework;
using RobotArena.PlayerWeapon;

namespace RobotArena.PlayerWeapon.Tests
{
    public sealed class ShoulderClearanceTests
    {
        [Test]
        public void Safe_shoulder_offset_keeps_the_largest_unobstructed_value()
        {
            float safeOffset = PlayerWeaponAim.ResolveSafeShoulderOffset(
                2f,
                offset => offset > 0.75f);

            Assert.That(safeOffset, Is.EqualTo(0.75f).Within(0.01f));
        }

        [Test]
        public void Safe_shoulder_offset_keeps_full_composition_when_path_is_clear()
        {
            float safeOffset = PlayerWeaponAim.ResolveSafeShoulderOffset(
                2f,
                offset => false);

            Assert.That(safeOffset, Is.EqualTo(2f).Within(0.001f));
        }
    }
}
