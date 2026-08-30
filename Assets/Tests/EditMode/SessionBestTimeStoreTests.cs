using NUnit.Framework;
using UnityEngine;

namespace RobotArena.Session.Tests
{
    public class SessionBestTimeStoreTests
    {
        private const string TestKeyPrefix = "RobotArena.Tests.SessionBestTime";

        private PlayerPrefsSessionBestTimeStore store;

        [SetUp]
        public void SetUp()
        {
            store = new PlayerPrefsSessionBestTimeStore(TestKeyPrefix);
            ClearStoredBestTime();
        }

        [TearDown]
        public void TearDown()
        {
            ClearStoredBestTime();
        }

        [Test]
        public void Missing_schema_uses_safe_no_best_time_default()
        {
            Assert.That(store.TryLoadBestTime(out float bestTime), Is.False);
            Assert.That(bestTime, Is.EqualTo(0f));
        }

        [Test]
        public void Outdated_schema_uses_safe_no_best_time_default()
        {
            PlayerPrefs.SetInt(store.SchemaVersionKey, PlayerPrefsSessionBestTimeStore.CurrentSchemaVersion - 1);
            PlayerPrefs.SetFloat(store.BestTimeKey, 12f);
            PlayerPrefs.Save();

            Assert.That(store.TryLoadBestTime(out float bestTime), Is.False);
            Assert.That(bestTime, Is.EqualTo(0f));
        }

        [Test]
        public void Saved_best_time_survives_a_new_store_instance()
        {
            store.SaveBestTime(12f);
            var reloadedStore = new PlayerPrefsSessionBestTimeStore(TestKeyPrefix);

            Assert.That(reloadedStore.TryLoadBestTime(out float bestTime), Is.True);
            Assert.That(bestTime, Is.EqualTo(12f));
        }

        private void ClearStoredBestTime()
        {
            PlayerPrefs.DeleteKey(store.SchemaVersionKey);
            PlayerPrefs.DeleteKey(store.BestTimeKey);
            PlayerPrefs.Save();
        }
    }
}
