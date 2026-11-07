using System;
using System.Collections.Generic;

namespace TuringSignalRelay
{
    public static class FullMessageSystem
    {
        public static void Spawn(RelayState state, RelayMapData map, int seed)
        {
            var reports = Array.FindAll(map.sites, site => site.kind == "report" && site.side == 0);
            PlainMessageSystem.Spawn(state, reports, seed);
            if (state.tick >= 1800 && state.tick <= 7800 && (state.tick - 1800) % 1200 == 0)
                Pair(state, "urgent", 100 + (state.tick - 1800) / 1200 * 2, 300, 47, 47, true);
            if (state.tick >= 2400 && state.tick <= 7800 && (state.tick - 2400) % 1800 == 0)
            {
                int index = (state.tick - 2400) / 1800;
                var site = reports[Choice(seed, index, 23)];
                Pair(state, "secret", 200 + index * 2, 900, site.x, site.y, false);
            }
            if (state.tick >= 3600 && state.tick <= 7200 && (state.tick - 3600) % 1800 == 0)
            {
                int index = (state.tick - 3600) / 1800;
                int first = Choice(seed, index, 71), second = (first + 1 + Choice(seed, index, 97) % 2) % 3;
                Pair(state, "broadcast", 300 + index * 2, 600, reports[first].x, reports[first].y, false);
                foreach (var message in state.messages)
                    if (message.id == 300 + index * 2 || message.id == 301 + index * 2)
                    {
                        message.secondX = message.side == 0 ? reports[second].x : 95 - reports[second].x;
                        message.secondY = message.side == 0 ? reports[second].y : 95 - reports[second].y;
                    }
            }
            if (state.tick == 7200) Pair(state, "assault", 400, 300, 47, 47, true);
        }

        private static int Choice(int seed, int index, uint stream)
        {
            uint value = unchecked((uint)seed ^ ((uint)(index + 1) * 0x9e3779b9u) ^ stream);
            value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            return (int)(value % 3);
        }

        private static void Pair(RelayState state, string kind, int firstId, int deadline, int x, int y, bool shared)
        {
            for (int side = 0; side < 2; side++)
                state.messages.Add(new RelayMessage { id = firstId + side, side = side, kind = kind, createdTick = state.tick,
                    deadlineTicks = deadline, sharedTaskId = shared ? firstId : -1,
                    x = shared || side == 0 ? x : 95 - x, y = shared || side == 0 ? y : 95 - y });
        }

        public static void Award(RelayState state, int side, string reason, int halfPoints, int messageId = -1, int age = 0, int baseHalf = 0)
        {
            state.scoreHalfPoints[side] += halfPoints;
            state.ledger.Add(new RelayScoreEntry { tick = state.tick, side = side, reason = reason, messageId = messageId,
                ageTicks = age, baseHalfPoints = baseHalf == 0 ? halfPoints : baseHalf, awardedHalfPoints = halfPoints });
        }

        public static bool Deliver(RelayState state, RelayMessage message)
        {
            if (message.deliveredTick >= 0 || message.expired || (message.kind == "secret" && !message.decoded)) return false;
            int age = state.tick - message.createdTick;
            int half;
            int basis;
            if (message.kind == "urgent" || message.kind == "assault")
            {
                if (age > message.deadlineTicks) return false;
                bool later = state.messages.Exists(other => other.sharedTaskId == message.sharedTaskId && other.side != message.side && other.deliveredTick >= 0 && other.deliveredTick < state.tick);
                basis = message.kind == "assault" ? 120 : 90;
                half = later ? 40 : basis;
            }
            else if (message.kind == "broadcast") { basis = 40; half = 40; }
            else
            {
                basis = message.kind == "secret" ? 50 : 20;
                half = age <= message.deadlineTicks ? basis * 3 / 2 : age <= message.deadlineTicks + 300 ? basis : basis / 2;
            }
            Award(state, message.side, message.kind + "-delivery", half, message.id, age, basis);
            foreach (var unit in state.units) unit.messageIds.Remove(message.id);
            message.carrierId = -1; message.stationId = -1; message.deliveredTick = state.tick;
            return true;
        }

        public static string ValidateLoad(RelayState state, RelayUnit unit, int id, RelayMapData map)
        {
            if (unit.materials > 0 || unit.reservedMaterials > 0 || unit.messageIds.Count >= PlainMessageSystem.Capacity(unit)) return "message-capacity";
            var message = state.messages.Find(value => value.id == id);
            if (message == null || message.side != unit.side || message.carrierId >= 0 || message.deliveredTick >= 0 || message.expired || message.kind == "broadcast") return "message-unavailable";
            bool position = message.x == unit.x && message.y == unit.y;
            if (message.sharedTaskId >= 0 && message.stationId < 0)
                position = Array.Exists(map.sites, site => site.kind == "hub" && site.x == unit.x && site.y == unit.y);
            return position ? null : "message-unavailable";
        }

