using RobotArena.Session;

public static class DesktopLocalization
{
    public static string Get(GameSettingsRuntime settingsRuntime, LocalizationKey key)
    {
        GameLanguage language = settingsRuntime == null
            ? GameLanguage.English
            : settingsRuntime.Current.Language;
        return LocalizationCatalog.Get(language, key);
    }
}
