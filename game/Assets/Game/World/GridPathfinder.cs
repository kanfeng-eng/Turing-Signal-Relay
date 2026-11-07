using System;
using System.Collections.Generic;

namespace TuringSignalRelay
{
    public sealed class GridMap
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        private readonly TerrainKind[] terrain;
        internal readonly CrossingDefinition[] Crossings;

        public GridMap(int width, int height, TerrainKind[] cells)
            : this(width, height, cells, new CrossingDefinition[0]) { }

        public GridMap(int width, int height, TerrainKind[] cells, CrossingDefinition[] crossings)
        {
            if (width < 1 || height < 1 || width > 96 || height > 96 || cells == null || cells.Length != width * height)
                throw new ArgumentException("Invalid grid dimensions.");
            foreach (var cell in cells)
                if (cell < TerrainKind.Grass || cell > TerrainKind.Mountain)
                    throw new ArgumentException("Unknown terrain.");
            Width = width;
            Height = height;
            terrain = (TerrainKind[])cells.Clone();
            if (crossings == null) throw new ArgumentNullException("crossings");
            Crossings = Array.ConvertAll(crossings, value => value.Copy());
        }

        public bool Contains(GridPosition p) { return p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height; }
        public TerrainKind At(GridPosition p)
        {
            if (!Contains(p)) throw new ArgumentOutOfRangeException("p");
            return terrain[p.Y * Width + p.X];
        }

        public static bool InRadius(GridPosition a, GridPosition b, int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException("radius");
            long x = (long)a.X - b.X, y = (long)a.Y - b.Y;
            if (Math.Abs(x) > radius || Math.Abs(y) > radius) return false;
            return x * x + y * y <= (long)radius * radius;
        }
    }

    public static class GridPathfinder
    {
        public static int EnterTicks(TerrainKind terrain, UnitRole role)
        {
            int speed;
            switch (role)
            {
                case UnitRole.Communicator: speed = 120; break;
                case UnitRole.Engineer: speed = 90; break;
                case UnitRole.Transporter: speed = 70; break;
                default: throw new ArgumentOutOfRangeException("role");
            }
            int multiplier;
            switch (terrain)
            {
                case TerrainKind.Road: multiplier = 10; break;
                case TerrainKind.Grass: multiplier = 14; break;
                case TerrainKind.Mountain: multiplier = 22; break;
                default: throw new ArgumentException("Terrain cannot be entered.");
            }
            return (100 * multiplier + speed - 1) / speed;
        }

        private struct Candidate : IComparable<Candidate>
        {
            public int Index, Cost, Estimate, Rank;
            public int CompareTo(Candidate other)
            {
                int result = Estimate.CompareTo(other.Estimate);
                if (result == 0) result = Rank.CompareTo(other.Rank);
                if (result == 0) result = Cost.CompareTo(other.Cost);
                return result;
            }
        }

        // Returns the cells to enter, excluding start. Crossings are added by the movement scheduler.
        public static bool TryFind(GridMap map, GridPosition start, GridPosition goal, UnitRole role, int side,
            ISet<int> blocked, out GridPosition[] path, out int costTicks)
        {
            if (map == null) throw new ArgumentNullException("map");
            if (side < 0 || side > 1) throw new ArgumentOutOfRangeException("side");
            int minimum = EnterTicks(TerrainKind.Road, role);
            path = new GridPosition[0];
            costTicks = 0;
            if (!map.Contains(start) || !map.Contains(goal) || map.At(start) == TerrainKind.River || map.At(goal) == TerrainKind.River)
                return false;
            int count = map.Width * map.Height;
            int begin = start.Y * map.Width + start.X, end = goal.Y * map.Width + goal.X;
            if (blocked != null && blocked.Contains(end) && begin != end) return false;
            int[] costs = new int[count], previous = new int[count];
            for (int i = 0; i < count; i++) { costs[i] = int.MaxValue; previous[i] = -1; }
            costs[begin] = 0;
            var open = new SortedSet<Candidate>();
            open.Add(new Candidate { Index = begin, Cost = 0, Estimate = minimum * Distance(start, goal), Rank = side == 0 ? begin : count - 1 - begin });
            int[] dx = side == 0 ? new[] { -1, 0, 1, 0 } : new[] { 1, 0, -1, 0 };
            int[] dy = side == 0 ? new[] { 0, -1, 0, 1 } : new[] { 0, 1, 0, -1 };
            while (open.Count > 0)
            {
                Candidate current = open.Min;
                open.Remove(current);
                if (current.Cost != costs[current.Index]) continue;
                if (current.Index == end)
                {
                    var cells = new List<GridPosition>();
                    for (int at = end; at != begin; at = previous[at])
                        cells.Add(new GridPosition(at % map.Width, at / map.Width));
                    cells.Reverse();
                    path = cells.ToArray();
                    costTicks = current.Cost;
                    return true;
                }
                var position = new GridPosition(current.Index % map.Width, current.Index / map.Width);
                for (int direction = 0; direction < 4 + map.Crossings.Length; direction++)
                {
                    GridPosition next;
                    int edgeCost;
                    if (direction < 4)
                    {
                        next = new GridPosition(position.X + dx[direction], position.Y + dy[direction]);
                        if (!map.Contains(next) || map.At(next) == TerrainKind.River) continue;
                        edgeCost = EnterTicks(map.At(next), role);
                    }
                    else
                    {
                        var crossing = map.Crossings[direction - 4];
                        if (!crossing.enabled) continue;
                        var from = new GridPosition(crossing.fromX, crossing.fromY);
                        var to = new GridPosition(crossing.toX, crossing.toY);
                        if (position.Equals(from)) next = to;
                        else if (position.Equals(to)) next = from;
                        else continue;
                        edgeCost = CrossingTicks(crossing.kind, role);
                    }
                    if (!map.Contains(next) || map.At(next) == TerrainKind.River) continue;
                    int index = next.Y * map.Width + next.X;
                    if (blocked != null && blocked.Contains(index)) continue;
                    int cost = current.Cost + edgeCost;
                    if (cost >= costs[index]) continue;
                    costs[index] = cost;
                    previous[index] = current.Index;
                    open.Add(new Candidate { Index = index, Cost = cost, Estimate = cost + minimum * Distance(next, goal), Rank = side == 0 ? index : count - 1 - index });
                }
            }
            return false;
        }

        public static int CrossingTicks(CrossingKind kind, UnitRole role)
        {
            if (kind < CrossingKind.RopeBridge || kind > CrossingKind.BuiltBridge)
                throw new ArgumentOutOfRangeException("kind");
            int ticks = kind == CrossingKind.Ferry ? 30 : 20;
            return role == UnitRole.Transporter ? ticks * 2 : ticks;
        }

        private static int Distance(GridPosition a, GridPosition b)
        {
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        }
    }
}
