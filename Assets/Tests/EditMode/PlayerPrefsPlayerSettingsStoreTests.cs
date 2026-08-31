using NUnit.Framework;
using RobotArena.Session;
using UnityEngine;

namespace RobotArena.Session.Tests
{
    public class PlayerPrefsPlayerSettingsStoreTests
    {
        private const string TestKeyPrefix = "RobotArena.Tests.PlayerSettings";

        private PlayerPrefsPlayerSettingsStore store;

        [SetUp]
        public void SetUp()
        {
            store = new PlayerPrefsPlayerSettingsStore(TestKeyPrefix, GameLanguage.English);
            ClearStoredSettings();
        }

        [TearDown]
        public void TearDown()
        {
            ClearStoredSettings();
        }

        [Test]
        public void Missing_settings_use_safe_defaults()
        {
            PlayerSettings settings = store.Load();

            Assert.That(settings, Is.EqualTo(PlayerSettings.CreateDefaults(GameLanguage.English)));
        }

        [Test]
        public void Saved_settings_survive_a_new_store_instance()
        {
            PlayerSettings expected = new PlayerSettings(
                GameLanguage.Russian,
                0.65f,
                0.25f,
                0.9f,
                true,
                GraphicsQualityProfile.Quality);

            store.Save(expected);
            var reloadedStore = new PlayerPrefsPlayerSettingsStore(TestKeyPrefix, GameLanguage.English);

            Assert.That(reloadedStore.Load(), Is.EqualTo(expected));
        }

        [Test]
        public void Legacy_schema_preserves_known_fields_and_fills_new_fields()
        {
            PlayerPrefs.SetInt(store.SchemaVersionKey, PlayerPrefsPlayerSettingsStore.LegacySchemaVersion);
            PlayerPrefs.SetInt(store.LanguageKey, (int)GameLanguage.Russian);
            PlayerPrefs.SetFloat(store.MasterVolumeKey, 0.4f);
            PlayerPrefs.SetFloat(store.MusicVolumeKey, 0.2f);
            PlayerPrefs.SetFloat(store.SfxVolumeKey, 0.8f);
            PlayerPrefs.SetInt(store.MutedKey, 1);
            PlayerPrefs.Save();

            PlayerSettings migrated = store.Load();

            Assert.That(migrated.Language, Is.EqualTo(GameLanguage.Russian));
            Assert.That(migrated.MasterVolume, Is.EqualTo(0.4f));
            Assert.That(migrated.MusicVolume, Is.EqualTo(0.2f));
            Assert.That(migrated.SfxVolume, Is.EqualTo(0.8f));
            Assert.That(migrated.IsMuted, Is.True);
            Assert.That(migrated.GraphicsProfile, Is.EqualTo(GraphicsQualityProfile.Performance));
            Assert.That(PlayerPrefs.GetInt(store.SchemaVersionKey), Is.EqualTo(PlayerPrefsPlayerSettingsStore.CurrentSchemaVersion));
            Assert.That(PlayerPrefs.HasKey(store.GraphicsProfileKey), Is.True);
        }

        [Test]
        public void Missing_fields_are_migrated_without_discarding_existing_values()
        {
            PlayerPrefs.SetInt(store.SchemaVersionKey, PlayerPrefsPlayerSettingsStore.CurrentSchemaVersion);
            PlayerPrefs.SetInt(store.LanguageKey, (int)GameLanguage.Russian);
            PlayerPrefs.SetFloat(store.MasterVolumeKey, 0.5f);
            PlayerPrefs.Save();

            PlayerSettings migrated = store.Load();

            Assert.That(migrated.Language, Is.EqualTo(GameLanguage.Russian));
            Assert.That(migrated.MasterVolume, Is.EqualTo(0.5f));
            Assert.That(migrated.MusicVolume, Is.EqualTo(1f));
            Assert.That(migrated.SfxVolume, Is.EqualTo(1f));
            Assert.That(migrated.IsMuted, Is.False);
            Assert.That(migrated.GraphicsProfile, Is.EqualTo(GraphicsQualityProfile.Performance));
            Assert.That(PlayerPrefs.HasKey(store.MusicVolumeKey), Is.True);
            Assert.That(PlayerPrefs.HasKey(store.GraphicsProfileKey), Is.True);
        }

        [Test]
        public void Invalid_values_are_normalized_to_safe_runtime_values()
        {
            PlayerSettings settings = new PlayerSettings(
                (GameLanguage)99,
                -1f,
                2f,
                float.NaN,
                false,
                (GraphicsQualityProfile)99);

            Assert.That(settings.Language, Is.EqualTo(GameLanguage.English));
            Assert.That(settings.MasterVolume, Is.EqualTo(0f));
            Assert.That(settings.MusicVolume, Is.EqualTo(1f));
            Assert.That(settings.SfxVolume, Is.EqualTo(1f));
            Assert.That(settings.GraphicsProfile, Is.EqualTo(GraphicsQualityProfile.Performance));
        }

        private void ClearStoredSettings()
        {
            PlayerPrefs.DeleteKey(store.SchemaVersionKey);
            PlayerPrefs.DeleteKey(store.LanguageKey);
            PlayerPrefs.DeleteKey(store.MasterVolumeKey);
            PlayerPrefs.DeleteKey(store.MusicVolumeKey);
            PlayerPrefs.DeleteKey(store.SfxVolumeKey);
            PlayerPrefs.DeleteKey(store.MutedKey);
            PlayerPrefs.DeleteKey(store.GraphicsProfileKey);
            PlayerPrefs.Save();
        }
    }
}
