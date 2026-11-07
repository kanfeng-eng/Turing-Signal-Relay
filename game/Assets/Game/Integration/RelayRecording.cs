using System;

namespace TuringSignalRelay
{
    [Serializable]
    public sealed class RelayRecordedTick
    {
        public int tick;
        public RelayCommandBatch[] batches = new RelayCommandBatch[0];
        public string stateHash;
    }

    [Serializable]
    public sealed class RelayRecording
    {
        public string type = "relay-replay";
        public int replayVersion = 1;
        public RelayConfig config;
        public RelayMapData map;
        public string mapHash, initialStateHash;
        public RelayRecordedTick[] frames;
        public string[] aiSourceHashes = new string[0], aiBinaryHashes = new string[0];
        public string sdkSourceHash = "";
        public bool forfeit;
        public int faultSide = -1;
        public string terminationReason = "";
    }

    public static class RelayReplay
    {
        public static RelayMatchEngine Verify(RelayRecording recording, Action<RelayMatchEngine> capture = null)
        {
            if (recording == null || recording.type != "relay-replay" || recording.replayVersion != 1 || recording.config == null ||
                recording.frames == null || recording.frames.Length > recording.config.durationTicks ||
                (!recording.forfeit && recording.frames.Length != recording.config.durationTicks) ||
                (recording.forfeit && (recording.faultSide < 0 || recording.faultSide > 2 || string.IsNullOrWhiteSpace(recording.terminationReason))))
                throw new ArgumentException("Invalid or incomplete relay replay.");
            var engine = new RelayMatchEngine(recording.config, recording.map);
            if (engine.MapHash != recording.mapHash || engine.StateHash() != recording.initialStateHash)
                throw new ArgumentException("Replay initial state or map digest mismatch.");
            if (capture != null) capture(engine);
            foreach (var frame in recording.frames)
            {
                if (frame == null || frame.tick != engine.Tick + 1) throw new ArgumentException("Replay skipped a tick.");
                if (frame.batches == null || frame.batches.Length > 2) throw new ArgumentException("Invalid replay command array.");
                RelayCommandBatch red = null, blue = null;
                foreach (var batch in frame.batches)
                {
                    if (batch == null || batch.side < 0 || batch.side > 1 || (batch.side == 0 ? red != null : blue != null))
                        throw new ArgumentException("Duplicate or invalid replay side.");
                    if (batch.side == 0) red = batch;
                    else blue = batch;
                }
                engine.Step(red, blue);
                if (engine.StateHash() != frame.stateHash) throw new ArgumentException("Replay state digest mismatch at tick " + frame.tick);
                if (capture != null) capture(engine);
            }
            return engine;
        }
    }
}
