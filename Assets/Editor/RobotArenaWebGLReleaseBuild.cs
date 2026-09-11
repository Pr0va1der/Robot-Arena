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

    public sealed class RobotArenaReleaseBuildResult
    {
        public RobotArenaReleaseBuildResult(string result, ulong totalSize)
        {
            Result = string.IsNullOrEmpty(result) ? "NotStarted" : result;
            TotalSize = totalSize;
        }

        public string Result { get; }
        public ulong TotalSize { get; }
        public bool Succeeded => string.Equals(Result, "Succeeded", StringComparison.Ordinal);
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
            IRobotArenaWebGLReleaseOperations operations)
        {
            OutputDirectory = Path.GetFullPath(outputDirectory);
            ArchivePath = Path.GetFullPath(archivePath);
            ReportPath = Path.GetFullPath(reportPath);
            DefineSymbols = defineSymbols ?? string.Empty;
            PluginVersion = pluginVersion ?? string.Empty;
            TemplateSource = templateSource ?? string.Empty;
            Operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public string OutputDirectory { get; }
        public string ArchivePath { get; }
        public string ReportPath { get; }
        public string DefineSymbols { get; }
        public string PluginVersion { get; }
        public string TemplateSource { get; }
        public IRobotArenaWebGLReleaseOperations Operations { get; }
    }

    public static class RobotArenaWebGLReleaseBuild
    {
        private const string ReleaseTemplate = "PROJECT:RobotArenaPluginYG2";
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
        private const string ExpectedPluginYG2Platform = "YandexGamesPlatform_yg";
        private const string ExpectedPluginYG2SdkLoader = "<script src=\"/sdk.js\"></script>";
        private const string ExpectedPluginYG2SdkInitializer = "YaGames.init()";
        private const string ExpectedPluginYG2SourceArchiveSha256 =
            "8A5CBD1DEA0CFB0772A8E28976663CD7D91E8594B2F70DB9E03E0AC682DFADC3";
        private const string ExpectedPluginYG2VendoredFingerprint =
            "AB4551EFDB23E1DC417598F406AF2997F16080BBBCF8E9FB08CDFAFF9763395F";
        private static readonly string[] ExpectedPluginYG2Modules =
            { "Core", "YandexGames", "EnvirData" };
        private static readonly string[] ExpectedPluginYG2Defines =
        {
            "YandexGamesPlatform_yg",
            "ROBOTARENA_PLUGINYG2",
            "PLUGIN_YG_2",
            "EnvirData_yg"
        };
        private static readonly string[] RequiredPluginYG2VendorFiles =
        {
            "Scripts/Basic/YG2.cs",
            "Scripts/Basic/GameReadyAPI.cs",
            "Platforms/YandexGames/Scripts/YandexGamePlatform.cs",
            "Platforms/YandexGames/Plugins/YandexGame.jslib",
            "Modules/EnvirData/Scripts/EnvirData_yg.cs",
            "Modules/EnvirData/Plugins/EnvirData.jslib"
        };
        private static readonly string[] ExpectedPluginYG2RequiredArtifactMarkers =
        {
            "game_api_pause",
            "game_api_resume",
            "RequestingEnvironmentData",
            "SetEnvirData",
            "PluginYG2 v2.0092"
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
            string validationStage = "configuration";
            int changedTextureCount = 0;
            bool releaseIsUploadReady = false;

            try
            {
                validationStage = "prepare";
                context.Operations.PreparePaths(outputDirectory, archivePath, reportPath);

                validationStage = "configuration";
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

                validationStage = "settings";
                PlayerSettings.WebGL.template = ReleaseTemplate;
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

                validationStage = "build";
                buildResult = context.Operations.Build(activeScenes, outputDirectory);

                if (buildResult == null || !buildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        "Robot Arena WebGL release build failed: "
                        + (buildResult == null ? "NotStarted" : buildResult.Result));
                }

                validationStage = "artifact";
                artifactValidation = context.Operations.ValidateArtifact(outputDirectory);
                if (artifactValidation == null || !artifactValidation.IsValid)
                {
                    AddValidationErrors(validationErrors, artifactValidation);
                    throw new BuildFailedException(
                        "PluginYG2 post-processed WebGL artifact is invalid: "
                        + string.Join(" | ", GetValidationErrors(artifactValidation)));
                }

                validationStage = "archive";
                context.Operations.CreateArchive(outputDirectory, archivePath);

                archiveValidation = context.Operations.ValidateArchive(archivePath);
                if (archiveValidation == null || !archiveValidation.IsValid)
                {
                    AddValidationErrors(validationErrors, archiveValidation);
                    throw new BuildFailedException(
                        "PluginYG2 upload archive is invalid: "
                        + string.Join(" | ", GetValidationErrors(archiveValidation)));
                }

                validationStage = "package";
                packageResult = context.Operations.MeasurePackage(
                    archivePath,
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

                releaseIsUploadReady = true;

                Debug.Log(
                    "Robot Arena WebGL release package created: "
                    + archivePath
                    + " ("
                    + packageResult.UncompressedBytes
                    + " uncompressed bytes)");
            }
            catch (Exception exception)
            {
                string failure = validationStage + ": " + exception.Message;
                if (!validationErrors.Contains(failure))
                {
                    validationErrors.Add(failure);
                }

                throw;
            }
            finally
            {
                try
                {
                    WriteReport(
                        reportPath,
                        outputDirectory,
                        buildResult,
                        packageResult,
                        artifactValidation,
                        archiveValidation,
                        archivePath,
                        changedTextureCount,
                        validationStage,
                        validationErrors,
                        releaseIsUploadReady);
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
            }
        }

        public static RobotArenaWebGLReleaseBuildContext CreateProductionContext()
        {
            string projectRoot = GetProjectRootPath();
            string pluginVersionPath = Path.Combine(projectRoot, ExpectedPluginYG2VersionFile);
            string templatePath = Path.Combine(projectRoot, ExpectedPluginYG2TemplateFile);
            return new RobotArenaWebGLReleaseBuildContext(
                ReleaseOutput,
                ReleaseArchive,
                ReleaseReport,
                PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL),
                File.Exists(pluginVersionPath) ? File.ReadAllText(pluginVersionPath).Trim() : string.Empty,
                File.Exists(templatePath) ? File.ReadAllText(templatePath) : string.Empty,
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
            int changedTextureCount,
            string validationStage,
            List<string> validationErrors,
            bool releaseIsUploadReady)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            List<ReleaseFileReport> files = new List<ReleaseFileReport>();
            if (File.Exists(archivePath))
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

            string artifactPath = Path.Combine(outputDirectory, "index.html");
            string artifactChecksum = artifactValidation == null
                ? string.Empty
                : TryComputeFileSha256(artifactPath);
            string archiveChecksum = archiveValidation == null
                ? string.Empty
                : TryComputeFileSha256(archivePath);
            string vendoredFingerprint = string.Empty;
            string vendorRoot = Path.Combine(GetProjectRootPath(), "Assets/PluginYourGames");
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
                buildResult = buildResult == null ? "NotStarted" : buildResult.Result,
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
                validationStage = validationStage,
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

        private sealed class UnityReleaseOperations : IRobotArenaWebGLReleaseOperations
        {
            public void PreparePaths(string outputDirectory, string archivePath, string reportPath)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                DeleteGeneratedPath(outputDirectory, true);
                DeleteGeneratedPath(archivePath, false);
                DeleteGeneratedPath(reportPath, false);
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
                    buildReport.summary.result.ToString(),
                    buildReport.summary.totalSize);
            }

            public RobotArenaReleaseValidationResult ValidateArtifact(string outputDirectory)
            {
                return ToReleaseValidationResult(ValidatePluginYG2ReleaseArtifact(outputDirectory));
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
                return ToReleaseValidationResult(ValidatePluginYG2ReleaseArchive(archivePath));
            }

            public WebGLPackageBudgetResult MeasurePackage(string archivePath, long limitBytes)
            {
                return WebGLPackageBudget.Measure(archivePath, limitBytes);
            }
        }

        private static RobotArenaReleaseValidationResult ToReleaseValidationResult(
            PluginYG2ArtifactValidation validation)
        {
            if (validation == null)
            {
                return null;
            }

            return new RobotArenaReleaseValidationResult(
                ToIntegrationCoordinates(validation.Manifest),
                validation.Errors);
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

        public static List<string> GetPluginYG2ArtifactErrors(string artifactSource)
        {
            var manifestErrors = new List<string>();
            PluginYG2IntegrationManifest manifest = LoadPluginYG2Manifest(manifestErrors);
            if (manifest == null)
            {
                return manifestErrors;
            }

            var errors = new List<string>();
            string rawSource = artifactSource ?? string.Empty;
            string source = RemoveArtifactComments(rawSource);
            if (CountOccurrences(source, manifest.sdkLoader) != 1)
            {
                errors.Add("PluginYG2 artifact must contain exactly one /sdk.js loader.");
            }

            if (CountOccurrences(source, manifest.sdkInitializer) != 1)
            {
                errors.Add("PluginYG2 artifact must contain exactly one YaGames.init() call.");
            }

            foreach (string marker in manifest.requiredArtifactMarkers ?? new string[0])
            {
                if (string.IsNullOrEmpty(marker))
                {
                    continue;
                }

                int occurrences = CountOccurrences(source, marker);
                bool lifecycleMarker = marker == "game_api_pause" || marker == "game_api_resume";
                if ((lifecycleMarker && occurrences != 1) ||
                    (!lifecycleMarker && occurrences == 0))
                {
                    errors.Add(
                        lifecycleMarker
                            ? "PluginYG2 artifact must contain exactly one lifecycle marker: " + marker
                            : "PluginYG2 artifact is missing required marker: " + marker);
                }
            }

            errors.AddRange(GetPluginYG2ForbiddenArtifactErrors(manifest, rawSource));

            return errors;
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

        private static List<string> GetPluginYG2ConfigurationErrors(
            string defineSymbols,
            string pluginVersion,
            string templateSource,
            PluginYG2IntegrationManifest manifest)
        {
            var errors = new List<string>();
            errors.AddRange(GetPluginYG2ManifestErrors(manifest));
            if (manifest == null)
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
                    "Official PluginYG2 version must be "
                    + manifest.pluginVersion
                    + ", got "
                    + (string.IsNullOrEmpty(pluginVersion) ? "<missing>" : pluginVersion));
            }

            if (CountOccurrences(templateSource, manifest.sdkLoader) != 1)
            {
                errors.Add("PluginYG2 template must contain exactly one /sdk.js loader.");
            }

            if (CountOccurrences(templateSource, manifest.sdkInitializer) != 1)
            {
                errors.Add("PluginYG2 template must contain exactly one YaGames.init() call.");
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

        private static PluginYG2ArtifactValidation ValidatePluginYG2ReleaseArtifact(
            string outputDirectory)
        {
            var manifestErrors = new List<string>();
            PluginYG2IntegrationManifest manifest = LoadPluginYG2Manifest(manifestErrors);
            if (manifest == null)
            {
                return new PluginYG2ArtifactValidation(manifest, manifestErrors);
            }

            string indexPath = Path.Combine(outputDirectory, "index.html");
            if (!File.Exists(indexPath))
            {
                manifestErrors.Add("PluginYG2 release artifact is missing index.html.");
                return new PluginYG2ArtifactValidation(manifest, manifestErrors);
            }

            string indexSource = File.ReadAllText(indexPath);
            manifestErrors.AddRange(GetPluginYG2ArtifactErrors(indexSource));
            int loaderOccurrences = CountOccurrences(
                RemoveArtifactComments(indexSource),
                manifest == null ? string.Empty : manifest.sdkLoader);
            int initializerOccurrences = CountOccurrences(
                RemoveArtifactComments(indexSource),
                manifest == null ? string.Empty : manifest.sdkInitializer);
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

                if (string.Equals(filePath, indexPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string source = File.ReadAllText(filePath);
                manifestErrors.AddRange(GetPluginYG2ForbiddenArtifactErrors(manifest, source));
                string runtimeSource = RemoveArtifactComments(source);
                loaderOccurrences += CountOccurrences(
                    runtimeSource,
                    manifest == null ? string.Empty : manifest.sdkLoader);
                initializerOccurrences += CountOccurrences(
                    runtimeSource,
                    manifest == null ? string.Empty : manifest.sdkInitializer);
            }

            AddPluginYG2ArtifactWideErrors(
                manifest,
                loaderOccurrences,
                initializerOccurrences,
                manifestErrors);

            return new PluginYG2ArtifactValidation(manifest, manifestErrors);
        }

        private static PluginYG2ArtifactValidation ValidatePluginYG2ReleaseArchive(
            string archivePath)
        {
            var errors = new List<string>();
            PluginYG2IntegrationManifest manifest = LoadPluginYG2Manifest(errors);
            if (manifest == null)
            {
                return new PluginYG2ArtifactValidation(manifest, errors);
            }

            if (!File.Exists(archivePath))
            {
                errors.Add("PluginYG2 upload archive is missing.");
                return new PluginYG2ArtifactValidation(manifest, errors);
            }

            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                ZipArchiveEntry indexEntry = null;
                int loaderOccurrences = 0;
                int initializerOccurrences = 0;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName == "index.html")
                    {
                        indexEntry = entry;
                        break;
                    }
                }

                if (indexEntry == null)
                {
                    errors.Add("PluginYG2 upload archive is missing root index.html.");
                    return new PluginYG2ArtifactValidation(manifest, errors);
                }

                using (StreamReader reader = new StreamReader(indexEntry.Open()))
                {
                    string indexSource = reader.ReadToEnd();
                    errors.AddRange(GetPluginYG2ArtifactErrors(indexSource));
                    string runtimeSource = RemoveArtifactComments(indexSource);
                    loaderOccurrences += CountOccurrences(
                        runtimeSource,
                        manifest == null ? string.Empty : manifest.sdkLoader);
                    initializerOccurrences += CountOccurrences(
                        runtimeSource,
                        manifest == null ? string.Empty : manifest.sdkInitializer);
                }

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string extension = Path.GetExtension(entry.FullName);
                    if (!string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName == "index.html")
                    {
                        continue;
                    }

                    using (StreamReader reader = new StreamReader(entry.Open()))
                    {
                        string source = reader.ReadToEnd();
                        errors.AddRange(GetPluginYG2ForbiddenArtifactErrors(manifest, source));
                        string runtimeSource = RemoveArtifactComments(source);
                        loaderOccurrences += CountOccurrences(
                            runtimeSource,
                            manifest == null ? string.Empty : manifest.sdkLoader);
                        initializerOccurrences += CountOccurrences(
                            runtimeSource,
                            manifest == null ? string.Empty : manifest.sdkInitializer);
                    }
                }

                AddPluginYG2ArtifactWideErrors(
                    manifest,
                    loaderOccurrences,
                    initializerOccurrences,
                    errors);
            }

            return new PluginYG2ArtifactValidation(manifest, errors);
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

            try
            {
                PluginYG2IntegrationManifest manifest = JsonUtility.FromJson<PluginYG2IntegrationManifest>(
                    File.ReadAllText(manifestPath));
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

            if (!string.Equals(manifest.plugin, ExpectedPluginYG2, StringComparison.Ordinal))
            {
                errors.Add("Manifest plugin must be PluginYG2.");
            }

            if (string.IsNullOrEmpty(manifest.pluginVersion))
            {
                errors.Add("Manifest pluginVersion is missing.");
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

            if (string.IsNullOrEmpty(manifest.versionFile) ||
                string.IsNullOrEmpty(manifest.templateFile))
            {
                errors.Add("Manifest versionFile and templateFile are required.");
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

            if (!string.Equals(manifest.pluginVersion, ExpectedPluginYG2Version, StringComparison.Ordinal))
            {
                errors.Add(
                    "Manifest pluginVersion must be the pinned official version: "
                    + ExpectedPluginYG2Version
                    + ".");
            }

            if (string.IsNullOrEmpty(manifest.platform) ||
                manifest.requiredDefines == null ||
                manifest.requiredDefines.Length == 0)
            {
                errors.Add("Manifest platform and requiredDefines are required.");
            }
            else if (!string.Equals(
                         manifest.platform,
                         ExpectedPluginYG2Platform,
                         StringComparison.Ordinal))
            {
                errors.Add(
                    "Manifest platform must be the official PluginYG2 platform: "
                    + ExpectedPluginYG2Platform
                    + ".");
            }

            string[] requiredModules = ExpectedPluginYG2Modules;
            if (manifest.modules == null || manifest.modules.Length == 0)
            {
                errors.Add("Manifest modules are required.");
            }
            else
            {
                if (manifest.modules.Length != requiredModules.Length)
                {
                    errors.Add(
                        "Manifest modules must contain only Core, YandexGames, and EnvirData.");
                }

                foreach (string requiredModule in requiredModules)
                {
                    bool present = false;
                    foreach (string module in manifest.modules)
                    {
                        if (string.Equals(module, requiredModule, StringComparison.Ordinal))
                        {
                            present = true;
                            break;
                        }
                    }

                    if (!present)
                    {
                        errors.Add("Manifest is missing required module: " + requiredModule);
                    }
                }

                if (manifest.modules.Length == requiredModules.Length)
                {
                    foreach (string module in manifest.modules)
                    {
                        bool isExpected = false;
                        foreach (string requiredModule in requiredModules)
                        {
                            if (string.Equals(module, requiredModule, StringComparison.Ordinal))
                            {
                                isExpected = true;
                                break;
                            }
                        }

                        if (!isExpected)
                        {
                            errors.Add("Manifest contains an unsupported PluginYG2 module: " + module);
                        }
                    }
                }
            }

            if (manifest.requiredDefines != null)
            {
                if (manifest.requiredDefines.Length != ExpectedPluginYG2Defines.Length)
                {
                    errors.Add("Manifest requiredDefines must contain only the pinned PluginYG2 defines.");
                }

                foreach (string requiredDefine in ExpectedPluginYG2Defines)
                {
                    if (Array.IndexOf(manifest.requiredDefines, requiredDefine) < 0)
                    {
                        errors.Add("Manifest is missing required define: " + requiredDefine);
                    }
                }
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

            if (string.IsNullOrEmpty(manifest.sdkLoader) ||
                string.IsNullOrEmpty(manifest.sdkInitializer))
            {
                errors.Add("Manifest sdkLoader and sdkInitializer are required.");
            }

            if (!HasExactValues(
                    manifest.requiredArtifactMarkers,
                    ExpectedPluginYG2RequiredArtifactMarkers))
            {
                errors.Add("Manifest requiredArtifactMarkers do not match the PluginYG2 invariants.");
            }

            if (!HasExactValues(
                    manifest.forbiddenArtifactMarkers,
                    ExpectedPluginYG2ForbiddenArtifactMarkers))
            {
                errors.Add(
                    "Manifest forbiddenArtifactMarkers do not match the legacy bridge policy.");
            }

            return errors;
        }

        private static List<string> GetPluginYG2VendoredErrors(
            PluginYG2IntegrationManifest manifest)
        {
            var errors = new List<string>();
            string vendorRoot = Path.Combine(GetProjectRootPath(), "Assets/PluginYourGames");
            if (!Directory.Exists(vendorRoot))
            {
                errors.Add("Vendored PluginYG2 directory is missing: Assets/PluginYourGames.");
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
                    manifest.vendoredFingerprint,
                    ExpectedPluginYG2VendoredFingerprint,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    "Manifest vendoredFingerprint does not match the pinned PluginYG2 import receipt.");
            }
            else if (!string.Equals(
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
                    if (!string.Equals(moduleDirectory.Name, "EnvirData", StringComparison.Ordinal))
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

        private static bool HasExactValues(string[] actual, string[] expected)
        {
            if (actual == null || actual.Length != expected.Length)
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
                    "PluginYG2 artifact must contain exactly one /sdk.js loader across all generated files.");
            }

            if (initializerOccurrences != 1)
            {
                errors.Add(
                    "PluginYG2 artifact must contain exactly one YaGames.init() call across all generated files.");
            }
        }

        private static string GetProjectRootPath()
        {
            return Directory.GetParent(Application.dataPath).FullName;
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

        private static void DeleteGeneratedPath(string path, bool directory)
        {
            string generatedRoot = Path.GetFullPath("Build/WebGL") + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(generatedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to modify a path outside Build/WebGL: " + fullPath);
            }

            if (directory)
            {
                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, recursive: true);
                }
            }
            else if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
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
            public string platform;
            public string[] modules;
            public string[] requiredDefines;
            public string sdkLoader;
            public string sdkInitializer;
            public string[] requiredArtifactMarkers;
            public string[] forbiddenArtifactMarkers;
        }

        private sealed class PluginYG2ArtifactValidation
        {
            public PluginYG2ArtifactValidation(
                PluginYG2IntegrationManifest manifest,
                List<string> errors)
            {
                Manifest = manifest;
                Errors = errors ?? new List<string>();
            }

            public PluginYG2IntegrationManifest Manifest { get; }
            public List<string> Errors { get; }
            public bool IsValid => Manifest != null && Errors.Count == 0;
        }
    }
}
