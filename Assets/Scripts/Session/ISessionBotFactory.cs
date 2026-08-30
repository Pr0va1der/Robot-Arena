namespace RobotArena.Session
{
    public interface ISessionBotFactory
    {
        bool TryCreateBot(WaveSchedule wave, out BotId bot);
    }
}
