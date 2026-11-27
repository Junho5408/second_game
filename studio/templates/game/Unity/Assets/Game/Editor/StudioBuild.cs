using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// CI build entry point (GameCI buildMethod: StudioBuild.Build).
/// Generates the single empty bootstrap scene, then builds StandaloneWindows64.
/// </summary>
public static class StudioBuild
{
    private const string ScenePath = "Assets/Game/Scenes/Generated/Bootstrap.unity";

    public static void Build()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        var output = GetArg("-customBuildPath") ?? "Build/Windows/{{GAME}}.exe";
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });

        var summary = report.summary;
        Debug.Log($"[StudioBuild] result={summary.result} errors={summary.totalErrors} size={summary.totalSize} output={output}");
        if (summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }

    private static string GetArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }
}
