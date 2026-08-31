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
                string russian = LocalizationCatalog.Get(GameLanguage.Russian, key);
                string english = LocalizationCatalog.Get(GameLanguage.English, key);
                Assert.That(russian, Is.Not.Empty, key.ToString());
                Assert.That(english, Is.Not.Empty, key.ToString());
                Assert.That(russian, Does.Not.Contain("\uFFFD"), key.ToString());
                Assert.That(english, Does.Not.Contain("\uFFFD"), key.ToString());
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
