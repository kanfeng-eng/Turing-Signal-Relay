using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TuringSignalRelay
{
    public static class RelayStateHash
    {
        private static string Digest(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            using (var sha = SHA256.Create())
            {
                write(writer);
                writer.Flush();
                return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }

        public static string Map(RelayMapData map)
        {
            return Digest(writer => {
                writer.Write("TSR_MAP_V1"); writer.Write(map.mapVersion); writer.Write(map.width); writer.Write(map.height);
                foreach (string row in map.rows) writer.Write(row);
                writer.Write(map.sites.Length);
                foreach (var site in map.sites) { writer.Write(site.id); writer.Write(site.side); writer.Write(site.kind); writer.Write(site.x); writer.Write(site.y); }
                writer.Write(map.crossings.Length);
                foreach (var edge in map.crossings) { writer.Write(edge.id); writer.Write((int)edge.kind); writer.Write(edge.fromX); writer.Write(edge.fromY); writer.Write(edge.toX); writer.Write(edge.toY); writer.Write(edge.enabled); }
            });
        }

        public static string Compute(RelayConfig config, string mapHash, RelayState state)
        {
            config.Validate();
            return Digest(writer => {
                writer.Write("TSR_RELAY_STATE_V1"); writer.Write(config.protocolVersion); writer.Write(config.rulesVersion);
                writer.Write(config.featureSet); writer.Write(config.rulesHash); writer.Write(config.seed); writer.Write(config.durationTicks); writer.Write(mapHash);
                writer.Write(state.tick); writer.Write(state.completed);
                foreach (int score in state.scoreHalfPoints) writer.Write(score);
                writer.Write(state.units.Count);
                foreach (var unit in state.units)
                {
                    writer.Write(unit.id); writer.Write(unit.side); writer.Write((int)unit.role); writer.Write(unit.x); writer.Write(unit.y);
                    writer.Write(unit.task); writer.Write(unit.targetX); writer.Write(unit.targetY); writer.Write(unit.taskUntilTick);
                    writer.Write(unit.waitingSinceTick); writer.Write(unit.blockedTicks); writer.Write(unit.edgeX); writer.Write(unit.edgeY);
                    writer.Write(unit.edgeCompleteTick); writer.Write(unit.crossingId); writer.Write(unit.pathIndex);
                    writer.Write(unit.actionMessageId); writer.Write(unit.actionTargetUnitId);
                    writer.Write(unit.taskDuration);
                    writer.Write(unit.path.Length); foreach (var cell in unit.path) { writer.Write(cell.x); writer.Write(cell.y); }
                    writer.Write(unit.messageIds.Count); foreach (int id in unit.messageIds) writer.Write(id);
                }
                writer.Write(state.messages.Count);
                foreach (var message in state.messages)
                {
                    writer.Write(message.id); writer.Write(message.side); writer.Write(message.kind); writer.Write(message.x); writer.Write(message.y);
                    writer.Write(message.createdTick); writer.Write(message.carrierId); writer.Write(message.deliveredTick);
                }
                writer.Write(state.ledger.Count);
                foreach (var entry in state.ledger)
                {
                    writer.Write(entry.tick); writer.Write(entry.side); writer.Write(entry.messageId); writer.Write(entry.ageTicks);
                    writer.Write(entry.baseHalfPoints); writer.Write(entry.awardedHalfPoints); writer.Write(entry.reason);
                }
                writer.Write(state.commandResults.Length);
                writer.Write(state.resources.Count);
                foreach (var resource in state.resources)
                {
                    writer.Write(resource.id); writer.Write(resource.side); writer.Write(resource.x); writer.Write(resource.y); writer.Write(resource.amount);
                }
                foreach (var result in state.commandResults)
                {
                    writer.Write(result.side); writer.Write(result.unitId); writer.Write(result.action ?? ""); writer.Write(result.reason); writer.Write(result.accepted);
                }
                writer.Write(state.events.Length); foreach (string entry in state.events) writer.Write(entry);
                if (config.FullRules()) { writer.Write(config.supplementalRulesHash); RelayFullStateHash.Write(writer, state); }
            });
        }
    }
}
