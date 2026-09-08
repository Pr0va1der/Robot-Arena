using NUnit.Framework;
using RobotArena.Session;

namespace RobotArena.Session.Tests
{
    public class JumpInputGateTests
    {
        [Test]
        public void Held_jump_is_consumed_once_until_released()
        {
            var gate = new JumpInputGate();

            Assert.That(gate.Consume(true, false), Is.True);
            Assert.That(gate.Consume(true, false), Is.False);
            Assert.That(gate.Consume(false, false), Is.False);
            Assert.That(gate.Consume(true, false), Is.True);
        }

        [Test]
        public void Paused_jump_does_not_replay_after_resume()
        {
            var gate = new JumpInputGate();

            Assert.That(gate.Consume(true, true), Is.False);
            Assert.That(gate.Consume(true, false), Is.False);
            Assert.That(gate.Consume(false, false), Is.False);
            Assert.That(gate.Consume(true, false), Is.True);
        }

        [Test]
        public void Sampling_during_pause_discards_pending_press_until_a_fresh_press()
        {
            var gate = new JumpInputGate();

            gate.Sample(false, false);
            gate.Sample(true, false);
            Assert.That(gate.Consume(), Is.True);

            gate.Sample(false, false);
            gate.Sample(true, true);
            Assert.That(gate.Consume(), Is.False);
            gate.Sample(true, false);
            Assert.That(gate.Consume(), Is.False);

            gate.Sample(false, false);
            gate.Sample(true, false);
            Assert.That(gate.Consume(), Is.True);
        }
    }
}
