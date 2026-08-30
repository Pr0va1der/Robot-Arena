namespace RobotArena.Session
{
    public interface ISessionBotFactory
    {
        bool TryCreateBot(out BotId bot);
    }
}
