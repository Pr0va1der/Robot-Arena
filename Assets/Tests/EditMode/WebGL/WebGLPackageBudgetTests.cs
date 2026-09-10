using System;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using RobotArena.WebGL.Editor;
using UnityEditor;

namespace RobotArena.WebGL.Editor.Tests
{
    public sealed class WebGLPackageBudgetTests
    {
        private string archivePath;

        [SetUp]
        public void SetUp()
        {
            archivePath = Path.Combine(
                Path.GetTempPath(),
                "robot-arena-webgl-budget-" + Guid.NewGuid().ToString("N") + ".zip");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }

        [Test]
        public void Valid_root_index_and_build_entries_pass_under_the_limit()
        {
            CreateArchive(
                ("index.html", 5),
                ("Build/Game.data", 10),
                ("Build/Game.wasm", 10));

            WebGLPackageBudgetResult result = WebGLPackageBudget.Measure(archivePath, 30);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.IsWithinBudget, Is.True);
            Assert.That(result.UncompressedBytes, Is.EqualTo(25));
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void Unity_template_style_sheet_is_allowed_at_archive_root()
        {
            CreateArchive(
                ("index.html", 5),
                ("style.css", 3),
                ("Build/Game.data", 10));

            WebGLPackageBudgetResult result = WebGLPackageBudget.Measure(archivePath, 30);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Errors, Is.Empty);
        }

        [Test]
        public void Budget_uses_uncompressed_entry_sizes_not_zip_size()
        {
            CreateArchive(("index.html", 1), ("Build/Game.data", 20));

            WebGLPackageBudgetResult result = WebGLPackageBudget.Measure(archivePath, 10);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.IsWithinBudget, Is.False);
            Assert.That(result.IsPassing, Is.False);
            Assert.That(result.UncompressedBytes, Is.EqualTo(21));
            Assert.That(string.Join("\n", result.Errors), Does.Contain("exceeds the limit"));
        }

        [Test]
        public void Invalid_archive_layout_is_rejected()
        {
            CreateArchive(
                ("nested/index.html", 1),
                ("Build/Game data.wasm", 1),
                ("Other/readme.txt", 1));

            WebGLPackageBudgetResult result = WebGLPackageBudget.Measure(archivePath, 100);

            Assert.That(result.IsValid, Is.False);
            string errors = string.Join("\n", result.Errors);
            Assert.That(errors, Does.Contain("exactly one index.html at the archive root"));
            Assert.That(errors, Does.Contain("spaces or non-ASCII characters"));
            Assert.That(errors, Does.Contain("must be under Build/"));
        }

        [Test]
        public void Release_texture_policy_uses_desktop_compression_and_caps_resolution()
        {
            TextureImporterPlatformSettings current = new TextureImporterPlatformSettings
            {
                overridden = false,
                maxTextureSize = 2048,
                textureCompression = TextureImporterCompression.Uncompressed,
                crunchedCompression = false,
                compressionQuality = 100,
                format = TextureImporterFormat.RGBA32
            };

            TextureImporterPlatformSettings result = WebGLTextureImportPolicy.CreateSettings(current, 1024);

            Assert.That(result.overridden, Is.True);
            Assert.That(result.maxTextureSize, Is.EqualTo(1024));
            Assert.That(result.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed));
            Assert.That(result.crunchedCompression, Is.True);
            Assert.That(result.compressionQuality, Is.EqualTo(50));
            Assert.That(result.format, Is.EqualTo(TextureImporterFormat.DXT5Crunched));
        }

        [Test]
        public void PluginYG2_release_configuration_accepts_the_pinned_official_path()
        {
            var errors = RobotArenaWebGLReleaseBuild.GetPluginYG2ConfigurationErrors(
                "UNITY_POST_PROCESSING_STACK_V2;YandexGamesPlatform_yg;ROBOTARENA_PLUGINYG2;PLUGIN_YG_2;EnvirData_yg",
                "v2.0092",
                "<script src=\"/sdk.js\"></script>\n<script>YaGames.init()</script>");

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void PluginYG2_release_configuration_rejects_a_partial_or_custom_sdk_path()
        {
            var errors = RobotArenaWebGLReleaseBuild.GetPluginYG2ConfigurationErrors(
                "ROBOTARENA_PLUGINYG2;PLUGIN_YG_2",
                "v2.0091",
                "<script src=\"/custom-sdk.js\"></script>");

            Assert.That(errors, Has.Some.Contains("YandexGamesPlatform_yg"));
            Assert.That(errors, Has.Some.Contains("EnvirData_yg"));
            Assert.That(errors, Has.Some.Contains("v2.0092"));
            Assert.That(errors, Has.Some.Contains("exactly one /sdk.js loader"));
            Assert.That(errors, Has.Some.Contains("exactly one YaGames.init()"));
        }

        [Test]
        public void PluginYG2_post_processed_artifact_accepts_the_official_runtime_markers()
        {
            var errors = RobotArenaWebGLReleaseBuild.GetPluginYG2ArtifactErrors(
                "<script src=\"/sdk.js\"></script>\n"
                + "const sdk = await YaGames.init();\n"
                + "ysdk.on('game_api_pause', PauseCallback);\n"
                + "ysdk.on('game_api_resume', ResumeCallback);\n"
                + "await RequestingEnvironmentData();\n"
                + "YG2Instance('SetEnvirData', environmentData);\n"
                + "[PluginYG2 v2.0092] [Platform: YandexGames]");

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void PluginYG2_post_processed_artifact_rejects_legacy_or_duplicate_runtime_paths()
        {
            var errors = RobotArenaWebGLReleaseBuild.GetPluginYG2ArtifactErrors(
                "<script src=\"/sdk.js\"></script>\n"
                + "<script src=\"/sdk.js\"></script>\n"
                + "YaGames.init(); YaGames.init();\n"
                + "RobotArenaPlatformProbe\n");

            Assert.That(errors, Has.Some.Contains("exactly one /sdk.js loader"));
            Assert.That(errors, Has.Some.Contains("exactly one YaGames.init()"));
            Assert.That(errors, Has.Some.Contains("legacy custom bridge"));
            Assert.That(errors, Has.Some.Contains("game_api_pause"));
            Assert.That(errors, Has.Some.Contains("RequestingEnvironmentData"));
        }

        private void CreateArchive(params (string Name, int Size)[] entries)
        {
            using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                foreach ((string name, int size) in entries)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                    using Stream stream = entry.Open();
                    for (int index = 0; index < size; index++)
                    {
                        stream.WriteByte((byte)(index % 251));
                    }
                }
            }
        }
    }
}
