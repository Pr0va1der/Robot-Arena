using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RobotArena.WebGL.Editor
{
    public sealed class RobotArenaReleaseIntegrationCoordinates
    {
        public RobotArenaReleaseIntegrationCoordinates(
            string platformSdk,
            string pluginVersion,
            string pluginSourceArchiveSha256,
            string pluginVendoredFingerprint,
            string sdkLoader)
        {
            PlatformSdk = platformSdk ?? string.Empty;
            PluginVersion = pluginVersion ?? string.Empty;
            PluginSourceArchiveSha256 = pluginSourceArchiveSha256 ?? string.Empty;
            PluginVendoredFingerprint = pluginVendoredFingerprint ?? string.Empty;
            SdkLoader = sdkLoader ?? string.Empty;
        }

        public string PlatformSdk { get; }
        public string PluginVersion { get; }
        public string PluginSourceArchiveSha256 { get; }
        public string PluginVendoredFingerprint { get; }
        public string SdkLoader { get; }
    }

    public sealed class RobotArenaReleaseValidationResult
    {
        public RobotArenaReleaseValidationResult(
            RobotArenaReleaseIntegrationCoordinates integration,
            IEnumerable<string> errors)
        {
            Integration = integration;
            Errors = new List<string>(errors ?? new string[0]);
        }

        public RobotArenaReleaseIntegrationCoordinates Integration { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Integration != null && Errors.Count == 0;
    }

    public enum RobotArenaReleaseBuildOutcome
    {
        NotStarted,
        Succeeded,
        Failed,
        Cancelled,
        Unknown
    }

    public enum RobotArenaReleaseValidationStage
    {
        Prepare,
        Configuration,
        Settings,
        Build,
        Artifact,
        Archive,
        Package,
        Promotion
    }

    public sealed class RobotArenaReleaseBuildResult
    {
        public RobotArenaReleaseBuildResult(
            RobotArenaReleaseBuildOutcome outcome,
            ulong totalSize)
        {
            Outcome = outcome;
            TotalSize = totalSize;
        }

        public RobotArenaReleaseBuildOutcome Outcome { get; }
        public ulong TotalSize { get; }
        public bool Succeeded => Outcome == RobotArenaReleaseBuildOutcome.Succeeded;
    }

    public interface IRobotArenaWebGLReleaseOperations
    {
        void PreparePaths(string outputDirectory, string archivePath, string reportPath);

        RobotArenaReleaseValidationResult ValidateConfiguration(
            string defineSymbols,
            string pluginVersion,
            string templateSource);

        int ApplyTexturePolicy();

        RobotArenaReleaseBuildResult Build(string[] activeScenes, string outputDirectory);

        RobotArenaReleaseValidationResult ValidateArtifact(string outputDirectory);

        void CreateArchive(string outputDirectory, string archivePath);

        RobotArenaReleaseValidationResult ValidateArchive(string archivePath);

        WebGLPackageBudgetResult MeasurePackage(string archivePath, long limitBytes);
    }

    public sealed class RobotArenaWebGLReleaseBuildContext
    {
        public RobotArenaWebGLReleaseBuildContext(
            string outputDirectory,
            string archivePath,
            string reportPath,
            string defineSymbols,
            string pluginVersion,
            string templateSource,
            string templateName,
            IRobotArenaWebGLReleaseOperations operations)
        {
            OutputDirectory = Path.GetFullPath(outputDirectory);
            ArchivePath = Path.GetFullPath(archivePath);
            ReportPath = Path.GetFullPath(reportPath);
            DefineSymbols = defineSymbols ?? string.Empty;
            PluginVersion = pluginVersion ?? string.Empty;
            TemplateSource = templateSource ?? string.Empty;
            TemplateName = templateName ?? string.Empty;
            Operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public string OutputDirectory { get; }
        public string ArchivePath { get; }
        public string ReportPath { get; }
        public string DefineSymbols { get; }
        public string PluginVersion { get; }
        public string TemplateSource { get; }
        public string TemplateName { get; }
        public IRobotArenaWebGLReleaseOperations Operations { get; }
    }

    public static class RobotArenaWebGLReleaseBuild
    {
        private const string ReleaseOutput = "Build/WebGL/RobotArenaRelease";
        private const string ReleaseArchive = "Build/WebGL/RobotArenaRelease-upload.zip";
        private const string ReleaseReport = "Build/WebGL/RobotArenaRelease-report.json";
        private const string PluginYG2IntegrationManifestPath =
            "Tools/RobotArenaPluginYG2Integration.json";
        private const string ExpectedPluginYG2 = "PluginYG2";
        private const string ExpectedPluginYG2Version = "v2.0092";
        private const string ExpectedPluginYG2VersionFile =
            "Assets/PluginYourGames/Version.txt";
        private const string ExpectedPluginYG2TemplateFile =
            "Assets/WebGLTemplates/RobotArenaPluginYG2/index.html";
        private const string ExpectedPluginYG2UnityTemplate =
            "PROJECT:RobotArenaPluginYG2";
        private const string ExpectedPluginYG2VendorRoot =
            "Assets/PluginYourGames";
        private const string ExpectedPluginYG2Platform = "YandexGamesPlatform_yg";
        private const string ExpectedPluginYG2SdkLoader = "<script src=\"/sdk.js\"></script>";
        private const string ExpectedPluginYG2SdkInitializer = "YaGames.init()";
        private const string ExpectedPluginYG2SourceArchiveSha256 =
            "8A5CBD1DEA0CFB0772A8E28976663CD7D91E8594B2F70DB9E03E0AC682DFADC3";
        private const string ExpectedPluginYG2VendoredFingerprint =
            "AB4551EFDB23E1DC417598F406AF2997F16080BBBCF8E9FB08CDFAFF9763395F";
        private static readonly string[] ExpectedPluginYG2Modules =
            { "Core", "YandexGames", "EnvirData" };
        private static readonly string[] RequiredPluginYG2VendorFiles =
        {
            "Scripts/Basic/YG2.cs",
            "Scripts/Basic/GameReadyAPI.cs",
            "Platforms/YandexGames/Scripts/YandexGamePlatform.cs",
            "Platforms/YandexGames/Plugins/YandexGame.jslib",
            "Modules/EnvirData/Scripts/EnvirData_yg.cs",
            "Modules/EnvirData/Plugins/EnvirData.jslib"
        };
        private static readonly string[] ExpectedPluginYG2Defines =
        {
            "YandexGamesPlatform_yg",
            "ROBOTARENA_PLUGINYG2",
            "PLUGIN_YG_2",
            "EnvirData_yg"
        };
        private static readonly string[] ExpectedPluginYG2RequiredArtifactMarkers =
        {
            "game_api_pause",
            "game_api_resume",
            "RequestingEnvironmentData",
            "SetEnvirData",
            "PluginYG2 v2.0092"
        };
        private static readonly string[] ExpectedPluginYG2ExactlyOnceArtifactMarkers =
        {
            "game_api_pause",
            "game_api_resume"
        };
        private static readonly string[] ExpectedPluginYG2ForbiddenArtifactMarkers =
        {
            "RobotArenaPlatformProbe",
            "__robotArenaPlatformProbe",
            "RobotArenaPlatformProbe_"
        };

        [MenuItem("Robot Arena/Build WebGL release package")]
        public static void BuildReleasePackage()
        {
            RunReleasePackage(CreateProductionContext());
        }

        public static void RunReleasePackage(RobotArenaWebGLReleaseBuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            string outputDirectory = context.OutputDirectory;
            string archivePath = context.ArchivePath;
            string reportPath = context.ReportPath;
            string candidateOutputDirectory = GetCandidateOutputPath(outputDirectory);
            string candidateArchivePath = GetCandidateArchivePath(archivePath);
            string candidateReportPath = GetCandidateReportPath(reportPath);

            string previousTemplate = PlayerSettings.WebGL.template;
            WebGLTextureSubtarget previousSubtarget = EditorUserBuildSettings.webGLBuildSubtarget;
            bool previousDevelopment = EditorUserBuildSettings.development;
            bool previousDebugging = EditorUserBuildSettings.allowDebugging;
            bool previousProfiler = EditorUserBuildSettings.connectProfiler;
            WebGLDebugSymbolMode previousDebugSymbolMode = PlayerSettings.WebGL.debugSymbolMode;
            WebGLCompressionFormat previousCompressionFormat = PlayerSettings.WebGL.compressionFormat;
            bool previousDecompressionFallback = PlayerSettings.WebGL.decompressionFallback;
            bool previousStripEngineCode = PlayerSettings.stripEngineCode;
            ManagedStrippingLevel previousManagedStrippingLevel =
                PlayerSettings.GetManagedStrippingLevel(BuildTargetGroup.WebGL);

            RobotArenaReleaseBuildResult buildResult = null;
            WebGLPackageBudgetResult packageResult = null;
            RobotArenaReleaseValidationResult artifactValidation = null;
            RobotArenaReleaseValidationResult archiveValidation = null;
            var validationErrors = new List<string>();
            RobotArenaReleaseValidationStage validationStage =
                RobotArenaReleaseValidationStage.Configuration;
            int changedTextureCount = 0;
            bool releaseIsUploadReady = false;
            bool candidatePromoted = false;
            bool candidatePathsPrepared = false;

            try
            {
                validationStage = RobotArenaReleaseValidationStage.Prepare;
                context.Operations.PreparePaths(
                    candidateOutputDirectory,
                    candidateArchivePath,
                    candidateReportPath);
                candidatePathsPrepared = true;

                validationStage = RobotArenaReleaseValidationStage.Configuration;
                if (string.IsNullOrEmpty(context.TemplateName))
                {
                    validationErrors.Add("PluginYG2 Unity WebGL template is missing.");
                    throw new BuildFailedException(
                        "PluginYG2 release configuration is missing the Unity WebGL template.");
                }

                RobotArenaReleaseValidationResult configuration =
                    context.Operations.ValidateConfiguration(
                        context.DefineSymbols,
                        context.PluginVersion,
                        context.TemplateSource);
                if (configuration == null || !configuration.IsValid)
                {
                    AddValidationErrors(validationErrors, configuration);
                    throw new BuildFailedException(
                        "PluginYG2 release configuration is invalid: "
                        + string.Join(" | ", GetValidationErrors(configuration)));
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputDirectory));

                validationStage = RobotArenaReleaseValidationStage.Settings;
                PlayerSettings.WebGL.template = context.TemplateName;
                EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.DXT;
                EditorUserBuildSettings.development = false;
                EditorUserBuildSettings.allowDebugging = false;
                EditorUserBuildSettings.connectProfiler = false;
                PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.stripEngineCode = true;
                PlayerSettings.SetManagedStrippingLevel(
                    BuildTargetGroup.WebGL,
                    ManagedStrippingLevel.Low);

                string[] activeScenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
                if (activeScenes.Length == 0)
                {
                    throw new BuildFailedException("The WebGL release requires at least one enabled scene.");
                }

                changedTextureCount = context.Operations.ApplyTexturePolicy();
                Debug.Log(
                    "Robot Arena WebGL texture policy applied to "
                    + changedTextureCount
                    + " texture importers (WebGL max size "
                    + WebGLTextureImportPolicy.DefaultMaxTextureSize
                    + ", DXT5 Crunch).");

                validationStage = RobotArenaReleaseValidationStage.Build;
                buildResult = context.Operations.Build(activeScenes, candidateOutputDirectory);

                if (buildResult == null || !buildResult.Succeeded)
                {
                    RobotArenaReleaseBuildOutcome outcome = buildResult == null
                        ? RobotArenaReleaseBuildOutcome.NotStarted
                        : buildResult.Outcome;
                    throw new BuildFailedException(
                        "Robot Arena WebGL release build failed: "
                        + outcome);
                }

                validationStage = RobotArenaReleaseValidationStage.Artifact;
                artifactValidation = context.Operations.ValidateArtifact(candidateOutputDirectory);
                if (artifactValidation == null || !artifactValidation.IsValid)
                {
                    AddValidationErrors(validationErrors, artifactValidation);
                    throw new BuildFailedException(
                        "PluginYG2 post-processed WebGL artifact is invalid: "
                        + string.Join(" | ", GetValidationErrors(artifactValidation)));
                }

                validationStage = RobotArenaReleaseValidationStage.Archive;
                context.Operations.CreateArchive(candidateOutputDirectory, candidateArchivePath);

                archiveValidation = context.Operations.ValidateArchive(candidateArchivePath);
                if (archiveValidation == null || !archiveValidation.IsValid)
                {
                    AddValidationErrors(validationErrors, archiveValidation);
                    throw new BuildFailedException(
                        "PluginYG2 upload archive is invalid: "
                        + string.Join(" | ", GetValidationErrors(archiveValidation)));
                }

                validationStage = RobotArenaReleaseValidationStage.Package;
                packageResult = context.Operations.MeasurePackage(
                    candidateArchivePath,
                    WebGLPackageBudget.DefaultLimitBytes);

                if (packageResult == null || !packageResult.IsPassing)
                {
                    if (packageResult != null)
                    {
                        validationErrors.AddRange(packageResult.Errors);
                    }

                    throw new BuildFailedException(
                        "Robot Arena WebGL release package failed validation: "
                        + string.Join(
                            " | ",
                            packageResult == null
                                ? new[] { "Package measurement did not return a result." }
                                : packageResult.Errors));
                }

                validationStage = RobotArenaReleaseValidationStage.Promotion;
                releaseIsUploadReady = true;
                WriteReport(
                    candidateReportPath,
                    candidateOutputDirectory,
                    buildResult,
                    packageResult,
                    artifactValidation,
                    archiveValidation,
                    candidateArchivePath,
                    candidatePathsPrepared,
                    context.PluginVersion,
                    changedTextureCount,
                    validationStage,
                    validationErrors,
                    releaseIsUploadReady);
                PromoteSuccessfulCandidate(
                    candidateOutputDirectory,
                    candidateArchivePath,
                    candidateReportPath,
                    outputDirectory,
                    archivePath,
                    reportPath);
                candidatePromoted = true;

                Debug.Log(
                    "Robot Arena WebGL release package created: "
                    + archivePath
                    + " ("
                    + packageResult.UncompressedBytes
                    + " uncompressed bytes)");
            }
            catch (Exception exception)
            {
                releaseIsUploadReady = false;
                string failure = GetValidationStageName(validationStage) + ": " + exception.Message;
                if (!validationErrors.Contains(failure))
                {
                    validationErrors.Add(failure);
                }

                if (exception is BuildFailedException)
                {
                    throw;
                }

                throw new BuildFailedException(failure);
            }
            finally
            {
                try
                {
                    if (!candidatePromoted)
                    {
                        WriteReport(
                            candidateReportPath,
                            candidateOutputDirectory,
                            buildResult,
                            packageResult,
                            artifactValidation,
                            archiveValidation,
                            candidateArchivePath,
                            candidatePathsPrepared,
                            context.PluginVersion,
                            changedTextureCount,
                            validationStage,
                            validationErrors,
                            releaseIsUploadReady);
                    }
                }
                catch (Exception reportException)
                {
                    Debug.LogError(
                        "Robot Arena WebGL release report could not be written: "
                        + reportException.Message);
                }

                PlayerSettings.WebGL.template = previousTemplate;
                EditorUserBuildSettings.webGLBuildSubtarget = previousSubtarget;
                EditorUserBuildSettings.development = previousDevelopment;
                EditorUserBuildSettings.allowDebugging = previousDebugging;
                EditorUserBuildSettings.connectProfiler = previousProfiler;
                PlayerSettings.WebGL.debugSymbolMode = previousDebugSymbolMode;
                PlayerSettings.WebGL.compressionFormat = previousCompressionFormat;
                PlayerSettings.WebGL.decompressionFallback = previousDecompressionFallback;
                PlayerSettings.stripEngineCode = previousStripEngineCode;
                PlayerSettings.SetManagedStrippingLevel(
                    BuildTargetGroup.WebGL,
                    previousManagedStrippingLevel);

                if (!candidatePromoted)
                {
                    TryDeleteReleasePath(
                        candidateOutputDirectory,
                        directory: true,
                        requiredMarker: ".candidate");
                    TryDeleteReleasePath(
                        candidateArchivePath,
                        directory: false,
                        requiredMarker: ".candidate");
                }
            }
        }

        public static RobotArenaWebGLReleaseBuildContext CreateProductionContext()
        {
            string projectRoot = GetProjectRootPath();
            var manifestErrors = new List<string>();
            PluginYG2IntegrationManifest manifest = LoadPluginYG2Manifest(manifestErrors);
            string pluginVersionPath = Path.Combine(projectRoot, ExpectedPluginYG2VersionFile);
            string templatePath = Path.Combine(projectRoot, ExpectedPluginYG2TemplateFile);
            return new RobotArenaWebGLReleaseBuildContext(
                ReleaseOutput,
                ReleaseArchive,
                ReleaseReport,
                PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL),
                File.Exists(pluginVersionPath) ? File.ReadAllText(pluginVersionPath).Trim() : string.Empty,
                File.Exists(templatePath) ? File.ReadAllText(templatePath) : string.Empty,
                manifest == null ? string.Empty : manifest.unityTemplate,
                new UnityReleaseOperations());
        }

        private static void WriteReport(
            string reportPath,
            string outputDirectory,
            RobotArenaReleaseBuildResult buildResult,
            WebGLPackageBudgetResult packageResult,
            RobotArenaReleaseValidationResult artifactValidation,
            RobotArenaReleaseValidationResult archiveValidation,
            string archivePath,
            bool candidatePathsPrepared,
            string attemptedPluginVersion,
            int changedTextureCount,
            RobotArenaReleaseValidationStage validationStage,
            List<string> validationErrors,
            bool releaseIsUploadReady)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            List<ReleaseFileReport> files = new List<ReleaseFileReport>();
            if (candidatePathsPrepared && File.Exists(archivePath))
            {
                try
                {
                    using (ZipArchive archive = ZipFile.OpenRead(archivePath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            files.Add(new ReleaseFileReport
                            {
                                name = entry.FullName,
                                compressedBytes = entry.CompressedLength,
                                uncompressedBytes = entry.Length
                            });
                        }
                    }
                }
                catch (Exception exception)
                {
                    validationErrors.Add("report: archive entries unavailable: " + exception.Message);
                }
            }

            RobotArenaReleaseIntegrationCoordinates integration = artifactValidation == null
                ? archiveValidation == null ? null : archiveValidation.Integration
                : artifactValidation.Integration;
            if (integration == null)
            {
                var manifestErrors = new List<string>();
                integration = ToIntegrationCoordinates(LoadPluginYG2Manifest(manifestErrors));
            }
            integration = WithAttemptedPluginVersion(integration, attemptedPluginVersion);

            string artifactPath = Path.Combine(outputDirectory, "index.html");
            string artifactChecksum = candidatePathsPrepared
                ? TryComputeFileSha256(artifactPath)
                : string.Empty;
            string archiveChecksum = candidatePathsPrepared
                ? TryComputeFileSha256(archivePath)
                : string.Empty;
            string vendoredFingerprint = string.Empty;
            var reportManifestErrors = new List<string>();
            PluginYG2IntegrationManifest reportManifest = LoadPluginYG2Manifest(reportManifestErrors);
            string vendorRoot = reportManifest == null ||
                                !IsSafeRelativePath(reportManifest.vendorRoot)
                ? string.Empty
                : Path.Combine(GetProjectRootPath(), reportManifest.vendorRoot);
            if (Directory.Exists(vendorRoot))
            {
                try
                {
                    vendoredFingerprint = ComputePluginYG2VendoredFingerprint(vendorRoot);
                }
                catch (Exception exception)
                {
                    validationErrors.Add("report: vendored fingerprint unavailable: " + exception.Message);
                }
            }

            List<string> artifactErrors = artifactValidation == null
                ? new List<string>()
                : new List<string>(artifactValidation.Errors);
            List<string> archiveErrors = archiveValidation == null
                ? new List<string>()
                : new List<string>(archiveValidation.Errors);
            List<string> packageErrors = packageResult == null
                ? new List<string>()
                : new List<string>(packageResult.Errors);

            ReleaseBuildReport report = new ReleaseBuildReport
            {
                buildResult = buildResult == null
                    ? RobotArenaReleaseBuildOutcome.NotStarted.ToString()
                    : buildResult.Outcome.ToString(),
                unityBuildBytes = buildResult == null ? 0 : buildResult.TotalSize,
                packageUncompressedBytes = packageResult == null ? 0 : packageResult.UncompressedBytes,
                packageLimitBytes = packageResult == null
                    ? WebGLPackageBudget.DefaultLimitBytes
                    : packageResult.LimitBytes,
                packageIsValid = packageResult != null && packageResult.IsValid,
                packageIsWithinBudget = packageResult != null && packageResult.IsWithinBudget,
                packageIsPassing = packageResult != null && packageResult.IsPassing,
                texturePolicyMaxTextureSize = WebGLTextureImportPolicy.DefaultMaxTextureSize,
                texturePolicyFormat = TextureImporterFormat.DXT5Crunched.ToString(),
                texturePolicyCrunchQuality = WebGLTextureImportPolicy.CrunchQuality,
                texturePolicyChangedCount = changedTextureCount,
                platformSdk = integration == null ? string.Empty : integration.PlatformSdk,
                pluginVersion = integration == null ? string.Empty : integration.PluginVersion,
                pluginSourceArchiveSha256 = integration == null
                    ? string.Empty
                    : integration.PluginSourceArchiveSha256,
                pluginVendoredFingerprint = integration == null
                    ? string.Empty
                    : integration.PluginVendoredFingerprint,
                sdkLoader = integration == null ? string.Empty : integration.SdkLoader,
                validationStage = GetValidationStageName(validationStage),
                releaseIsUploadReady = releaseIsUploadReady,
                validationErrors = new List<string>(validationErrors),
                artifactIsValid = artifactValidation != null && artifactValidation.IsValid,
                artifactValidationCompleted = artifactValidation != null,
                artifactChecksumSha256 = artifactChecksum,
                artifactErrors = artifactErrors,
                archiveIsValid = archiveValidation != null && archiveValidation.IsValid,
                archiveValidationCompleted = archiveValidation != null,
                archiveChecksumSha256 = archiveChecksum,
                archiveErrors = archiveErrors,
                actualVendoredFingerprint = vendoredFingerprint,
                stripEngineCode = true,
                managedStrippingLevel = ManagedStrippingLevel.Low.ToString(),
                packageErrors = packageErrors,
                files = files
            };

            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        }

        private static void AddValidationErrors(
            List<string> destination,
            RobotArenaReleaseValidationResult validation)
        {
            if (validation == null || validation.Errors == null)
            {
                return;
            }

            destination.AddRange(validation.Errors);
        }

        private static IReadOnlyList<string> GetValidationErrors(
            RobotArenaReleaseValidationResult validation)
        {
            if (validation == null || validation.Errors == null || validation.Errors.Count == 0)
            {
                return new[] { "Validation did not return a result." };
            }

            return validation.Errors;
        }

        private static string GetValidationStageName(RobotArenaReleaseValidationStage stage)
        {
            return stage.ToString().ToLowerInvariant();
        }

        private static RobotArenaReleaseIntegrationCoordinates ToIntegrationCoordinates(
            PluginYG2IntegrationManifest manifest)
        {
            if (manifest == null)
            {
                return null;
            }

            return new RobotArenaReleaseIntegrationCoordinates(
                manifest.plugin,
                manifest.pluginVersion,
                manifest.sourceArchiveSha256,
                manifest.vendoredFingerprint,
                manifest.sdkLoader);
        }

        private static RobotArenaReleaseIntegrationCoordinates WithAttemptedPluginVersion(
            RobotArenaReleaseIntegrationCoordinates integration,
            string attemptedPluginVersion)
        {
            if (integration == null)
            {
                return null;
            }

            return new RobotArenaReleaseIntegrationCoordinates(
                integration.PlatformSdk,
                attemptedPluginVersion,
                integration.PluginSourceArchiveSha256,
                integration.PluginVendoredFingerprint,
                integration.SdkLoader);
        }

        private sealed class UnityReleaseOperations : IRobotArenaWebGLReleaseOperations
        {
            public void PreparePaths(string outputDirectory, string archivePath, string reportPath)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                TryDeleteReleasePath(
                    outputDirectory,
                    directory: true,
                    requiredMarker: ".candidate",
                    warnOnly: false);
                TryDeleteReleasePath(
                    archivePath,
                    directory: false,
                    requiredMarker: ".candidate",
                    warnOnly: false);
                TryDeleteReleasePath(
                    reportPath,
                    directory: false,
                    requiredMarker: ".candidate",
                    warnOnly: false);
            }

            public RobotArenaReleaseValidationResult ValidateConfiguration(
                string defineSymbols,
                string pluginVersion,
                string templateSource)
            {
                var manifestErrors = new List<string>();
                PluginYG2IntegrationManifest manifest = LoadPluginYG2Manifest(manifestErrors);
                List<string> errors = manifest == null
                    ? manifestErrors
                    : GetPluginYG2ConfigurationErrors(
                        defineSymbols,
                        pluginVersion,
                        templateSource,
                        manifest);
                return new RobotArenaReleaseValidationResult(
                    ToIntegrationCoordinates(manifest),
                    errors);
            }

            public int ApplyTexturePolicy()
            {
                return WebGLTextureImportPolicy.ApplyForRelease();
            }

            public RobotArenaReleaseBuildResult Build(string[] activeScenes, string outputDirectory)
            {
                BuildReport buildReport = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = activeScenes,
                    locationPathName = outputDirectory,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });
                return new RobotArenaReleaseBuildResult(
                    ToReleaseBuildOutcome(buildReport.summary.result),
                    buildReport.summary.totalSize);
            }

            private static RobotArenaReleaseBuildOutcome ToReleaseBuildOutcome(BuildResult result)
            {
                switch (result)
                {
                    case BuildResult.Succeeded:
                        return RobotArenaReleaseBuildOutcome.Succeeded;
                    case BuildResult.Failed:
                        return RobotArenaReleaseBuildOutcome.Failed;
                    case BuildResult.Cancelled:
                        return RobotArenaReleaseBuildOutcome.Cancelled;
                    default:
                        return RobotArenaReleaseBuildOutcome.Unknown;
                }
            }

            public RobotArenaReleaseValidationResult ValidateArtifact(string outputDirectory)
            {
                return ValidatePluginYG2ReleaseArtifact(outputDirectory);
            }

            public void CreateArchive(string outputDirectory, string archivePath)
            {
                ZipFile.CreateFromDirectory(
                    outputDirectory,
                    archivePath,
                    System.IO.Compression.CompressionLevel.Optimal,
                    includeBaseDirectory: false);
            }

            public RobotArenaReleaseValidationResult ValidateArchive(string archivePath)
            {
                return ValidatePluginYG2ReleaseArchive(archivePath);
            }

            public WebGLPackageBudgetResult MeasurePackage(string archivePath, long limitBytes)
            {
                return WebGLPackageBudget.Measure(archivePath, limitBytes);
            }
        }

        public static List<string> GetPluginYG2ConfigurationErrors(
            string defineSymbols,
            string pluginVersion,
            string templateSource)
        {
            var manifestErrors = new List<string>();
            PluginYG2IntegrationManifest manifest = LoadPluginYG2Manifest(manifestErrors);
            if (manifest == null)
            {
                return manifestErrors;
            }

            return GetPluginYG2ConfigurationErrors(
                defineSymbols,
                pluginVersion,
                templateSource,
                manifest);
        }

        public static List<string> GetPluginYG2ManifestPolicyErrors(string manifestJson)
        {
            var errors = new List<string>();
            PluginYG2IntegrationManifest manifest = ParsePluginYG2Manifest(manifestJson, errors);
            if (manifest == null)
            {
                return errors;
            }

            errors.AddRange(GetPluginYG2ManifestErrors(manifest));
            return errors;
        }

        public static List<string> GetPluginYG2ArtifactErrors(string artifactSource)
        {
            var manifestErrors = new List<string>();
            PluginYG2IntegrationManifest manifest = LoadPluginYG2Manifest(manifestErrors);
            if (manifest == null)
            {
                return manifestErrors;
            }

            manifestErrors.AddRange(GetPluginYG2ManifestErrors(manifest));
            if (manifestErrors.Count > 0)
            {
                return manifestErrors;
            }

            return ValidatePluginYG2ArtifactEntries(
                manifest,
                new[]
                {
                    new PluginYG2ArtifactEntry("artifact", artifactSource ?? string.Empty)
                },
                requireRootIndex: false);
        }

        public static List<string> GetPluginYG2ArtifactDirectoryErrors(string outputDirectory)
        {
            return new List<string>(ValidatePluginYG2ReleaseArtifact(outputDirectory).Errors);
        }

        public static List<string> GetPluginYG2ArtifactArchiveErrors(string archivePath)
        {
            return new List<string>(ValidatePluginYG2ReleaseArchive(archivePath).Errors);
        }

        private static List<string> GetPluginYG2ForbiddenArtifactErrors(
            PluginYG2IntegrationManifest manifest,
            string source)
        {
            var errors = new List<string>();
            if (manifest == null)
            {
                return errors;
            }

            foreach (string marker in manifest.forbiddenArtifactMarkers ?? new string[0])
            {
                if (!string.IsNullOrEmpty(marker) && source.Contains(marker))
                {
                    errors.Add("PluginYG2 artifact contains the legacy custom bridge marker: " + marker);
                }
            }

            return errors;
        }

        private static List<string> ValidatePluginYG2ArtifactEntries(
            PluginYG2IntegrationManifest manifest,
            IEnumerable<PluginYG2ArtifactEntry> entries,
            bool requireRootIndex)
        {
            var errors = new List<string>();
            int indexEntryCount = 0;
            int loaderOccurrences = 0;
            int initializerOccurrences = 0;
            var markerOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (string marker in manifest.requiredArtifactMarkers ?? new string[0])
            {
                if (!string.IsNullOrEmpty(marker))
                {
                    markerOccurrences[marker] = 0;
                }
            }

            foreach (PluginYG2ArtifactEntry entry in entries ?? new PluginYG2ArtifactEntry[0])
            {
                string entryName = entry == null ? string.Empty : entry.Name;
                string rawSource = entry == null ? string.Empty : entry.Source;
                if (string.Equals(entryName, "index.html", StringComparison.Ordinal))
                {
                    indexEntryCount++;
                }

                errors.AddRange(GetPluginYG2ForbiddenArtifactErrors(manifest, rawSource));
                string source = RemoveArtifactComments(rawSource);
                loaderOccurrences += CountOccurrences(source, manifest.sdkLoader);
                initializerOccurrences += CountOccurrences(source, manifest.sdkInitializer);

                foreach (string marker in new List<string>(markerOccurrences.Keys))
                {
                    markerOccurrences[marker] += CountOccurrences(source, marker);
                }
            }

            if (requireRootIndex && indexEntryCount == 0)
            {
                errors.Add("PluginYG2 release artifact is missing root index.html.");
            }
            else if (requireRootIndex && indexEntryCount > 1)
            {
                errors.Add("PluginYG2 release artifact contains more than one root index.html.");
            }

            AddPluginYG2ArtifactWideErrors(
                manifest,
                loaderOccurrences,
                initializerOccurrences,
                errors);

            var exactlyOnceMarkers = new HashSet<string>(
                manifest.exactlyOnceArtifactMarkers ?? new string[0],
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> marker in markerOccurrences)
            {
                bool exactlyOnce = exactlyOnceMarkers.Contains(marker.Key);
                if ((exactlyOnce && marker.Value != 1) ||
                    (!exactlyOnce && marker.Value == 0))
                {
                    errors.Add(
                        exactlyOnce
                            ? "PluginYG2 artifact must contain exactly one marker: " + marker.Key
                            : "PluginYG2 artifact is missing required marker: " + marker.Key);
                }
            }

            return errors;
        }

        private static List<string> GetPluginYG2ConfigurationErrors(
            string defineSymbols,
            string pluginVersion,
            string templateSource,
            PluginYG2IntegrationManifest manifest)
        {
            var errors = new List<string>();
            errors.AddRange(GetPluginYG2ManifestErrors(manifest));
            if (manifest == null || errors.Count > 0)
            {
                return errors;
            }

            errors.AddRange(GetPluginYG2VendoredErrors(manifest));

            var configuredDefines = new HashSet<string>(
                (defineSymbols ?? string.Empty).Split(
                    new[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);

            foreach (string requiredDefine in manifest.requiredDefines ?? new string[0])
            {
                if (!configuredDefines.Contains(requiredDefine))
                {
                    errors.Add("WebGL scripting define is missing: " + requiredDefine);
                }
            }

            if (!string.Equals(
                    pluginVersion,
                    manifest.pluginVersion,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Official "
                    + manifest.plugin
                    + " version must be "
                    + manifest.pluginVersion
                    + ", got "
                    + (string.IsNullOrEmpty(pluginVersion) ? "<missing>" : pluginVersion));
            }

            if (CountOccurrences(templateSource, manifest.sdkLoader) != 1)
            {
                errors.Add(
                    "Plugin template must contain exactly one SDK loader: "
                    + manifest.sdkLoader
                    + ".");
            }

            if (CountOccurrences(templateSource, manifest.sdkInitializer) != 1)
            {
                errors.Add(
                    "Plugin template must contain exactly one SDK initializer: "
                    + manifest.sdkInitializer
                    + ".");
            }

            foreach (string marker in manifest.forbiddenArtifactMarkers ?? new string[0])
            {
                if (!string.IsNullOrEmpty(marker) && templateSource.Contains(marker))
                {
                    errors.Add(
                        "The legacy custom bridge must not be present in the PluginYG2 template: "
                        + marker);
                }
            }

            return errors;
        }

        private static RobotArenaReleaseValidationResult ValidatePluginYG2ReleaseArtifact(
            string outputDirectory)
        {
            var manifestErrors = new List<string>();
            PluginYG2IntegrationManifest manifest = LoadPluginYG2Manifest(manifestErrors);
            if (manifest == null)
            {
                return new RobotArenaReleaseValidationResult(null, manifestErrors);
            }

            manifestErrors.AddRange(GetPluginYG2ManifestErrors(manifest));
            if (manifestErrors.Count > 0)
            {
                return new RobotArenaReleaseValidationResult(
                    ToIntegrationCoordinates(manifest),
                    manifestErrors);
            }

            if (!Directory.Exists(outputDirectory))
            {
                manifestErrors.Add("PluginYG2 release artifact directory is missing: " + outputDirectory);
                return new RobotArenaReleaseValidationResult(
                    ToIntegrationCoordinates(manifest),
                    manifestErrors);
            }

            var entries = new List<PluginYG2ArtifactEntry>();
            foreach (string filePath in Directory.GetFiles(
                         outputDirectory,
                         "*",
                         SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(filePath);
                if (!string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relativePath = filePath.Substring(outputDirectory.Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                entries.Add(new PluginYG2ArtifactEntry(relativePath, File.ReadAllText(filePath)));
            }

            List<string> validationErrors = ValidatePluginYG2ArtifactEntries(
                manifest,
                entries,
                requireRootIndex: true);
            manifestErrors.AddRange(validationErrors);
            return new RobotArenaReleaseValidationResult(
                ToIntegrationCoordinates(manifest),
                manifestErrors);
        }

        private static RobotArenaReleaseValidationResult ValidatePluginYG2ReleaseArchive(
            string archivePath)
        {
            var errors = new List<string>();
            PluginYG2IntegrationManifest manifest = LoadPluginYG2Manifest(errors);
            if (manifest == null)
            {
                return new RobotArenaReleaseValidationResult(null, errors);
            }

            errors.AddRange(GetPluginYG2ManifestErrors(manifest));
            if (errors.Count > 0)
            {
                return new RobotArenaReleaseValidationResult(
                    ToIntegrationCoordinates(manifest),
                    errors);
            }

            if (!File.Exists(archivePath))
            {
                errors.Add("PluginYG2 upload archive is missing.");
                return new RobotArenaReleaseValidationResult(
                    ToIntegrationCoordinates(manifest),
                    errors);
            }

            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                var entries = new List<PluginYG2ArtifactEntry>();
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    string extension = Path.GetExtension(entry.FullName);
                    if (!string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using (StreamReader reader = new StreamReader(entry.Open()))
                    {
                        entries.Add(new PluginYG2ArtifactEntry(entry.FullName, reader.ReadToEnd()));
                    }
                }

                List<string> validationErrors = ValidatePluginYG2ArtifactEntries(
                    manifest,
                    entries,
                    requireRootIndex: true);
                errors.AddRange(validationErrors);
            }

            return new RobotArenaReleaseValidationResult(
                ToIntegrationCoordinates(manifest),
                errors);
        }

        private static PluginYG2IntegrationManifest LoadPluginYG2Manifest(
            List<string> errors)
        {
            string manifestPath = Path.Combine(
                GetProjectRootPath(),
                PluginYG2IntegrationManifestPath);
            if (!File.Exists(manifestPath))
            {
                errors.Add("Manifest file is missing: " + PluginYG2IntegrationManifestPath);
                return null;
            }

            string manifestJson;
            try
            {
                manifestJson = File.ReadAllText(manifestPath);
            }
            catch (Exception exception)
            {
                errors.Add("Manifest file cannot be read: " + exception.Message);
                return null;
            }

            return ParsePluginYG2Manifest(manifestJson, errors);
        }

        private static PluginYG2IntegrationManifest ParsePluginYG2Manifest(
            string manifestJson,
            List<string> errors)
        {
            try
            {
                PluginYG2IntegrationManifest manifest = JsonUtility.FromJson<PluginYG2IntegrationManifest>(
                    manifestJson ?? string.Empty);
                if (manifest == null)
                {
                    errors.Add("Manifest JSON is empty.");
                    return null;
                }

                return manifest;
            }
            catch (Exception exception)
            {
                errors.Add("Manifest JSON cannot be parsed: " + exception.Message);
                return null;
            }
        }

        private static List<string> GetPluginYG2ManifestErrors(
            PluginYG2IntegrationManifest manifest)
        {
            var errors = new List<string>();
            if (manifest == null)
            {
                errors.Add("Manifest is missing.");
                return errors;
            }

            AddRequiredManifestString(errors, "plugin", manifest.plugin);
            AddRequiredManifestString(errors, "pluginVersion", manifest.pluginVersion);
            AddRequiredManifestString(errors, "versionFile", manifest.versionFile);
            AddRequiredManifestString(errors, "templateFile", manifest.templateFile);
            AddRequiredManifestString(errors, "unityTemplate", manifest.unityTemplate);
            AddRequiredManifestString(errors, "vendorRoot", manifest.vendorRoot);
            AddRequiredManifestString(errors, "platform", manifest.platform);
            AddRequiredManifestString(errors, "sdkLoader", manifest.sdkLoader);
            AddRequiredManifestString(errors, "sdkInitializer", manifest.sdkInitializer);
            ValidateRelativeManifestPath(errors, "versionFile", manifest.versionFile);
            ValidateRelativeManifestPath(errors, "templateFile", manifest.templateFile);
            ValidateRelativeManifestPath(errors, "vendorRoot", manifest.vendorRoot);

            if (!string.Equals(manifest.plugin, ExpectedPluginYG2, StringComparison.Ordinal))
            {
                errors.Add("Manifest plugin must be PluginYG2.");
            }

            if (!string.Equals(
                    manifest.pluginVersion,
                    ExpectedPluginYG2Version,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Manifest pluginVersion must be the pinned official version: "
                    + ExpectedPluginYG2Version
                    + ".");
            }

            if (!string.Equals(
                    manifest.versionFile,
                    ExpectedPluginYG2VersionFile,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Manifest versionFile must be the pinned PluginYG2 version path: "
                    + ExpectedPluginYG2VersionFile
                    + ".");
            }

            if (!string.Equals(
                    manifest.templateFile,
                    ExpectedPluginYG2TemplateFile,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Manifest templateFile must be the pinned PluginYG2 template path: "
                    + ExpectedPluginYG2TemplateFile
                    + ".");
            }

            if (!string.Equals(
                    manifest.unityTemplate,
                    ExpectedPluginYG2UnityTemplate,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Manifest unityTemplate must be the pinned PluginYG2 template: "
                    + ExpectedPluginYG2UnityTemplate
                    + ".");
            }

            if (!string.Equals(
                    manifest.vendorRoot,
                    ExpectedPluginYG2VendorRoot,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Manifest vendorRoot must be the pinned PluginYG2 vendor root: "
                    + ExpectedPluginYG2VendorRoot
                    + ".");
            }

            if (!string.Equals(
                    manifest.platform,
                    ExpectedPluginYG2Platform,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Manifest platform must be the official PluginYG2 platform: "
                    + ExpectedPluginYG2Platform
                    + ".");
            }

            if (!IsSha256(manifest.sourceArchiveSha256))
            {
                errors.Add("Manifest sourceArchiveSha256 must be a 64-character hexadecimal hash.");
            }
            else if (!string.Equals(
                         manifest.sourceArchiveSha256,
                         ExpectedPluginYG2SourceArchiveSha256,
                         StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    "Manifest sourceArchiveSha256 does not match the pinned PluginYG2 archive receipt.");
            }

            if (!IsSha256(manifest.vendoredFingerprint))
            {
                errors.Add("Manifest vendoredFingerprint must be a 64-character hexadecimal hash.");
            }
            else if (!string.Equals(
                         manifest.vendoredFingerprint,
                         ExpectedPluginYG2VendoredFingerprint,
                         StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    "Manifest vendoredFingerprint does not match the pinned PluginYG2 import receipt.");
            }

            ValidateManifestList(errors, "modules", manifest.modules);
            ValidateManifestList(errors, "requiredVendorFiles", manifest.requiredVendorFiles);
            ValidateManifestList(errors, "requiredDefines", manifest.requiredDefines);
            ValidateManifestList(errors, "requiredArtifactMarkers", manifest.requiredArtifactMarkers);
            ValidateManifestList(errors, "exactlyOnceArtifactMarkers", manifest.exactlyOnceArtifactMarkers);
            ValidateManifestList(
                errors,
                "forbiddenArtifactMarkers",
                manifest.forbiddenArtifactMarkers,
                required: true);

            foreach (string requiredVendorFile in manifest.requiredVendorFiles ?? new string[0])
            {
                ValidateRelativeManifestPath(
                    errors,
                    "requiredVendorFiles entry",
                    requiredVendorFile);
            }

            if (!HasExactValues(manifest.modules, ExpectedPluginYG2Modules))
            {
                errors.Add(
                    "Manifest modules must contain only Core, YandexGames, and EnvirData.");
            }

            if (!HasExactValues(
                    manifest.requiredVendorFiles,
                    RequiredPluginYG2VendorFiles))
            {
                errors.Add(
                    "Manifest requiredVendorFiles do not match the PluginYG2 integration policy.");
            }

            if (!HasExactValues(manifest.requiredDefines, ExpectedPluginYG2Defines))
            {
                errors.Add(
                    "Manifest requiredDefines do not match the pinned PluginYG2 defines.");
            }

            if (!string.Equals(
                    manifest.sdkLoader,
                    ExpectedPluginYG2SdkLoader,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Manifest sdkLoader must be the official PluginYG2 loader: "
                    + ExpectedPluginYG2SdkLoader
                    + ".");
            }

            if (!string.Equals(
                    manifest.sdkInitializer,
                    ExpectedPluginYG2SdkInitializer,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Manifest sdkInitializer must be the official PluginYG2 initializer: "
                    + ExpectedPluginYG2SdkInitializer
                    + ".");
            }

            if (!HasExactValues(
                    manifest.requiredArtifactMarkers,
                    ExpectedPluginYG2RequiredArtifactMarkers))
            {
                errors.Add(
                    "Manifest requiredArtifactMarkers do not match the PluginYG2 invariants.");
            }

            if (!HasExactValues(
                    manifest.exactlyOnceArtifactMarkers,
                    ExpectedPluginYG2ExactlyOnceArtifactMarkers))
            {
                errors.Add(
                    "Manifest exactlyOnceArtifactMarkers do not match the PluginYG2 invariants.");
            }

            if (!HasExactValues(
                    manifest.forbiddenArtifactMarkers,
                    ExpectedPluginYG2ForbiddenArtifactMarkers))
            {
                errors.Add(
                    "Manifest forbiddenArtifactMarkers do not match the legacy bridge policy.");
            }

            if (!string.IsNullOrEmpty(manifest.platform) &&
                Array.IndexOf(manifest.requiredDefines ?? new string[0], manifest.platform) < 0)
            {
                errors.Add(
                    "Manifest platform must also be listed in requiredDefines: "
                    + manifest.platform
                    + ".");
            }

            foreach (string marker in manifest.exactlyOnceArtifactMarkers ?? new string[0])
            {
                if (Array.IndexOf(manifest.requiredArtifactMarkers ?? new string[0], marker) < 0)
                {
                    errors.Add(
                        "Manifest exactlyOnceArtifactMarkers must also be listed in "
                        + "requiredArtifactMarkers: "
                        + marker
                        + ".");
                }
            }

            return errors;
        }

        private static List<string> GetPluginYG2VendoredErrors(
            PluginYG2IntegrationManifest manifest)
        {
            var errors = new List<string>();
            string vendorRoot = Path.Combine(GetProjectRootPath(), ExpectedPluginYG2VendorRoot);
            if (!Directory.Exists(vendorRoot))
            {
                errors.Add(
                    "Vendored PluginYG2 directory is missing: "
                    + ExpectedPluginYG2VendorRoot
                    + ".");
                return errors;
            }

            foreach (string relativePath in RequiredPluginYG2VendorFiles)
            {
                if (!File.Exists(Path.Combine(vendorRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))))
                {
                    errors.Add("Vendored PluginYG2 file is missing: " + relativePath);
                }
            }

            string actualFingerprint = ComputePluginYG2VendoredFingerprint(vendorRoot);
            if (!string.Equals(
                         actualFingerprint,
                         manifest.vendoredFingerprint,
                         StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    "Vendored PluginYG2 fingerprint mismatch: expected "
                    + manifest.vendoredFingerprint
                    + ", got "
                    + actualFingerprint
                    + ".");
            }

            string modulesRoot = Path.Combine(vendorRoot, "Modules");
            if (Directory.Exists(modulesRoot))
            {
                foreach (DirectoryInfo moduleDirectory in new DirectoryInfo(modulesRoot).GetDirectories())
                {
                    bool isDeclaredModule = false;
                    foreach (string module in ExpectedPluginYG2Modules)
                    {
                        if (string.Equals(moduleDirectory.Name, module, StringComparison.Ordinal))
                        {
                            isDeclaredModule = true;
                            break;
                        }
                    }

                    if (!isDeclaredModule)
                    {
                        errors.Add(
                            "Unsupported PluginYG2 module directory is present: "
                            + moduleDirectory.Name);
                    }
                }
            }

            return errors;
        }

        private static string ComputePluginYG2VendoredFingerprint(string vendorRoot)
        {
            var relativeFiles = new List<string>();
            foreach (string filePath in Directory.GetFiles(vendorRoot, "*", SearchOption.AllDirectories))
            {
                string relativePath = filePath.Substring(vendorRoot.Length + 1)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                if (relativePath == "Editor/BuildLogYG2.txt" ||
                    relativePath == "Editor/PluginPrefs.json")
                {
                    continue;
                }

                relativeFiles.Add(relativePath);
            }

            relativeFiles.Sort(StringComparer.Ordinal);
            using (SHA256 hash = SHA256.Create())
            {
                foreach (string relativePath in relativeFiles)
                {
                    byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
                    byte[] contentBytes = File.ReadAllBytes(
                        Path.Combine(vendorRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                    hash.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);
                    hash.TransformBlock(new byte[] { 0 }, 0, 1, new byte[] { 0 }, 0);
                    hash.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
                    hash.TransformBlock(new byte[] { 0 }, 0, 1, new byte[] { 0 }, 0);
                }

                hash.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(hash.Hash).Replace("-", string.Empty);
            }
        }

        private static string TryComputeFileSha256(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return string.Empty;
            }

            try
            {
                using (SHA256 hash = SHA256.Create())
                using (FileStream stream = File.OpenRead(filePath))
                {
                    return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
                }
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
            {
                return false;
            }

            foreach (char character in value)
            {
                bool isDigit = character >= '0' && character <= '9';
                bool isLowerHex = character >= 'a' && character <= 'f';
                bool isUpperHex = character >= 'A' && character <= 'F';
                if (!isDigit && !isLowerHex && !isUpperHex)
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddRequiredManifestString(
            List<string> errors,
            string fieldName,
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                errors.Add("Manifest " + fieldName + " is required.");
            }
        }

        private static void ValidateRelativeManifestPath(
            List<string> errors,
            string fieldName,
            string value)
        {
            if (!string.IsNullOrEmpty(value) && !IsSafeRelativePath(value))
            {
                errors.Add(
                    "Manifest "
                    + fieldName
                    + " must be a relative path without '..': "
                    + value
                    + ".");
            }
        }

        private static void ValidateManifestList(
            List<string> errors,
            string fieldName,
            string[] values,
            bool required = true)
        {
            if (values == null)
            {
                if (required)
                {
                    errors.Add("Manifest " + fieldName + " is required.");
                }

                return;
            }

            if (required && values.Length == 0)
            {
                errors.Add("Manifest " + fieldName + " is required.");
            }

            foreach (string value in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    errors.Add("Manifest " + fieldName + " must contain non-empty strings.");
                    break;
                }
            }

            var uniqueValues = new HashSet<string>(values, StringComparer.Ordinal);
            if (uniqueValues.Count != values.Length)
            {
                errors.Add("Manifest " + fieldName + " must not contain duplicates.");
            }
        }

        private static bool HasExactValues(string[] actual, string[] expected)
        {
            if (actual == null || expected == null || actual.Length != expected.Length)
            {
                return false;
            }

            var actualValues = new HashSet<string>(actual, StringComparer.Ordinal);
            if (actualValues.Count != expected.Length)
            {
                return false;
            }

            foreach (string expectedValue in expected)
            {
                if (!actualValues.Contains(expectedValue))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddPluginYG2ArtifactWideErrors(
            PluginYG2IntegrationManifest manifest,
            int loaderOccurrences,
            int initializerOccurrences,
            List<string> errors)
        {
            if (manifest == null)
            {
                return;
            }

            if (loaderOccurrences != 1)
            {
                errors.Add(
                    "PluginYG2 artifact must contain exactly one SDK loader across all generated files: "
                    + manifest.sdkLoader
                    + ".");
            }

            if (initializerOccurrences != 1)
            {
                errors.Add(
                    "PluginYG2 artifact must contain exactly one SDK initializer across all generated files: "
                    + manifest.sdkInitializer
                    + ".");
            }
        }

        private static string GetProjectRootPath()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static bool IsSafeRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path))
            {
                return false;
            }

            string normalizedPath = path.Replace('\\', '/');
            foreach (string segment in normalizedPath.Split('/'))
            {
                if (segment == "..")
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountOccurrences(string source, string value)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int count = 0;
            int searchIndex = 0;
            while ((searchIndex = source.IndexOf(
                       value,
                       searchIndex,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                searchIndex += value.Length;
            }

            return count;
        }

        private static string RemoveArtifactComments(string source)
        {
            var result = new StringBuilder(source.Length);
            bool inSingleQuotedString = false;
            bool inDoubleQuotedString = false;
            bool inTemplateString = false;
            bool escaped = false;
            for (int index = 0; index < source.Length; index++)
            {
                char current = source[index];
                char next = index + 1 < source.Length ? source[index + 1] : '\0';

                if (!inSingleQuotedString && !inDoubleQuotedString && !inTemplateString)
                {
                    if (current == '<' && next == '!' &&
                        index + 3 < source.Length && source[index + 2] == '-' && source[index + 3] == '-')
                    {
                        index += 3;
                        while (index + 2 < source.Length &&
                               !(source[index] == '-' && source[index + 1] == '-' && source[index + 2] == '>'))
                        {
                            index++;
                        }

                        index = Math.Min(index + 2, source.Length - 1);
                        continue;
                    }

                    if (current == '/' && next == '/')
                    {
                        index++;
                        while (index + 1 < source.Length && source[index + 1] != '\n')
                        {
                            index++;
                        }

                        continue;
                    }

                    if (current == '/' && next == '*')
                    {
                        index += 2;
                        while (index + 1 < source.Length &&
                               !(source[index] == '*' && source[index + 1] == '/'))
                        {
                            index++;
                        }

                        index = Math.Min(index + 1, source.Length - 1);
                        continue;
                    }
                }

                result.Append(current);
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if ((inSingleQuotedString || inDoubleQuotedString || inTemplateString) && current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (!inDoubleQuotedString && !inTemplateString && current == '\'')
                {
                    inSingleQuotedString = !inSingleQuotedString;
                }
                else if (!inSingleQuotedString && !inTemplateString && current == '"')
                {
                    inDoubleQuotedString = !inDoubleQuotedString;
                }
                else if (!inSingleQuotedString && !inDoubleQuotedString && current == '`')
                {
                    inTemplateString = !inTemplateString;
                }
            }

            return result.ToString();
        }

        private static string GetCandidateOutputPath(string outputDirectory)
        {
            return outputDirectory + ".candidate";
        }

        private static string GetCandidateArchivePath(string archivePath)
        {
            return archivePath + ".candidate";
        }

        private static string GetCandidateReportPath(string reportPath)
        {
            return reportPath + ".candidate";
        }

        private static void PromoteSuccessfulCandidate(
            string candidateOutputDirectory,
            string candidateArchivePath,
            string candidateReportPath,
            string outputDirectory,
            string archivePath,
            string reportPath)
        {
            if (!Directory.Exists(candidateOutputDirectory))
            {
                throw new InvalidOperationException(
                    "Successful release candidate directory is missing: "
                    + candidateOutputDirectory);
            }

            if (!File.Exists(candidateArchivePath))
            {
                throw new InvalidOperationException(
                    "Successful release candidate archive is missing: "
                    + candidateArchivePath);
            }

            if (!File.Exists(candidateReportPath))
            {
                throw new InvalidOperationException(
                    "Successful release candidate report is missing: "
                    + candidateReportPath);
            }

            string suffix = ".backup-" + Guid.NewGuid().ToString("N");
            string outputBackupPath = outputDirectory + suffix;
            string archiveBackupPath = archivePath + suffix;
            string reportBackupPath = reportPath + suffix;
            bool outputBackupCreated = false;
            bool archiveBackupCreated = false;
            bool reportBackupCreated = false;
            bool outputPromoted = false;
            bool archivePromoted = false;
            bool reportPromoted = false;

            try
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Move(outputDirectory, outputBackupPath);
                    outputBackupCreated = true;
                }

                if (File.Exists(archivePath))
                {
                    File.Move(archivePath, archiveBackupPath);
                    archiveBackupCreated = true;
                }

                if (File.Exists(reportPath))
                {
                    File.Move(reportPath, reportBackupPath);
                    reportBackupCreated = true;
                }

                Directory.Move(candidateOutputDirectory, outputDirectory);
                outputPromoted = true;
                File.Move(candidateArchivePath, archivePath);
                archivePromoted = true;
                File.Move(candidateReportPath, reportPath);
                reportPromoted = true;
            }
            catch
            {
                if (reportPromoted && File.Exists(reportPath))
                {
                    File.Delete(reportPath);
                }

                if (archivePromoted && File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }

                if (outputPromoted && Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, recursive: true);
                }

                if (archiveBackupCreated && File.Exists(archiveBackupPath))
                {
                    File.Move(archiveBackupPath, archivePath);
                }

                if (reportBackupCreated && File.Exists(reportBackupPath))
                {
                    File.Move(reportBackupPath, reportPath);
                }

                if (outputBackupCreated && Directory.Exists(outputBackupPath))
                {
                    Directory.Move(outputBackupPath, outputDirectory);
                }

                throw;
            }

            TryDeleteReleasePath(
                outputBackupPath,
                directory: true,
                requiredMarker: ".backup-");
            TryDeleteReleasePath(
                archiveBackupPath,
                directory: false,
                requiredMarker: ".backup-");
            TryDeleteReleasePath(
                reportBackupPath,
                directory: false,
                requiredMarker: ".backup-");
        }

        private static void TryDeleteReleasePath(
            string path,
            bool directory,
            string requiredMarker,
            bool warnOnly = true)
        {
            if (!IsAllowedReleaseCleanupPath(path, requiredMarker))
            {
                throw new InvalidOperationException(
                    "Refusing to delete a release path outside the allowed cleanup namespace: "
                    + path);
            }

            try
            {
                if (directory)
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive: true);
                    }
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                if (!warnOnly)
                {
                    throw;
                }

                Debug.LogWarning(
                    "Could not remove release cleanup path: "
                    + exception.Message);
            }
        }

        private static bool IsAllowedReleaseCleanupPath(
            string path,
            string requiredMarker)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(requiredMarker))
            {
                return false;
            }

            string fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            if (string.Equals(requiredMarker, ".candidate", StringComparison.OrdinalIgnoreCase))
            {
                return fileName.EndsWith(requiredMarker, StringComparison.OrdinalIgnoreCase);
            }

            int markerIndex = fileName.LastIndexOf(
                requiredMarker,
                StringComparison.OrdinalIgnoreCase);
            return markerIndex >= 0 &&
                   markerIndex + requiredMarker.Length < fileName.Length;
        }

        [Serializable]
        private sealed class ReleaseBuildReport
        {
            public string buildResult;
            public ulong unityBuildBytes;
            public long packageUncompressedBytes;
            public long packageLimitBytes;
            public bool packageIsValid;
            public bool packageIsWithinBudget;
            public bool packageIsPassing;
            public int texturePolicyMaxTextureSize;
            public string texturePolicyFormat;
            public int texturePolicyCrunchQuality;
            public int texturePolicyChangedCount;
            public string platformSdk;
            public string pluginVersion;
            public string pluginSourceArchiveSha256;
            public string pluginVendoredFingerprint;
            public string sdkLoader;
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
            public string actualVendoredFingerprint;
            public List<string> archiveErrors;
            public bool stripEngineCode;
            public string managedStrippingLevel;
            public List<string> packageErrors;
            public List<ReleaseFileReport> files;
        }

        [Serializable]
        private sealed class ReleaseFileReport
        {
            public string name;
            public long compressedBytes;
            public long uncompressedBytes;
        }

        [Serializable]
        private sealed class PluginYG2IntegrationManifest
        {
            public string plugin;
            public string pluginVersion;
            public string sourceArchiveSha256;
            public string vendoredFingerprint;
            public string versionFile;
            public string templateFile;
            public string unityTemplate;
            public string vendorRoot;
            public string platform;
            public string[] modules;
            public string[] requiredVendorFiles;
            public string[] requiredDefines;
            public string sdkLoader;
            public string sdkInitializer;
            public string[] requiredArtifactMarkers;
            public string[] exactlyOnceArtifactMarkers;
            public string[] forbiddenArtifactMarkers;
        }

        private sealed class PluginYG2ArtifactEntry
        {
            public PluginYG2ArtifactEntry(string name, string source)
            {
                Name = name ?? string.Empty;
                Source = source ?? string.Empty;
            }

            public string Name { get; }
            public string Source { get; }
        }
    }
}
