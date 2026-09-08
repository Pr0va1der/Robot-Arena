using NUnit.Framework;
using RobotArena.Session;

namespace RobotArena.Session.Tests
{
    public class MusicStateMachineTests
    {
        [Test]
        public void Starting_calm_waits_for_audio_permission()
        {
            var stateMachine = new MusicStateMachine();
            MusicCue observedCue = MusicCue.None;
            stateMachine.CueRequested += cue => observedCue = cue;

            stateMachine.StartCalm();

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Silent));
            Assert.That(observedCue, Is.EqualTo(MusicCue.None));

            stateMachine.RegisterAudioGesture();

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Calm));
            Assert.That(observedCue, Is.EqualTo(MusicCue.CalmIntro));
        }

        [Test]
        public void Combat_presence_before_audio_permission_emits_no_cue()
        {
            var stateMachine = new MusicStateMachine();
            int cueCount = 0;
            stateMachine.CueRequested += cue => cueCount++;

            stateMachine.StartCalm();
            stateMachine.SetCombatPresence(true);

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Silent));
            Assert.That(stateMachine.CombatPresence, Is.True);
            Assert.That(cueCount, Is.Zero);
        }

        [Test]
        public void Repeated_audio_gestures_are_idempotent()
        {
            var stateMachine = new MusicStateMachine();
            int calmIntroCount = 0;
            stateMachine.CueRequested += cue =>
            {
                if (cue == MusicCue.CalmIntro)
                {
                    calmIntroCount++;
                }
            };

            stateMachine.StartCalm();
            stateMachine.RegisterAudioGesture();
            stateMachine.RegisterAudioGesture();

            Assert.That(stateMachine.AudioPermissionGranted, Is.True);
            Assert.That(calmIntroCount, Is.EqualTo(1));
        }

        [Test]
        public void Repeated_calm_start_does_not_restart_active_music()
        {
            var stateMachine = new MusicStateMachine();
            int calmIntroCount = 0;
            stateMachine.CueRequested += cue =>
            {
                if (cue == MusicCue.CalmIntro)
                {
                    calmIntroCount++;
                }
            };

            stateMachine.RegisterAudioGesture();
            stateMachine.StartCalm();
            stateMachine.CompleteCalmIntro();
            stateMachine.StartCalm();

            Assert.That(calmIntroCount, Is.EqualTo(1));
            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Calm));
        }

        [Test]
        public void Repeated_calm_start_does_not_restart_intro_during_combat()
        {
            var stateMachine = new MusicStateMachine();
            int calmIntroCount = 0;
            stateMachine.CueRequested += cue =>
            {
                if (cue == MusicCue.CalmIntro)
                {
                    calmIntroCount++;
                }
            };

            stateMachine.RegisterAudioGesture();
            stateMachine.SetCombatPresence(true);
            stateMachine.StartCalm();

            Assert.That(calmIntroCount, Is.EqualTo(1));
            Assert.That(stateMachine.IsCalmIntroPlaying, Is.True);
        }

        [Test]
        public void Combat_presence_waits_for_the_first_calm_intro_to_complete()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.StartCalm();
            stateMachine.SetCombatPresence(true);
            MusicCue observedCue = MusicCue.None;
            stateMachine.CueRequested += cue => observedCue = cue;

            stateMachine.RegisterAudioGesture();

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Calm));
            Assert.That(observedCue, Is.EqualTo(MusicCue.CalmIntro));

            stateMachine.CompleteCalmIntro();

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Combat));
            Assert.That(observedCue, Is.EqualTo(MusicCue.CombatIntro));
        }

        [Test]
        public void Calm_waits_for_two_seconds_of_stable_quiet()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.StartCalm();
            stateMachine.RegisterAudioGesture();
            stateMachine.CompleteCalmIntro();
            stateMachine.SetCombatPresence(true);
            stateMachine.SetCombatPresence(false);
            int calmIntroCount = 0;
            stateMachine.CueRequested += cue =>
            {
                if (cue == MusicCue.CalmIntro)
                {
                    calmIntroCount++;
                }
            };

            stateMachine.Advance(1.99f, false);
            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Combat));

            stateMachine.Advance(0.01f, false);

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Calm));
            Assert.That(calmIntroCount, Is.EqualTo(1));
        }

        [Test]
        public void Combat_clearing_during_calm_intro_leaves_calm_mode_after_completion()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.StartCalm();
            stateMachine.SetCombatPresence(true);
            stateMachine.RegisterAudioGesture();

            stateMachine.SetCombatPresence(false);
            stateMachine.CompleteCalmIntro();

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Calm));
            Assert.That(stateMachine.CombatPresence, Is.False);
        }

        [Test]
        public void New_combat_cancels_pending_calm_return()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.StartCalm();
            stateMachine.RegisterAudioGesture();
            stateMachine.CompleteCalmIntro();
            stateMachine.SetCombatPresence(true);
            stateMachine.SetCombatPresence(false);
            stateMachine.Advance(1.5f, false);

            stateMachine.SetCombatPresence(true);
            stateMachine.Advance(2f, false);

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Combat));
        }

        [Test]
        public void Combat_reentry_during_calm_debounce_replays_combat_intro()
        {
            var stateMachine = new MusicStateMachine();
            int combatIntroCount = 0;
            stateMachine.CueRequested += cue =>
            {
                if (cue == MusicCue.CombatIntro)
                {
                    combatIntroCount++;
                }
            };

            stateMachine.StartCalm();
            stateMachine.RegisterAudioGesture();
            stateMachine.CompleteCalmIntro();
            stateMachine.SetCombatPresence(true);
            stateMachine.SetCombatPresence(false);
            stateMachine.SetCombatPresence(true);

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Combat));
            Assert.That(combatIntroCount, Is.EqualTo(2));
        }

        [Test]
        public void Death_requests_one_death_cue_and_becomes_terminal()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.StartCalm();
            stateMachine.RegisterAudioGesture();
            MusicCue observedCue = MusicCue.None;
            int cueCount = 0;
            stateMachine.CueRequested += cue =>
            {
                observedCue = cue;
                cueCount++;
            };

            stateMachine.CompleteDeath();
            stateMachine.CompleteDeath();
            stateMachine.SetCombatPresence(true);

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Death));
            Assert.That(observedCue, Is.EqualTo(MusicCue.Death));
            Assert.That(cueCount, Is.EqualTo(1));
        }

        [Test]
        public void Death_becomes_silent_when_playback_completes()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.RegisterAudioGesture();
            MusicCue observedCue = MusicCue.None;
            stateMachine.CueRequested += cue => observedCue = cue;

            stateMachine.CompleteDeath();
            stateMachine.CompleteDeathPlayback();
            stateMachine.SetCombatPresence(true);

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Silent));
            Assert.That(observedCue, Is.EqualTo(MusicCue.Silent));
        }

        [Test]
        public void Restarting_calm_replays_intro_after_a_terminal_result()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.CompleteVictory();
            MusicCue observedCue = MusicCue.None;
            stateMachine.CueRequested += cue => observedCue = cue;

            stateMachine.StartCalm();

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Silent));
            Assert.That(observedCue, Is.EqualTo(MusicCue.None));

            stateMachine.RegisterAudioGesture();

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Calm));
            Assert.That(observedCue, Is.EqualTo(MusicCue.CalmIntro));
        }

        [Test]
        public void Victory_requests_silence_and_blocks_later_combat()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.RegisterAudioGesture();
            MusicCue observedCue = MusicCue.None;
            stateMachine.CueRequested += cue => observedCue = cue;

            stateMachine.CompleteVictory();
            stateMachine.SetCombatPresence(true);

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Silent));
            Assert.That(observedCue, Is.EqualTo(MusicCue.Silent));
        }

        [Test]
        public void Paused_time_does_not_advance_the_quiet_debounce()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.StartCalm();
            stateMachine.RegisterAudioGesture();
            stateMachine.CompleteCalmIntro();
            stateMachine.SetCombatPresence(true);
            stateMachine.SetCombatPresence(false);

            stateMachine.Advance(5f, true);

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Combat));
        }

        [Test]
        public void Paused_time_does_not_complete_the_calm_intro()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.StartCalm();
            stateMachine.RegisterAudioGesture();

            stateMachine.Advance(5f, true);

            Assert.That(stateMachine.IsCalmIntroPlaying, Is.True);
            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Calm));
        }

        [Test]
        public void Terminal_outcome_interrupts_the_calm_intro_immediately()
        {
            var stateMachine = new MusicStateMachine();
            MusicCue observedCue = MusicCue.None;
            stateMachine.CueRequested += cue => observedCue = cue;

            stateMachine.StartCalm();
            stateMachine.RegisterAudioGesture();
            stateMachine.CompleteVictory();

            Assert.That(stateMachine.IsCalmIntroPlaying, Is.False);
            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Silent));
            Assert.That(observedCue, Is.EqualTo(MusicCue.Silent));
        }
    }
}
