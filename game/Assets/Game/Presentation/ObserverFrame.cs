using System;

namespace TuringSignalRelay
{
    [Serializable]
    public sealed class ObserverEntity
    {
        public int id, side, kind, visibleToMask;
        public float x, y;
        public string label;
        public bool suppressed;
    }
    [Serializable]
    public sealed class ObserverLink
    {
        public int from, to;
        public bool connected;
    }
    [Serializable]
    public sealed class ObserverFrame
    {
        public int protocolVersion;
        public string type;
        public int tick;
        public int durationTicks = 9000;
        public int width = 96, height = 96;
        public int[] terrain = new int[0];
        public int[] visibleToMask = new int[0];
        public int[] scoreHalfPoints = new int[2];
        public ObserverEntity[] entities = new ObserverEntity[0];
        public ObserverLink[] links = new ObserverLink[0];
        public string weather = "Clear", eventText = "", rulesVersion = "observer-fixture";
        public float hubProgress;
        public int eventFocusId = -1;
        public int nextEventTick = -1;

        public void Validate()
        {
            if (protocolVersion != 1 || type != "observer-frame" || tick < 0 || durationTicks <= 0 || tick > durationTicks || width <= 0 || height <= 0 ||
                width > 96 || height > 96 || scoreHalfPoints == null || scoreHalfPoints.Length != 2 ||
                terrain == null || visibleToMask == null || terrain.Length != width * height ||
                visibleToMask.Length != terrain.Length || entities == null || links == null ||
                float.IsNaN(hubProgress) || hubProgress < 0 || hubProgress > 1)
                throw new ArgumentException("Invalid observer frame.");
            foreach (int tile in terrain)
            if (tile < 0 || tile > 3)
                throw new ArgumentException("Invalid terrain.");
            foreach (int mask in visibleToMask)
            if (mask < 0 || mask > 3)
                throw new ArgumentException("Invalid visibility mask.");
            var ids = new System.Collections.Generic.HashSet<int>();
            foreach (var entity in entities)
                if (entity == null || entity.id < 0 || !ids.Add(entity.id) || entity.side < 0 || entity.side > 1 || entity.kind < 0 || entity.kind > 4 ||
                    float.IsNaN(entity.x) || float.IsNaN(entity.y) || entity.x < 0 || entity.y < 0 ||
                    entity.x >= width || entity.y >= height || entity.visibleToMask < 0 || entity.visibleToMask > 3)
                    throw new ArgumentException("Invalid observer entity.");
            foreach (var link in links)
                if (link == null || !ids.Contains(link.from) || !ids.Contains(link.to))
                    throw new ArgumentException("Invalid observer link.");
        }
        public bool Visible(ObserverEntity entity, int side)
        {
            if (side < -1 || side > 1)
                throw new ArgumentOutOfRangeException("side");
            return side < 0 || (entity.visibleToMask & (1 << side)) != 0;
        }
    }
    [Serializable]
    public sealed class ObserverSnapshotResponse
    {
        public int protocolVersion;
        public int tick;
        public string status;
        public ObserverFrame snapshot;
    }
    [Serializable]
    public sealed class ObserverRecording
    {
        public string type;
        public int replayVersion;
        public int ticksPerSecond;
        public ObserverFrame[] frames;
    }
}
