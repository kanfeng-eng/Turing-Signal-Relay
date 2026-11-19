using System;
using System.Collections.Generic;

namespace TuringSignalRelay
{
    public sealed class FullRuleSystem
    {
        private readonly RelayMapData map;
        public FullRuleSystem(RelayMapData definition) { map = definition; }

        public static int NextEventTick(int tick, int duration, RelayRuleParameters parameters, bool fullRules)
        {
            int next = NextPeriodic(tick, parameters.plainFirstTick, parameters.plainPeriodTicks, 50, duration);
            if (fullRules)
            {
                foreach (int candidate in new[] {
                    NextPeriodic(tick, parameters.urgentFirstTick, parameters.urgentPeriodTicks, parameters.urgentCount, duration),
                    NextPeriodic(tick, parameters.secretFirstTick, parameters.secretPeriodTicks, parameters.secretCount, duration),
                    NextPeriodic(tick, parameters.broadcastFirstTick, parameters.broadcastPeriodTicks, parameters.broadcastCount, duration),
                    NextPeriodic(tick, parameters.depotPeriodTicks, parameters.depotPeriodTicks, duration / parameters.depotPeriodTicks, duration),
                    parameters.airdropTick, parameters.centralDepotTick, parameters.ferryFirstTick, parameters.stormTick,
                    NextPeriodic(tick, parameters.ferryFirstTick, parameters.ferryPeriodTicks, duration / parameters.ferryPeriodTicks + 1, duration),
                    NextPeriodic(tick, parameters.ferryFirstTick + parameters.ferryOpenTicks, parameters.ferryPeriodTicks, duration / parameters.ferryPeriodTicks + 1, duration),
                    parameters.stormTick + parameters.stormDurationTicks, parameters.interferenceTick,
                    parameters.interferenceTick + parameters.interferenceDurationTicks, parameters.hubOpenTick, parameters.assaultTick })
                    if (candidate > tick && candidate < duration && (next < 0 || candidate < next)) next = candidate;
            }
            return next;
        }

        private static int NextPeriodic(int tick, int first, int period, int count, int duration)
        {
            int index = tick < first ? 0 : (tick - first) / period + 1;
            long next = first + (long)index * period;
            return index < count && next < duration ? (int)next : -1;
        }

        public void Initialize(RelayState state)
        {
            state.resources.Clear();
            foreach (var site in map.sites)
                if (site.kind == "depot") state.resources.Add(new RelayResource { id = site.id, side = site.side, x = site.x, y = site.y, kind = "depot" });
                else if (site.kind == "headquarters") state.resources.Add(new RelayResource { id = 30000 + site.side, side = site.side, x = site.x, y = site.y, kind = "headquarters" });
            var central = Array.Find(map.sites, site => site.kind == "central-depot");
            state.resources.Add(new RelayResource { id = central.id, side = -1, x = central.x, y = central.y, kind = "central-depot", availableTick = state.ruleParameters.centralDepotTick });
            state.resources.Add(new RelayResource { id = 3000, side = -1, x = 47, y = 47, kind = "airdrop", availableTick = state.ruleParameters.airdropTick });
            state.resources.Sort((a, b) => a.id.CompareTo(b.id));
            RadioNetwork.Rebuild(state, map);
        }

