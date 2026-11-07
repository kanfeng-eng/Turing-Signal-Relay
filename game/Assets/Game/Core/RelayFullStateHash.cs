using System.IO;

namespace TuringSignalRelay
{
    internal static class RelayFullStateHash
    {
        internal static void Write(BinaryWriter writer, RelayState state)
        {
            writer.Write("TSR_FULL_RULES_V1");
            writer.Write(state.weather); writer.Write(state.weatherReduction);
            writer.Write(state.eventFocusUnitId); writer.Write(state.eventFocusMessageId);
            foreach (int value in state.hubMaterials) writer.Write(value);
            foreach (int value in state.compensationCount) writer.Write(value);
            foreach (var unit in state.units)
            {
                writer.Write(unit.materials); writer.Write(unit.reservedMaterials); writer.Write(unit.suppressedUntilTick);
                writer.Write(unit.suppressReadyTick); writer.Write(unit.stationActive); writer.Write(unit.decodeMessageId); writer.Write(unit.decodeStartedTick);
            }
            foreach (var message in state.messages)
            {
                writer.Write(message.deadlineTicks); writer.Write(message.sharedTaskId); writer.Write(message.stationId);
                writer.Write(message.decoded); writer.Write(message.expired); writer.Write(message.secondX); writer.Write(message.secondY); writer.Write(message.progressStartedTick);
            }
            foreach (var resource in state.resources) { writer.Write(resource.kind); writer.Write(resource.availableTick); }
            writer.Write(state.stations.Count);
            foreach (var station in state.stations)
            {
                writer.Write(station.id); writer.Write(station.side); writer.Write(station.x); writer.Write(station.y); writer.Write(station.suppressedUntilTick);
            }
            writer.Write(state.bridges.Count);
            foreach (var bridge in state.bridges) { writer.Write(bridge.crossingId); writer.Write(bridge.side); writer.Write(bridge.completedTick); }
            writer.Write(state.nodes.Count);
            foreach (var node in state.nodes)
            {
                writer.Write(node.id); writer.Write(node.side); writer.Write(node.x); writer.Write(node.y); writer.Write(node.kind);
                writer.Write(node.radius); writer.Write(node.active); writer.Write(node.connectedToHome);
            }
            writer.Write(state.links.Count);
            foreach (var link in state.links) { writer.Write(link.from); writer.Write(link.to); }
        }
    }
}
