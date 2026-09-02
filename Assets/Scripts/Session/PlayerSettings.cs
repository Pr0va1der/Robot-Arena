using System;

namespace RobotArena.Session
{
    public readonly struct PlayerSettings : IEquatable<PlayerSettings>
    {
        public PlayerSettings(
            GameLanguage language,
            float masterVolume,
            float musicVolume,
            float sfxVolume,
            bool isMuted,
            GraphicsQualityProfile graphicsProfile)
            : this(
                language,
                masterVolume,
                musicVolume,
                sfxVolume,
                isMuted,
                graphicsProfile,
                false)
        {
        }

        public PlayerSettings(
            GameLanguage language,
            float masterVolume,
            float musicVolume,
            float sfxVolume,
            bool isMuted,
            GraphicsQualityProfile graphicsProfile,
            bool hasCompletedTutorial)
        {
            Language = GameLanguageResolver.Normalize(language);
            MasterVolume = NormalizeVolume(masterVolume);
            MusicVolume = NormalizeVolume(musicVolume);
            SfxVolume = NormalizeVolume(sfxVolume);
            IsMuted = isMuted;
            GraphicsProfile = GraphicsQualityProfileCatalog.Normalize(graphicsProfile);
            HasCompletedTutorial = hasCompletedTutorial;
        }

        public GameLanguage Language { get; }
        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float SfxVolume { get; }
        public bool IsMuted { get; }
        public GraphicsQualityProfile GraphicsProfile { get; }
        public bool HasCompletedTutorial { get; }

        public static PlayerSettings CreateDefaults(GameLanguage language)
        {
            return new PlayerSettings(
                language,
                1f,
                1f,
                1f,
                false,
                GraphicsQualityProfile.Performance);
        }

        public static PlayerSettings Default => CreateDefaults(GameLanguage.English);

        public PlayerSettings WithLanguage(GameLanguage language)
        {
            return new PlayerSettings(
                language,
                MasterVolume,
                MusicVolume,
                SfxVolume,
                IsMuted,
                GraphicsProfile,
                HasCompletedTutorial);
        }

        public PlayerSettings WithMasterVolume(float volume)
        {
            return new PlayerSettings(
                Language,
                volume,
                MusicVolume,
                SfxVolume,
                IsMuted,
                GraphicsProfile,
                HasCompletedTutorial);
        }

        public PlayerSettings WithMusicVolume(float volume)
        {
            return new PlayerSettings(
                Language,
                MasterVolume,
                volume,
                SfxVolume,
                IsMuted,
                GraphicsProfile,
                HasCompletedTutorial);
        }

        public PlayerSettings WithSfxVolume(float volume)
        {
            return new PlayerSettings(
                Language,
                MasterVolume,
                MusicVolume,
                volume,
                IsMuted,
                GraphicsProfile,
                HasCompletedTutorial);
        }

        public PlayerSettings WithMuted(bool isMuted)
        {
            return new PlayerSettings(
                Language,
                MasterVolume,
                MusicVolume,
                SfxVolume,
                isMuted,
                GraphicsProfile,
                HasCompletedTutorial);
        }

        public PlayerSettings WithGraphicsProfile(GraphicsQualityProfile graphicsProfile)
        {
            return new PlayerSettings(
                Language,
                MasterVolume,
                MusicVolume,
                SfxVolume,
                IsMuted,
                graphicsProfile,
                HasCompletedTutorial);
        }

        public PlayerSettings WithTutorialCompleted()
        {
            return new PlayerSettings(
                Language,
                MasterVolume,
                MusicVolume,
                SfxVolume,
                IsMuted,
                GraphicsProfile,
                true);
        }

        public bool Equals(PlayerSettings other)
        {
            return Language == other.Language &&
                   MasterVolume.Equals(other.MasterVolume) &&
                   MusicVolume.Equals(other.MusicVolume) &&
                   SfxVolume.Equals(other.SfxVolume) &&
                   IsMuted == other.IsMuted &&
                   GraphicsProfile == other.GraphicsProfile &&
                   HasCompletedTutorial == other.HasCompletedTutorial;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Language;
                hash = (hash * 397) ^ MasterVolume.GetHashCode();
                hash = (hash * 397) ^ MusicVolume.GetHashCode();
                hash = (hash * 397) ^ SfxVolume.GetHashCode();
                hash = (hash * 397) ^ IsMuted.GetHashCode();
                hash = (hash * 397) ^ (int)GraphicsProfile;
                hash = (hash * 397) ^ HasCompletedTutorial.GetHashCode();
                return hash;
            }
        }

        private static float NormalizeVolume(float volume)
        {
            if (float.IsNaN(volume) || float.IsInfinity(volume))
            {
                return 1f;
            }

            return Math.Max(0f, Math.Min(1f, volume));
        }
    }
}
