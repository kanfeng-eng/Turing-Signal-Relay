using System;
using System.IO;
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

        public static void Delivery()
        {
            Windows();
            LinuxServer();
            Debug.Log("DELIVERY_BUILD_COMPLETED");
        }

        private static void Build(BuildTarget target, string output, bool server)
        {
            bool developmentCopy = Array.IndexOf(Environment.GetCommandLineArgs(), "-bootstrapDevelopmentCopy") >= 0;
            if (Application.unityVersion != "6000.3.26f1" &&
                !(developmentCopy && Application.unityVersion == "6000.6.8f1"))
                throw new InvalidOperationException("Use Unity 6000.3.26f1; refusing a different editor.");
            if (developmentCopy) Debug.LogWarning("Development copy build; team baseline compatibility is not verified.");
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.productName = "Turing Signal Relay";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            var options = new BuildPlayerOptions {
                scenes = server ? new[] { Scene } : new[] { "Assets/Game/Scenes/Observer.unity", Scene }, target = target,
                locationPathName = Path.GetFullPath(Path.Combine(Application.dataPath, "..", output)),
                options = BuildOptions.None
            };
            options.subtarget = (int)(server ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player);
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Build failed: " + report.summary.result);
            Debug.Log("BOOTSTRAP_BUILD_COMPLETED " + target);
        }
    }
}
