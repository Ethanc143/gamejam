using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace FocusSim.Editor
{
    public static class FocusBuildUtility
    {
        [MenuItem("FOCUS/Build/macOS Vertical Slice")]
        public static void BuildMac()
        {
            Build(BuildTarget.StandaloneOSX, "Builds/FOCUS/FOCUS.app");
        }

        [MenuItem("FOCUS/Build/Windows Vertical Slice")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "Builds/FOCUS/FOCUS.exe");
        }

        private static void Build(BuildTarget target, string outputPath)
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                UnityEngine.Debug.LogError("FOCUS: No enabled scenes in Build Settings.");
                return;
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                target = target,
                locationPathName = outputPath,
                scenes = scenes
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                UnityEngine.Debug.Log($"FOCUS build succeeded: {outputPath}");
            }
            else
            {
                UnityEngine.Debug.LogError($"FOCUS build failed: {report.summary.result}");
            }
        }
    }
}
