using System;
using System.Collections.Generic;

namespace TuringSignalRelay
{
    public static class RadioNetwork
    {
        public static int HomeId(int side) { return 10000 + side; }
        public static void Rebuild(RelayState state, RelayMapData map)
        {
            state.nodes.Clear(); state.links.Clear();
            foreach (var site in map.sites)
                if (site.kind == "headquarters") state.nodes.Add(new RelayRadioNode { id = HomeId(site.side), side = site.side,
                    x = site.x, y = site.y, kind = "headquarters", radius = Math.Max(0, 9 - state.weatherReduction), active = true, connectedToHome = true });
            foreach (var unit in state.units)
                if (unit.stationActive) state.nodes.Add(new RelayRadioNode { id = unit.id, side = unit.side, x = unit.x, y = unit.y,
                    kind = "communicator", radius = Math.Max(0, 9 - state.weatherReduction), active = state.tick >= unit.suppressedUntilTick });
            foreach (var station in state.stations)
                state.nodes.Add(new RelayRadioNode { id = 20000 + station.id, side = station.side, x = station.x, y = station.y,
                    kind = "relay", radius = Math.Max(0, 12 - state.weatherReduction), active = state.tick >= station.suppressedUntilTick });
            state.nodes.Sort((a, b) => a.id.CompareTo(b.id));
            for (int i = 0; i < state.nodes.Count; i++)
                for (int j = i + 1; j < state.nodes.Count; j++)
                {
                    var a = state.nodes[i]; var b = state.nodes[j];
                    if (a.side == b.side && a.active && b.active && RelayGeometry.Within(a.x, a.y, b.x, b.y, Math.Min(a.radius, b.radius)))
                        state.links.Add(new RelayRadioLink { from = a.id, to = b.id });
                }
            var queue = new Queue<RelayRadioNode>();
            foreach (var node in state.nodes) if (node.connectedToHome) queue.Enqueue(node);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var link in state.links)
                {
                    int nextId = link.from == current.id ? link.to : link.to == current.id ? link.from : -1;
                    if (nextId < 0) continue;
                    var next = state.nodes.Find(value => value.id == nextId);
                    if (!next.connectedToHome) { next.connectedToHome = true; queue.Enqueue(next); }
                }
            }
        }

        public static RelayRadioNode Covering(RelayState state, int side, int x, int y)
        {
            return state.nodes.Find(node => node.side == side && node.active && node.connectedToHome && RelayGeometry.Within(node.x, node.y, x, y, node.radius));
        }
    }
}
