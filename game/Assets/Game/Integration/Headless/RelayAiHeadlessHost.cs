using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TuringSignalRelay
{
    public sealed class UnityRelayAiJson : IRelayAiJson
    {
        public string Initialization(RelayAiInit value) { return JsonUtility.ToJson(value); }
        public string Frame(RelayObservation value) { return JsonUtility.ToJson(value); }
        public RelayAiReady Ready(string text) { return JsonUtility.FromJson<RelayAiReady>(text); }
        public RelayCommandBatch Commands(string text) { return JsonUtility.FromJson<RelayCommandBatch>(text); }
    }

    public sealed class RelayAiHeadlessHost : MonoBehaviour
    {
        private Task<RelayAiGame> match;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private string replayPath, resultPath, reportPath;
        private bool finished;
        private string[] binaryHashes;

        public void Begin(RelayConfig config, RelayMapData map, string[] programs, string replay, string result)
        {
            replayPath = replay; resultPath = result; reportPath = result + ".ai.json";
            if (File.Exists(reportPath)) throw new IOException("AI diagnostics output already exists.");
            bool strict = File.Exists("/proc/self/schedstat");
            var codec = new UnityRelayAiJson();
            binaryHashes = new[] { HashFile(programs[0]), HashFile(programs[1]) };
            match = Task.Run(() => RelayAiSession.RunAsync(config, map, programs, codec, strict, cancellation.Token));
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (finished || match == null || !match.IsCompleted) return;
            finished = true;
            try
            {
                var game = match.GetAwaiter().GetResult();
                game.recording.aiBinaryHashes = binaryHashes;
                RelayReplay.Verify(game.recording);
                WriteNew(replayPath, JsonUtility.ToJson(game.recording));
                WriteNew(resultPath, JsonUtility.ToJson(game.result, true));
                WriteNew(reportPath, JsonUtility.ToJson(game, true));
                Debug.Log("RELAY_CPP_COMPLETED ticks=" + game.result.finalSnapshot.tick + " forfeit=" + game.recording.forfeit + " stateHash=" + game.result.stateHash);
                Application.Quit(0);
            }
            catch (Exception error) { Debug.LogException(error); Application.Quit(1); }
        }

        private static string HashFile(string path)
        {
            using (var input = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }
        private static void WriteNew(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(text);
        }
        private void OnDestroy() { cancellation.Cancel(); }
    }
}
