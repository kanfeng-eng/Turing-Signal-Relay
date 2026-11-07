using System;
using System.Collections.Generic;

namespace TuringSignalRelay
{
    public sealed class RelayMatchEngine
    {
        private readonly RelayConfig config;
        private readonly RelayMap map;
        private readonly RelayMapData definition;
        private readonly FullRuleSystem full;
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
            this.definition = map.Export();
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
            if (config.FullRules()) { full = new FullRuleSystem(this.definition); full.Initialize(state); }
        }

        public RelayState GetSnapshot() { return state.Copy(); }
        public string StateHash() { return RelayStateHash.Compute(config, map.Hash, state); }

        public void Step(RelayCommandBatch red, RelayCommandBatch blue)
        {
            if (Completed) throw new InvalidOperationException("Match already completed.");
            ValidateBatch(red, 0); ValidateBatch(blue, 1);
            state.tick++;
            if (full != null) { state.eventFocusUnitId = -1; state.eventFocusMessageId = -1; }
            var results = new List<RelayCommandResult>();
            var events = new List<string>();
            if (full != null && state.tick % 2 == 1) { ApplyBatch(blue, results); ApplyBatch(red, results); }
            else { ApplyBatch(red, results); ApplyBatch(blue, results); }
            if (full == null) PlainMessageSystem.Spawn(state, map.Reports(0), config.seed);
            else
            {
                full.Refresh(state, config.seed, events);
                full.CompleteTasks(state, events);
                map.SynchronizeBridges(state.bridges);
            }
            MovementSystem.Step(map, state);
            if (full != null) RadioNetwork.Rebuild(state, definition);
            foreach (var unit in state.units)
            {
                if ((unit.task != "loadMsg" && unit.task != "deliverMsg" && unit.task != "transferMsg") || unit.taskUntilTick > state.tick) continue;
                string error = null;
                string action = unit.task;
                if (action == "loadMsg") error = full == null ? PlainMessageSystem.Load(state, unit, unit.actionMessageId) : FullMessageSystem.Load(state, unit, unit.actionMessageId, definition);
                else if (action == "transferMsg")
                {
                    var receiver = state.units.Find(value => value.id == unit.actionTargetUnitId);
                    if (full != null && receiver != null && receiver.materials + receiver.reservedMaterials > 0) error = "message-capacity";
                    else error = PlainMessageSystem.Transfer(state, unit, receiver, unit.actionMessageId);
                }
                else if (full != null) error = full.SubmitMessages(state, unit);
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
            if (full != null)
            {
                FullMessageSystem.ProcessNetwork(state);
                if (state.tick == 9000) full.FinalizeScores(state);
            }
            state.commandResults = results.ToArray();
            if (full != null)
                foreach (var result in results)
                    if (result.accepted && result.action == "suppress") events.Add("suppress unit=" + result.unitId);
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
            var commands = new List<RelayCommand>();
            foreach (var command in batch.actions)
            {
                if (full != null && !seen.Add(command.unitId))
                    results.Add(new RelayCommandResult { side = batch.side, unitId = command.unitId, action = command.action, reason = "duplicate-unit-command", accepted = false });
                else commands.Add(command);
            }
            if (full != null) commands.Sort((a, b) => a.unitId.CompareTo(b.unitId));
            seen.Clear();
            foreach (var command in commands)
            {
                string error;
                var unit = state.units.Find(value => value.id == command.unitId && value.side == batch.side);
                if (!seen.Add(command.unitId)) error = "duplicate-unit-command";
                else if (unit == null) error = "unit-not-owned";
                else
                {
                    error = Apply(unit, command);
                    if (error == null && full != null) FullRuleSystem.AfterAccepted(unit, command.action);
                }
                results.Add(new RelayCommandResult { side = batch.side, unitId = command.unitId,
                    action = command.action, accepted = error == null, reason = error ?? "accepted" });
            }
        }

        private string Apply(RelayUnit unit, RelayCommand command)
        {
            string fullError;
            if (full != null && full.TryStart(state, unit, command, out fullError)) return fullError;
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
                string error = full == null ? PlainMessageSystem.ValidateLoad(state, unit, command.messageId) : FullMessageSystem.ValidateLoad(state, unit, command.messageId, definition);
                if (error != null) return error;
            }
            else if (command.action == "deliverMsg")
            {
                if (full != null)
                {
                    string error = full.ValidateMessageSubmission(state, unit);
                    if (error != null) return error;
                }
                else
                {
                    var home = map.Headquarters(unit.side);
                    if (unit.x != home.x || unit.y != home.y) return "not-at-headquarters";
                    if (unit.messageIds.Count == 0) return "no-carried-message";
                }
            }
            else
            {
                var target = state.units.Find(value => value.id == command.targetUnitId);
                if (target == null || target.side != unit.side || target.id == unit.id || target.x != unit.x || target.y != unit.y ||
                    !unit.messageIds.Contains(command.messageId) || target.messageIds.Count >= PlainMessageSystem.Capacity(target) ||
                    (full != null && target.materials + target.reservedMaterials > 0)) return "invalid-transfer";
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
            if (full != null) foreach (var unit in own) { unit.path = new RelayCell[0]; unit.pathIndex = 0; }
            var enemies = new List<RelayUnit>();
            foreach (var unit in state.units)
                if (unit.side != side && VisibilitySystem.Visible(map, state, side, unit.x, unit.y))
                    enemies.Add(new RelayUnit { id = unit.id, side = unit.side, x = unit.x, y = unit.y, role = unit.role,
                        stationActive = unit.stationActive, task = unit.stationActive && state.tick < unit.suppressedUntilTick ? "suppressed-station" : "observed" });
            return new RelayObservation { tick = Tick, side = side, completed = Completed,
                rulesVersion = config.rulesVersion, featureSet = config.featureSet, mapHash = map.Hash,
                scoreHalfPoints = (int[])state.scoreHalfPoints.Clone(), units = own.ToArray(), visibleEnemies = enemies.ToArray(),
                messages = state.messages.FindAll(value => value.side == side).ConvertAll(value => value.Copy()).ToArray(),
                resources = state.resources.FindAll(value => value.side == side || ResourceVisible(value, side)).ConvertAll(value => value.Copy()).ToArray(),
                nodes = state.nodes.FindAll(value => value.side == side).ConvertAll(value => value.Copy()).ToArray(),
                links = state.links.FindAll(value => state.nodes.Exists(node => node.id == value.from && node.side == side)).ConvertAll(value => value.Copy()).ToArray(),
                stations = state.stations.FindAll(value => value.side == side || VisibilitySystem.Visible(map, state, side, value.x, value.y)).ConvertAll(value =>
                    value.side == side ? value.Copy() : new RelayFixedStation { id = value.id, side = value.side, x = value.x, y = value.y }).ToArray(),
                bridges = state.bridges.FindAll(value => value.side == side || BridgeVisible(value, side)).ConvertAll(value => value.Copy()).ToArray(),
                hubMaterials = full == null ? new int[2] : new[] { side == 0 ? state.hubMaterials[0] : -1, side == 1 ? state.hubMaterials[1] : -1 }, weather = state.weather };
        }

        private bool ResourceVisible(RelayResource resource, int side)
        {
            if (full == null) return VisibilitySystem.Visible(map, state, side, resource.x, resource.y);
            return Array.Exists(full.ResourceCells(resource), cell => VisibilitySystem.Visible(map, state, side, cell.x, cell.y));
        }

        private bool BridgeVisible(RelayBuiltBridge bridge, int side)
        {
            var crossing = Array.Find(definition.crossings, value => value.id == bridge.crossingId);
            return VisibilitySystem.Visible(map, state, side, (crossing.fromX + crossing.toX) / 2, (crossing.fromY + crossing.toY) / 2);
        }
    }
}
