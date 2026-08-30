namespace RobotArena.Session
{
    public interface ISessionBotRegistry
    {
        bool RegisterBot(SessionBotRegistration bot);
        bool UnregisterBot(SessionBotRegistration bot);
    }
}