        public void Refresh(RelayState state, int seed, List<string> events = null)
        {
            string oldWeather = state.weather;
            bool storm = state.tick >= state.ruleParameters.stormTick && state.tick < state.ruleParameters.stormTick + state.ruleParameters.stormDurationTicks;
            bool interference = state.tick >= state.ruleParameters.interferenceTick && state.tick < state.ruleParameters.interferenceTick + state.ruleParameters.interferenceDurationTicks;
            state.weatherReduction = Math.Max(storm ? state.ruleParameters.stormReduction : 0, interference ? state.ruleParameters.interferenceReduction : 0);
            state.weather = interference && state.ruleParameters.interferenceReduction == state.weatherReduction ? "interference" : storm ? "storm" : "clear";
            if (events != null && state.weather != oldWeather) events.Add("weather " + state.weather);
            if (state.tick == state.ruleParameters.airdropTick) state.resources.Find(value => value.kind == "airdrop").amount = state.ruleParameters.airdropAmount;
            if (state.tick % state.ruleParameters.depotPeriodTicks == 0)
            {
                foreach (var resource in state.resources)
                    if (resource.kind == "depot" || (resource.kind == "central-depot" && state.tick > state.ruleParameters.centralDepotTick))
                        resource.amount = Math.Min(state.ruleParameters.depotCapacity, resource.amount + state.ruleParameters.depotRefreshAmount);
                for (int side = 0; side < 2; side++)
                {
                    int leading = state.scoreHalfPoints[1 - side], own = state.scoreHalfPoints[side];
                    if (leading <= 0 || (long)(leading - own) * 100 <= (long)leading * state.ruleParameters.compensationPercent || state.compensationCount[side] >= state.ruleParameters.compensationLimit) continue;
                    var candidates = state.resources.FindAll(value => value.kind == "depot" && value.side == side && value.amount < state.ruleParameters.depotCapacity);
                    candidates.Sort((a, b) => a.amount == b.amount ? a.id.CompareTo(b.id) : a.amount.CompareTo(b.amount));
                    if (candidates.Count > 0) { candidates[0].amount++; state.compensationCount[side]++; }
                }
            }
            int count = state.messages.Count;
            FullMessageSystem.Spawn(state, map, seed);
            for (int i = count; i < state.messages.Count; i++)
                if (state.messages[i].side == 0 && state.messages[i].kind != "plain")
                {
                    if (events != null) events.Add(state.messages[i].kind + " task=" + state.messages[i].id);
                    if (state.messages[i].kind == "urgent" || state.messages[i].kind == "assault") state.eventFocusMessageId = state.messages[i].id;
                }
        }

        public static int MaterialCapacity(RelayUnit unit, RelayRuleParameters parameters = null)
        {
            parameters = parameters ?? new RelayRuleParameters();
            return unit.role == UnitRole.Engineer ? parameters.engineerMaterials : unit.role == UnitRole.Transporter ? parameters.transporterMaterials : 0;
        }
        public bool InHub(int x, int y) { return Array.Exists(map.sites, site => site.kind == "hub" && site.x == x && site.y == y); }

        private bool AtResource(RelayResource resource, RelayUnit unit)
        {
            if (resource.kind == "airdrop") return InHub(unit.x, unit.y);
            if (resource.kind == "central-depot") return Array.Exists(map.sites, site => site.kind == "central-depot" && site.x == unit.x && site.y == unit.y);
            return resource.x == unit.x && resource.y == unit.y;
        }

        private RelayResource PickupStock(RelayState state, RelayUnit unit)
        {
            return state.resources.Find(value => (value.side == unit.side || value.side == -1) && value.availableTick <= state.tick && value.amount > 0 && AtResource(value, unit));
        }

        private RelayResource UnloadStock(RelayState state, RelayUnit unit)
        {
            return state.resources.Find(value => value.side == unit.side && (value.kind == "depot" || value.kind == "headquarters") && AtResource(value, unit) &&
                (value.kind == "headquarters" || value.amount < state.ruleParameters.depotCapacity));
        }

