using NUnit.Framework;

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
    }
}
