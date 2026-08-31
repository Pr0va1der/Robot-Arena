#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using NUnit.Framework;
using RobotArena.Session;

namespace RobotArena.Session.Tests
{
    public class RobotArenaUiFontCoverageTests
    {
        private const string FontResourceAssetPath =
            "Assets/Resources/RobotArenaUiFont.asset";

        [Test]
        public void UI_font_covers_every_character_in_the_localized_catalog()
        {
            UnityEngine.Object resource = AssetDatabase.LoadMainAssetAtPath(FontResourceAssetPath);
            Assert.That(resource, Is.Not.Null, FontResourceAssetPath);

            var serializedResource = new SerializedObject(resource);
            SerializedProperty fontProperty = serializedResource.FindProperty("font");
            Assert.That(fontProperty, Is.Not.Null, FontResourceAssetPath + ".font");

            TMP_FontAsset font = fontProperty.objectReferenceValue as TMP_FontAsset;
            Assert.That(font, Is.Not.Null, FontResourceAssetPath + ".font");

            var missingCharacters = new List<string>();
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                foreach (LocalizationKey key in Enum.GetValues(typeof(LocalizationKey)))
                {
                    string value = LocalizationCatalog.Get(language, key);
                    foreach (char character in value)
                    {
                        if (char.IsControl(character) || font.HasCharacter(character, true))
                        {
                            continue;
                        }

                        missingCharacters.Add(
                            language + ":" + key + " U+" + ((int)character).ToString("X4"));
                    }
                }
            }

            Assert.That(missingCharacters, Is.Empty);
        }
    }
}
#endif
