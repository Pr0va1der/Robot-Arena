using System;
using NUnit.Framework;
using RobotArena.Session;

namespace RobotArena.Session.Tests
{
    public class LocalizationCatalogTests
    {
        [Test]
        public void Every_required_text_has_russian_and_english_copy()
        {
            foreach (LocalizationKey key in Enum.GetValues(typeof(LocalizationKey)))
            {
                Assert.That(LocalizationCatalog.Get(GameLanguage.Russian, key), Is.Not.Empty, key.ToString());
                Assert.That(LocalizationCatalog.Get(GameLanguage.English, key), Is.Not.Empty, key.ToString());
            }
        }

        [Test]
        public void Unsupported_system_language_uses_english_fallback()
        {
            Assert.That(
                GameLanguageResolver.FromSystemLanguage(UnityEngine.SystemLanguage.German),
                Is.EqualTo(GameLanguage.English));
        }

        [Test]
        public void Quality_profiles_map_to_the_two_release_quality_bases()
        {
            Assert.That(
                GraphicsQualityProfileCatalog.GetQualityLevelName(GraphicsQualityProfile.Performance),
                Is.EqualTo("Low"));
            Assert.That(
                GraphicsQualityProfileCatalog.GetQualityLevelName(GraphicsQualityProfile.Quality),
                Is.EqualTo("High"));
        }
    }
}
