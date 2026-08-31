namespace RobotArena.Session
{
    public interface IPlayerSettingsStore
    {
        PlayerSettings Load();

        void Save(PlayerSettings settings);
    }
}
