using System;
using System.Collections.Generic;
using TuringSignalRelay;

internal static class WorldCheckMain
{
    private static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    public static void Main()
    {
        Require(GridPathfinder.EnterTicks(TerrainKind.Road, UnitRole.Communicator) == 9, "Road timing");
        Require(GridPathfinder.EnterTicks(TerrainKind.Grass, UnitRole.Engineer) == 16, "Grass timing");
        Require(GridPathfinder.EnterTicks(TerrainKind.Mountain, UnitRole.Transporter) == 32, "Mountain timing");
        var cells = new TerrainKind[15];
        for (int i = 0; i < cells.Length; i++) cells[i] = TerrainKind.Road;
        for (int x = 1; x <= 3; x++) cells[5 + x] = TerrainKind.Mountain;
        var map = new GridMap(5, 3, cells);
        GridPosition[] path, rotated;
        int cost, otherCost;
        Require(GridPathfinder.TryFind(map, new GridPosition(0, 1), new GridPosition(4, 1), UnitRole.Communicator, 0, null, out path, out cost), "Reachable");
        Require(cost == 54 && path.Length == 6, "Weighted path must avoid mountain shortcut");
        Require(GridPathfinder.TryFind(map, new GridPosition(4, 1), new GridPosition(0, 1), UnitRole.Communicator, 1, null, out rotated, out otherCost), "Rotated reachability");
        Require(cost == otherCost && path.Length == rotated.Length, "Rotated cost");
        for (int i = 0; i < path.Length; i++)
            Require(path[i].X == 4 - rotated[i].X && path[i].Y == 2 - rotated[i].Y, "Rotated tie breaking");
        var blocked = new HashSet<int> { 2, 7, 12 };
        Require(!GridPathfinder.TryFind(map, new GridPosition(0, 1), new GridPosition(4, 1), UnitRole.Communicator, 0, blocked, out path, out cost), "Blocked corridor");
        Require(GridPathfinder.TryFind(map, new GridPosition(0, 1), new GridPosition(0, 1), UnitRole.Communicator, 0, null, out path, out cost) && path.Length == 0 && cost == 0, "Already at target");
        Require(!GridPathfinder.TryFind(map, new GridPosition(-1, 0), new GridPosition(4, 1), UnitRole.Communicator, 0, null, out path, out cost), "Out of range");
        cells[5] = TerrainKind.River;
        Require(map.At(new GridPosition(0, 1)) == TerrainKind.Road, "Map owns terrain copy");
        var river = new GridMap(5, 3, cells);
        Require(!GridPathfinder.TryFind(river, new GridPosition(0, 1), new GridPosition(4, 1), UnitRole.Communicator, 0, null, out path, out cost), "River start rejected");
        Require(GridMap.InRadius(new GridPosition(0, 0), new GridPosition(3, 4), 5), "Coverage boundary included");
        Require(!GridMap.InRadius(new GridPosition(0, 0), new GridPosition(4, 4), 5), "Coverage outside excluded");
        var full = new GridMap(96, 96, new TerrainKind[96 * 96]);
        Require(GridPathfinder.TryFind(full, new GridPosition(0, 0), new GridPosition(95, 95), UnitRole.Transporter, 0, null, out path, out cost) && path.Length == 190 && cost == 3800, "Full map integer route");
        var log = new ObserverEventLog(2);
        log.Accept(1, 0, "first"); log.Accept(1, 1, "second"); log.Accept(1, 2, "third");
        Require(!log.Text.Contains("first") && log.Text.Contains("second") && log.Text.Contains("third"), "Log limit applied");
        string saved = log.Text;
        log.Accept(1, 2, "duplicate"); Require(log.Text == saved, "Duplicate tick ignored");
        log.Accept(1, 1, "seek"); Require(!log.Text.Contains("third") && log.Text.Contains("seek"), "Rewind clears log");
        log.Accept(2, 3, "new source"); Require(!log.Text.Contains("seek"), "Source clears log");
        log.Accept(2, 4, new string('x', 10000)); Require(log.Text.Length < 2200, "Untrusted event bounded");
        Console.WriteLine("WORLD_AND_LEGACY_CHECKS_PASSED");
    }
}
