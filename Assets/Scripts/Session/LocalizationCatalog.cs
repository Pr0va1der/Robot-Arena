using System;
using System.Collections.Generic;

namespace RobotArena.Session
{
    public static class LocalizationCatalog
    {
        private static readonly Dictionary<LocalizationKey, string> Russian =
            new Dictionary<LocalizationKey, string>
            {
                { LocalizationKey.GameTitle, "ROBOT ARENA" },
                { LocalizationKey.Subtitle, "Desktop WebGL прототип" },
                { LocalizationKey.StartSession, "Начать сессию" },
                { LocalizationKey.Controls, "Управление" },
                { LocalizationKey.Settings, "Настройки" },
                { LocalizationKey.Quit, "Выйти" },
                { LocalizationKey.Back, "Назад" },
                { LocalizationKey.MenuHint, "Enter / Space — выбрать    Esc — назад" },
                {
                    LocalizationKey.ControlsDetails,
                    "WASD / стрелки — движение\n" +
                    "Мышь — обзор и огонь\n" +
                    "Space — прыжок\n" +
                    "Left Shift — торможение\n" +
                    "Q — импульс отдачи\n" +
                    "Esc — пауза"
                },
                { LocalizationKey.SettingsTitle, "Настройки" },
                { LocalizationKey.Language, "Язык" },
                { LocalizationKey.Russian, "Русский" },
                { LocalizationKey.English, "English" },
                { LocalizationKey.MasterVolume, "Общая громкость" },
                { LocalizationKey.MusicVolume, "Музыка" },
                { LocalizationKey.SfxVolume, "Эффекты" },
                { LocalizationKey.Mute, "Без звука: {0}" },
                { LocalizationKey.On, "да" },
                { LocalizationKey.Off, "нет" },
                { LocalizationKey.GraphicsProfile, "Профиль графики" },
                { LocalizationKey.Performance, "Производительность" },
                { LocalizationKey.Quality, "Качество" },
                { LocalizationKey.TutorialTitle, "Управление" },
                {
                    LocalizationKey.TutorialDetails,
                    "WASD / стрелки — движение\n" +
                    "Мышь — обзор и огонь\n" +
                    "Space — прыжок\n" +
                    "Left Shift — торможение\n" +
                    "Q — импульс отдачи\n" +
                    "Esc — пауза\n\n" +
                    "Уничтожьте всех ботов в каждой волне."
                },
                { LocalizationKey.ClickToContinue, "КЛИКНИТЕ ПО АРЕНЕ, ЧТОБЫ ПРОДОЛЖИТЬ" },
                { LocalizationKey.Pause, "Пауза" },
                { LocalizationKey.GameStopped, "Сессия остановлена" },
                { LocalizationKey.Resume, "Продолжить" },
                { LocalizationKey.MainMenu, "В главное меню" },
                { LocalizationKey.SessionCompleted, "Сессия завершена" },
                { LocalizationKey.Victory, "Победа" },
                { LocalizationKey.Defeat, "Поражение" },
                { LocalizationKey.ReachedWave, "Достигнута волна: {0}" },
                { LocalizationKey.ActiveTime, "Активное время: {0:0.0} с" },
                { LocalizationKey.BestTime, "Лучшее время: {0}" },
                { LocalizationKey.Seconds, "с" },
                { LocalizationKey.Wave, "ВОЛНА {0}/{1}" },
                { LocalizationKey.State, "СОСТОЯНИЕ: {0}" },
                { LocalizationKey.Bots, "БОТЫ: {0}" },
                { LocalizationKey.Spawning, "ПОЯВЛЕНИЕ" },
                { LocalizationKey.Clearing, "ЗАЧИСТКА" },
                { LocalizationKey.Intermission, "ПЕРЕРЫВ" },
                { LocalizationKey.NextWave, "СЛЕДУЮЩАЯ ВОЛНА: {0:0}" },
                { LocalizationKey.ClearArena, "ЗАЧИСТИТЕ АРЕНУ" },
                { LocalizationKey.Health, "ЗДОРОВЬЕ {0:0}/{1:0}" },
                { LocalizationKey.ImpulseReady, "ИМПУЛЬС: ГОТОВ" },
                { LocalizationKey.ImpulseCooldown, "ИМПУЛЬС: {0:0.0} С" },
                { LocalizationKey.Spawn, "ПОЯВЛЕНИЕ: {0:0}" },
                {
                    LocalizationKey.ControlsHint,
                    "WASD / стрелки — движение    Мышь — обзор и огонь    Space — прыжок    Shift — торможение    Q — импульс    Esc — пауза"
                },
                { LocalizationKey.PointerPrompt, "КЛИКНИТЕ ПО АРЕНЕ, ЧТОБЫ ПРОДОЛЖИТЬ" },
                { LocalizationKey.Waiting, "ОЖИДАНИЕ" }
            };

