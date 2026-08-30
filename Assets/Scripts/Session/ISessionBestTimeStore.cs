namespace RobotArena.Session
{
    public interface ISessionBestTimeStore
    {
        bool TryLoadBestTime(out float bestTime);
        void SaveBestTime(float bestTime);
    }
}
