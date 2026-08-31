using System;
using UnityEngine;

namespace RobotArena.Session
{
    public sealed class PlayerPrefsPlayerSettingsStore : IPlayerSettingsStore
    {
        public const int LegacySchemaVersion = 1;
        public const int CurrentSchemaVersion = 2;
        public const string DefaultKeyPrefix = "RobotArena.Settings";

        private readonly GameLanguage defaultLanguage;
        private readonly string[] currentKeys;

        public PlayerPrefsPlayerSettingsStore()
            : this(DefaultKeyPrefix, GameLanguageResolver.FromSystemLanguage(Application.systemLanguage))
        {
        }

        public PlayerPrefsPlayerSettingsStore(string keyPrefix)
            : this(keyPrefix, GameLanguageResolver.FromSystemLanguage(Application.systemLanguage))
        {
        }

        public PlayerPrefsPlayerSettingsStore(string keyPrefix, GameLanguage defaultLanguage)
        {
            if (string.IsNullOrEmpty(keyPrefix))
            {
                throw new ArgumentException("A PlayerPrefs key prefix is required.", nameof(keyPrefix));
            }

            this.defaultLanguage = GameLanguageResolver.Normalize(defaultLanguage);
            SchemaVersionKey = keyPrefix + ".SchemaVersion";
            LanguageKey = keyPrefix + ".Language";
            MasterVolumeKey = keyPrefix + ".MasterVolume";
            MusicVolumeKey = keyPrefix + ".MusicVolume";
            SfxVolumeKey = keyPrefix + ".SfxVolume";
            MutedKey = keyPrefix + ".Muted";
            GraphicsProfileKey = keyPrefix + ".GraphicsProfile";
            currentKeys = new[]
            {
                SchemaVersionKey,
                LanguageKey,
                MasterVolumeKey,
                MusicVolumeKey,
                SfxVolumeKey,
                MutedKey,
                GraphicsProfileKey
            };
        }

        public string SchemaVersionKey { get; }
        public string LanguageKey { get; }
        public string MasterVolumeKey { get; }
        public string MusicVolumeKey { get; }
        public string SfxVolumeKey { get; }
        public string MutedKey { get; }
        public string GraphicsProfileKey { get; }

        public PlayerSettings Load()
        {
            PlayerSettings defaults = PlayerSettings.CreateDefaults(defaultLanguage);
            int schemaVersion = PlayerPrefs.HasKey(SchemaVersionKey)
                ? PlayerPrefs.GetInt(SchemaVersionKey)
                : 0;

            if (schemaVersion > CurrentSchemaVersion)
            {
                return defaults;
            }

            if (!HasStoredValue())
            {
                return defaults;
            }

            PlayerSettings settings = schemaVersion <= LegacySchemaVersion
                ? ReadLegacySettings(defaults)
                : ReadCurrentSettings(defaults);

            if (schemaVersion != CurrentSchemaVersion || !HasAllCurrentValues())
            {
                Save(settings);
            }

            return settings;
        }

        public void Save(PlayerSettings settings)
        {
            PlayerPrefs.SetInt(SchemaVersionKey, CurrentSchemaVersion);
            PlayerPrefs.SetInt(LanguageKey, (int)settings.Language);
            PlayerPrefs.SetFloat(MasterVolumeKey, settings.MasterVolume);
            PlayerPrefs.SetFloat(MusicVolumeKey, settings.MusicVolume);
            PlayerPrefs.SetFloat(SfxVolumeKey, settings.SfxVolume);
            PlayerPrefs.SetInt(MutedKey, settings.IsMuted ? 1 : 0);
            PlayerPrefs.SetInt(GraphicsProfileKey, (int)settings.GraphicsProfile);
            PlayerPrefs.Save();
        }

        private GameLanguage ReadLanguage(GameLanguage fallback)
        {
            if (!PlayerPrefs.HasKey(LanguageKey))
            {
                return fallback;
            }

            int value = PlayerPrefs.GetInt(LanguageKey);
            return Enum.IsDefined(typeof(GameLanguage), value)
                ? (GameLanguage)value
                : fallback;
        }

        private GraphicsQualityProfile ReadGraphicsProfile(GraphicsQualityProfile fallback)
        {
            if (!PlayerPrefs.HasKey(GraphicsProfileKey))
            {
                return fallback;
            }

            int value = PlayerPrefs.GetInt(GraphicsProfileKey);
            return Enum.IsDefined(typeof(GraphicsQualityProfile), value)
                ? (GraphicsQualityProfile)value
                : fallback;
        }

        private float ReadFloat(string key, float fallback)
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetFloat(key) : fallback;
        }

        private bool ReadBool(string key, bool fallback)
        {
            return PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) != 0 : fallback;
        }

        private PlayerSettings ReadLegacySettings(PlayerSettings defaults)
        {
            return new PlayerSettings(
                ReadLanguage(defaults.Language),
                ReadFloat(MasterVolumeKey, defaults.MasterVolume),
                ReadFloat(MusicVolumeKey, defaults.MusicVolume),
                ReadFloat(SfxVolumeKey, defaults.SfxVolume),
                ReadBool(MutedKey, defaults.IsMuted),
                defaults.GraphicsProfile);
        }

        private PlayerSettings ReadCurrentSettings(PlayerSettings defaults)
        {
            return new PlayerSettings(
                ReadLanguage(defaults.Language),
                ReadFloat(MasterVolumeKey, defaults.MasterVolume),
                ReadFloat(MusicVolumeKey, defaults.MusicVolume),
                ReadFloat(SfxVolumeKey, defaults.SfxVolume),
                ReadBool(MutedKey, defaults.IsMuted),
                ReadGraphicsProfile(defaults.GraphicsProfile));
        }

        private bool HasStoredValue()
        {
            return HasAnyKey(currentKeys);
        }

        private bool HasAllCurrentValues()
        {
            foreach (string key in currentKeys)
            {
                if (!PlayerPrefs.HasKey(key))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasAnyKey(string[] keys)
        {
            foreach (string key in keys)
            {
                if (PlayerPrefs.HasKey(key))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
