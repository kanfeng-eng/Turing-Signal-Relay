using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TuringSignalRelay
{
    [Serializable]
    public sealed class BootstrapReplayHeader
    {
        public string type;
        public int replayVersion;
        public MatchConfig config;
    }

    [Serializable]
    public sealed class BootstrapReplayFrame
    {
        public Snapshot snapshot;
        public string stateHash;
    }

    // Bootstrap snapshot stream only. Formal actions/events/checkpoints need 2/3/5.
    public sealed class BootstrapReplayWriter : IDisposable
    {
        private readonly StreamWriter writer;
        private readonly MatchConfig config;
        public BootstrapReplayWriter(string path, MatchConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");
            config.Validate();
            this.config = new MatchConfig {
                protocolVersion = config.protocolVersion, ticksPerSecond = config.ticksPerSecond,
                durationTicks = config.durationTicks, seed = config.seed, rulesVersion = config.rulesVersion
            };
            writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new System.Text.UTF8Encoding(false));
            writer.WriteLine(JsonUtility.ToJson(new BootstrapReplayHeader { type = "bootstrap-replay", replayVersion = 2, config = this.config }));
        }
        public void Write(Snapshot snapshot)
        {
            var frame = new BootstrapReplayFrame { snapshot = snapshot, stateHash = BootstrapStateHash.Compute(config, snapshot) };
            writer.WriteLine(JsonUtility.ToJson(frame));
        }
        public void Dispose() { writer.Dispose(); }
    }

    public static class BootstrapReplayReader
    {
        // Exhaust the sequence to validate completion; early consumers validate only their consumed prefix.
        // Version 1 remains readable but has no per-frame hash verification.
        public static IEnumerable<Snapshot> ReadSnapshots(string path)
        {
            using (var reader = new StreamReader(path))
            {
                string line = reader.ReadLine();
                if (line == null) throw new InvalidDataException("Missing replay header.");
                var header = Parse<BootstrapReplayHeader>(line);
                if (header == null || header.type != "bootstrap-replay" ||
                    (header.replayVersion != 1 && header.replayVersion != 2) || header.config == null)
                    throw new InvalidDataException("Unsupported bootstrap replay.");
                header.config.Validate();
                long nextTick = 0;
                Snapshot last = null;
                while ((line = reader.ReadLine()) != null)
                {
                    var frame = header.replayVersion == 2 ? Parse<BootstrapReplayFrame>(line) : null;
                    var snapshot = header.replayVersion == 1 ? Parse<Snapshot>(line) : frame == null ? null : frame.snapshot;
                    if (snapshot == null || snapshot.tick != nextTick || nextTick > header.config.durationTicks)
                        throw new InvalidDataException("Invalid snapshot sequence at tick " + nextTick);
                    string hash;
                    try { hash = BootstrapStateHash.Compute(header.config, snapshot); }
                    catch (ArgumentException error) { throw new InvalidDataException("Invalid snapshot at tick " + nextTick, error); }
                    if (header.replayVersion == 2 && frame.stateHash != hash)
                        throw new InvalidDataException("State hash mismatch at tick " + nextTick);
                    last = snapshot;
                    nextTick++;
                    yield return snapshot;
                }
                if (last == null || last.tick != header.config.durationTicks)
                    throw new InvalidDataException("Incomplete bootstrap replay.");
            }
        }

        private static T Parse<T>(string line)
        {
            try { return JsonUtility.FromJson<T>(line); }
            catch (ArgumentException error) { throw new InvalidDataException("Malformed bootstrap replay JSON.", error); }
        }

        public static Snapshot ReadFinal(string path)
        {
            Snapshot last = null;
            foreach (var snapshot in ReadSnapshots(path)) last = snapshot;
            return last;
        }
    }
}
