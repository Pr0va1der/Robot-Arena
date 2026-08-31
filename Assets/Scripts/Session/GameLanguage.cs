using UnityEngine;

namespace RobotArena.Session
{
    public enum GameLanguage
    {
        Russian = 0,
        English = 1
    }

    public static class GameLanguageResolver
    {
        public static GameLanguage FromSystemLanguage(SystemLanguage systemLanguage)
        {
            return systemLanguage == SystemLanguage.Russian
                ? GameLanguage.Russian
                : GameLanguage.English;
        }

        public static GameLanguage Normalize(GameLanguage language)
        {
            return language == GameLanguage.Russian || language == GameLanguage.English
                ? language
                : GameLanguage.English;
        }
    }
}
