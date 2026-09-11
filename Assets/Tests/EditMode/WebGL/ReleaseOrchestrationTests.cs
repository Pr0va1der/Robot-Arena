using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RobotArena.WebGL.Editor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.TestTools;

namespace RobotArena.WebGL.Editor.Tests
{
    public sealed class ReleaseOrchestrationTests
    {
        private string temporaryDirectory;
        private string outputDirectory;
        private string archivePath;
        private string reportPath;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "robot-arena-release-orchestration-" + Guid.NewGuid().ToString("N"));
            outputDirectory = Path.Combine(temporaryDirectory, "artifact-output");
            archivePath = Path.Combine(temporaryDirectory, "candidate.zip");
            reportPath = Path.Combine(temporaryDirectory, "report.json");
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }

        [Test]
        public void Configuration_failure_runs_common_finally_and_writes_full_failure_report()
        {
            string lastSuccessfulCandidate = Path.Combine(
                temporaryDirectory,
                "last-successful-candidate.txt");
            File.WriteAllText(lastSuccessfulCandidate, "keep this candidate");

            var operations = new ControlledReleaseOperations
            {
                ConfigurationErrors = RobotArenaWebGLReleaseBuild.GetPluginYG2ConfigurationErrors(
                    "ROBOTARENA_PLUGINYG2;PLUGIN_YG_2",
                    "v2.0091",
                    "<script src=\"/custom-sdk.js\"></script>")
            };
            RobotArenaWebGLReleaseBuildContext context = CreateContext(operations);

            BuildFailedException failure = Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(context));

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Message, Does.Contain("release configuration is invalid"));
            Assert.That(File.Exists(reportPath), Is.True);
            Assert.That(File.ReadAllText(lastSuccessfulCandidate), Is.EqualTo("keep this candidate"));
            Assert.That(operations.BuildCallCount, Is.Zero);

            ReleaseReportProbe report = ReadReport();
            Assert.That(report.validationStage, Is.EqualTo("configuration"));
            Assert.That(report.validationErrors, Does.Contain(
                "WebGL scripting define is missing: YandexGamesPlatform_yg"));
            Assert.That(report.validationErrors, Does.Contain(
                "WebGL scripting define is missing: EnvirData_yg"));
            Assert.That(report.validationErrors, Does.Contain(
                "Official PluginYG2 version must be v2.0092, got v2.0091"));
            Assert.That(report.validationErrors, Does.Contain(
                "PluginYG2 template must contain exactly one /sdk.js loader."));
            Assert.That(report.validationErrors, Does.Contain(
                "PluginYG2 template must contain exactly one YaGames.init() call."));
            Assert.That(report.platformSdk, Is.EqualTo("PluginYG2"));
            Assert.That(report.pluginVersion, Is.EqualTo("v2.0092"));
            Assert.That(report.sdkLoader, Is.EqualTo("<script src=\"/sdk.js\"></script>"));
            Assert.That(report.buildResult, Is.EqualTo("NotStarted"));
            Assert.That(report.artifactValidationCompleted, Is.False);
            Assert.That(report.artifactIsValid, Is.False);
            Assert.That(report.archiveValidationCompleted, Is.False);
            Assert.That(report.archiveIsValid, Is.False);
            Assert.That(report.packageIsPassing, Is.False);
            Assert.That(report.releaseIsUploadReady, Is.False);
            Assert.That(report.artifactChecksumSha256, Is.Empty);
            Assert.That(report.archiveChecksumSha256, Is.Empty);
        }

        [Test]
        public void Report_writer_failure_does_not_mask_the_original_gate_failure()
        {
            Directory.CreateDirectory(reportPath);
            var operations = new ControlledReleaseOperations
            {
                ConfigurationErrors = new[] { "controlled configuration failure" }
            };

            LogAssert.Expect(
                LogType.Error,
                new Regex("Robot Arena WebGL release report could not be written: .* is denied\\."));
            BuildFailedException failure = Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(CreateContext(operations)));

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Message, Does.Contain("release configuration is invalid"));
            Assert.That(failure.Message, Does.Contain("controlled configuration failure"));
            Assert.That(Directory.Exists(reportPath), Is.True);
        }

        [TestCase(ControlledFailureStage.Artifact, "artifact")]
        [TestCase(ControlledFailureStage.Archive, "archive")]
        [TestCase(ControlledFailureStage.Package, "package")]
        public void Post_build_gate_failure_writes_stage_specific_report_with_available_evidence(
            ControlledFailureStage failureStage,
            string expectedStage)
        {
            var operations = new ControlledReleaseOperations
            {
                FailureStage = failureStage
            };
            RobotArenaWebGLReleaseBuildContext context = CreateContext(operations);

            BuildFailedException failure = Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(context));

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Message, Does.Contain(expectedStage));
            Assert.That(File.Exists(reportPath), Is.True);

            ReleaseReportProbe report = ReadReport();
            Assert.That(report.validationStage, Is.EqualTo(expectedStage));
            Assert.That(report.buildResult, Is.EqualTo("Succeeded"));
            Assert.That(report.releaseIsUploadReady, Is.False);
            Assert.That(report.validationErrors, Is.Not.Empty);
            Assert.That(report.packageIsPassing, Is.False);
            Assert.That(report.platformSdk, Is.EqualTo("PluginYG2"));
            Assert.That(report.pluginVersion, Is.EqualTo("v2.0092"));
            Assert.That(
                report.pluginSourceArchiveSha256,
                Is.EqualTo("8A5CBD1DEA0CFB0772A8E28976663CD7D91E8594B2F70DB9E03E0AC682DFADC3"));
            Assert.That(
                report.pluginVendoredFingerprint,
                Is.EqualTo("AB4551EFDB23E1DC417598F406AF2997F16080BBBCF8E9FB08CDFAFF9763395F"));
            Assert.That(report.sdkLoader, Is.EqualTo("<script src=\"/sdk.js\"></script>"));

            if (failureStage == ControlledFailureStage.Artifact)
            {
                Assert.That(report.artifactValidationCompleted, Is.True);
                Assert.That(report.artifactIsValid, Is.False);
                Assert.That(report.artifactErrors, Has.Some.Contains("exactly one /sdk.js loader"));
                Assert.That(report.artifactChecksumSha256, Has.Length.EqualTo(64));
                Assert.That(report.archiveValidationCompleted, Is.False);
                Assert.That(report.archiveChecksumSha256, Is.Empty);
            }
            else if (failureStage == ControlledFailureStage.Archive)
            {
                Assert.That(report.artifactValidationCompleted, Is.True);
                Assert.That(report.artifactIsValid, Is.True);
                Assert.That(report.archiveValidationCompleted, Is.True);
                Assert.That(report.archiveIsValid, Is.False);
                Assert.That(report.archiveErrors, Has.Some.Contains("legacy custom bridge"));
                Assert.That(report.artifactChecksumSha256, Has.Length.EqualTo(64));
                Assert.That(report.archiveChecksumSha256, Has.Length.EqualTo(64));
            }
            else
            {
                Assert.That(report.artifactValidationCompleted, Is.True);
                Assert.That(report.artifactIsValid, Is.True);
                Assert.That(report.archiveValidationCompleted, Is.True);
                Assert.That(report.archiveIsValid, Is.True);
                Assert.That(report.packageErrors, Has.Some.Contains("exceeds the limit"));
                Assert.That(report.artifactChecksumSha256, Has.Length.EqualTo(64));
                Assert.That(report.archiveChecksumSha256, Has.Length.EqualTo(64));
            }
        }

        private RobotArenaWebGLReleaseBuildContext CreateContext(
            ControlledReleaseOperations operations)
        {
            return new RobotArenaWebGLReleaseBuildContext(
                outputDirectory,
                archivePath,
                reportPath,
                "ROBOTARENA_PLUGINYG2;PLUGIN_YG_2",
                "v2.0091",
                "<script src=\"/custom-sdk.js\"></script>",
                operations);
        }

        private ReleaseReportProbe ReadReport()
        {
            return JsonUtility.FromJson<ReleaseReportProbe>(File.ReadAllText(reportPath));
        }

        public enum ControlledFailureStage
        {
            None,
            Artifact,
            Archive,
            Package
        }

        private sealed class ControlledReleaseOperations : IRobotArenaWebGLReleaseOperations
        {
            private static readonly RobotArenaReleaseIntegrationCoordinates Integration =
                new RobotArenaReleaseIntegrationCoordinates(
                    "PluginYG2",
                    "v2.0092",
                    "8A5CBD1DEA0CFB0772A8E28976663CD7D91E8594B2F70DB9E03E0AC682DFADC3",
                    "AB4551EFDB23E1DC417598F406AF2997F16080BBBCF8E9FB08CDFAFF9763395F",
                    "<script src=\"/sdk.js\"></script>");

            public ControlledFailureStage FailureStage { get; set; }
            public IReadOnlyList<string> ConfigurationErrors { get; set; } = new string[0];
            public int BuildCallCount { get; private set; }

            public void PreparePaths(string outputPath, string zipPath, string reportPath)
            {
                if (Directory.Exists(outputPath))
                {
                    Directory.Delete(outputPath, recursive: true);
                }

                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                if (File.Exists(reportPath))
                {
                    File.Delete(reportPath);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            }

            public RobotArenaReleaseValidationResult ValidateConfiguration(
                string defineSymbols,
                string pluginVersion,
                string templateSource)
            {
                return new RobotArenaReleaseValidationResult(Integration, ConfigurationErrors);
            }

            public int ApplyTexturePolicy()
            {
                return 0;
            }

            public RobotArenaReleaseBuildResult Build(string[] activeScenes, string outputPath)
            {
                BuildCallCount++;
                Directory.CreateDirectory(outputPath);
                File.WriteAllText(Path.Combine(outputPath, "index.html"),
                    FailureStage == ControlledFailureStage.Artifact
                        ? InvalidArtifactSource
                        : ValidArtifactSource);
                return new RobotArenaReleaseBuildResult("Succeeded", 1234);
            }

            public RobotArenaReleaseValidationResult ValidateArtifact(string outputPath)
            {
                List<string> errors = RobotArenaWebGLReleaseBuild.GetPluginYG2ArtifactErrors(
                    File.ReadAllText(Path.Combine(outputPath, "index.html")));
                return new RobotArenaReleaseValidationResult(Integration, errors);
            }

            public void CreateArchive(string outputPath, string zipPath)
            {
                ZipFile.CreateFromDirectory(
                    outputPath,
                    zipPath,
                    System.IO.Compression.CompressionLevel.Optimal,
                    includeBaseDirectory: false);

                if (FailureStage == ControlledFailureStage.Archive)
                {
                    using (ZipArchive archive = ZipFile.Open(zipPath, ZipArchiveMode.Update))
                    {
                        ZipArchiveEntry entry = archive.CreateEntry("runtime.js");
                        using (StreamWriter writer = new StreamWriter(entry.Open()))
                        {
                            writer.Write(InvalidRuntimeSource);
                        }
                    }
                }
            }

            public RobotArenaReleaseValidationResult ValidateArchive(string zipPath)
            {
                var errors = new List<string>();
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName == "index.html")
                        {
                            continue;
                        }

                        using (StreamReader reader = new StreamReader(entry.Open()))
                        {
                            errors.AddRange(RobotArenaWebGLReleaseBuild.GetPluginYG2ArtifactErrors(
                                reader.ReadToEnd()));
                        }
                    }
                }

                return new RobotArenaReleaseValidationResult(Integration, errors);
            }

            public WebGLPackageBudgetResult MeasurePackage(string zipPath, long limitBytes)
            {
                return WebGLPackageBudget.Measure(
                    zipPath,
                    FailureStage == ControlledFailureStage.Package ? 1 : limitBytes);
            }

            private const string ValidArtifactSource =
                "<script src=\"/sdk.js\"></script>\n"
                + "YaGames.init();\n"
                + "game_api_pause game_api_resume RequestingEnvironmentData SetEnvirData "
                + "PluginYG2 v2.0092";

            private const string InvalidArtifactSource =
                "<script src=\"/sdk.js\"></script>\n"
                + "<script src=\"/sdk.js\"></script>\n"
                + "YaGames.init(); YaGames.init();\n"
                + "game_api_pause game_api_pause game_api_resume game_api_resume "
                + "RequestingEnvironmentData SetEnvirData PluginYG2 v2.0092\n"
                + "RobotArenaPlatformProbe";

            private const string InvalidRuntimeSource =
                "<script src=\"/sdk.js\"></script>\n"
                + "<script src=\"/sdk.js\"></script>\n"
                + "YaGames.init(); YaGames.init();\n"
                + "RequestingEnvironmentData SetEnvirData PluginYG2 v2.0092\n"
                + "RobotArenaPlatformProbe";
        }

        [Serializable]
        private sealed class ReleaseReportProbe
        {
            public string buildResult;
            public string pluginVersion;
            public string platformSdk;
            public string sdkLoader;
            public string pluginSourceArchiveSha256;
            public string pluginVendoredFingerprint;
            public string validationStage;
            public bool releaseIsUploadReady;
            public List<string> validationErrors;
            public bool artifactIsValid;
            public bool artifactValidationCompleted;
            public string artifactChecksumSha256;
            public List<string> artifactErrors;
            public bool archiveIsValid;
            public bool archiveValidationCompleted;
            public string archiveChecksumSha256;
            public List<string> archiveErrors;
            public bool packageIsPassing;
            public List<string> packageErrors;
        }
    }
}
