using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TuringSignalRelay;

internal static class Program
{
    internal static readonly JsonSerializerOptions Json = new JsonSerializerOptions {
        IncludeFields = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static T Read<T>(string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
    }

    internal static void Write<T>(string path, T value)
    {
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            JsonSerializer.Serialize(stream, value, Json);
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 3 && args[0] == "verify")
            {
                var verifiedResult = RelayScenario.Result(RelayReplay.Verify(Read<RelayRecording>(args[1])));
                Write(args[2], verifiedResult);
                Console.WriteLine("RELAY_REPLAY_VERIFIED " + verifiedResult.stateHash);
                return 0;
            }
            if (args.Length != 3 || (args[0] != "check" && args[0] != "demo"))
                throw new ArgumentException("Usage: RelayRunner check|demo ROOT NEW_OUTPUT_DIRECTORY, or verify REPLAY NEW_RESULT_FILE");
            string root = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]);
            if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Use a new output directory.");
            var config = Read<RelayConfig>(Path.Combine(root, "fixtures/protocol/relay-config.json"));
            var map = Read<RelayMapData>(Path.Combine(root, "fixtures/world/map-v1.json"));
            config.Validate();
            if (args[0] == "check")
            {
                RelayChecks.Run(config, map);
                var sampleEngine = new RelayMatchEngine(config, map);
                var sampleState = Read<RelayState>(Path.Combine(root, "fixtures/protocol/relay-snapshot.json"));
                var sampleFrame = Read<RelayObservation>(Path.Combine(root, "fixtures/protocol/relay-frame.json"));
                if (RelayStateHash.Compute(config, sampleEngine.MapHash, sampleState) != sampleEngine.StateHash() ||
                    JsonSerializer.Serialize(sampleFrame, Json) != JsonSerializer.Serialize(sampleEngine.GetObservation(0), Json))
                    throw new Exception("Protocol fixtures differ from the authoritative initial state.");
                sampleEngine.Step(Read<RelayCommandBatch>(Path.Combine(root, "fixtures/protocol/relay-command.json")), null);
                if (sampleEngine.GetSnapshot().commandResults.Length != 1 || !sampleEngine.GetSnapshot().commandResults[0].accepted)
                    throw new Exception("Protocol command fixture was rejected.");
                Console.WriteLine("RELAY_PROTOCOL_FIXTURES_PASSED");
            }
            Directory.CreateDirectory(output);
            var initial = new RelayMatchEngine(config, map);
            Write(Path.Combine(output, "initial.frame.red.json"), initial.GetObservation(0));
            Write(Path.Combine(output, "initial.snapshot.json"), initial.GetSnapshot());
            var recording = RelayScenario.Run(config, map);
            var replayPath = Path.Combine(output, "relay.replay.json");
            Write(replayPath, recording);
            // Read back through the public file format before claiming success.
            var final = RelayReplay.Verify(Read<RelayRecording>(replayPath));
            var result = RelayScenario.Result(final);
            Write(Path.Combine(output, "relay.result.json"), result);
            if (result.finalSnapshot.messages.Count != 60 || result.finalSnapshot.ledger.Count == 0 ||
                result.finalSnapshot.scoreHalfPoints[0] <= 0 || result.finalSnapshot.scoreHalfPoints[1] <= 0)
                throw new Exception("Both scripted couriers must deliver reports in the full match.");
            Console.WriteLine("RELAY_SCENARIO_PASSED ticks=" + final.Tick + " scoresHalf=" +
                result.finalSnapshot.scoreHalfPoints[0] + "," + result.finalSnapshot.scoreHalfPoints[1] + " hash=" + result.stateHash);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
