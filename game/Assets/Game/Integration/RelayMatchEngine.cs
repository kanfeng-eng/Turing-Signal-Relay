using System;
using System.Collections.Generic;

namespace TuringSignalRelay
{
    public sealed class RelayMatchEngine
    {
        private readonly RelayConfig config;
        private readonly RelayMap map;
        private readonly RelayState state = new RelayState();
        public int Tick { get { return state.tick; } }
        public bool Completed { get { return state.completed; } }
        public string MapHash { get { return map.Hash; } }
        public RelayConfig Config { get { return config.Copy(); } }
        public RelayMapData MapData { get { return map.Export(); } }

        public RelayMatchEngine(RelayConfig settings, RelayMapData definition)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            settings.Validate();
            config = settings.Copy();
            map = new RelayMap(definition);
            foreach (var site in map.Export().sites)
                if (site.kind == "depot" || site.kind == "central-depot")
                    state.resources.Add(new RelayResource { id = site.id, side = site.side, x = site.x, y = site.y });
            for (int side = 0; side < 2; side++)
            {
                var home = map.Headquarters(side);
                for (int number = 0; number < 7; number++)
                    state.units.Add(new RelayUnit { id = side * 100 + number, side = side, x = home.x, y = home.y,
                        role = number < 3 ? UnitRole.Communicator : number < 5 ? UnitRole.Engineer : UnitRole.Transporter });
            }
        }

        public RelayState GetSnapshot() { return state.Copy(); }
        public string StateHash() { return RelayStateHash.Compute(config, map.Hash, state); }

        public void Step(RelayCommandBatch red, RelayCommandBatch blue)
        {
            if (Completed) throw new InvalidOperationException("Match already completed.");
            ValidateBatch(red, 0); ValidateBatch(blue, 1);
            state.tick++;
            var results = new List<RelayCommandResult>();
            var events = new List<string>();
            ApplyBatch(red, results); ApplyBatch(blue, results);
            PlainMessageSystem.Spawn(state, map.Reports(0), config.seed);
            MovementSystem.Step(map, state);
            foreach (var unit in state.units)
            {
                if ((unit.task != "loadMsg" && unit.task != "deliverMsg" && unit.task != "transferMsg") || unit.taskUntilTick > state.tick) continue;
                string error = null;
                string action = unit.task;
                if (action == "loadMsg") error = PlainMessageSystem.Load(state, unit, unit.actionMessageId);
                else if (action == "transferMsg") error = PlainMessageSystem.Transfer(state, unit,
                    state.units.Find(value => value.id == unit.actionTargetUnitId), unit.actionMessageId);
                else
                {
                    var home = map.Headquarters(unit.side);
                    if (unit.x != home.x || unit.y != home.y) error = "not-at-headquarters";
                    else PlainMessageSystem.Deliver(state, unit);
                }
                events.Add(action + " unit=" + unit.id + " " + (error ?? "completed"));
                unit.task = "idle";
                unit.actionMessageId = unit.actionTargetUnitId = -1;
            }
            foreach (var message in state.messages)
                if (message.carrierId >= 0)
                {
                    var carrier = state.units.Find(value => value.id == message.carrierId);
                    message.x = carrier.x; message.y = carrier.y;
                }
            state.commandResults = results.ToArray();
            state.events = events.ToArray();
            state.completed = state.tick == config.durationTicks;
        }

        private void ValidateBatch(RelayCommandBatch batch, int side)
        {
            if (batch == null) return;
            if (batch.protocolVersion != 2 || batch.type != "relay-commands" || batch.tick != state.tick || batch.side != side ||
                batch.actions == null || batch.actions.Length > 128 || Array.Exists(batch.actions, value => value == null))
                throw new ArgumentException("Invalid command envelope for side " + side);
        }

        private void ApplyBatch(RelayCommandBatch batch, List<RelayCommandResult> results)
        {
            if (batch == null) return;
            var seen = new HashSet<int>();
            foreach (var command in batch.actions)
            {
                string error;
                var unit = state.units.Find(value => value.id == command.unitId && value.side == batch.side);
                if (!seen.Add(command.unitId)) error = "duplicate-unit-command";
                else if (unit == null) error = "unit-not-owned";
                else error = Apply(unit, command);
                results.Add(new RelayCommandResult { side = batch.side, unitId = command.unitId,
                    action = command.action, accepted = error == null, reason = error ?? "accepted" });
            }
        }

        private string Apply(RelayUnit unit, RelayCommand command)
        {
            if (command.action == "moveTo") return MovementSystem.MoveTo(map, unit, command.x, command.y);
            if (command.action == "wait")
            {
                if (command.ticks < 1 || command.ticks > 9000) return "invalid-wait-duration";
                if (unit.task != "wait" || unit.taskDuration != command.ticks) MovementSystem.Wait(unit, state.tick, command.ticks);
                return null;
            }
            if (command.action != "loadMsg" && command.action != "deliverMsg" && command.action != "transferMsg") return "unsupported-action";
            if (unit.edgeCompleteTick > 0) return "unit-in-transit";
            if (command.action == "loadMsg")
            {
                string error = PlainMessageSystem.ValidateLoad(state, unit, command.messageId);
                if (error != null) return error;
            }
            else if (command.action == "deliverMsg")
            {
                var home = map.Headquarters(unit.side);
                if (unit.x != home.x || unit.y != home.y) return "not-at-headquarters";
                if (unit.messageIds.Count == 0) return "no-carried-message";
            }
            else
            {
                var target = state.units.Find(value => value.id == command.targetUnitId);
                if (target == null || target.side != unit.side || target.id == unit.id || target.x != unit.x || target.y != unit.y ||
                    !unit.messageIds.Contains(command.messageId) || target.messageIds.Count >= PlainMessageSystem.Capacity(target)) return "invalid-transfer";
            }
            if (unit.task == command.action && unit.actionMessageId == command.messageId && unit.actionTargetUnitId == command.targetUnitId) return null;
            unit.task = command.action; unit.taskUntilTick = state.tick + 1;
            unit.taskDuration = 1;
            unit.actionMessageId = command.messageId; unit.actionTargetUnitId = command.targetUnitId;
            unit.path = new RelayCell[0]; unit.pathIndex = 0; unit.waitingSinceTick = -1; unit.blockedTicks = 0;
            return null;
        }

        public RelayObservation GetObservation(int side)
        {
            if (side < 0 || side > 1) throw new ArgumentOutOfRangeException("side");
            var own = state.units.FindAll(value => value.side == side).ConvertAll(value => value.Copy());
            var enemies = new List<RelayUnit>();
            foreach (var unit in state.units)
                if (unit.side != side && VisibilitySystem.Visible(map, state, side, unit.x, unit.y))
                    enemies.Add(new RelayUnit { id = unit.id, side = unit.side, x = unit.x, y = unit.y, role = unit.role, task = "observed" });
            return new RelayObservation { tick = Tick, side = side, completed = Completed,
                rulesVersion = config.rulesVersion, featureSet = config.featureSet, mapHash = map.Hash,
                scoreHalfPoints = (int[])state.scoreHalfPoints.Clone(), units = own.ToArray(), visibleEnemies = enemies.ToArray(),
                messages = state.messages.FindAll(value => value.side == side).ConvertAll(value => value.Copy()).ToArray(),
                resources = state.resources.FindAll(value => value.side == side || VisibilitySystem.Visible(map, state, side, value.x, value.y)).ConvertAll(value => value.Copy()).ToArray() };
        }
    }
}
