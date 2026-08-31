using NUnit.Framework;
using RobotArena.Session;

namespace RobotArena.Session.Tests
{
    public class BotCombatTrackerTests
    {
        [Test]
        public void Combat_presence_is_raised_once_for_the_first_bot()
        {
            var tracker = new BotCombatTracker();
            int changes = 0;
            tracker.CombatPresenceChanged += isPresent => changes++;

            Assert.That(tracker.EnterCombat(new BotId(1)), Is.True);
            Assert.That(tracker.EnterCombat(new BotId(1)), Is.False);

            Assert.That(tracker.AnyBotInCombat, Is.True);
            Assert.That(tracker.ActiveBotCount, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void Removing_one_bot_keeps_combat_presence_for_the_other_bot()
        {
            var tracker = new BotCombatTracker();
            int changes = 0;
            tracker.CombatPresenceChanged += isPresent => changes++;

            tracker.EnterCombat(new BotId(1));
            tracker.EnterCombat(new BotId(2));
            tracker.ExitCombat(new BotId(1));

            Assert.That(tracker.AnyBotInCombat, Is.True);
            Assert.That(tracker.ActiveBotCount, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));

            tracker.ExitCombat(new BotId(2));

            Assert.That(tracker.AnyBotInCombat, Is.False);
            Assert.That(tracker.ActiveBotCount, Is.Zero);
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public void Exiting_an_unknown_bot_is_idempotent()
        {
            var tracker = new BotCombatTracker();

            Assert.That(tracker.ExitCombat(new BotId(99)), Is.False);
            Assert.That(tracker.AnyBotInCombat, Is.False);
        }
    }
}
