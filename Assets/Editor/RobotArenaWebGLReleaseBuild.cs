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

        [MenuItem("Robot Arena/Build WebGL release package")]
        public static void BuildReleasePackage()
        {
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

                ZipFile.CreateFromDirectory(
                    outputDirectory,
                    archivePath,
                    System.IO.Compression.CompressionLevel.Optimal,
                    includeBaseDirectory: false);

                WebGLPackageBudgetResult packageResult = WebGLPackageBudget.Measure(
                    archivePath,
                    WebGLPackageBudget.DefaultLimitBytes);
                WriteReport(reportPath, buildReport, packageResult, archivePath, changedTextureCount);

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
                stripEngineCode = true,
                managedStrippingLevel = ManagedStrippingLevel.Low.ToString(),
                packageErrors = new List<string>(packageResult.Errors),
                files = files
            };

            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
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
    }
}
