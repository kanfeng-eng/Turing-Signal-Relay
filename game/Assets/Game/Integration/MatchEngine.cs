using System;

namespace TuringSignalRelay
{
    // Bootstrap engine: a clock and snapshots, NOT a competitive rules engine.
    public sealed class MatchEngine
    {
        public TickClock Clock { get; private set; }
        public MatchState State { get; private set; }
        private readonly MatchConfig config;

        public MatchEngine(MatchConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");
            this.config = new MatchConfig {
                protocolVersion = config.protocolVersion, ticksPerSecond = config.ticksPerSecond,
                durationTicks = config.durationTicks, seed = config.seed, rulesVersion = config.rulesVersion
            };
            Clock = new TickClock(this.config);
            State = new MatchState();
        }

        public void Advance(double seconds, Action<Snapshot> capture = null)
        {
            Clock.Advance(seconds, tick => CaptureTick(tick, capture));
        }

        public void Step(Action<Snapshot> capture = null)
        {
            Clock.Step(tick => CaptureTick(tick, capture));
        }

        private void CaptureTick(int tick, Action<Snapshot> capture)
        {
            State.tick = tick;
            if (capture != null) capture(GetSnapshot());
        }

        public Snapshot GetSnapshot()
        {
            return new Snapshot {
                tick = State.tick, completed = Clock.Completed,
                scoreHalfPoints = (int[])State.scoreHalfPoints.Clone(), rulesVersion = config.rulesVersion
            };
        }
    }
}
