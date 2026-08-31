using NUnit.Framework;
using RobotArena.Session;

namespace RobotArena.Session.Tests
{
    public class PauseCoordinatorTests
    {
        [Test]
        public void Releasing_one_pause_source_keeps_pause_until_every_source_is_released()
        {
            var pauseCoordinator = new PauseCoordinator();

            pauseCoordinator.SetSource(PauseSource.Focus, true);
            pauseCoordinator.SetSource(PauseSource.Advertisement, true);

            Assert.That(pauseCoordinator.IsPaused, Is.True);
            Assert.That(
                pauseCoordinator.ActiveSources,
                Is.EqualTo(PauseSource.Focus | PauseSource.Advertisement));

            pauseCoordinator.SetSource(PauseSource.Focus, false);

            Assert.That(pauseCoordinator.IsPaused, Is.True);
            Assert.That(pauseCoordinator.ActiveSources, Is.EqualTo(PauseSource.Advertisement));

            pauseCoordinator.SetSource(PauseSource.Advertisement, false);

            Assert.That(pauseCoordinator.IsPaused, Is.False);
            Assert.That(pauseCoordinator.ActiveSources, Is.EqualTo(PauseSource.None));
        }

        [Test]
        public void System_pause_requires_a_user_click_before_pointer_lock()
        {
            var pauseCoordinator = new PauseCoordinator();

            pauseCoordinator.SetSource(PauseSource.Focus, true);
            pauseCoordinator.SetSource(PauseSource.Focus, false);

            Assert.That(pauseCoordinator.IsPaused, Is.False);
            Assert.That(pauseCoordinator.RequiresPointerLockClick, Is.True);
            Assert.That(pauseCoordinator.TryConsumePointerLockRequest(), Is.True);
            Assert.That(pauseCoordinator.RequiresPointerLockClick, Is.False);
            Assert.That(pauseCoordinator.TryConsumePointerLockRequest(), Is.False);
        }

        [Test]
        public void Result_pause_stops_gameplay_but_keeps_death_music_audible()
        {
            var pauseCoordinator = new PauseCoordinator();

            pauseCoordinator.SetSource(PauseSource.Result, true);

            Assert.That(pauseCoordinator.IsPaused, Is.True);
            Assert.That(pauseCoordinator.IsAudioPaused, Is.False);

            pauseCoordinator.SetSource(PauseSource.Focus, true);

            Assert.That(pauseCoordinator.IsAudioPaused, Is.True);

            pauseCoordinator.SetSource(PauseSource.Focus, false);
            Assert.That(pauseCoordinator.IsAudioPaused, Is.False);
        }
    }
}