        private static readonly Dictionary<LocalizationKey, string> English =
            new Dictionary<LocalizationKey, string>
            {
                { LocalizationKey.GameTitle, "ROBOT ARENA" },
                { LocalizationKey.Subtitle, "Desktop WebGL prototype" },
                { LocalizationKey.StartSession, "Start session" },
                { LocalizationKey.Controls, "Controls" },
                { LocalizationKey.Settings, "Settings" },
                { LocalizationKey.Quit, "Quit" },
                { LocalizationKey.Back, "Back" },
                { LocalizationKey.MenuHint, "Enter / Space — select    Esc — back" },
                {
                    LocalizationKey.ControlsDetails,
                    "WASD / arrow keys — move\n" +
                    "Mouse — look and fire\n" +
                    "Space — jump\n" +
                    "Left Shift — brake\n" +
                    "Q — knockback impulse\n" +
                    "Esc — pause"
                },
                { LocalizationKey.SettingsTitle, "Settings" },
                { LocalizationKey.Language, "Language" },
                { LocalizationKey.Russian, "Русский" },
                { LocalizationKey.English, "English" },
                { LocalizationKey.MasterVolume, "Master volume" },
                { LocalizationKey.MusicVolume, "Music volume" },
                { LocalizationKey.SfxVolume, "SFX volume" },
                { LocalizationKey.Mute, "Mute: {0}" },
                { LocalizationKey.On, "on" },
                { LocalizationKey.Off, "off" },
                { LocalizationKey.GraphicsProfile, "Graphics profile" },
                { LocalizationKey.Performance, "Performance" },
                { LocalizationKey.Quality, "Quality" },
                { LocalizationKey.TutorialTitle, "Controls" },
                {
                    LocalizationKey.TutorialDetails,
                    "WASD / arrow keys — move\n" +
                    "Mouse — look and fire\n" +
                    "Space — jump\n" +
                    "Left Shift — brake\n" +
                    "Q — knockback impulse\n" +
                    "Esc — pause\n\n" +
                    "Destroy every bot in each wave."
                },
                { LocalizationKey.ClickToContinue, "CLICK THE ARENA TO CONTINUE" },
                { LocalizationKey.Pause, "Paused" },
                { LocalizationKey.GameStopped, "Session is paused" },
                { LocalizationKey.Resume, "Continue" },
                { LocalizationKey.MainMenu, "Main menu" },
                { LocalizationKey.SessionCompleted, "Session complete" },
                { LocalizationKey.Victory, "Victory" },
                { LocalizationKey.Defeat, "Session over" },
                { LocalizationKey.ReachedWave, "Wave reached: {0}" },
                { LocalizationKey.ActiveTime, "Active time: {0:0.0} s" },
                { LocalizationKey.BestTime, "Best time: {0}" },
                { LocalizationKey.Seconds, "s" },
                { LocalizationKey.Wave, "WAVE {0}/{1}" },
                { LocalizationKey.State, "STATE: {0}" },
                { LocalizationKey.Bots, "BOTS: {0}" },
                { LocalizationKey.Spawning, "SPAWNING" },
                { LocalizationKey.Clearing, "CLEARING" },
                { LocalizationKey.Intermission, "INTERMISSION" },
                { LocalizationKey.NextWave, "NEXT WAVE: {0:0}" },
                { LocalizationKey.ClearArena, "CLEAR THE ARENA" },
                { LocalizationKey.Health, "HEALTH {0:0}/{1:0}" },
                { LocalizationKey.ImpulseReady, "IMPULSE: READY" },
                { LocalizationKey.ImpulseCooldown, "IMPULSE: {0:0.0} S" },
                { LocalizationKey.Spawn, "SPAWN: {0:0}" },
                {
                    LocalizationKey.ControlsHint,
                    "WASD / arrow keys — move    Mouse — look and fire    Space — jump    Shift — brake    Q — impulse    Esc — pause"
                },
                { LocalizationKey.PointerPrompt, "CLICK THE ARENA TO CONTINUE" },
                { LocalizationKey.Waiting, "WAITING" }
            };

        public static string Get(GameLanguage language, LocalizationKey key)
        {
            Dictionary<LocalizationKey, string> catalog =
                GameLanguageResolver.Normalize(language) == GameLanguage.Russian
                    ? Russian
                    : English;

            if (!catalog.TryGetValue(key, out string value))
            {
                throw new ArgumentOutOfRangeException(nameof(key), key, "No localized text is registered for this key.");
            }

            return value;
        }
    }
}
