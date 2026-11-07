using System;
using System.Collections.Generic;

namespace TuringSignalRelay
{
    public static class PlainMessageSystem
    {
        public static void Spawn(RelayState state, RelaySite[] reports, int seed)
        {
            if (state.tick < 200 || state.tick >= 9000 || (state.tick - 200) % 300 != 0) return;
            int number = (state.tick - 200) / 300;
            uint value = unchecked((uint)seed ^ ((uint)(number + 1) * 0x9e3779b9u));
            value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            var first = reports[(int)(value % (uint)reports.Length)];
            for (int side = 0; side < 2; side++)
                state.messages.Add(new RelayMessage { id = number * 2 + side, side = side, createdTick = state.tick,
                    x = side == 0 ? first.x : 95 - first.x, y = side == 0 ? first.y : 95 - first.y });
        }

        public static int Capacity(RelayUnit unit) { return unit.role == UnitRole.Communicator ? 1 : unit.role == UnitRole.Transporter ? 3 : 0; }

        public static string ValidateLoad(RelayState state, RelayUnit unit, int messageId)
        {
            if (unit.messageIds.Count >= Capacity(unit)) return "message-capacity";
            var message = state.messages.Find(value => value.id == messageId);
            if (message == null || message.side != unit.side || message.carrierId != -1 || message.deliveredTick >= 0 || message.x != unit.x || message.y != unit.y)
                return "message-unavailable";
            return null;
        }

        public static string Load(RelayState state, RelayUnit unit, int messageId)
        {
            string error = ValidateLoad(state, unit, messageId);
            if (error != null) return error;
            var requested = state.messages.Find(value => value.id == messageId);
            var candidates = state.messages.FindAll(value => value.side == unit.side && value.carrierId == -1 && value.deliveredTick < 0 && value.x == unit.x && value.y == unit.y);
            candidates.Sort((a, b) => a.id.CompareTo(b.id));
            candidates.Remove(requested); candidates.Insert(0, requested);
            foreach (var message in candidates)
            {
                if (unit.messageIds.Count == Capacity(unit)) break;
                message.carrierId = unit.id;
                unit.messageIds.Add(message.id);
            }
            unit.messageIds.Sort();
            return null;
        }

        public static string Transfer(RelayState state, RelayUnit sender, RelayUnit target, int messageId)
        {
            if (target == null || sender.id == target.id || sender.side != target.side || sender.x != target.x || sender.y != target.y ||
                !sender.messageIds.Contains(messageId) || target.messageIds.Count >= Capacity(target)) return "invalid-transfer";
            sender.messageIds.Remove(messageId); target.messageIds.Add(messageId); target.messageIds.Sort();
            state.messages.Find(value => value.id == messageId).carrierId = target.id;
            return null;
        }

        public static int PlainHalfPoints(int ageTicks)
        {
            if (ageTicks < 0) throw new ArgumentOutOfRangeException("ageTicks");
            return ageTicks <= 600 ? 30 : ageTicks <= 900 ? 20 : 10;
        }

        public static void Deliver(RelayState state, RelayUnit unit)
        {
            foreach (int id in unit.messageIds)
            {
                var message = state.messages.Find(value => value.id == id);
                if (message == null || message.side != unit.side || message.carrierId != unit.id || message.deliveredTick >= 0)
                    throw new InvalidOperationException("Inconsistent carried message.");
                int age = state.tick - message.createdTick;
                int points = PlainHalfPoints(age);
                state.ledger.Add(new RelayScoreEntry { tick = state.tick, side = unit.side, messageId = id, ageTicks = age, awardedHalfPoints = points });
                state.scoreHalfPoints[unit.side] += points;
                message.deliveredTick = state.tick; message.carrierId = -1;
                message.x = unit.x; message.y = unit.y;
            }
            unit.messageIds.Clear();
        }
    }
}
