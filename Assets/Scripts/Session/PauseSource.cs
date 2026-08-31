using System;

namespace RobotArena.Session
{
    [Flags]
    public enum PauseSource
    {
        None = 0,
        User = 1,
        Focus = 2,
        Platform = 4,
        Advertisement = 8,
        Result = 16
    }
}
