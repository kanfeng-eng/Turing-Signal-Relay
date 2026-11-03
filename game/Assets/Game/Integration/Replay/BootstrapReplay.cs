using System;
using System.IO;
using UnityEngine;

namespace TuringSignalRelay
{
    [Serializable]
    public sealed class BootstrapReplayHeader
    {
        public string type = "bootstrap-replay";
        public int replayVersion = 1;
        public MatchConfig config;
    }

    // Bootstrap snapshot stream only. Formal actions/events/checkpoints need 2/3/5.
    public sealed class BootstrapReplayWriter : IDisposable
    {
        private readonly StreamWriter writer;
        public BootstrapReplayWriter(string path, MatchConfig config)
        {
            config.Validate();
            writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
            writer.WriteLine(JsonUtility.ToJson(new BootstrapReplayHeader { config = config }));
        }
        public void Write(Snapshot snapshot) { writer.WriteLine(JsonUtility.ToJson(snapshot)); }
        public void Dispose() { writer.Dispose(); }
    }

    public static class BootstrapReplayReader
    {
        public static Snapshot ReadFinal(string path)
        {
            using (var reader = new StreamReader(path))
            {
                string line = reader.ReadLine();
                if (line == null) throw new InvalidDataException("Missing replay header.");
                var header = JsonUtility.FromJson<BootstrapReplayHeader>(line);
                if (header == null || header.type != "bootstrap-replay" || header.replayVersion != 1 || header.config == null)
                    throw new InvalidDataException("Unsupported bootstrap replay.");
                header.config.Validate();
                int nextTick = 0;
                Snapshot last = null;
                while ((line = reader.ReadLine()) != null)
                {
                    var snapshot = JsonUtility.FromJson<Snapshot>(line);
                    if (snapshot == null || snapshot.protocolVersion != 1 || snapshot.tick != nextTick ||
                        snapshot.rulesVersion != header.config.rulesVersion || snapshot.scoreHalfPoints == null ||
                        snapshot.scoreHalfPoints.Length != 2 || snapshot.completed != (nextTick == header.config.durationTicks))
                        throw new InvalidDataException("Invalid snapshot sequence.");
                    last = snapshot;
                    nextTick++;
                }
                if (last == null || last.tick != header.config.durationTicks)
                    throw new InvalidDataException("Incomplete bootstrap replay.");
                return last;
            }
        }
    }
}
