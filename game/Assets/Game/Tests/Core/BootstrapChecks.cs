using System;

namespace TuringSignalRelay.Tests
{
    // Also compiled by tools/verify-bootstrap.ps1 without Unity to test pure logic.
    public static class BootstrapChecks
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        public static void Run()
        {
            var config = new MatchConfig { durationTicks = 4 };
            var engine = new MatchEngine(config);
            config.durationTicks = 99; // Caller mutation must not change this match.
            int callbacks = 0;
            engine.Advance(0.3, snapshot => callbacks++);
            Check(engine.State.tick == 3 && callbacks == 3, "Slow frame must capture each tick.");
            engine.Clock.Paused = true;
            engine.Advance(10);
            Check(engine.State.tick == 3, "Paused clock must not advance or accumulate elapsed time.");
            engine.Step();
            Check(engine.State.tick == 4 && engine.Clock.Completed, "Paused single-step must advance once and finish.");
            engine.Step();
            Check(engine.State.tick == 4, "Completed match must not advance.");

            engine = new MatchEngine(new MatchConfig());
            engine.Advance(0.05);
            engine.Clock.Paused = true;
            engine.Advance(50);
            engine.Clock.Paused = false;
            engine.Advance(0.05);
            Check(engine.State.tick == 1, "Pause must preserve prior fractional time without accumulating pause time.");
            var copy = engine.GetSnapshot();
            copy.scoreHalfPoints[0] = 999;
            Check(engine.State.scoreHalfPoints[0] == 0, "Snapshot must not alias authority scores.");
            int initial = engine.State.tick;
            foreach (double invalid in new[] { -1.0, double.NaN, double.PositiveInfinity })
            {
                bool rejected = false;
                try { engine.Advance(invalid); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected && engine.State.tick == initial, "Invalid delta must not mutate clock.");
            }
            engine = new MatchEngine(new MatchConfig());
            callbacks = 0;
            engine.Advance(1000, snapshot => callbacks++);
            Check(callbacks == 9000 && engine.State.tick == 9000 && engine.Clock.Completed,
                "Full bootstrap must stop at 9000 and preserve every tick.");
            bool badConfig = false;
            try { new TickClock(new MatchConfig { ticksPerSecond = 0 }); }
            catch (ArgumentException) { badConfig = true; }
            Check(badConfig, "Reject invalid tick configuration.");
        }
    }
}
