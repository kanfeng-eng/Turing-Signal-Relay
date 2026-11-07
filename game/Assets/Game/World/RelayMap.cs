using System;
using System.Collections.Generic;

namespace TuringSignalRelay
{
    public sealed class RelayMap
    {
        public GridMap Grid { get; private set; }
        public string Hash { get; private set; }
        private readonly RelayMapData data;

        public RelayMap(RelayMapData source)
        {
            if (source == null || source.mapVersion != "map-v1" || source.width != 96 || source.height != 96 ||
                source.rows == null || source.rows.Length != 96 || source.sites == null || source.crossings == null)
                throw new ArgumentException("Expected the complete map-v1 definition.");
            data = new RelayMapData { mapVersion = source.mapVersion, width = 96, height = 96,
                rows = (string[])source.rows.Clone(), sites = Array.ConvertAll(source.sites, value => value == null ? null : value.Copy()),
                crossings = Array.ConvertAll(source.crossings, value => value == null ? null : value.Copy()) };
            var cells = new TerrainKind[96 * 96];
            for (int y = 0; y < 96; y++)
            {
                if (data.rows[y] == null || data.rows[y].Length != 96) throw new ArgumentException("Invalid terrain row.");
                for (int x = 0; x < 96; x++)
                {
                    char tile = data.rows[y][x];
                    if (tile < '0' || tile > '3') throw new ArgumentException("Invalid terrain code.");
                    cells[y * 96 + x] = (TerrainKind)(tile - '0');
                }
            }
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] != cells[cells.Length - 1 - i]) throw new ArgumentException("Terrain is not rotationally symmetric.");
            var ids = new HashSet<int>();
            foreach (var site in data.sites)
            {
                if (site == null || !ids.Add(site.id) || site.id < 0 || site.side < -1 || site.side > 1 ||
                    !Inside(site.x, site.y) || cells[site.y * 96 + site.x] == TerrainKind.River ||
                    (site.kind != "headquarters" && site.kind != "report" && site.kind != "depot" && site.kind != "hub" && site.kind != "central-depot"))
                    throw new ArgumentException("Invalid map site.");
                if ((site.kind == "hub" || site.kind == "central-depot") != (site.side == -1))
                    throw new ArgumentException("Invalid site ownership.");
                if (!Array.Exists(data.sites, other => other != null && other.kind == site.kind && other.side == (site.side == -1 ? -1 : 1 - site.side) &&
                    other.x == 95 - site.x && other.y == 95 - site.y)) throw new ArgumentException("Unpaired map site.");
            }
            for (int side = 0; side < 2; side++)
                foreach (string kind in new[] { "headquarters", "report", "depot" })
                    if (Array.FindAll(data.sites, site => site.side == side && site.kind == kind).Length != (kind == "report" ? 3 : kind == "depot" ? 2 : 1))
                        throw new ArgumentException("Incorrect site count.");
            var hubs = Array.FindAll(data.sites, site => site.kind == "hub");
            if (hubs.Length != 4 || !Array.TrueForAll(hubs, site => site.x >= 47 && site.x <= 48 && site.y >= 47 && site.y <= 48) ||
                Array.FindAll(data.sites, site => site.kind == "central-depot").Length == 0)
                throw new ArgumentException("Missing central regions.");
            ids.Clear();
            foreach (var crossing in data.crossings)
            {
                if (crossing == null || crossing.id < 0 || !ids.Add(crossing.id) || crossing.kind < CrossingKind.RopeBridge || crossing.kind > CrossingKind.BuiltBridge ||
                    !Inside(crossing.fromX, crossing.fromY) || !Inside(crossing.toX, crossing.toY) ||
                    Math.Abs(crossing.fromX - crossing.toX) + Math.Abs(crossing.fromY - crossing.toY) != 2 ||
                    (crossing.fromX != crossing.toX && crossing.fromY != crossing.toY) ||
                    cells[crossing.fromY * 96 + crossing.fromX] == TerrainKind.River || cells[crossing.toY * 96 + crossing.toX] == TerrainKind.River ||
                    cells[((crossing.fromY + crossing.toY) / 2) * 96 + (crossing.fromX + crossing.toX) / 2] != TerrainKind.River ||
                    crossing.enabled != (crossing.kind != CrossingKind.BuiltBridge))
                    throw new ArgumentException("Invalid crossing.");
                if (!Array.Exists(data.crossings, other => other != null && other.kind == crossing.kind && other.enabled == crossing.enabled &&
                    ((other.fromX == 95 - crossing.fromX && other.fromY == 95 - crossing.fromY && other.toX == 95 - crossing.toX && other.toY == 95 - crossing.toY) ||
                    (other.toX == 95 - crossing.fromX && other.toY == 95 - crossing.fromY && other.fromX == 95 - crossing.toX && other.fromY == 95 - crossing.toY))))
                    throw new ArgumentException("Unpaired crossing.");
            }
            foreach (CrossingKind kind in Enum.GetValues(typeof(CrossingKind)))
                if (Array.FindAll(data.crossings, value => value.kind == kind).Length != (kind == CrossingKind.BuiltBridge ? 6 : 2))
                    throw new ArgumentException("Incorrect crossing count.");
            Array.Sort(data.sites, (a, b) => a.id.CompareTo(b.id));
            Array.Sort(data.crossings, (a, b) => a.id.CompareTo(b.id));
            Grid = new GridMap(96, 96, cells, data.crossings);
            ValidateReachability();
            Hash = RelayStateHash.Map(data);
        }

        public RelayMapData Export()
        {
            return new RelayMapData { mapVersion = data.mapVersion, width = data.width, height = data.height,
                rows = (string[])data.rows.Clone(), sites = Array.ConvertAll(data.sites, value => value.Copy()),
                crossings = Array.ConvertAll(data.crossings, value => value.Copy()) };
        }

        public RelaySite Headquarters(int side)
        {
            var site = Array.Find(data.sites, value => value.kind == "headquarters" && value.side == side);
            if (site == null) throw new ArgumentOutOfRangeException("side");
            return site.Copy();
        }
        public RelaySite[] Reports(int side) { return Array.ConvertAll(Array.FindAll(data.sites, value => value.kind == "report" && value.side == side), value => value.Copy()); }
        public bool SharedCell(int x, int y) { return Array.Exists(data.sites, value => value.x == x && value.y == y); }
        public CrossingDefinition Crossing(int x, int y, int toX, int toY)
        {
            var crossing = Array.Find(data.crossings, value => value.enabled &&
                ((value.fromX == x && value.fromY == y && value.toX == toX && value.toY == toY) ||
                (value.toX == x && value.toY == y && value.fromX == toX && value.fromY == toY)));
            return crossing == null ? null : crossing.Copy();
        }

        public static bool FerryOpen(int tick) { return tick >= 2400 && (tick - 2400) % 450 < 150; }
        private static bool Inside(int x, int y) { return x >= 0 && y >= 0 && x < 96 && y < 96; }

        private void ValidateReachability()
        {
            // Check initial permanent routes without relying on closed ferries or unbuilt bridges.
            bool[] seen = new bool[96 * 96];
            var first = Headquarters(0);
            var queue = new Queue<GridPosition>();
            queue.Enqueue(new GridPosition(first.x, first.y));
            seen[first.y * 96 + first.x] = true;
            int[] dx = { -1, 0, 1, 0 }, dy = { 0, -1, 0, 1 };
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var next = new List<GridPosition>();
                for (int i = 0; i < 4; i++) next.Add(new GridPosition(current.X + dx[i], current.Y + dy[i]));
                foreach (var crossing in data.crossings)
                    if (crossing.kind == CrossingKind.RopeBridge)
                    {
                        if (current.X == crossing.fromX && current.Y == crossing.fromY) next.Add(new GridPosition(crossing.toX, crossing.toY));
                        if (current.X == crossing.toX && current.Y == crossing.toY) next.Add(new GridPosition(crossing.fromX, crossing.fromY));
                    }
                foreach (var cell in next)
                    if (Grid.Contains(cell) && Grid.At(cell) != TerrainKind.River && !seen[cell.Y * 96 + cell.X])
                    {
                        seen[cell.Y * 96 + cell.X] = true;
                        queue.Enqueue(cell);
                    }
            }
            foreach (var site in data.sites)
                if (!seen[site.y * 96 + site.x]) throw new ArgumentException("Map site is unreachable at match start.");
        }
    }
}
