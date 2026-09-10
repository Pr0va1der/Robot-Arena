using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RobotArena.WebGL.Editor
{
    public static class RobotArenaWebGLReleaseBuild
    {
        private const string ReleaseTemplate = "PROJECT:RobotArenaPluginYG2";
        private const string ReleaseOutput = "Build/WebGL/RobotArenaRelease";
        private const string ReleaseArchive = "Build/WebGL/RobotArenaRelease-upload.zip";
        private const string ReleaseReport = "Build/WebGL/RobotArenaRelease-report.json";
        private const string PluginYG2IntegrationManifestPath =
            "Tools/RobotArenaPluginYG2Integration.json";

        [MenuItem("Robot Arena/Build WebGL release package")]
        public static void BuildReleasePackage()
        {
            ValidatePluginYG2ReleaseConfiguration();

            string outputDirectory = Path.GetFullPath(ReleaseOutput);
            string archivePath = Path.GetFullPath(ReleaseArchive);
            string reportPath = Path.GetFullPath(ReleaseReport);

            DeleteGeneratedPath(outputDirectory, true);
            DeleteGeneratedPath(archivePath, false);
            DeleteGeneratedPath(reportPath, false);
            Directory.CreateDirectory(Path.GetDirectoryName(outputDirectory));

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

            try
            {
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

                int changedTextureCount = WebGLTextureImportPolicy.ApplyForRelease();
                Debug.Log(
                    "Robot Arena WebGL texture policy applied to "
                    + changedTextureCount
                    + " texture importers (WebGL max size "
                    + WebGLTextureImportPolicy.DefaultMaxTextureSize
                    + ", DXT5 Crunch).");

                BuildReport buildReport = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = activeScenes,
                    locationPathName = outputDirectory,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });

                if (buildReport.summary.result != BuildResult.Succeeded)
                {
                    throw new BuildFailedException(
                        "Robot Arena WebGL release build failed: " + buildReport.summary.result);
                }

                PluginYG2ArtifactValidation artifactValidation =
                    ValidatePluginYG2ReleaseArtifact(outputDirectory);
                if (!artifactValidation.IsValid)
                {
                    throw new BuildFailedException(
                        "PluginYG2 post-processed WebGL artifact is invalid: "
                        + string.Join(" | ", artifactValidation.Errors));
                }

                ZipFile.CreateFromDirectory(
                    outputDirectory,
                    archivePath,
                    System.IO.Compression.CompressionLevel.Optimal,
                    includeBaseDirectory: false);

                WebGLPackageBudgetResult packageResult = WebGLPackageBudget.Measure(
                    archivePath,
                    WebGLPackageBudget.DefaultLimitBytes);
                WriteReport(
                    reportPath,
                    buildReport,
                    packageResult,
                    artifactValidation,
                    archivePath,
                    changedTextureCount);

                if (!packageResult.IsPassing)
                {
                    throw new BuildFailedException(
                        "Robot Arena WebGL release package failed validation: "
                        + string.Join(" | ", packageResult.Errors));
                }

                Debug.Log(
                    "Robot Arena WebGL release package created: "
                    + archivePath
                    + " ("
                    + packageResult.UncompressedBytes
                    + " uncompressed bytes)");
            }
            finally
            {
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

        private static void WriteReport(
            string reportPath,
            BuildReport buildReport,
            WebGLPackageBudgetResult packageResult,
            PluginYG2ArtifactValidation artifactValidation,
            string archivePath,
            int changedTextureCount)
        {
            List<ReleaseFileReport> files = new List<ReleaseFileReport>();
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

            ReleaseBuildReport report = new ReleaseBuildReport
            {
                buildResult = buildReport.summary.result.ToString(),
                unityBuildBytes = buildReport.summary.totalSize,
                packageUncompressedBytes = packageResult.UncompressedBytes,
                packageLimitBytes = packageResult.LimitBytes,
                packageIsValid = packageResult.IsValid,
                packageIsWithinBudget = packageResult.IsWithinBudget,
                packageIsPassing = packageResult.IsPassing,
                texturePolicyMaxTextureSize = WebGLTextureImportPolicy.DefaultMaxTextureSize,
                texturePolicyFormat = TextureImporterFormat.DXT5Crunched.ToString(),
                texturePolicyCrunchQuality = WebGLTextureImportPolicy.CrunchQuality,
                texturePolicyChangedCount = changedTextureCount,
                platformSdk = artifactValidation.Manifest.plugin,
                pluginVersion = artifactValidation.Manifest.pluginVersion,
                pluginSourceArchiveSha256 = artifactValidation.Manifest.sourceArchiveSha256,
                sdkLoader = artifactValidation.Manifest.sdkLoader,
                artifactIsValid = artifactValidation.IsValid,
                artifactErrors = new List<string>(artifactValidation.Errors),
                stripEngineCode = true,
                managedStrippingLevel = ManagedStrippingLevel.Low.ToString(),
                packageErrors = new List<string>(packageResult.Errors),
                files = files
            };

            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        }

        private static void ValidatePluginYG2ReleaseConfiguration()
        {
            var manifestErrors = new List<string>();
            PluginYG2IntegrationManifest manifest = LoadPluginYG2Manifest(manifestErrors);
            if (manifest == null)
            {
                throw new BuildFailedException(
                    "PluginYG2 integration manifest is invalid: "
                    + string.Join(" | ", manifestErrors));
            }

            string projectRoot = GetProjectRootPath();
            string defineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(
                BuildTargetGroup.WebGL);
            string pluginVersionPath = Path.Combine(projectRoot, manifest.versionFile);
            string templatePath = Path.Combine(projectRoot, manifest.templateFile);
            string pluginVersion = File.Exists(pluginVersionPath)
                ? File.ReadAllText(pluginVersionPath).Trim()
                : string.Empty;
            string templateSource = File.Exists(templatePath)
                ? File.ReadAllText(templatePath)
                : string.Empty;
            List<string> errors = GetPluginYG2ConfigurationErrors(
                defineSymbols,
                pluginVersion,
                templateSource,
                manifest);
            if (errors.Count > 0)
            {
                throw new BuildFailedException(
                    "PluginYG2 release configuration is invalid: "
                    + string.Join(" | ", errors));
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

        public static List<string> GetPluginYG2ArtifactErrors(string artifactSource)
        {
            var manifestErrors = new List<string>();
            PluginYG2IntegrationManifest manifest = LoadPluginYG2Manifest(manifestErrors);
            if (manifest == null)
            {
                return manifestErrors;
            }

            var errors = new List<string>();
            string source = artifactSource ?? string.Empty;
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
                if (string.IsNullOrEmpty(marker) || !source.Contains(marker))
                {
                    errors.Add("PluginYG2 artifact is missing required marker: " + marker);
                }
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
            if (errors.Count > 0)
            {
                return errors;
            }

            var configuredDefines = new HashSet<string>(
                (defineSymbols ?? string.Empty).Split(
                    new[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);

            foreach (string requiredDefine in manifest.requiredDefines)
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
            string indexPath = Path.Combine(outputDirectory, "index.html");
            if (!File.Exists(indexPath))
            {
                manifestErrors.Add("PluginYG2 release artifact is missing index.html.");
                return new PluginYG2ArtifactValidation(manifest, manifestErrors);
            }

            manifestErrors.AddRange(
                GetPluginYG2ArtifactErrors(File.ReadAllText(indexPath)));
            return new PluginYG2ArtifactValidation(manifest, manifestErrors);
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

            if (!string.Equals(manifest.plugin, "PluginYG2", StringComparison.Ordinal))
            {
                errors.Add("Manifest plugin must be PluginYG2.");
            }

            if (string.IsNullOrEmpty(manifest.pluginVersion))
            {
                errors.Add("Manifest pluginVersion is missing.");
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

            if (string.IsNullOrEmpty(manifest.platform) ||
                manifest.requiredDefines == null ||
                manifest.requiredDefines.Length == 0)
            {
                errors.Add("Manifest platform and requiredDefines are required.");
            }

            if (manifest.modules == null || manifest.modules.Length == 0)
            {
                errors.Add("Manifest modules are required.");
            }
            else
            {
                foreach (string requiredModule in new[] { "Core", "YandexGames", "EnvirData" })
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
            }

            if (string.IsNullOrEmpty(manifest.sdkLoader) ||
                string.IsNullOrEmpty(manifest.sdkInitializer))
            {
                errors.Add("Manifest sdkLoader and sdkInitializer are required.");
            }

            return errors;
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
            public string sdkLoader;
            public bool artifactIsValid;
            public List<string> artifactErrors;
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
