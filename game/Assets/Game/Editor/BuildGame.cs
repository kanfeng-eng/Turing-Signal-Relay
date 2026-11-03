using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TuringSignalRelay.Editor
{
    public static class BuildGame
    {
        private const string Scene = "Assets/Game/Scenes/Bootstrap.unity";

        [MenuItem("Turing Signal Relay/Build Windows Bootstrap")]
        public static void Windows() { Build(BuildTarget.StandaloneWindows64, "../artifacts/windows/TuringSignalRelay.exe", false); }

        [MenuItem("Turing Signal Relay/Build Linux Server Bootstrap")]
        public static void LinuxServer() { Build(BuildTarget.StandaloneLinux64, "../artifacts/linux/TuringSignalRelay", true); }

        private static void Build(BuildTarget target, string output, bool server)
        {
            if (Application.unityVersion != "6000.3.26f1")
                throw new InvalidOperationException("Use Unity 6000.3.26f1; refusing a different editor.");
            EditorSettings.serializationMode = SerializationMode.ForceText;
            var options = new BuildPlayerOptions {
                scenes = new[] { Scene }, target = target, locationPathName = output,
                options = BuildOptions.Development
            };
            options.subtarget = (int)(server ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player);
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Build failed: " + report.summary.result);
            Debug.Log("BOOTSTRAP_BUILD_COMPLETED " + target);
        }
    }
}
