using System;

namespace RobotArena.Session
{
    public readonly struct BotId : IEquatable<BotId>
    {
        public BotId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(BotId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is BotId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }
    }
}
