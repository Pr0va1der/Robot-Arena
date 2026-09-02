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
        public void Releasing_the_last_system_source_leaves_gameplay_paused_but_unpauses_audio()
        {
            var pauseCoordinator = new PauseCoordinator();

            pauseCoordinator.SetSource(PauseSource.Focus, true);
            pauseCoordinator.SetSource(PauseSource.Focus, false);

            Assert.That(pauseCoordinator.IsPaused, Is.False);
            Assert.That(pauseCoordinator.IsGameplayPaused, Is.True);
            Assert.That(pauseCoordinator.IsAudioPaused, Is.False);
            Assert.That(pauseCoordinator.IsWaitingForResume, Is.True);
        }

        [Test]
        public void Consuming_the_resume_request_unpauses_gameplay()
        {
            var pauseCoordinator = new PauseCoordinator();
            pauseCoordinator.RequirePointerLockClick();

            Assert.That(pauseCoordinator.TryConsumePointerLockRequest(), Is.True);

            Assert.That(pauseCoordinator.IsGameplayPaused, Is.False);
            Assert.That(pauseCoordinator.IsWaitingForResume, Is.False);
        }

        [Test]
        public void Clearing_resume_request_is_idempotent_for_terminal_result()
        {
            var pauseCoordinator = new PauseCoordinator();
            pauseCoordinator.RequirePointerLockClick();

            pauseCoordinator.ClearResumeRequirement();
            pauseCoordinator.ClearResumeRequirement();

            Assert.That(pauseCoordinator.IsGameplayPaused, Is.False);
            Assert.That(pauseCoordinator.RequiresPointerLockClick, Is.False);
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

        [Test]
        public void Tutorial_pause_does_not_pause_audio()
        {
            var pauseCoordinator = new PauseCoordinator();

            pauseCoordinator.SetSource(PauseSource.Tutorial, true);

            Assert.That(pauseCoordinator.IsGameplayPaused, Is.True);
            Assert.That(pauseCoordinator.IsAudioPaused, Is.False);
        }

        [Test]
        public void Terminal_result_clears_waiting_for_resume()
        {
            var pauseCoordinator = new PauseCoordinator();
            pauseCoordinator.RequirePointerLockClick();

            pauseCoordinator.SetSource(PauseSource.Result, true);

            Assert.That(pauseCoordinator.IsPaused, Is.True);
            Assert.That(pauseCoordinator.IsGameplayPaused, Is.True);
            Assert.That(pauseCoordinator.RequiresPointerLockClick, Is.False);
            Assert.That(pauseCoordinator.TryConsumePointerLockRequest(), Is.False);
        }

        [Test]
        public void Releasing_system_pause_does_not_clear_a_user_pause()
        {
            var pauseCoordinator = new PauseCoordinator();
            pauseCoordinator.SetSource(PauseSource.User, true);
            pauseCoordinator.SetSource(PauseSource.Focus, true);
            pauseCoordinator.SetSource(PauseSource.Focus, false);

            Assert.That(pauseCoordinator.IsPaused, Is.True);
            Assert.That(pauseCoordinator.IsSourceActive(PauseSource.User), Is.True);
            Assert.That(pauseCoordinator.RequiresPointerLockClick, Is.True);

            pauseCoordinator.SetSource(PauseSource.User, false);

            Assert.That(pauseCoordinator.IsWaitingForResume, Is.True);
        }

        [Test]
        public void A_user_pause_can_be_released_into_a_single_resume_gesture()
        {
            var pauseCoordinator = new PauseCoordinator();
            pauseCoordinator.SetSource(PauseSource.User, true);

            pauseCoordinator.RequirePointerLockClick();
            pauseCoordinator.SetSource(PauseSource.User, false);

            Assert.That(pauseCoordinator.TryConsumePointerLockRequest(), Is.True);
            Assert.That(pauseCoordinator.IsGameplayPaused, Is.False);
        }
    }
}
