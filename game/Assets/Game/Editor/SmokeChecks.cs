using System;
using System.IO;
using UnityEngine;

namespace TuringSignalRelay.Editor
{
    public static class SmokeChecks
    {
        [UnityEditor.MenuItem("Turing Signal Relay/Run Bootstrap Checks")]
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");
            if (UnityEngine.Object.FindFirstObjectByType<BootstrapController>() == null)
                throw new Exception("Bootstrap scene is missing its controller.");
            Tests.BootstrapChecks.Run();
            string path = Path.Combine(Path.GetTempPath(), "tsr-bootstrap-" + Guid.NewGuid() + ".jsonl");
            try
            {
                var config = new MatchConfig { durationTicks = 3 };
                var engine = new MatchEngine(config);
                using (var writer = new BootstrapReplayWriter(path, config))
                {
                    writer.Write(engine.GetSnapshot());
                    engine.Advance(1, writer.Write);
                }
                if (BootstrapReplayReader.ReadFinal(path).tick != 3)
                    throw new Exception("Replay did not retain final tick.");
                File.WriteAllText(path, "{\"type\":\"bootstrap-replay\",\"replayVersion\":99}");
                bool rejected = false;
                try { BootstrapReplayReader.ReadFinal(path); }
                catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new Exception("Unknown replay version accepted.");
                Debug.Log("BOOTSTRAP_CHECKS_PASSED");
            }
            finally { File.Delete(path); }
        }
    }
}
