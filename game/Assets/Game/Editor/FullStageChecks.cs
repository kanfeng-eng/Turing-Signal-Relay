using System;
using System.IO;
using UnityEngine;

namespace TuringSignalRelay.Editor
{
    public static class FullStageChecks
    {
        public static void ReplayFile()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-relayReplayCheck");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Expected -relayReplayCheck PATH");
            var recording = JsonUtility.FromJson<RelayRecording>(File.ReadAllText(args[index + 1]));
            var engine = RelayReplay.Verify(recording);
            var ui = new GameObject("C++ AI replay check");
            try
            {
                var controller = ui.AddComponent<ObserverController>(); controller.enabled = false;
                controller.LoadReplay(args[index + 1]);
                if (controller.Error != "" || controller.Playback.Frames.Count != engine.Tick + 1) throw new Exception("AI replay load failed " + controller.Error);
                ui.AddComponent<ObserverHud>().Initialize(controller);
                foreach (int tick in new[] { 0, engine.Tick / 2, engine.Tick })
                {
                    controller.Playback.Seek(tick); controller.Frame.Validate(); ObserverInteractionChecks.Render(ui);
                }
                if (controller.Frame.scoreHalfPoints[0] != engine.GetSnapshot().scoreHalfPoints[0]) throw new Exception("AI replay viewer result mismatch.");
            }
            finally { UnityEngine.Object.DestroyImmediate(ui); }
            Debug.Log("CPP_AI_REPLAY_VIEWER_PASSED ticks=" + engine.Tick + " stateHash=" + engine.StateHash());
        }

        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var config = JsonUtility.FromJson<RelayConfig>(File.ReadAllText(Path.Combine(root, "fixtures/protocol/relay-full-config.json")));
            var map = JsonUtility.FromJson<RelayMapData>(File.ReadAllText(Path.Combine(root, "fixtures/world/map-v1.json")));
            var recording = RelayScenario.Run(config, map);
            var engine = RelayReplay.Verify(recording);
            var final = engine.GetSnapshot();
            if (!final.completed || final.messages.Count != 88 || final.scoreHalfPoints[0] != 420 || final.scoreHalfPoints[1] != 420)
                throw new Exception("Full gameplay native result differs from its acceptance fixture.");
            string output = Path.Combine(root, "artifacts", "full-native-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, "relay.replay.json");
            File.WriteAllText(path, JsonUtility.ToJson(recording));
            File.WriteAllText(Path.Combine(output, "relay.result.json"), JsonUtility.ToJson(RelayScenario.Result(engine), true));
            var ui = new GameObject("Full rules replay checks");
            try
            {
                var controller = ui.AddComponent<ObserverController>();
                controller.enabled = false;
                controller.LoadReplay(path);
                if (controller.Error != "" || controller.Playback.Frames.Count != 9001)
                    throw new Exception("Full rules replay rejected by viewer " + controller.Error);
                controller.Playback.Seek(3600);
                if (controller.Frame.weather != "storm") throw new Exception("Viewer lost full-rules weather.");
                controller.Frame.Validate();
                controller.Playback.Seek(9000);
                controller.Frame.Validate();
                if (controller.Frame.scoreHalfPoints[0] != 420) throw new Exception("Viewer score differs from engine.");
                ui.AddComponent<ObserverHud>().Initialize(controller);
                ObserverInteractionChecks.Render(ui);
                var constructed = engine.GetSnapshot();
                constructed.stations.Add(new RelayFixedStation { id = 200, side = 0, x = 10, y = 8 });
                constructed.units[0].stationActive = true;
                RadioNetwork.Rebuild(constructed, map);
                new ObserverSnapshotBuilder(config, map).Create(constructed).Validate();
            }
            finally { UnityEngine.Object.DestroyImmediate(ui); }
            Debug.Log("RELAY_STAGE45_CHECKS_PASSED ticks=9000 stateHash=" + engine.StateHash() + " output=" + output);
        }
    }
}
