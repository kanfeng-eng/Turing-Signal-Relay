using System.Collections.Generic;

namespace TuringSignalRelay
{
    public sealed class ObserverSnapshotBuilder
    {
        private readonly RelayConfig config;
        private readonly RelayMap map;
        private readonly RelayMapData definition;
        private readonly int[] terrain;

        public ObserverSnapshotBuilder(RelayConfig settings, RelayMapData data)
        {
            config = settings.Copy();
            map = new RelayMap(data);
            definition = map.Export();
            terrain = new int[96 * 96];
            for (int y = 0; y < 96; y++)
                for (int x = 0; x < 96; x++) terrain[y * 96 + x] = definition.rows[y][x] - '0';
        }

        public ObserverFrame Create(RelayState state)
        {
            int[] masks = VisibilitySystem.Masks(map, state);
            var entities = new List<ObserverEntity>();
            foreach (var unit in state.units)
                entities.Add(new ObserverEntity { id = unit.id, side = unit.side, x = unit.x, y = unit.y,
                    kind = (int)unit.role, visibleToMask = masks[unit.y * 96 + unit.x] | (1 << unit.side),
                    suppressed = unit.stationActive && state.tick < unit.suppressedUntilTick,
                    label = unit.role + " " + unit.id + "  " + unit.task + "  reports=" + unit.messageIds.Count +
                        (config.FullRules() ? " materials=" + unit.materials + " reserved=" + unit.reservedMaterials + " radio=" + unit.stationActive : "") });
            foreach (var site in definition.sites)
                entities.Add(new ObserverEntity { id = SiteId(site), side = site.side, kind = 4,
                    x = site.x, y = site.y, visibleToMask = 3, label = site.kind });
            foreach (var crossing in definition.crossings)
                entities.Add(new ObserverEntity { id = (config.FullRules() ? 2000000 : 15000) + crossing.id, side = -1, kind = 4,
                    x = (crossing.fromX + crossing.toX) / 2f, y = (crossing.fromY + crossing.toY) / 2f,
                    visibleToMask = 3, label = crossing.kind + " " + (crossing.enabled || state.bridges.Exists(value => value.crossingId == crossing.id) ? "available" : "unbuilt") });
            foreach (var station in state.stations)
                entities.Add(new ObserverEntity { id = 3000000 + station.id, side = station.side, kind = 4, x = station.x, y = station.y,
                    visibleToMask = masks[station.y * 96 + station.x] | (1 << station.side), suppressed = state.tick < station.suppressedUntilTick, label = "relay " + station.id });
            if (config.FullRules()) foreach (var resource in state.resources)
                if (resource.availableTick <= state.tick)
                    entities.Add(new ObserverEntity { id = 4000000 + resource.id, side = resource.side, kind = 4, x = resource.x, y = resource.y,
                        visibleToMask = masks[resource.y * 96 + resource.x] | (resource.side >= 0 ? 1 << resource.side : 0), label = resource.kind + " materials=" + resource.amount });
            foreach (var message in state.messages)
                if (message.deliveredTick < 0 && !message.expired)
                {
                    entities.Add(new ObserverEntity { id = MessageId(message.id), side = message.side, kind = 3,
                        x = message.x, y = message.y, visibleToMask = masks[message.y * 96 + message.x] | (1 << message.side),
                        label = message.kind + " " + message.id + " carrier=" + message.carrierId + " decoded=" + message.decoded });
                    if (message.kind == "broadcast") entities.Add(new ObserverEntity { id = MessageId(message.id + 1000), side = message.side, kind = 3,
                        x = message.secondX, y = message.secondY, visibleToMask = 1 << message.side, label = "broadcast second target " + message.id });
                }
            var links = new List<ObserverLink>();
            foreach (var link in state.links)
                links.Add(new ObserverLink { from = NodeId(state.nodes.Find(value => value.id == link.from)), to = NodeId(state.nodes.Find(value => value.id == link.to)), connected = true });
            var hub = new float[2];
            for (int side = 0; side < 2; side++)
            {
                int members = state.units.FindAll(unit => unit.side == side && unit.x >= 47 && unit.x <= 48 && unit.y >= 47 && unit.y <= 48).Count;
                bool connected = System.Array.Exists(definition.sites, site => site.kind == "hub" && RadioNetwork.Covering(state, side, site.x, site.y) != null);
                hub[side] = (System.Math.Min(3, members) / 3f + System.Math.Min(6, state.hubMaterials[side]) / 6f + (connected ? 1 : 0)) / 3;
            }
            return new ObserverFrame { protocolVersion = 1, type = "observer-frame", tick = state.tick,
                durationTicks = config.durationTicks, terrain = terrain, visibleToMask = masks, staticTerrainKnown = true,
                scoreHalfPoints = (int[])state.scoreHalfPoints.Clone(), entities = entities.ToArray(), links = links.ToArray(),
                weather = state.weather, hubProgressBySide = hub, hubProgress = System.Math.Max(hub[0], hub[1]),
                eventFocusId = state.eventFocusUnitId >= 0 ? state.eventFocusUnitId : state.eventFocusMessageId >= 0 ? MessageId(state.eventFocusMessageId) : -1,
                rulesVersion = config.rulesVersion + "/" + config.featureSet, eventText = string.Join("\n", state.events) };
        }
        private int SiteId(RelaySite site) { return (config.FullRules() ? 1000000 : 10000) + site.id; }
        private int MessageId(int id) { return (config.FullRules() ? 5000000 : 20000) + id; }
        private int NodeId(RelayRadioNode node)
        {
            if (node.kind == "communicator") return node.id;
            if (node.kind == "relay") return 3000000 + node.id - 20000;
            return SiteId(System.Array.Find(definition.sites, site => site.kind == "headquarters" && site.side == node.side));
        }
    }
}
