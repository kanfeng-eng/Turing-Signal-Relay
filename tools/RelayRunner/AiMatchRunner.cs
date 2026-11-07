using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TuringSignalRelay;

internal sealed class DotnetAiJson : IRelayAiJson
{
    public string Initialization(RelayAiInit value) { return JsonSerializer.Serialize(value, Program.Json); }
    public string Frame(RelayObservation value) { return JsonSerializer.Serialize(value, Program.Json); }
    public RelayAiReady Ready(string text) { return JsonSerializer.Deserialize<RelayAiReady>(text, Program.Json); }
    public RelayCommandBatch Commands(string text) { return JsonSerializer.Deserialize<RelayCommandBatch>(text, Program.Json); }
}

internal static class AiMatchRunner
{
    internal static async Task<RelayGameOutcome> Run(string root, string redPath, string bluePath, string output, bool strictCpu, bool fullRules)
    {
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Use a new match output directory.");
        var config = Program.Read<RelayConfig>(Path.Combine(root, "fixtures/protocol/" + (fullRules ? "relay-full-config.json" : "relay-config.json")));
        var map = Program.Read<RelayMapData>(Path.Combine(root, "fixtures/world/map-v1.json"));
        var paths = new[] { redPath, bluePath };
        var binaries = Array.ConvertAll(paths, path => Hash(File.ReadAllBytes(path)));
        string sdkHash = SdkHash(Path.Combine(root, "sdk/cpp"));
        var sources = new string[2];
        for (int side = 0; side < 2; side++)
        {
            string source = Path.Combine(root, "sdk/cpp/examples/" + Path.GetFileNameWithoutExtension(paths[side]) + ".cpp");
            sources[side] = File.Exists(source) ? Hash(File.ReadAllBytes(source)) : "";
        }
        Directory.CreateDirectory(output);
        var game = await RelayAiSession.RunAsync(config, map, paths, new DotnetAiJson(), strictCpu, CancellationToken.None,
            tick => Console.WriteLine("CPP_AI_PROGRESS tick=" + tick + " output=" + output));
        game.recording.aiSourceHashes = sources; game.recording.aiBinaryHashes = binaries;
        game.recording.sdkSourceHash = sdkHash;
        var replayPath = Path.Combine(output, "relay.replay.json");
        Program.Write(replayPath, game.recording);
        var replayed = RelayReplay.Verify(Program.Read<RelayRecording>(replayPath));
        if (replayed.StateHash() != game.result.stateHash) throw new Exception("AI match replay mismatch.");
        Program.Write(Path.Combine(output, "relay.result.json"), game.result);
        for (int side = 0; side < 2; side++) File.WriteAllText(Path.Combine(output, "ai-" + side + ".stderr.txt"), game.diagnostics[side] ?? "");
        Program.Write(Path.Combine(output, "ai.report.json"), new {
            type = "cpp-ai-match", completed = game.result.finalSnapshot.completed, game.recording.forfeit, game.recording.faultSide,
            game.failures, cpuBudgetEnforced = strictCpu, wallMilliseconds = 100, cpuMilliseconds = 10,
            game.timeouts, observedPeakWorkingSetBytes = game.observedPeakBytes, stateHash = game.result.stateHash,
            aiSourceHashes = sources, aiBinaryHashes = binaries, sdkSourceHash = game.recording.sdkSourceHash
        });
        Console.WriteLine("CPP_AI_MATCH_" + (game.result.finalSnapshot.completed ? "PASSED" : "FORFEIT") + " ticks=" + game.result.finalSnapshot.tick + " hash=" + game.result.stateHash);
        return new RelayGameOutcome { result = game.result, forfeit = game.recording.forfeit, faultSide = game.recording.faultSide };
    }

    private static string SdkHash(string directory)
    {
        string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        foreach (string file in files)
        {
            writer.Write(Path.GetRelativePath(directory, file).Replace('\\', '/'));
            byte[] bytes = File.ReadAllBytes(file); writer.Write(bytes.Length); writer.Write(bytes);
        }
        writer.Flush(); return Hash(stream.ToArray());
    }
    private static string Hash(byte[] bytes) { return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(); }
}