        public bool TryStart(RelayState state, RelayUnit unit, RelayCommand command, out string error)
        {
            error = null;
            string action = command.action;
            if (action != "setupStation" && action != "withdrawStation" && action != "suppress" && action != "buildBridge" && action != "buildRelay" &&
                action != "pickup" && action != "unloadMaterials" && action != "deliverMaterials" && action != "transferMaterials") return false;
            if (unit.edgeCompleteTick > 0) { error = "unit-in-transit"; return true; }
            if (action == "setupStation" || action == "withdrawStation" || action == "suppress")
            {
                if (unit.role != UnitRole.Communicator) { error = "requires-communicator"; return true; }
                if (action == "setupStation")
                {
                    if (unit.stationActive || unit.task == action) return true;
                    Begin(unit, action, state.tick, state.ruleParameters.stationSetupTicks);
                }
                else if (action == "withdrawStation") { unit.stationActive = false; Begin(unit, "idle", state.tick, 0); }
                else
                {
                    if (state.tick < unit.suppressReadyTick || !RelayGeometry.Within(unit.x, unit.y, command.x, command.y, state.ruleParameters.suppressionRadius) ||
                        command.x < 0 || command.y < 0 || command.x >= 96 || command.y >= 96) { error = "invalid-suppression"; return true; }
                    var targets = state.units.FindAll(value => value.side != unit.side && value.stationActive && value.x == command.x && value.y == command.y);
                    var fixedTargets = state.stations.FindAll(value => value.side != unit.side && value.x == command.x && value.y == command.y);
                    if (targets.Count == 0 && fixedTargets.Count == 0) { error = "no-enemy-station"; return true; }
                    foreach (var target in targets) target.suppressedUntilTick = Math.Max(target.suppressedUntilTick, state.tick + state.ruleParameters.suppressionTicks);
                    foreach (var target in fixedTargets) target.suppressedUntilTick = Math.Max(target.suppressedUntilTick, state.tick + state.ruleParameters.suppressionTicks);
                    state.eventFocusUnitId = targets.Count > 0 ? targets[0].id : unit.id;
                    unit.suppressReadyTick = state.tick + state.ruleParameters.suppressionCooldownTicks; Begin(unit, "idle", state.tick, 0);
                }
                return true;
            }
            if (action == "buildBridge" || action == "buildRelay")
            {
                error = StartBuilding(state, unit, command);
                return true;
            }
            if (action == "pickup")
            {
                if (unit.messageIds.Count > 0 || unit.materials + unit.reservedMaterials >= MaterialCapacity(unit, state.ruleParameters)) error = "material-capacity";
                else if (PickupStock(state, unit) == null) error = "no-owned-materials-here";
            }
            else if (action == "unloadMaterials")
            {
                if (unit.materials <= 0 || UnloadStock(state, unit) == null) error = "cannot-unload-materials";
            }
            else if (action == "deliverMaterials")
            {
                if (unit.materials <= 0 || state.tick < state.ruleParameters.hubOpenTick || !InHub(unit.x, unit.y)) error = "cannot-deliver-to-hub";
            }
            else
            {
                var target = state.units.Find(value => value.id == command.targetUnitId);
                if (!CanTransfer(unit, target, state.ruleParameters)) error = "invalid-material-transfer";
            }
            if (error == null)
            {
                if (unit.task == action && unit.actionTargetUnitId == command.targetUnitId) return true;
                Begin(unit, action, state.tick, 1); unit.actionTargetUnitId = command.targetUnitId;
            }
            return true;
        }

        private string StartBuilding(RelayState state, RelayUnit unit, RelayCommand command)
        {
            if (unit.role != UnitRole.Engineer) return "requires-engineer";
            if (unit.task == command.action && unit.targetX == command.x && unit.targetY == command.y) return null;
            bool bridge = command.action == "buildBridge";
            int cost = bridge ? state.ruleParameters.bridgeCost : state.ruleParameters.relayCost;
            if (unit.materials + unit.reservedMaterials < cost) return "insufficient-materials";
            if (command.x < 0 || command.y < 0 || command.x >= 96 || command.y >= 96) return "invalid-build-site";
            if (bridge)
            {
                var crossing = Array.Find(map.crossings, value => value.kind == CrossingKind.BuiltBridge && (value.fromX + value.toX) / 2 == command.x && (value.fromY + value.toY) / 2 == command.y);
                if (crossing == null || state.bridges.Exists(value => value.crossingId == crossing.id) ||
                    !((unit.x == crossing.fromX && unit.y == crossing.fromY) || (unit.x == crossing.toX && unit.y == crossing.toY))) return "invalid-build-site";
            }
            else if (unit.x != command.x || unit.y != command.y || (map.rows[command.y][command.x] != '0' && map.rows[command.y][command.x] != '1') ||
                Array.Exists(map.sites, site => site.x == command.x && site.y == command.y) ||
                Array.Exists(map.crossings, value => (value.fromX == command.x && value.fromY == command.y) || (value.toX == command.x && value.toY == command.y)) ||
                state.stations.Exists(value => value.x == command.x && value.y == command.y)) return "invalid-build-site";
            if (state.units.Exists(value => value.id != unit.id && value.task == command.action && value.targetX == command.x && value.targetY == command.y)) return "site-already-reserved";
            unit.materials += unit.reservedMaterials; unit.reservedMaterials = cost; unit.materials -= cost;
            Begin(unit, command.action, state.tick, bridge ? state.ruleParameters.bridgeBuildTicks : state.ruleParameters.relayBuildTicks); unit.targetX = command.x; unit.targetY = command.y;
            return null;
        }

