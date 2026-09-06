using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class RobotArenaPlatformProbeBuild
{
    private const string ProbeTemplate = "PROJECT:RobotArenaYandex";
    private const string ProbeOutput = "Build/WebGL/RobotArenaPlatformProbe";

    [MenuItem("Robot Arena/Build WebGL platform probe")]
    public static void BuildWebGlProbe()
    {
        string previousTemplate = PlayerSettings.WebGL.template;
        try
        {
            Directory.CreateDirectory(ProbeOutput);
            PlayerSettings.WebGL.template = ProbeTemplate;

            string[] activeScenes =
                EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes);
            const string titleScenePath = "Assets/Scenes/Title Screen.unity";
            bool hasTitleScene = false;
            foreach (string scenePath in activeScenes)
            {
                if (string.Equals(scenePath, titleScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    hasTitleScene = true;
                    break;
                }
            }

            if (!hasTitleScene)
            {
                throw new BuildFailedException(
                    "RobotArena platform probe requires the active scene list to contain " + titleScenePath + ".");
            }

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = activeScenes,
                locationPathName = ProbeOutput,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    "RobotArena platform probe build failed: " + report.summary.result);
            }

            Debug.Log("RobotArena platform probe build created at " + ProbeOutput);
        }
        finally
        {
            PlayerSettings.WebGL.template = previousTemplate;
        }
    }
}
