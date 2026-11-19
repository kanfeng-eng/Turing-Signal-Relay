using System;

namespace TuringSignalRelay
{
    // M0 draft only: no movement precision or gameplay ordering is frozen here.
    [Serializable]
    public sealed class MatchConfig
    {
        public int protocolVersion = 1;
        public int ticksPerSecond = 10;
        public int durationTicks = 9000;
        public int seed;
        public string rulesVersion = "bootstrap-provisional";

        public void Validate()
        {
            if (protocolVersion != 1 || ticksPerSecond <= 0 || durationTicks <= 0)
                throw new ArgumentException("Unsupported protocol or invalid tick configuration.");
            if (string.IsNullOrWhiteSpace(rulesVersion))
                throw new ArgumentException("rulesVersion is required.");
        }
    }

    [Serializable]
    public sealed class MatchState
    {
        public int tick;
        public int[] scoreHalfPoints = new int[2];
    }

    [Serializable]
    public sealed class Command
    {
        public int unitId;
        public string action;
        public int x;
        public int y;
    }

    [Serializable]
    public sealed class CommandBatch
    {
        public int protocolVersion = 1;
        public string type = "commands";
        public int tick;
        public Command[] actions = new Command[0];
    }

    [Serializable]
    public sealed class Observation
    {
        public int protocolVersion = 1;
        public string type = "frame";
        public int tick;
        public int side;
        // Until World/Rules define their DTOs, bootstrap frames have no entities.
        public int[] units = new int[0];
        public int[] visibleEnemies = new int[0];
        public int[] messages = new int[0];
        public int[] resources = new int[0];
        public int scoreHalfPoints;
    }

    [Serializable]
    public sealed class Snapshot
    {
        public int protocolVersion = 1;
        public int tick;
        public bool completed;
        public int[] scoreHalfPoints = new int[2];
        public string rulesVersion;
    }
}
