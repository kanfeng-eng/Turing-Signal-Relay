using System;
using System.Collections.Generic;

namespace TuringSignalRelay
{
    [Serializable]
    public sealed class RelayResult
    {
        public string type = "relay-result";
        public int resultVersion = 1;
        public string status = "movement-plain-report-completed";
        public RelayConfig config;
        public string mapHash, stateHash;
        public RelayState finalSnapshot;
    }

    public static class RelayScenario
    {
        // Trusted deterministic script for stages 1-3. It consumes each side's filtered frame.
        public static RelayRecording Run(RelayConfig config, RelayMapData mapData)
        {
            var engine = new RelayMatchEngine(config, mapData);
            var map = new RelayMap(mapData);
            var recording = new RelayRecording { config = engine.Config, map = engine.MapData,
                mapHash = engine.MapHash, initialStateHash = engine.StateHash() };
            var frames = new List<RelayRecordedTick>();
            while (!engine.Completed)
            {
                var red = Commands(engine.GetObservation(0), map);
                var blue = Commands(engine.GetObservation(1), map);
                engine.Step(red, blue);
                var batches = new List<RelayCommandBatch>();
                if (red != null) batches.Add(red);
                if (blue != null) batches.Add(blue);
                frames.Add(new RelayRecordedTick { tick = engine.Tick, batches = batches.ToArray(), stateHash = engine.StateHash() });
            }
            recording.frames = frames.ToArray();
            return recording;
        }

        private static RelayCommandBatch Commands(RelayObservation frame, RelayMap map)
        {
            var courier = Array.Find(frame.units, value => value.id == frame.side * 100);
            if (courier.task != "idle") return null;
            var home = map.Headquarters(frame.side);
            RelayCommand command;
            if (courier.messageIds.Count > 0)
                command = courier.x == home.x && courier.y == home.y ? new RelayCommand { unitId = courier.id, action = "deliverMsg" } :
                    new RelayCommand { unitId = courier.id, action = "moveTo", x = home.x, y = home.y };
            else
            {
                var message = Array.Find(frame.messages, value => value.kind == "plain" && !value.expired && value.carrierId == -1 && value.deliveredTick < 0);
                var target = map.Reports(frame.side)[0];
                int x = message == null ? target.x : message.x, y = message == null ? target.y : message.y;
                if (courier.x == x && courier.y == y)
                {
                    if (message == null) return null;
                    command = new RelayCommand { unitId = courier.id, action = "loadMsg", messageId = message.id };
                }
                else command = new RelayCommand { unitId = courier.id, action = "moveTo", x = x, y = y };
            }
            return new RelayCommandBatch { protocolVersion = 2, type = "relay-commands", tick = frame.tick, side = frame.side, actions = new[] { command } };
        }

        public static RelayResult Result(RelayMatchEngine engine)
        {
            if (!engine.Completed) throw new InvalidOperationException("Cannot finalize a running scenario.");
            return new RelayResult { config = engine.Config, mapHash = engine.MapHash, stateHash = engine.StateHash(), finalSnapshot = engine.GetSnapshot(),
                status = engine.Config.FullRules() ? "full-rules-completed" : "movement-plain-report-completed" };
        }
    }
}
