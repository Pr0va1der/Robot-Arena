using System;
using UnityEngine;

namespace RobotArena.Session
{
    public sealed class PlayerPrefsSessionBestTimeStore : ISessionBestTimeStore
    {
        public const int CurrentSchemaVersion = 1;
        public const string DefaultKeyPrefix = "RobotArena.SessionBestTime";

        public PlayerPrefsSessionBestTimeStore()
            : this(DefaultKeyPrefix)
        {
        }

        public PlayerPrefsSessionBestTimeStore(string keyPrefix)
        {
            if (string.IsNullOrEmpty(keyPrefix))
            {
                throw new ArgumentException("A PlayerPrefs key prefix is required.", nameof(keyPrefix));
            }

            SchemaVersionKey = keyPrefix + ".SchemaVersion";
            BestTimeKey = keyPrefix + ".Seconds";
        }

        public string SchemaVersionKey { get; }
        public string BestTimeKey { get; }

        public bool TryLoadBestTime(out float bestTime)
        {
            bestTime = default;
            if (!PlayerPrefs.HasKey(SchemaVersionKey) ||
                PlayerPrefs.GetInt(SchemaVersionKey) != CurrentSchemaVersion ||
                !PlayerPrefs.HasKey(BestTimeKey))
            {
                return false;
            }

            float storedBestTime = PlayerPrefs.GetFloat(BestTimeKey);
            if (!SessionResult.IsValidActiveTime(storedBestTime))
            {
                return false;
            }

            bestTime = storedBestTime;
            return true;
        }

        public void SaveBestTime(float bestTime)
        {
            if (!SessionResult.IsValidActiveTime(bestTime))
            {
                throw new ArgumentOutOfRangeException(nameof(bestTime));
            }

            PlayerPrefs.SetInt(SchemaVersionKey, CurrentSchemaVersion);
            PlayerPrefs.SetFloat(BestTimeKey, bestTime);
            PlayerPrefs.Save();
        }

    }
}