        private static void Begin(RelayUnit unit, string task, int tick, int duration)
        {
            unit.task = task; unit.taskDuration = duration; unit.taskUntilTick = tick + duration;
            unit.path = new RelayCell[0]; unit.pathIndex = 0; unit.waitingSinceTick = -1; unit.blockedTicks = 0;
            unit.actionMessageId = unit.actionTargetUnitId = -1;
        }

        public static void AfterAccepted(RelayUnit unit, string action)
        {
            if (unit.reservedMaterials > 0 && unit.task != "buildBridge" && unit.task != "buildRelay")
            {
                unit.materials += unit.reservedMaterials; unit.reservedMaterials = 0;
            }
            if (action == "moveTo") unit.stationActive = false;
            if (action != "setupStation") { unit.decodeMessageId = -1; unit.decodeStartedTick = -1; }
        }

        private static bool CanTransfer(RelayUnit sender, RelayUnit target, RelayRuleParameters parameters)
        {
            return target != null && target.id != sender.id && target.side == sender.side && target.x == sender.x && target.y == sender.y &&
                sender.materials > 0 && target.messageIds.Count == 0 && target.materials + target.reservedMaterials < MaterialCapacity(target, parameters);
        }

        public void CompleteTasks(RelayState state, List<string> events)
        {
            var ordered = new List<RelayUnit>(state.units);
            ordered.Sort((a, b) => {
                int priority = (a.side == state.tick % 2 ? 0 : 1).CompareTo(b.side == state.tick % 2 ? 0 : 1);
                return priority == 0 ? a.id.CompareTo(b.id) : priority;
            });
            foreach (var unit in ordered)
            {
                if (unit.taskUntilTick > state.tick) continue;
                string action = unit.task;
                if (action == "setupStation") unit.stationActive = true;
                else if (action == "buildBridge" || action == "buildRelay")
                {
                    if (action == "buildBridge")
                    {
                        var crossing = Array.Find(map.crossings, value => value.kind == CrossingKind.BuiltBridge && (value.fromX + value.toX) / 2 == unit.targetX && (value.fromY + value.toY) / 2 == unit.targetY);
                        state.bridges.Add(new RelayBuiltBridge { crossingId = crossing.id, side = unit.side, completedTick = state.tick });
                        if (state.bridges.FindAll(value => value.side == unit.side).Count <= state.ruleParameters.bridgeScoreLimit) FullMessageSystem.Award(state, unit.side, "bridge-construction", state.ruleParameters.bridgeHalfPoints);
                    }
                    else
                    {
                        state.stations.Add(new RelayFixedStation { id = unit.side * 10000 + unit.targetY * 96 + unit.targetX, side = unit.side, x = unit.targetX, y = unit.targetY });
                        if (state.stations.FindAll(value => value.side == unit.side).Count <= state.ruleParameters.relayScoreLimit) FullMessageSystem.Award(state, unit.side, "relay-construction", state.ruleParameters.relayHalfPoints);
                    }
                    unit.reservedMaterials = 0;
                }
                else if (action == "pickup")
                {
                    var resource = PickupStock(state, unit);
                    if (resource != null && unit.messageIds.Count == 0)
                    {
                        int count = Math.Min(resource.amount, MaterialCapacity(unit, state.ruleParameters) - unit.materials - unit.reservedMaterials);
                        unit.materials += count; resource.amount -= count;
                    }
                }
                else if (action == "unloadMaterials")
                {
                    var resource = UnloadStock(state, unit);
                    if (resource != null)
                    {
                        int count = resource.kind == "headquarters" ? unit.materials : Math.Min(unit.materials, state.ruleParameters.depotCapacity - resource.amount);
                        resource.amount += count; unit.materials -= count;
                    }
                }
                else if (action == "deliverMaterials")
                {
                    if (state.tick >= state.ruleParameters.hubOpenTick && InHub(unit.x, unit.y)) { state.hubMaterials[unit.side] += unit.materials; unit.materials = 0; }
                }
                else if (action == "transferMaterials")
                {
                    var target = state.units.Find(value => value.id == unit.actionTargetUnitId);
                    if (CanTransfer(unit, target, state.ruleParameters))
                    {
                        int count = Math.Min(unit.materials, MaterialCapacity(target, state.ruleParameters) - target.materials - target.reservedMaterials);
                        target.materials += count; unit.materials -= count;
                    }
                }
                else continue;
                unit.task = "idle";
                if (action == "buildRelay" || action == "buildBridge") state.eventFocusUnitId = unit.id;
                events.Add(action + " unit=" + unit.id + " completed");
            }
        }

