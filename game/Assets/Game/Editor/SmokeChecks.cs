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
                int frames = 0;
                foreach (var snapshot in BootstrapReplayReader.ReadSnapshots(path))
                {
                    if (snapshot.tick != frames) throw new Exception("Streaming replay skipped a tick.");
                    frames++;
                }
                if (frames != 4 || BootstrapReplayReader.ReadFinal(path).tick != 3)
                    throw new Exception("Replay did not retain all ticks.");
                string[] lines = File.ReadAllLines(path);
                var frame = JsonUtility.FromJson<BootstrapReplayFrame>(lines[2]);
                frame.snapshot.scoreHalfPoints[0]++;
                string[] changed = (string[])lines.Clone();
                changed[2] = JsonUtility.ToJson(frame);
                ExpectRejected(path, changed, "Changed score with unchanged hash");
                ExpectRejected(path, new[] { lines[0], lines[1], lines[2] }, "Truncated replay");
                changed = (string[])lines.Clone();
                changed[2] = lines[1];
                ExpectRejected(path, changed, "Duplicate tick");
                changed = (string[])lines.Clone();
                changed[2] = "{broken-json";
                ExpectRejected(path, changed, "Malformed JSON");
                var header = JsonUtility.FromJson<BootstrapReplayHeader>(lines[0]);
                header.config.seed++;
                changed = (string[])lines.Clone();
                changed[0] = JsonUtility.ToJson(header);
                ExpectRejected(path, changed, "Changed header seed");
                ExpectRejected(path, new[] { "{\"type\":\"bootstrap-replay\",\"replayVersion\":99}" }, "Unknown version");
                header = JsonUtility.FromJson<BootstrapReplayHeader>(lines[0]);
                header.replayVersion = 1;
                changed[0] = JsonUtility.ToJson(header);
                for (int i = 1; i < lines.Length; i++)
                    changed[i] = JsonUtility.ToJson(JsonUtility.FromJson<BootstrapReplayFrame>(lines[i]).snapshot);
                File.WriteAllLines(path, changed);
                if (BootstrapReplayReader.ReadFinal(path).tick != 3)
                    throw new Exception("Legacy v1 replay is no longer readable.");
                Debug.Log("BOOTSTRAP_CHECKS_PASSED");
            }
            finally { File.Delete(path); }
        }

        private static void ExpectRejected(string path, string[] lines, string label)
        {
            File.WriteAllLines(path, lines);
            bool rejected = false;
            try { BootstrapReplayReader.ReadFinal(path); }
            catch (InvalidDataException) { rejected = true; }
            if (!rejected) throw new Exception(label + " accepted.");
        }
    }
}
