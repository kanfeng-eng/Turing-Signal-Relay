using System;
using System.Collections.Generic;

namespace TuringSignalRelay
{
    public static class MovementSystem
    {
        public static string MoveTo(RelayMap map, RelayUnit unit, int x, int y)
        {
            if (unit.task == "move" && unit.targetX == x && unit.targetY == y) return null;
            var start = new GridPosition(unit.edgeCompleteTick > 0 ? unit.edgeX : unit.x, unit.edgeCompleteTick > 0 ? unit.edgeY : unit.y);
            GridPosition[] route;
            int cost;
            if (!GridPathfinder.TryFind(map.Grid, start, new GridPosition(x, y), unit.role, unit.side, null, out route, out cost))
                return "unreachable-target";
            unit.task = "move"; unit.targetX = x; unit.targetY = y;
            unit.path = Array.ConvertAll(route, cell => new RelayCell(cell.X, cell.Y));
            unit.pathIndex = 0; unit.blockedTicks = 0; unit.waitingSinceTick = -1;
            unit.actionMessageId = unit.actionTargetUnitId = -1;
            unit.taskDuration = 0;
            return null;
        }

        public static void Wait(RelayUnit unit, int tick, int duration)
        {
            unit.task = "wait";
            unit.taskDuration = duration;
            unit.taskUntilTick = Math.Max(tick, unit.edgeCompleteTick) + duration;
            unit.path = new RelayCell[0]; unit.pathIndex = 0;
            unit.waitingSinceTick = -1; unit.blockedTicks = 0;
            unit.actionMessageId = unit.actionTargetUnitId = -1;
        }

        public static void Step(RelayMap map, RelayState state)
        {
            foreach (var unit in state.units)
            {
                if (unit.edgeCompleteTick > 0 && unit.edgeCompleteTick <= state.tick)
                {
                    unit.x = unit.edgeX; unit.y = unit.edgeY;
                    unit.edgeCompleteTick = 0; unit.crossingId = -1;
                    if (unit.pathIndex < unit.path.Length && unit.path[unit.pathIndex].x == unit.x && unit.path[unit.pathIndex].y == unit.y)
                        unit.pathIndex++;
                }
                if (unit.task == "wait" && unit.taskUntilTick <= state.tick) unit.task = "idle";
            }
            var ordered = new List<RelayUnit>(state.units);
            ordered.Sort((a, b) => {
                int first = a.waitingSinceTick < 0 ? state.tick : a.waitingSinceTick;
                int second = b.waitingSinceTick < 0 ? state.tick : b.waitingSinceTick;
                int result = first.CompareTo(second);
                if (result == 0) result = (a.side == state.tick % 2 ? 0 : 1).CompareTo(b.side == state.tick % 2 ? 0 : 1);
                return result == 0 ? a.id.CompareTo(b.id) : result;
            });
            foreach (var unit in ordered)
            {
                if (unit.task != "move" || unit.edgeCompleteTick > 0) continue;
                if (unit.x == unit.targetX && unit.y == unit.targetY)
                {
                    unit.task = "idle"; unit.path = new RelayCell[0]; unit.pathIndex = 0;
                    unit.blockedTicks = 0; unit.waitingSinceTick = -1;
                    continue;
                }
                if (unit.pathIndex >= unit.path.Length) continue;
                var next = unit.path[unit.pathIndex];
                var crossing = map.Crossing(unit.x, unit.y, next.x, next.y);
                if (crossing != null)
                {
                    if (unit.waitingSinceTick < 0) unit.waitingSinceTick = state.tick;
                    if ((crossing.kind == CrossingKind.Ferry && !RelayMap.FerryOpen(state.tick)) ||
                        state.units.Exists(other => other.edgeCompleteTick > 0 && other.crossingId == crossing.id)) continue;
                }
                if (Occupied(map, state, unit, next.x, next.y))
                {
                    if (++unit.blockedTicks >= 10)
                        Replan(map, state, unit);
                    continue;
                }
                int ticks = crossing == null ? GridPathfinder.EnterTicks(map.Grid.At(new GridPosition(next.x, next.y)), unit.role) :
                    GridPathfinder.CrossingTicks(crossing.kind, unit.role);
                unit.edgeX = next.x; unit.edgeY = next.y; unit.edgeCompleteTick = state.tick + ticks;
                unit.crossingId = crossing == null ? -1 : crossing.id;
                unit.blockedTicks = 0; unit.waitingSinceTick = -1;
            }
        }

        private static bool Occupied(RelayMap map, RelayState state, RelayUnit moving, int x, int y)
        {
            if (map.SharedCell(x, y)) return false;
            return state.units.Exists(other => other.id != moving.id &&
                ((other.x == x && other.y == y) || (other.edgeCompleteTick > 0 && other.edgeX == x && other.edgeY == y)));
        }

        private static void Replan(RelayMap map, RelayState state, RelayUnit unit)
        {
            var blocked = new HashSet<int>();
            foreach (var other in state.units)
            {
                if (other.id == unit.id) continue;
                if (!map.SharedCell(other.x, other.y)) blocked.Add(other.y * 96 + other.x);
                if (other.edgeCompleteTick > 0 && !map.SharedCell(other.edgeX, other.edgeY)) blocked.Add(other.edgeY * 96 + other.edgeX);
            }
            GridPosition[] route;
            int cost;
            if (GridPathfinder.TryFind(map.Grid, new GridPosition(unit.x, unit.y), new GridPosition(unit.targetX, unit.targetY), unit.role, unit.side, blocked, out route, out cost))
            {
                unit.path = Array.ConvertAll(route, cell => new RelayCell(cell.X, cell.Y));
                unit.pathIndex = 0;
                unit.waitingSinceTick = -1;
            }
            unit.blockedTicks = 0;
        }
    }
}