        public string ValidateMessageSubmission(RelayState state, RelayUnit unit)
        {
            if (unit.messageIds.Count == 0) return "no-carried-message";
            bool atHome = Array.Exists(map.sites, value => value.kind == "headquarters" && value.side == unit.side && value.x == unit.x && value.y == unit.y);
            var node = state.nodes.Find(value => value.side == unit.side && value.active && value.x == unit.x && value.y == unit.y);
            if (!atHome && node == null) return "not-at-friendly-station";
            if (atHome && unit.messageIds.Exists(id => state.messages.Exists(value => value.id == id && value.kind == "secret" && !value.decoded))) return "requires-decoding";
            return null;
        }

        public string SubmitMessages(RelayState state, RelayUnit unit)
        {
            string error = ValidateMessageSubmission(state, unit);
            if (error != null) return error;
            var node = state.nodes.Find(value => value.side == unit.side && value.active && value.x == unit.x && value.y == unit.y);
            foreach (int id in new List<int>(unit.messageIds))
            {
                var message = state.messages.Find(value => value.id == id);
                if (node != null && node.connectedToHome && (message.kind != "secret" || message.decoded)) FullMessageSystem.Deliver(state, message);
                else if (node != null)
                {
                    message.carrierId = -1; message.stationId = node.id; message.x = node.x; message.y = node.y;
                    unit.messageIds.Remove(id);
                }
            }
            return null;
        }

        public void FinalizeScores(RelayState state)
        {
            foreach (int side in new[] { 0, 1 })
            {
                if (state.units.FindAll(unit => unit.side == side && InHub(unit.x, unit.y)).Count >= state.ruleParameters.hubUnits) FullMessageSystem.Award(state, side, "hub-personnel", state.ruleParameters.hubConditionHalfPoints);
                if (Array.Exists(map.sites, site => site.kind == "hub" && RadioNetwork.Covering(state, side, site.x, site.y) != null)) FullMessageSystem.Award(state, side, "hub-network", state.ruleParameters.hubConditionHalfPoints);
                if (state.hubMaterials[side] >= state.ruleParameters.hubMaterials) FullMessageSystem.Award(state, side, "hub-materials", state.ruleParameters.hubConditionHalfPoints);
                int remaining = 0;
                foreach (var unit in state.units) if (unit.side == side) remaining += unit.materials + unit.reservedMaterials;
                foreach (var resource in state.resources) if (resource.side == side && resource.kind == "headquarters") remaining += resource.amount;
                if (remaining > 0) FullMessageSystem.Award(state, side, "material-remainder", Math.Min(state.ruleParameters.remainderLimit, remaining) * state.ruleParameters.remainderHalfPoints);
            }
        }

        public RelayCell[] ResourceCells(RelayResource resource)
        {
            if (resource.kind == "central-depot" || resource.kind == "airdrop")
            {
                string kind = resource.kind == "airdrop" ? "hub" : "central-depot";
                return Array.ConvertAll(Array.FindAll(map.sites, site => site.kind == kind), site => new RelayCell(site.x, site.y));
            }
            return new[] { new RelayCell(resource.x, resource.y) };
        }
    }
}
