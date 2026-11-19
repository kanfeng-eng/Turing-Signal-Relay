using System;
using System.IO;
using UnityEngine;

namespace TuringSignalRelay.Editor
{
    public static class RelayStageChecks
    {
        [UnityEditor.MenuItem("Turing Signal Relay/Run Movement and Plain Report Scenario")]
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            var config = JsonUtility.FromJson<RelayConfig>(File.ReadAllText(Path.Combine(root, "fixtures/protocol/relay-config.json")));
            var map = JsonUtility.FromJson<RelayMapData>(File.ReadAllText(Path.Combine(root, "fixtures/world/map-v1.json")));
            var recording = RelayScenario.Run(config, map);
            var verified = RelayReplay.Verify(recording);
            var final = verified.GetSnapshot();
            if (final.tick != 9000 || final.messages.Count != 60 || final.scoreHalfPoints[0] <= 0 || final.scoreHalfPoints[0] != final.scoreHalfPoints[1])
                throw new Exception("Native relay scenario did not complete paired deliveries.");
            string output = Path.Combine(root, "artifacts", "relay-native-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            string replay = Path.Combine(output, "relay.replay.json");
            File.WriteAllText(replay, JsonUtility.ToJson(recording));
            File.WriteAllText(Path.Combine(output, "relay.result.json"), JsonUtility.ToJson(RelayScenario.Result(verified), true));
            var ui = new GameObject("Relay scenario UI checks");
            try
            {
                var controller = ui.AddComponent<ObserverController>();
                controller.enabled = false;
                controller.LoadDemo();
                controller.LoadReplay(replay);
                if (controller.Error != "" || controller.Playback.Frames.Count != 9001)
                    throw new Exception("Normal replay entry point rejected relay scenario: " + controller.Error);
                controller.Playback.Seek(9000);
                if (controller.Frame.scoreHalfPoints[0] != final.scoreHalfPoints[0] || !controller.Frame.staticTerrainKnown)
                    throw new Exception("Observer state does not match the authoritative result.");
                ui.AddComponent<ObserverHud>().Initialize(controller);
                ObserverInteractionChecks.Render(ui);
                var version = controller.SourceVersion;
                recording.frames[10].stateHash = new string('0', 64);
                File.WriteAllText(replay + ".tampered.json", JsonUtility.ToJson(recording));
                controller.LoadReplay(replay + ".tampered.json");
                if (controller.Error == "" || controller.SourceVersion != version || controller.Frame.tick != 9000)
                    throw new Exception("Failed relay replay state: error=" + controller.Error + " source=" + controller.SourceVersion +
                        " expectedSource=" + version + " tick=" + controller.Frame.tick);
            }
            finally { UnityEngine.Object.DestroyImmediate(ui); }
            Debug.Log("RELAY_STAGE123_CHECKS_PASSED ticks=9000 scoreHalf=" + final.scoreHalfPoints[0] +
                " stateHash=" + verified.StateHash() + " output=" + output);
        }
    }
}
