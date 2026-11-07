using System;

namespace TuringSignalRelay
{
    public sealed class TickClock
    {
        public int Tick { get; private set; }
        public int DurationTicks { get; private set; }
        public bool Paused { get; set; }
        public bool Completed { get { return Tick == DurationTicks; } }
        public double SecondsPerTick { get; private set; }
        private double accumulatedSeconds;

        public TickClock(MatchConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");
            config.Validate();
            DurationTicks = config.durationTicks;
            SecondsPerTick = 1.0 / config.ticksPerSecond;
        }

        // The callback lets the adapter capture every tick, even with a slow UI frame.
        public void Advance(double seconds, Action<int> onTick)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException("seconds");
            if (Paused || Completed) return;
            accumulatedSeconds += seconds;
            while (!Completed && accumulatedSeconds + 1e-9 >= SecondsPerTick)
            {
                accumulatedSeconds = Math.Max(0, accumulatedSeconds - SecondsPerTick);
                Step(onTick);
            }
        }

        // Explicit stepping is available when paused for bootstrap inspection.
        public void Step(Action<int> onTick)
        {
            if (Completed) return;
            Tick++;
            if (onTick != null) onTick(Tick);
        }
    }
}
