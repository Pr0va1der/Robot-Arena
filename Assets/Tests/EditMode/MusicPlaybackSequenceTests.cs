using NUnit.Framework;
using RobotArena.Session;

namespace RobotArena.Session.Tests
{
    public sealed class MusicPlaybackSequenceTests
    {
        [Test]
        public void Queued_intro_waits_for_actual_start_before_counting_down()
        {
            var sequence = new MusicPlaybackSequence();

            sequence.QueueIntro(10f);

            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.WaitingForIntro));
            Assert.That(sequence.Advance(100f, isPaused: true), Is.False);
            Assert.That(sequence.Advance(100f, isPaused: false), Is.False);
            Assert.That(sequence.IntroRemainingSeconds, Is.EqualTo(10f));

            Assert.That(sequence.MarkIntroStarted(), Is.True);
            Assert.That(sequence.Advance(9.9f, isPaused: false), Is.False);
            Assert.That(sequence.Advance(0.11f, isPaused: false), Is.True);
            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.Loop));
        }

        [Test]
        public void Intro_lead_window_cannot_start_loop_before_playback_is_acknowledged()
        {
            var sequence = new MusicPlaybackSequence();

            sequence.QueueIntro(0.05f);

            Assert.That(sequence.Advance(1f, isPaused: false), Is.False);
            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.WaitingForIntro));

            sequence.MarkIntroStarted();

            Assert.That(sequence.Advance(0.05f, isPaused: false, actualIntroComplete: false), Is.False);
            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.Intro));
        }

        [Test]
        public void Intro_start_acknowledgement_is_idempotent()
        {
            var sequence = new MusicPlaybackSequence();

            sequence.QueueIntro(10f);

            Assert.That(sequence.MarkIntroStarted(), Is.True);
            Assert.That(sequence.MarkIntroStarted(), Is.False);
            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.Intro));
        }

        [Test]
        public void Queuing_a_zero_length_intro_enters_the_loop_immediately()
        {
            var sequence = new MusicPlaybackSequence();

            sequence.QueueIntro(0f);

            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.Loop));
            Assert.That(sequence.MarkIntroStarted(), Is.False);
            Assert.That(sequence.Advance(10f, isPaused: false), Is.False);
        }

        [Test]
        public void Paused_intro_keeps_loop_pending_until_intro_finishes()
        {
            var sequence = new MusicPlaybackSequence();

            sequence.StartIntro(10f);

            Assert.That(sequence.Advance(3f, isPaused: false), Is.False);
            Assert.That(sequence.Advance(10f, isPaused: true), Is.False);
            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.Intro));

            Assert.That(sequence.Advance(7f, isPaused: false), Is.True);
            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.Loop));
        }

        [Test]
        public void Completed_intro_does_not_request_loop_again_after_resume()
        {
            var sequence = new MusicPlaybackSequence();

            sequence.StartIntro(1f);

            Assert.That(sequence.Advance(1f, isPaused: false), Is.True);
            Assert.That(sequence.Advance(1f, isPaused: true), Is.False);
            Assert.That(sequence.Advance(1f, isPaused: false), Is.False);
            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.Loop));
        }

        [Test]
        public void Loop_waits_for_actual_intro_completion_after_duration_elapses()
        {
            var sequence = new MusicPlaybackSequence();

            sequence.StartIntro(1f);

            Assert.That(sequence.Advance(1f, isPaused: false, actualIntroComplete: false), Is.False);
            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.Intro));
            Assert.That(sequence.Advance(0f, isPaused: false, actualIntroComplete: true), Is.True);
            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.Loop));
        }

        [Test]
        public void Stopping_intro_cancels_pending_loop()
        {
            var sequence = new MusicPlaybackSequence();

            sequence.StartIntro(10f);
            sequence.Advance(3f, isPaused: false);
            sequence.Stop();

            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.Silent));
            Assert.That(sequence.Advance(20f, isPaused: false), Is.False);
        }

        [Test]
        public void Any_active_audio_pause_freezes_sequence_but_resume_wait_does_not()
        {
            var pauseCoordinator = new PauseCoordinator();
            var sequence = new MusicPlaybackSequence();

            sequence.StartIntro(10f);
            sequence.Advance(2f, pauseCoordinator.IsAudioPaused);

            pauseCoordinator.SetSource(PauseSource.Focus, true);
            pauseCoordinator.SetSource(PauseSource.Advertisement, true);
            Assert.That(sequence.Advance(20f, pauseCoordinator.IsAudioPaused), Is.False);

            pauseCoordinator.SetSource(PauseSource.Focus, false);
            Assert.That(pauseCoordinator.IsAudioPaused, Is.True);
            Assert.That(sequence.Advance(20f, pauseCoordinator.IsAudioPaused), Is.False);

            pauseCoordinator.SetSource(PauseSource.Advertisement, false);
            Assert.That(pauseCoordinator.IsAudioPaused, Is.False);
            Assert.That(pauseCoordinator.IsWaitingForResume, Is.True);
            Assert.That(sequence.Advance(8f, pauseCoordinator.IsAudioPaused), Is.True);
            Assert.That(sequence.Phase, Is.EqualTo(MusicPlaybackPhase.Loop));
        }
    }
}
