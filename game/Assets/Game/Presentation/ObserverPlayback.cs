using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TuringSignalRelay
{
    public sealed class ObserverPlayback
    {
        public readonly List<ObserverFrame> Frames = new List<ObserverFrame>();
        public int Index
        {
            get; private set;
        }
        public bool Playing;
        public float Speed = 1;
        public int TicksPerSecond = 10;
        private double elapsed;
        public ObserverFrame Current
        {
            get
            {
                return Frames[Index];
            }
        }

        public void Load(string text)
        {
            var pending = new List<ObserverFrame>();
            int rate = 10;
            var data = JsonUtility.FromJson<ObserverRecording>(text);
            if (data != null && data.type == "relay-replay")
            {
                LoadRelay(text);
                return;
            }
            if (data == null || data.type != "observer-recording" || data.replayVersion != 1 ||
                data.ticksPerSecond <= 0 || data.frames == null || data.frames.Length == 0 || data.frames.Length > 9001)
                throw new InvalidDataException("Unsupported observer recording.");
            rate = data.ticksPerSecond;
            foreach (var frame in data.frames)
            {
                if (frame == null)
                    throw new InvalidDataException("Missing observer frame.");
                frame.Validate();
                frame.ReuseUnchangedMap(pending.Count == 0 ? null : pending[pending.Count - 1]);
                pending.Add(frame);
            }
            for (int i = 0; i < pending.Count; i++)
                if (pending[i].tick != i)
                    throw new InvalidDataException("Recording must contain each tick from zero.");
            Frames.Clear();
            Frames.AddRange(pending);
            TicksPerSecond = rate;
            Seek(0);
            Playing = false;
        }
        public void LoadBootstrap(string path)
        {
            var pending = new List<ObserverFrame>();
            var emptyTerrain = new int[96 * 96];
            var hidden = new int[96 * 96];
            BootstrapReplayHeader header;
            using (var reader = File.OpenText(path))
                header = JsonUtility.FromJson<BootstrapReplayHeader>(reader.ReadLine());
            foreach (var snapshot in BootstrapReplayReader.ReadSnapshots(path))
                pending.Add(new ObserverFrame
                {
                    protocolVersion = 1,
                    type = "observer-frame",
                    tick = snapshot.tick,
                    durationTicks = header.config.durationTicks,
                    scoreHalfPoints = snapshot.scoreHalfPoints,
                    terrain = emptyTerrain,
                    visibleToMask = hidden,
                    rulesVersion = snapshot.rulesVersion
                });
            Frames.Clear();
            Frames.AddRange(pending);
            TicksPerSecond = header.config.ticksPerSecond;
            Seek(0);
            Playing = false;
        }
        public void LoadRelay(string text)
        {
            var recording = JsonUtility.FromJson<RelayRecording>(text);
            if (recording == null || recording.config == null || recording.map == null)
                throw new InvalidDataException("Missing relay replay configuration or map.");
            var builder = new ObserverSnapshotBuilder(recording.config, recording.map);
            var pending = new List<ObserverFrame>();
            RelayReplay.Verify(recording, engine => {
                var frame = builder.Create(engine.GetSnapshot());
                frame.Validate();
                frame.ReuseUnchangedMap(pending.Count == 0 ? null : pending[pending.Count - 1]);
                pending.Add(frame);
            });
            if (recording.forfeit) pending[pending.Count - 1].eventText = "Forfeit side=" + recording.faultSide + " " + recording.terminationReason;
            Frames.Clear(); Frames.AddRange(pending);
            TicksPerSecond = 10; Seek(0); Playing = false;
        }
        public void Seek(int index)
        {
            Index = Math.Max(0, Math.Min(index, Frames.Count - 1));
            elapsed = 0;
        }
        public void Step()
        {
            Playing = false;
            Seek(Index + 1);
        }
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || float.IsNaN(Speed) || float.IsInfinity(Speed) || Speed <= 0)
                throw new ArgumentOutOfRangeException("seconds");
            if (!Playing || Frames.Count == 0)
                return;
            elapsed += seconds * Speed * TicksPerSecond;
            int steps = (int)Math.Min(elapsed, Frames.Count);
            elapsed -= steps;
            Index = Math.Min(Index + steps, Frames.Count - 1);
            if (Index == Frames.Count - 1)
                Playing = false;
        }
    }
}
