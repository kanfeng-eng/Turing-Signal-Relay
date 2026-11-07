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
                    label = unit.role + " " + unit.id + "  " + unit.task + "  reports=" + unit.messageIds.Count });
            foreach (var site in definition.sites)
                entities.Add(new ObserverEntity { id = 10000 + site.id, side = site.side, kind = 4,
                    x = site.x, y = site.y, visibleToMask = 3, label = site.kind });
            foreach (var crossing in definition.crossings)
                entities.Add(new ObserverEntity { id = 15000 + crossing.id, side = -1, kind = 4,
                    x = (crossing.fromX + crossing.toX) / 2f, y = (crossing.fromY + crossing.toY) / 2f,
                    visibleToMask = 3, label = crossing.kind + " " + (crossing.enabled ? "available" : "unbuilt") });
            foreach (var message in state.messages)
                if (message.deliveredTick < 0)
                    entities.Add(new ObserverEntity { id = 20000 + message.id, side = message.side, kind = 3,
                        x = message.x, y = message.y, visibleToMask = masks[message.y * 96 + message.x] | (1 << message.side),
                        label = "plain " + message.id + " carrier=" + message.carrierId });
            return new ObserverFrame { protocolVersion = 1, type = "observer-frame", tick = state.tick,
                durationTicks = config.durationTicks, terrain = terrain, visibleToMask = masks, staticTerrainKnown = true,
                scoreHalfPoints = (int[])state.scoreHalfPoints.Clone(), entities = entities.ToArray(),
                rulesVersion = config.rulesVersion + "/" + config.featureSet, eventText = string.Join("\n", state.events) };
        }
    }
}
