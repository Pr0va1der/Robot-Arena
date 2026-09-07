using System;
using UnityEditor;

namespace RobotArena.WebGL.Editor
{
    public static class WebGLTextureImportPolicy
    {
        public const string PlatformName = "WebGL";
        public const int DefaultMaxTextureSize = 1024;
        public const int CrunchQuality = 50;

        public static TextureImporterPlatformSettings CreateSettings(
            TextureImporterPlatformSettings current,
            int maxTextureSize)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (maxTextureSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTextureSize));
            }

            TextureImporterPlatformSettings result = new TextureImporterPlatformSettings();
            current.CopyTo(result);
            result.name = PlatformName;
            result.overridden = true;
            result.maxTextureSize = Math.Min(
                current.maxTextureSize > 0 ? current.maxTextureSize : maxTextureSize,
                maxTextureSize);
            result.textureCompression = TextureImporterCompression.Compressed;
            result.crunchedCompression = true;
            result.compressionQuality = CrunchQuality;
            result.format = TextureImporterFormat.DXT5Crunched;
            return result;
        }

        public static int ApplyForRelease()
        {
            int changedCount = 0;
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            foreach (string textureGuid in textureGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(textureGuid);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                TextureImporterPlatformSettings current =
                    importer.GetPlatformTextureSettings(PlatformName);
                TextureImporterPlatformSettings desired =
                    CreateSettings(current, DefaultMaxTextureSize);
                if (!NeedsUpdate(current, desired))
                {
                    continue;
                }

                importer.SetPlatformTextureSettings(desired);
                importer.SaveAndReimport();
                changedCount++;
            }

            return changedCount;
        }

        private static bool NeedsUpdate(
            TextureImporterPlatformSettings current,
            TextureImporterPlatformSettings desired)
        {
            return current.overridden != desired.overridden
                || current.maxTextureSize != desired.maxTextureSize
                || current.textureCompression != desired.textureCompression
                || current.crunchedCompression != desired.crunchedCompression
                || current.compressionQuality != desired.compressionQuality
                || current.format != desired.format;
        }
    }
}
