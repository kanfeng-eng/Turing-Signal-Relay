using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TuringSignalRelay
{
    [Serializable]
    public sealed class BootstrapResult
    {
        public string type = "bootstrap-result";
        public int resultVersion = 1;
        public string status = "bootstrap-completed";
        public string unityVersion;
        public int replayVersion = 2;
        public MatchConfig config;
        public Snapshot finalSnapshot;
        public string stateHash;
        public string replaySha256;
    }

    public static class HeadlessEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.Exists(args, value => value.StartsWith("--relay-", StringComparison.Ordinal))) return;
            if (!Array.Exists(args, value => value.StartsWith("--bootstrap-", StringComparison.Ordinal))) return;
            try
            {
                foreach (string arg in args)
                    if (arg.StartsWith("--bootstrap-", StringComparison.Ordinal) &&
                        arg != "--bootstrap-replay" && arg != "--bootstrap-config" && arg != "--bootstrap-result")
                        throw new ArgumentException("Unknown bootstrap option: " + arg);
                string path = Path.GetFullPath(GetOption(args, "--bootstrap-replay", true));
                string configPath = GetOption(args, "--bootstrap-config", false);
                string resultOption = GetOption(args, "--bootstrap-result", false);
                string resultPath = resultOption == null ? Path.ChangeExtension(path, ".result.json") : Path.GetFullPath(resultOption);
                if (File.Exists(path) || File.Exists(resultPath) || path == resultPath)
                    throw new IOException("Replay/result paths must be distinct new files.");
                var config = configPath == null ? new MatchConfig() : JsonUtility.FromJson<MatchConfig>(File.ReadAllText(configPath));
                if (config == null) throw new ArgumentException("Missing bootstrap configuration.");
                config.Validate();
                var engine = new MatchEngine(config);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
                using (var writer = new BootstrapReplayWriter(path, config))
                {
                    writer.Write(engine.GetSnapshot());
                    while (!engine.Clock.Completed) engine.Step(writer.Write);
                }
                var final = BootstrapReplayReader.ReadFinal(path);
                string replayHash;
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                    replayHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                var result = new BootstrapResult {
                    unityVersion = Application.unityVersion, config = config, finalSnapshot = final,
                    stateHash = BootstrapStateHash.Compute(config, final), replaySha256 = replayHash
                };
                using (var stream = new FileStream(resultPath, FileMode.CreateNew, FileAccess.Write))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.WriteLine(JsonUtility.ToJson(result, true));
                Debug.Log("BOOTSTRAP_COMPLETED ticks=" + final.tick + " stateHash=" + result.stateHash +
                    " replay=" + path + " result=" + resultPath);
                Application.Quit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Application.Quit(1);
            }
        }

        private static string GetOption(string[] args, string name, bool required)
        {
            int index = Array.IndexOf(args, name);
            if (index < 0)
            {
                if (required) throw new ArgumentException("Required option: " + name);
                return null;
            }
            if (Array.LastIndexOf(args, name) != index || index + 1 >= args.Length ||
                string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                throw new ArgumentException("Expected one path after " + name);
            return args[index + 1];
        }
    }
}
