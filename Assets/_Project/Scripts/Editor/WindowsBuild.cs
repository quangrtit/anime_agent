using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AnimeAssistant.Editor
{
    public static class WindowsBuild
    {
        private const string OutputPath = "Builds/Windows/AnimeAssistant.exe";
        private const string ProfileOutputPath = "Builds/WindowsProfile/AnimeAssistant.exe";

        public static void Build()
        {
            BuildPlayer(OutputPath, BuildOptions.StrictMode);
        }

        public static void BuildDevelopment()
        {
            BuildPlayer(ProfileOutputPath,
                BuildOptions.StrictMode | BuildOptions.Development | BuildOptions.ConnectWithProfiler);
        }

        private static void BuildPlayer(string outputPath, BuildOptions options)
        {
            // This is a desktop overlay rather than a conventional game. Showing
            // Unity's splash creates an opaque game window before transparency is applied.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are configured for the Windows build.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Builds/Windows");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = options
            });

            var summary = report.summary;
            Debug.Log($"Windows build result={summary.result} errors={summary.totalErrors} " +
                      $"warnings={summary.totalWarnings} bytes={summary.totalSize}");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Windows build failed: {summary.result}");
            }
        }
    }
}
