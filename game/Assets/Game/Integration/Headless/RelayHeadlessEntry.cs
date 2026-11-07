using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace TuringSignalRelay
{
    public static class RelayHeadlessEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (!Array.Exists(args, value => value.StartsWith("--relay-", StringComparison.Ordinal))) return;
            try
            {
                foreach (string value in args)
                    if (value.StartsWith("--bootstrap-", StringComparison.Ordinal) ||
                        (value.StartsWith("--relay-", StringComparison.Ordinal) && value != "--relay-config" && value != "--relay-map" && value != "--relay-replay" && value != "--relay-result"))
                        throw new ArgumentException("Conflicting or unsupported headless option.");
                var config = JsonUtility.FromJson<RelayConfig>(File.ReadAllText(Option(args, "--relay-config")));
                var map = JsonUtility.FromJson<RelayMapData>(File.ReadAllText(Option(args, "--relay-map")));
                string replay = Path.GetFullPath(Option(args, "--relay-replay"));
                string result = Path.GetFullPath(Option(args, "--relay-result"));
                if (File.Exists(replay) || File.Exists(result) || string.Equals(replay, result, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Use distinct new replay and result files.");
                var recording = RelayScenario.Run(config, map);
                var engine = RelayReplay.Verify(recording);
                WriteNew(replay, JsonUtility.ToJson(recording));
                WriteNew(result, JsonUtility.ToJson(RelayScenario.Result(engine), true));
                Debug.Log("RELAY_SCENARIO_COMPLETED ticks=" + engine.Tick + " stateHash=" + engine.StateHash());
                Application.Quit(0);
            }
            catch (Exception error) { Debug.LogException(error); Application.Quit(1); }
        }

        private static string Option(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            if (index < 0 || index != Array.LastIndexOf(args, name) || index + 1 >= args.Length ||
                string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                throw new ArgumentException("Expected one value for " + name);
            return args[index + 1];
        }

        private static void WriteNew(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(text);
        }
    }
}
