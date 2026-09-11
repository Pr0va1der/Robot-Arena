using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
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
        private static readonly IntegrationManifestProbe Manifest = LoadManifest();

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
            SeedStableReleaseSet();
            string lastSuccessfulCandidate = Path.Combine(
                temporaryDirectory,
                "last-successful-candidate.txt");
            File.WriteAllText(lastSuccessfulCandidate, "keep this candidate");

            var operations = new ControlledReleaseOperations
            {
            };
            RobotArenaWebGLReleaseBuildContext context = CreateContext(
                operations,
                validConfiguration: false);

            BuildFailedException failure = Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(context));

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Message, Does.Contain("release configuration is invalid"));
            Assert.That(File.Exists(reportPath + ".candidate"), Is.True);
            AssertStableReleaseSet();
            Assert.That(File.ReadAllText(lastSuccessfulCandidate), Is.EqualTo("keep this candidate"));
            Assert.That(operations.BuildCallCount, Is.Zero);

            ReleaseReportProbe report = ReadAttemptReport();
            Assert.That(report.validationStage, Is.EqualTo("configuration"));
            foreach (string requiredDefine in Manifest.requiredDefines)
            {
                Assert.That(report.validationErrors, Does.Contain(
                    "WebGL scripting define is missing: " + requiredDefine));
            }
            Assert.That(report.validationErrors, Does.Contain(
                "Official "
                + Manifest.plugin
                + " version must be "
                + Manifest.pluginVersion
                + ", got "
                + Manifest.pluginVersion
                + "-invalid"));
            Assert.That(report.validationErrors, Has.Some.Contains(
                "Plugin template must contain exactly one SDK loader: " + Manifest.sdkLoader));
            Assert.That(report.validationErrors, Does.Contain(
                "Plugin template must contain exactly one SDK initializer: "
                + Manifest.sdkInitializer
                + "."));
            Assert.That(report.platformSdk, Is.EqualTo(Manifest.plugin));
            Assert.That(
                report.pluginVersion,
                Is.EqualTo(Manifest.pluginVersion + "-invalid"));
            Assert.That(report.sdkLoader, Is.EqualTo(Manifest.sdkLoader));
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
        public void Failed_unity_build_writes_typed_outcome_and_never_validates_an_artifact()
        {
            SeedStableReleaseSet();
            var operations = new ControlledReleaseOperations
            {
                FailureStage = ControlledFailureStage.Build
            };

            BuildFailedException failure = Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(CreateContext(operations)));

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Message, Does.Contain("release build failed: Failed"));
            Assert.That(operations.BuildCallCount, Is.EqualTo(1));
            AssertStableReleaseSet();

            ReleaseReportProbe report = ReadAttemptReport();
            Assert.That(report.validationStage, Is.EqualTo("build"));
            Assert.That(report.buildResult, Is.EqualTo("Failed"));
            Assert.That(report.artifactValidationCompleted, Is.False);
            Assert.That(report.archiveValidationCompleted, Is.False);
            Assert.That(report.releaseIsUploadReady, Is.False);
        }

        [Test]
        public void Failed_release_preserves_the_last_successful_candidate()
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(
                Path.Combine(outputDirectory, "last-successful.txt"),
                "keep this candidate");
            File.WriteAllText(archivePath, "last successful archive");
            File.WriteAllText(reportPath, "last successful report");

            var operations = new ControlledReleaseOperations
            {
                FailureStage = ControlledFailureStage.Artifact
            };

            Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(CreateContext(operations)));

            Assert.That(
                File.ReadAllText(Path.Combine(outputDirectory, "last-successful.txt")),
                Is.EqualTo("keep this candidate"));
            Assert.That(File.ReadAllText(archivePath), Is.EqualTo("last successful archive"));
            Assert.That(File.ReadAllText(reportPath), Is.EqualTo("last successful report"));
            Assert.That(File.Exists(reportPath + ".candidate"), Is.True);
            Assert.That(Directory.Exists(outputDirectory + ".candidate"), Is.False);
            Assert.That(File.Exists(archivePath + ".candidate"), Is.False);
        }

        [Test]
        public void Artifact_validator_without_result_keeps_available_artifact_checksum()
        {
            SeedStableReleaseSet();
            string expectedArtifactChecksum = ComputeContentsSha256(
                ControlledReleaseOperations.ValidArtifactSource);
            var operations = new ControlledReleaseOperations
            {
                FailureStage = ControlledFailureStage.ArtifactNoResult
            };

            BuildFailedException failure = Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(CreateContext(operations)));

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Message, Does.Contain("artifact"));
            AssertStableReleaseSet();

            ReleaseReportProbe report = ReadAttemptReport();
            Assert.That(report.validationStage, Is.EqualTo("artifact"));
            Assert.That(report.artifactValidationCompleted, Is.False);
            Assert.That(report.artifactIsValid, Is.False);
            Assert.That(report.artifactChecksumSha256, Is.EqualTo(expectedArtifactChecksum));
            Assert.That(report.archiveValidationCompleted, Is.False);
            Assert.That(report.archiveChecksumSha256, Is.Empty);
            Assert.That(report.validationErrors, Has.Some.Contains("Validation did not return a result"));
        }

        [Test]
        public void Archive_validator_without_result_keeps_available_archive_checksum()
        {
            SeedStableReleaseSet();
            string expectedArtifactChecksum = ComputeContentsSha256(
                ControlledReleaseOperations.ValidArtifactSource);
            var operations = new ControlledReleaseOperations
            {
                FailureStage = ControlledFailureStage.ArchiveNoResult
            };

            BuildFailedException failure = Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(CreateContext(operations)));

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Message, Does.Contain("archive"));
            AssertStableReleaseSet();

            ReleaseReportProbe report = ReadAttemptReport();
            Assert.That(report.validationStage, Is.EqualTo("archive"));
            Assert.That(report.artifactValidationCompleted, Is.True);
            Assert.That(report.artifactIsValid, Is.True);
            Assert.That(report.artifactChecksumSha256, Is.EqualTo(expectedArtifactChecksum));
            Assert.That(report.archiveValidationCompleted, Is.False);
            Assert.That(report.archiveIsValid, Is.False);
            Assert.That(report.archiveChecksumSha256, Has.Length.EqualTo(64));
            Assert.That(report.packageIsPassing, Is.False);
            Assert.That(report.validationErrors, Has.Some.Contains("Validation did not return a result"));
        }

        [Test]
        public void Prepare_failure_does_not_report_stale_candidate_evidence()
        {
            SeedStableReleaseSet();
            SeedStaleCandidateEvidence();
            var operations = new ControlledReleaseOperations
            {
                FailureStage = ControlledFailureStage.Prepare
            };

            BuildFailedException failure = Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(CreateContext(operations)));

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Message, Does.Contain("prepare"));
            AssertStableReleaseSet();

            ReleaseReportProbe report = ReadAttemptReport();
            Assert.That(report.validationStage, Is.EqualTo("prepare"));
            Assert.That(report.artifactValidationCompleted, Is.False);
            Assert.That(report.artifactChecksumSha256, Is.Empty);
            Assert.That(report.archiveValidationCompleted, Is.False);
            Assert.That(report.archiveChecksumSha256, Is.Empty);
            Assert.That(report.releaseIsUploadReady, Is.False);
            Assert.That(report.validationErrors, Has.Some.Contains("controlled prepare failure"));
        }

        [Test]
        public void Successful_release_promotes_candidate_and_marks_upload_ready()
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(
                Path.Combine(outputDirectory, "last-successful.txt"),
                "replace this candidate");
            File.WriteAllText(archivePath, "replace this archive");
            File.WriteAllText(reportPath, "replace this report");

            RobotArenaWebGLReleaseBuild.RunReleasePackage(
                CreateContext(new ControlledReleaseOperations()));

            Assert.That(File.Exists(Path.Combine(outputDirectory, "index.html")), Is.True);
            Assert.That(
                File.Exists(Path.Combine(outputDirectory, "last-successful.txt")),
                Is.False);
            Assert.That(File.Exists(archivePath), Is.True);
            Assert.That(Directory.Exists(outputDirectory + ".candidate"), Is.False);
            Assert.That(File.Exists(archivePath + ".candidate"), Is.False);
            Assert.That(File.Exists(reportPath + ".candidate"), Is.False);

            ReleaseReportProbe report = ReadReport();
            Assert.That(report.validationStage, Is.EqualTo("promotion"));
            Assert.That(report.releaseIsUploadReady, Is.True);
            Assert.That(
                report.artifactChecksumSha256,
                Is.EqualTo(ComputeSha256(Path.Combine(outputDirectory, "index.html"))));
            Assert.That(
                report.archiveChecksumSha256,
                Is.EqualTo(ComputeSha256(archivePath)));
        }

        [Test]
        public void Promotion_failure_rolls_back_the_previous_release()
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(
                Path.Combine(outputDirectory, "last-successful.txt"),
                "keep this candidate");
            Directory.CreateDirectory(archivePath);
            File.WriteAllText(reportPath, "keep this report");

            Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(
                    CreateContext(new ControlledReleaseOperations())));

            Assert.That(
                File.ReadAllText(Path.Combine(outputDirectory, "last-successful.txt")),
                Is.EqualTo("keep this candidate"));
            Assert.That(Directory.Exists(archivePath), Is.True);
            Assert.That(File.ReadAllText(reportPath), Is.EqualTo("keep this report"));
            Assert.That(File.Exists(reportPath + ".candidate"), Is.True);
            Assert.That(Directory.Exists(outputDirectory + ".candidate"), Is.False);
            Assert.That(File.Exists(archivePath + ".candidate"), Is.False);

            ReleaseReportProbe report = ReadAttemptReport();
            Assert.That(report.validationStage, Is.EqualTo("promotion"));
            Assert.That(report.releaseIsUploadReady, Is.False);
        }

        [Test]
        public void Output_promotion_failure_preserves_the_previous_release_set()
        {
            File.WriteAllText(outputDirectory, "stable output path");
            File.WriteAllText(archivePath, "stable archive");
            File.WriteAllText(reportPath, "stable report");

            Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(
                    CreateContext(new ControlledReleaseOperations())));

            Assert.That(File.ReadAllText(outputDirectory), Is.EqualTo("stable output path"));
            Assert.That(File.ReadAllText(archivePath), Is.EqualTo("stable archive"));
            Assert.That(File.ReadAllText(reportPath), Is.EqualTo("stable report"));
            Assert.That(File.Exists(reportPath + ".candidate"), Is.True);
            Assert.That(Directory.Exists(outputDirectory + ".candidate"), Is.False);
            Assert.That(File.Exists(archivePath + ".candidate"), Is.False);
        }

        [Test]
        public void Report_promotion_failure_preserves_the_previous_release_set()
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(
                Path.Combine(outputDirectory, "last-successful.txt"),
                "stable output");
            File.WriteAllText(archivePath, "stable archive");
            Directory.CreateDirectory(reportPath);

            Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(
                    CreateContext(new ControlledReleaseOperations())));

            Assert.That(
                File.ReadAllText(Path.Combine(outputDirectory, "last-successful.txt")),
                Is.EqualTo("stable output"));
            Assert.That(File.ReadAllText(archivePath), Is.EqualTo("stable archive"));
            Assert.That(Directory.Exists(reportPath), Is.True);
            Assert.That(File.Exists(reportPath + ".candidate"), Is.True);
            Assert.That(Directory.Exists(outputDirectory + ".candidate"), Is.False);
            Assert.That(File.Exists(archivePath + ".candidate"), Is.False);
        }

        [Test]
        public void Production_artifact_validators_scan_generated_files_beyond_root_index()
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(
                Path.Combine(outputDirectory, "index.html"),
                ControlledReleaseOperations.ValidArtifactSource);
            File.WriteAllText(
                Path.Combine(outputDirectory, "runtime.js"),
                Manifest.exactlyOnceArtifactMarkers[0]);

            List<string> directoryErrors = RobotArenaWebGLReleaseBuild.GetPluginYG2ArtifactDirectoryErrors(
                outputDirectory);
            Assert.That(directoryErrors, Has.Some.EqualTo(
                "PluginYG2 artifact must contain exactly one marker: "
                + Manifest.exactlyOnceArtifactMarkers[0]));

            ZipFile.CreateFromDirectory(
                outputDirectory,
                archivePath,
                System.IO.Compression.CompressionLevel.Optimal,
                includeBaseDirectory: false);
            List<string> archiveErrors = RobotArenaWebGLReleaseBuild.GetPluginYG2ArtifactArchiveErrors(
                archivePath);
            Assert.That(archiveErrors, Has.Some.EqualTo(
                "PluginYG2 artifact must contain exactly one marker: "
                + Manifest.exactlyOnceArtifactMarkers[0]));
        }

        [Test]
        public void Report_writer_failure_does_not_mask_the_original_gate_failure()
        {
            Directory.CreateDirectory(reportPath + ".candidate");
            var operations = new ControlledReleaseOperations();

            LogAssert.Expect(
                LogType.Error,
                new Regex("Robot Arena WebGL release report could not be written: .* is denied\\."));
            BuildFailedException failure = Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(
                    CreateContext(operations, validConfiguration: false)));

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Message, Does.Contain("release configuration is invalid"));
            Assert.That(Directory.Exists(reportPath + ".candidate"), Is.True);
        }

        [TestCase(ControlledFailureStage.Artifact, "artifact")]
        [TestCase(ControlledFailureStage.Archive, "archive")]
        [TestCase(ControlledFailureStage.Package, "package")]
        public void Post_build_gate_failure_writes_stage_specific_report_with_available_evidence(
            ControlledFailureStage failureStage,
            string expectedStage)
        {
            SeedStableReleaseSet();
            var operations = new ControlledReleaseOperations
            {
                FailureStage = failureStage
            };
            RobotArenaWebGLReleaseBuildContext context = CreateContext(operations);

            BuildFailedException failure = Assert.Throws<BuildFailedException>(
                () => RobotArenaWebGLReleaseBuild.RunReleasePackage(context));

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Message, Does.Contain(expectedStage));
            Assert.That(File.Exists(reportPath + ".candidate"), Is.True);
            AssertStableReleaseSet();

            ReleaseReportProbe report = ReadAttemptReport();
            Assert.That(report.validationStage, Is.EqualTo(expectedStage));
            Assert.That(report.buildResult, Is.EqualTo("Succeeded"));
            Assert.That(report.releaseIsUploadReady, Is.False);
            Assert.That(report.validationErrors, Is.Not.Empty);
            Assert.That(report.packageIsPassing, Is.False);
            Assert.That(report.platformSdk, Is.EqualTo(Manifest.plugin));
            Assert.That(report.pluginVersion, Is.EqualTo(Manifest.pluginVersion));
            Assert.That(
                report.pluginSourceArchiveSha256,
                Is.EqualTo(Manifest.sourceArchiveSha256));
            Assert.That(
                report.pluginVendoredFingerprint,
                Is.EqualTo(Manifest.vendoredFingerprint));
            Assert.That(report.sdkLoader, Is.EqualTo(Manifest.sdkLoader));

            if (failureStage == ControlledFailureStage.Artifact)
            {
                Assert.That(report.artifactValidationCompleted, Is.True);
                Assert.That(report.artifactIsValid, Is.False);
                Assert.That(report.artifactErrors, Has.Some.Contains("exactly one SDK loader"));
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
            ControlledReleaseOperations operations,
            bool validConfiguration = true)
        {
            return new RobotArenaWebGLReleaseBuildContext(
                outputDirectory,
                archivePath,
                reportPath,
                validConfiguration
                    ? string.Join(";", Manifest.requiredDefines)
                    : string.Empty,
                validConfiguration
                    ? Manifest.pluginVersion
                    : Manifest.pluginVersion + "-invalid",
                validConfiguration
                    ? ControlledReleaseOperations.ValidArtifactSource
                    : "<script src=\"/custom-sdk.js\"></script>",
                Manifest.unityTemplate,
                operations);
        }

        private ReleaseReportProbe ReadReport()
        {
            return JsonUtility.FromJson<ReleaseReportProbe>(File.ReadAllText(reportPath));
        }

        private ReleaseReportProbe ReadAttemptReport()
        {
            return JsonUtility.FromJson<ReleaseReportProbe>(
                File.ReadAllText(reportPath + ".candidate"));
        }

        private void SeedStableReleaseSet()
        {
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(
                Path.Combine(outputDirectory, "last-successful.txt"),
                "last successful output");
            File.WriteAllText(archivePath, "last successful archive");
            File.WriteAllText(reportPath, "last successful report");
        }

        private void SeedStaleCandidateEvidence()
        {
            string candidateOutputDirectory = outputDirectory + ".candidate";
            string candidateArchivePath = archivePath + ".candidate";
            Directory.CreateDirectory(candidateOutputDirectory);
            File.WriteAllText(
                Path.Combine(candidateOutputDirectory, "index.html"),
                ControlledReleaseOperations.ValidArtifactSource);
            Directory.CreateDirectory(Path.Combine(candidateOutputDirectory, "Build"));
            File.WriteAllText(
                Path.Combine(candidateOutputDirectory, "Build", "Game.data"),
                "stale package data");
            ZipFile.CreateFromDirectory(
                candidateOutputDirectory,
                candidateArchivePath,
                System.IO.Compression.CompressionLevel.Optimal,
                includeBaseDirectory: false);
        }

        private void AssertStableReleaseSet()
        {
            AssertFileBytes(
                Path.Combine(outputDirectory, "last-successful.txt"),
                "last successful output");
            AssertFileBytes(archivePath, "last successful archive");
            AssertFileBytes(reportPath, "last successful report");
        }

        private static void AssertFileBytes(string filePath, string expectedContents)
        {
            Assert.That(
                File.ReadAllBytes(filePath),
                Is.EqualTo(Encoding.UTF8.GetBytes(expectedContents)));
        }

        private static string ComputeSha256(string filePath)
        {
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(filePath)))
                    .Replace("-", string.Empty);
            }
        }

        private static string ComputeContentsSha256(string contents)
        {
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(
                    hash.ComputeHash(Encoding.UTF8.GetBytes(contents)))
                    .Replace("-", string.Empty);
            }
        }

        public enum ControlledFailureStage
        {
            None,
            Prepare,
            Build,
            Artifact,
            ArtifactNoResult,
            Archive,
            ArchiveNoResult,
            Package
        }

        private sealed class ControlledReleaseOperations : IRobotArenaWebGLReleaseOperations
        {
            private static readonly RobotArenaReleaseIntegrationCoordinates Integration =
                new RobotArenaReleaseIntegrationCoordinates(
                    Manifest.plugin,
                    Manifest.pluginVersion,
                    Manifest.sourceArchiveSha256,
                    Manifest.vendoredFingerprint,
                    Manifest.sdkLoader);

            public ControlledFailureStage FailureStage { get; set; }
            public int BuildCallCount { get; private set; }

            public void PreparePaths(string outputPath, string zipPath, string reportPath)
            {
                if (FailureStage == ControlledFailureStage.Prepare)
                {
                    throw new IOException("controlled prepare failure");
                }

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
                return new RobotArenaReleaseValidationResult(
                    Integration,
                    RobotArenaWebGLReleaseBuild.GetPluginYG2ConfigurationErrors(
                        defineSymbols,
                        pluginVersion,
                        templateSource));
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
                Directory.CreateDirectory(Path.Combine(outputPath, "Build"));
                File.WriteAllText(Path.Combine(outputPath, "Build", "Game.data"), "package data");
                return new RobotArenaReleaseBuildResult(
                    FailureStage == ControlledFailureStage.Build
                        ? RobotArenaReleaseBuildOutcome.Failed
                        : RobotArenaReleaseBuildOutcome.Succeeded,
                    1234);
            }

            public RobotArenaReleaseValidationResult ValidateArtifact(string outputPath)
            {
                if (FailureStage == ControlledFailureStage.ArtifactNoResult)
                {
                    return null;
                }

                List<string> errors = RobotArenaWebGLReleaseBuild.GetPluginYG2ArtifactDirectoryErrors(
                    outputPath);
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
                if (FailureStage == ControlledFailureStage.ArchiveNoResult)
                {
                    return null;
                }

                List<string> errors = RobotArenaWebGLReleaseBuild.GetPluginYG2ArtifactArchiveErrors(
                    zipPath);
                return new RobotArenaReleaseValidationResult(Integration, errors);
            }

            public WebGLPackageBudgetResult MeasurePackage(string zipPath, long limitBytes)
            {
                return WebGLPackageBudget.Measure(
                    zipPath,
                    FailureStage == ControlledFailureStage.Package ? 1 : limitBytes);
            }

            public static string ValidArtifactSource =>
                Manifest.sdkLoader
                + "\n"
                + Manifest.sdkInitializer
                + "\n"
                + string.Join(" ", Manifest.requiredArtifactMarkers);

            private static string InvalidArtifactSource =>
                Manifest.sdkLoader
                + "\n"
                + Manifest.sdkLoader
                + "\n"
                + Manifest.sdkInitializer
                + " "
                + Manifest.sdkInitializer
                + "\n"
                + string.Join(" ", Manifest.requiredArtifactMarkers)
                + "\n"
                + Manifest.forbiddenArtifactMarkers[0];

            private static string InvalidRuntimeSource =>
                Manifest.sdkLoader
                + "\n"
                + Manifest.sdkLoader
                + "\n"
                + Manifest.sdkInitializer
                + " "
                + Manifest.sdkInitializer
                + "\n"
                + Manifest.forbiddenArtifactMarkers[0];
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
            public string plugin;
            public string pluginVersion;
            public string sourceArchiveSha256;
            public string vendoredFingerprint;
            public string sdkLoader;
            public string sdkInitializer;
            public string unityTemplate;
            public string[] requiredDefines;
            public string[] requiredArtifactMarkers;
            public string[] exactlyOnceArtifactMarkers;
            public string[] forbiddenArtifactMarkers;
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
