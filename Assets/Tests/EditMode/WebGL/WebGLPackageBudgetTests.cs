using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using NUnit.Framework;
using RobotArena.WebGL.Editor;
using UnityEditor;
using UnityEngine;

namespace RobotArena.WebGL.Editor.Tests
{
    public sealed class WebGLPackageBudgetTests
    {
        private string archivePath;
        private static readonly IntegrationManifestProbe Manifest = LoadManifest();

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
                string.Join(";", Manifest.requiredDefines),
                Manifest.pluginVersion,
                Manifest.sdkLoader + "\n" + Manifest.sdkInitializer);

            Assert.That(errors, Is.Empty);
            Assert.That(Manifest.requiredDefines, Does.Contain(Manifest.platform));
        }

        [Test]
        public void PluginYG2_release_configuration_rejects_a_partial_or_custom_sdk_path()
        {
            var errors = RobotArenaWebGLReleaseBuild.GetPluginYG2ConfigurationErrors(
                string.Empty,
                Manifest.pluginVersion + "-invalid",
                "<script src=\"/custom-sdk.js\"></script>");

            foreach (string requiredDefine in Manifest.requiredDefines)
            {
                Assert.That(errors, Has.Some.Contains(requiredDefine));
            }
            Assert.That(errors, Has.Some.Contains(Manifest.pluginVersion));
            Assert.That(errors, Has.Some.Contains("exactly one SDK loader"));
            Assert.That(errors, Has.Some.Contains("exactly one SDK initializer"));
        }

        [Test]
        public void PluginYG2_post_processed_artifact_accepts_the_official_runtime_markers()
        {
            var errors = RobotArenaWebGLReleaseBuild.GetPluginYG2ArtifactErrors(
                Manifest.sdkLoader
                + "\n"
                + Manifest.sdkInitializer
                + "\n"
                + string.Join(" ", Manifest.requiredArtifactMarkers));

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void PluginYG2_post_processed_artifact_rejects_legacy_or_duplicate_runtime_paths()
        {
            var errors = RobotArenaWebGLReleaseBuild.GetPluginYG2ArtifactErrors(
                Manifest.sdkLoader
                + "\n"
                + Manifest.sdkLoader
                + "\n"
                + Manifest.sdkInitializer
                + " "
                + Manifest.sdkInitializer
                + "\n"
                + string.Join(" ", Manifest.requiredArtifactMarkers)
                + " "
                + string.Join(" ", Manifest.exactlyOnceArtifactMarkers)
                + "\n"
                + Manifest.forbiddenArtifactMarkers[0]
                + "\n");

            Assert.That(errors, Has.Some.Contains("exactly one SDK loader"));
            Assert.That(errors, Has.Some.Contains("exactly one SDK initializer"));
            foreach (string marker in Manifest.exactlyOnceArtifactMarkers)
            {
                Assert.That(errors, Has.Some.Contains("exactly one marker: " + marker));
            }
            Assert.That(errors, Has.Some.Contains("legacy custom bridge"));
        }

        [Test]
        public void PluginYG2_artifact_validation_ignores_comment_only_markers()
        {
            var errors = RobotArenaWebGLReleaseBuild.GetPluginYG2ArtifactErrors(
                "<!-- "
                + string.Join(" ", Manifest.requiredArtifactMarkers)
                + " -->\n"
                + Manifest.sdkLoader
                + "\n"
                + Manifest.sdkInitializer
                + "\n"
                + string.Join(" ", Manifest.requiredArtifactMarkers));

            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void Release_report_records_gate_failure_and_is_not_upload_ready()
        {
            string temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "robot-arena-webgl-report-" + Guid.NewGuid().ToString("N"));
            string reportPath = Path.Combine(temporaryDirectory, "release-report.json");
            string missingArchivePath = Path.Combine(temporaryDirectory, "missing.zip");
            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                MethodInfo writeReport = typeof(RobotArenaWebGLReleaseBuild).GetMethod(
                    "WriteReport",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(writeReport, Is.Not.Null);

                writeReport.Invoke(
                    null,
                    new object[]
                    {
                        reportPath,
                        temporaryDirectory,
                        null,
                        null,
                        null,
                        null,
                        missingArchivePath,
                        0,
                        RobotArenaReleaseValidationStage.Configuration,
                        new List<string> { "synthetic gate failure" },
                        false
                    });

                string report = File.ReadAllText(reportPath);
                Assert.That(report, Does.Contain("\"validationStage\": \"configuration\""));
                Assert.That(report, Does.Contain("\"releaseIsUploadReady\": false"));
                Assert.That(report, Does.Contain("synthetic gate failure"));
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
        }

        private void CreateArchive(params (string Name, int Size)[] entries)
        {
                using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                foreach ((string name, int size) in entries)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(
                        name,
                        System.IO.Compression.CompressionLevel.Optimal);
                    using Stream stream = entry.Open();
                    for (int index = 0; index < size; index++)
                    {
                        stream.WriteByte((byte)(index % 251));
                    }
                }
            }
        }

        private static IntegrationManifestProbe LoadManifest()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return JsonUtility.FromJson<IntegrationManifestProbe>(File.ReadAllText(
                Path.Combine(projectRoot, "Tools/RobotArenaPluginYG2Integration.json")));
        }

        [Serializable]
        private sealed class IntegrationManifestProbe
        {
            public string pluginVersion;
            public string platform;
            public string sdkLoader;
            public string sdkInitializer;
            public string[] requiredDefines;
            public string[] requiredArtifactMarkers;
            public string[] exactlyOnceArtifactMarkers;
            public string[] forbiddenArtifactMarkers;
        }
    }
}
