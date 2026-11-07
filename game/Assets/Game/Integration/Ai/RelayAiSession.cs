using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TuringSignalRelay
{
    [Serializable]
    public sealed class RelayAiInit
    {
        public string type = "relay-init";
        public int protocolVersion = 2, side;
        public RelayConfig config;
        public RelayMapData map;
    }
    [Serializable]
    public sealed class RelayAiReady
    {
        public string type;
        public int protocolVersion, side;
    }
    public interface IRelayAiJson
    {
        string Initialization(RelayAiInit value);
        string Frame(RelayObservation value);
        RelayAiReady Ready(string text);
        RelayCommandBatch Commands(string text);
    }
    [Serializable]
    public sealed class RelayAiGame
    {
        public string type = "cpp-ai-match";
        public RelayRecording recording;
        public RelayResult result;
        public bool cpuBudgetEnforced;
        public string[] failures = new string[2], diagnostics = new string[2];
        public int[] timeouts = new int[2];
        public long[] observedPeakBytes = new long[2];
    }

    public static class RelayAiSession
    {
        public static async Task<RelayAiGame> RunAsync(RelayConfig config, RelayMapData map, string[] paths, IRelayAiJson json,
            bool strictCpu, CancellationToken cancellation, Action<int> progress = null, Func<int, AiProcessBridge> bridgeFactory = null, Action<RelayMatchEngine> capture = null)
        {
            if (paths == null || paths.Length != 2 || json == null) throw new ArgumentException("Two programs and a codec are required.");
            var engine = new RelayMatchEngine(config, map);
            var game = new RelayAiGame { cpuBudgetEnforced = strictCpu, recording = new RelayRecording {
                config = engine.Config, map = engine.MapData, mapHash = engine.MapHash, initialStateHash = engine.StateHash() } };
            var frames = new List<RelayRecordedTick>();
            if (capture != null) capture(engine);
            var bridges = new AiProcessBridge[2];
            try
            {
                for (int side = 0; side < 2; side++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    try
                    {
                        bridges[side] = bridgeFactory == null ? new AiProcessBridge(paths[side]) : bridgeFactory(side);
                        if (strictCpu && !bridges[side].PreciseCpuAvailable) throw new PlatformNotSupportedException("Precise CPU accounting unavailable.");
                        string readyText = await bridges[side].ExchangeAsync(json.Initialization(new RelayAiInit { side = side, config = config, map = map }),
                            -1, null, 2000, false, cancellation);
                        if (readyText == null) throw new System.IO.InvalidDataException("AI startup timeout.");
                        var ready = json.Ready(readyText);
                        if (ready == null || ready.type != "relay-ready" || ready.protocolVersion != 2 || ready.side != side)
                            throw new System.IO.InvalidDataException("Invalid initialization response.");
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (PlatformNotSupportedException) { throw; }
                    catch (Exception error) { game.failures[side] = error.GetType().Name + ": " + error.Message; }
                }
                while (!engine.Completed && game.failures[0] == null && game.failures[1] == null)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var first = Request(bridges[0], engine, 0, json, game, cancellation);
                    var second = Request(bridges[1], engine, 1, json, game, cancellation);
                    var replies = await Task.WhenAll(first, second);
                    if (game.failures[0] != null || game.failures[1] != null) break;
                    engine.Step(replies[0], replies[1]);
                    var batches = new List<RelayCommandBatch>();
                    foreach (var reply in replies) if (reply != null) batches.Add(reply);
                    frames.Add(new RelayRecordedTick { tick = engine.Tick, batches = batches.ToArray(), stateHash = engine.StateHash() });
                    if (capture != null && engine.Tick % 10 == 0) capture(engine);
                    if (progress != null && engine.Tick % 1000 == 0) progress(engine.Tick);
                }
                game.recording.frames = frames.ToArray();
                if (!engine.Completed)
                {
                    game.recording.forfeit = true;
                    game.recording.faultSide = game.failures[0] != null && game.failures[1] != null ? 2 : game.failures[0] != null ? 0 : 1;
                    game.recording.terminationReason = string.Join(" | ", Array.FindAll(game.failures, value => value != null));
                }
                game.result = engine.Completed ? RelayScenario.Result(engine) : new RelayResult { status = "forfeit", config = engine.Config,
                    mapHash = engine.MapHash, stateHash = engine.StateHash(), finalSnapshot = engine.GetSnapshot() };
                for (int side = 0; side < 2; side++)
                    if (bridges[side] != null)
                    {
                        game.observedPeakBytes[side] = bridges[side].ObservedPeakBytes;
                        if (game.failures[side] == null) bridges[side].Complete();
                    }
            }
            finally
            {
                for (int side = 0; side < 2; side++)
                    if (bridges[side] != null) { bridges[side].Dispose(); game.diagnostics[side] = bridges[side].Diagnostics; }
            }
            return game;
        }

        private static async Task<RelayCommandBatch> Request(AiProcessBridge bridge, RelayMatchEngine engine, int side,
            IRelayAiJson json, RelayAiGame game, CancellationToken cancellation)
        {
            try
            {
                RelayCommandBatch reply = null;
                string text = await bridge.ExchangeAsync(json.Frame(engine.GetObservation(side)), engine.Tick, value => {
                    reply = json.Commands(value);
                    if (reply == null || reply.type != "relay-commands" || reply.protocolVersion != 2 || reply.side != side)
                        throw new System.IO.InvalidDataException("Invalid response envelope.");
                    return reply.tick;
                }, 100, game.cpuBudgetEnforced, cancellation);
                if (text == null)
                {
                    if (++game.timeouts[side] >= 3) game.failures[side] = "Three computation timeouts.";
                    return null;
                }
                if (reply.tick != engine.Tick || reply.actions == null || reply.actions.Length > 128 || Array.Exists(reply.actions, action => action == null))
                    throw new System.IO.InvalidDataException("Invalid command batch.");
                return reply;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { game.failures[side] = error.GetType().Name + ": " + error.Message; return null; }
        }
    }
}