        public static string Load(RelayState state, RelayUnit unit, int id, RelayMapData map)
        {
            string error = ValidateLoad(state, unit, id, map);
            if (error != null) return error;
            var selected = state.messages.Find(value => value.id == id);
            var candidates = state.messages.FindAll(value => value.id != id && value.kind == selected.kind && ValidateLoad(state, unit, value.id, map) == null);
            candidates.Sort((a, b) => a.id.CompareTo(b.id)); candidates.Insert(0, selected);
            foreach (var message in candidates)
            {
                if (unit.messageIds.Count == PlainMessageSystem.Capacity(unit)) break;
                message.carrierId = unit.id; message.stationId = -1; message.x = unit.x; message.y = unit.y;
                unit.messageIds.Add(message.id);
            }
            unit.messageIds.Sort();
            return null;
        }

        public static void ProcessNetwork(RelayState state)
        {
            foreach (var message in state.messages)
            {
                if (message.deliveredTick >= 0 || message.expired) continue;
                if (message.kind == "broadcast")
                {
                    bool covered = RadioNetwork.Covering(state, message.side, message.x, message.y) != null && RadioNetwork.Covering(state, message.side, message.secondX, message.secondY) != null;
                    if (!covered) message.progressStartedTick = -1;
                    else if (message.progressStartedTick < 0) message.progressStartedTick = state.tick;
                    else if (state.tick - message.progressStartedTick >= 30 && state.tick - message.createdTick <= message.deadlineTicks) Deliver(state, message);
                    continue;
                }
                if (message.carrierId >= 0) continue;
                if (message.stationId >= 0)
                {
                    var holding = state.nodes.Find(node => node.id == message.stationId);
                    if (holding == null) message.stationId = -1;
                    else
                    {
                        if (holding.active && holding.connectedToHome && (message.kind != "secret" || message.decoded)) Deliver(state, message);
                        continue;
                    }
                }
                var collector = RadioNetwork.Covering(state, message.side, message.x, message.y);
                if (message.sharedTaskId >= 0 && message.stationId < 0)
                    for (int y = 47; y <= 48 && collector == null; y++)
                        for (int x = 47; x <= 48 && collector == null; x++)
                            collector = RadioNetwork.Covering(state, message.side, x, y);
                if (collector == null) continue;
                if (message.kind != "secret" || message.decoded) Deliver(state, message);
                else if (message.stationId < 0)
                {
                    var decoder = state.nodes.Find(node => node.side == message.side && node.active && node.connectedToHome && node.kind == "communicator");
                    if (decoder == null) decoder = collector;
                    message.stationId = decoder.id; message.x = decoder.x; message.y = decoder.y;
                }
            }
            foreach (var unit in state.units)
            {
                if (!unit.stationActive || state.tick < unit.suppressedUntilTick || (unit.task != "idle" && unit.task != "decode"))
                {
                    unit.decodeMessageId = -1; unit.decodeStartedTick = -1;
                    if (unit.task == "decode") unit.task = "idle";
                    continue;
                }
                var eligible = state.messages.FindAll(value => value.kind == "secret" && value.side == unit.side && !value.decoded && !value.expired && value.deliveredTick < 0 &&
                    (value.stationId == unit.id || value.carrierId == unit.id));
                eligible.Sort((a, b) => a.id.CompareTo(b.id));
                var message = eligible.Find(value => value.id == unit.decodeMessageId);
                if (message == null && eligible.Count > 0) message = eligible[0];
                if (message == null) { unit.decodeMessageId = -1; unit.decodeStartedTick = -1; if (unit.task == "decode") unit.task = "idle"; continue; }
                unit.task = "decode";
                if (unit.decodeMessageId != message.id) { unit.decodeMessageId = message.id; unit.decodeStartedTick = state.tick; }
                if (state.tick - unit.decodeStartedTick >= 50)
                {
                    message.decoded = true; unit.task = "idle"; unit.decodeMessageId = -1; unit.decodeStartedTick = -1;
                    if (message.carrierId == unit.id)
                    {
                        unit.messageIds.Remove(message.id); message.carrierId = -1; message.stationId = unit.id;
                        message.x = unit.x; message.y = unit.y;
                    }
                    var node = state.nodes.Find(value => value.id == unit.id && value.active && value.connectedToHome);
                    if (node != null) Deliver(state, message);
                }
            }
            foreach (var message in state.messages)
                if ((message.kind == "urgent" || message.kind == "assault" || message.kind == "broadcast") &&
                    message.deliveredTick < 0 && !message.expired && state.tick - message.createdTick > message.deadlineTicks)
                {
                    message.expired = true;
                    foreach (var unit in state.units) unit.messageIds.Remove(message.id);
                    message.carrierId = -1; message.stationId = -1;
                }
        }
    }
}
