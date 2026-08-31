using NUnit.Framework;
using RobotArena.Session;

namespace RobotArena.Session.Tests
{
    public class MusicStateMachineTests
    {
        [Test]
        public void Starting_calm_requests_calm_intro()
        {
            var stateMachine = new MusicStateMachine();
            MusicCue observedCue = MusicCue.None;
            stateMachine.CueRequested += cue => observedCue = cue;

            stateMachine.StartCalm();

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Calm));
            Assert.That(observedCue, Is.EqualTo(MusicCue.CalmIntro));
        }

        [Test]
        public void Combat_starts_immediately_when_presence_appears()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.StartCalm();
            MusicCue observedCue = MusicCue.None;
            stateMachine.CueRequested += cue => observedCue = cue;

            stateMachine.SetCombatPresence(true);

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Combat));
            Assert.That(observedCue, Is.EqualTo(MusicCue.CombatIntro));
        }

        [Test]
        public void Calm_waits_for_two_seconds_of_stable_quiet()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.StartCalm();
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
        public void New_combat_cancels_pending_calm_return()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.StartCalm();
            stateMachine.SetCombatPresence(true);
            stateMachine.SetCombatPresence(false);
            stateMachine.Advance(1.5f, false);

            stateMachine.SetCombatPresence(true);
            stateMachine.Advance(2f, false);

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Combat));
        }

        [Test]
        public void Death_requests_one_death_cue_and_becomes_terminal()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.StartCalm();
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
        public void Restarting_calm_replays_intro_after_a_terminal_result()
        {
            var stateMachine = new MusicStateMachine();
            stateMachine.CompleteVictory();
            MusicCue observedCue = MusicCue.None;
            stateMachine.CueRequested += cue => observedCue = cue;

            stateMachine.StartCalm();

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Calm));
            Assert.That(observedCue, Is.EqualTo(MusicCue.CalmIntro));
        }

        [Test]
        public void Victory_requests_silence_and_blocks_later_combat()
        {
            var stateMachine = new MusicStateMachine();
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
            stateMachine.SetCombatPresence(true);
            stateMachine.SetCombatPresence(false);

            stateMachine.Advance(5f, true);

            Assert.That(stateMachine.Mode, Is.EqualTo(MusicMode.Combat));
        }
    }
}
