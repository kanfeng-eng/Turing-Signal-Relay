using System;
using System.Collections.Generic;
using TuringSignalRelay;

internal static class FullChecks
{
    private static int assertions;
    private static void Require(bool condition, string name)
    {
        if (!condition) throw new Exception("FULL_RULES_FAIL " + name);
        assertions++;
    }
    private static RelayState Fresh(RelayConfig config, RelayMapData map) { return new RelayMatchEngine(config, map).GetSnapshot(); }
    private static string Start(FullRuleSystem rules, RelayState state, RelayUnit unit, string action, int x = 0, int y = 0)
    {
        string error;
        if (!rules.TryStart(state, unit, new RelayCommand { action = action, unitId = unit.id, x = x, y = y }, out error)) throw new Exception("Unsupported test action");
        if (error == null) FullRuleSystem.AfterAccepted(unit, action);
        return error;
    }

    internal static void Run(RelayConfig config, RelayMapData definition)
    {
        var map = new RelayMap(definition);
        var rules = new FullRuleSystem(map.Export());
        var state = Fresh(config, definition);
        Require(state.nodes.Count == 2 && state.resources.Count == 8, "initial HQ nodes and grouped inventories");
        state.stations.Add(new RelayFixedStation { id = 17, side = 0, x = 17, y = 8 });
        state.stations.Add(new RelayFixedStation { id = 29, side = 0, x = 29, y = 8 });
        state.units[0].x = 38; state.units[0].y = 8; state.units[0].stationActive = true;
        RadioNetwork.Rebuild(state, definition);
        Require(state.nodes.Find(node => node.id == 0).connectedToHome, "12 range relay pairs and 9 range communicator");
        state.tick = 3600; rules.Refresh(state, config.seed); RadioNetwork.Rebuild(state, definition);
        Require(state.weatherReduction == 3 && !state.nodes.Find(node => node.id == 0).connectedToHome, "storm changes coverage and links");
        state.tick = 4050; rules.Refresh(state, config.seed); RadioNetwork.Rebuild(state, definition);
        Require(state.weatherReduction == 0 && state.nodes.Find(node => node.id == 0).connectedToHome, "storm end restores links");
        state.tick = 4800; rules.Refresh(state, config.seed);
        Require(state.weatherReduction == 5, "interference starts");
        state.tick = 5100; rules.Refresh(state, config.seed);
        Require(state.weatherReduction == 0, "interference end is exclusive");

        state = Fresh(config, definition);
        var communicator = state.units[0];
        Require(Start(rules, state, communicator, "setupStation") == null, "setup accepted");
        state.tick = 19; rules.CompleteTasks(state, new List<string>());
        Require(!communicator.stationActive, "setup needs full twenty ticks");
        state.tick = 20; rules.CompleteTasks(state, new List<string>());
        Require(communicator.stationActive, "setup completion boundary");
        var attacker = state.units.Find(unit => unit.id == 100); attacker.x = 15; attacker.y = 8;
        state.tick = 100;
        Require(Start(rules, state, attacker, "suppress", 8, 8) == null && communicator.suppressedUntilTick == 140 && attacker.suppressReadyTick == 350, "suppression range and timers");
        state.tick = 139; RadioNetwork.Rebuild(state, definition);
        Require(!state.nodes.Find(node => node.id == 0).active, "suppression active at last tick");
        state.tick = 140; RadioNetwork.Rebuild(state, definition);
        Require(state.nodes.Find(node => node.id == 0).active, "suppression recovery boundary");
        Require(Start(rules, state, attacker, "suppress", 8, 8) != null && attacker.suppressReadyTick == 350, "cooldown rejects without restarting");
        Require(Start(rules, state, state.units[3], "setupStation") == "requires-communicator", "engineer cannot setup radio");

        state = Fresh(config, definition);
        var engineer = state.units[3]; var crossing = Array.Find(definition.crossings, item => item.kind == CrossingKind.BuiltBridge);
        engineer.x = crossing.fromX; engineer.y = crossing.fromY; engineer.materials = 3;
        state.tick = 100;
        Require(Start(rules, state, engineer, "buildBridge", (crossing.fromX + crossing.toX) / 2, (crossing.fromY + crossing.toY) / 2) == null, "bridge reservation");
        Require(engineer.materials == 0 && engineer.reservedMaterials == 3 && engineer.taskUntilTick == 160, "bridge cost and duration");
        state.tick = 110;
        Start(rules, state, engineer, "buildBridge", engineer.targetX, engineer.targetY);
        Require(engineer.taskUntilTick == 160, "repeated construction preserves progress");
        MovementSystem.Wait(engineer, 110, 1); FullRuleSystem.AfterAccepted(engineer, "wait");
        Require(engineer.materials == 3 && engineer.reservedMaterials == 0, "cancelled construction refunds exact reservation");
        state.tick = 160; rules.CompleteTasks(state, new List<string>());
        Require(state.bridges.Count == 0 && state.scoreHalfPoints[0] == 0, "cancelled project does not complete");
        state.tick = 200; Start(rules, state, engineer, "buildBridge", (crossing.fromX + crossing.toX) / 2, (crossing.fromY + crossing.toY) / 2);
        state.tick = 260; rules.CompleteTasks(state, new List<string>()); map.SynchronizeBridges(state.bridges);
        Require(state.bridges.Count == 1 && engineer.reservedMaterials == 0 && state.scoreHalfPoints[0] == 40, "completed bridge scores once");
        GridPosition[] path; int ticks;
        Require(GridPathfinder.TryFind(map.Grid, new GridPosition(crossing.fromX, crossing.fromY), new GridPosition(crossing.toX, crossing.toY), UnitRole.Transporter, 0, null, out path, out ticks) && path.Length == 1 && ticks == 40, "built bridge updates crossing graph");
        state = Fresh(config, definition); engineer = state.units[3]; engineer.x = 10; engineer.y = 10; engineer.materials = 3;
        Require(Start(rules, state, engineer, "buildRelay", 10, 10) == null && engineer.reservedMaterials == 2, "relay construction reserves two");
        state.tick = 49; rules.CompleteTasks(state, new List<string>()); Require(state.stations.Count == 0, "relay needs fifty ticks");
        state.tick = 50; rules.CompleteTasks(state, new List<string>());
        Require(state.stations.Count == 1 && state.scoreHalfPoints[0] == 30 && engineer.materials == 1, "relay completion and remaining cargo");

        state = Fresh(config, definition); state.tick = 250; rules.Refresh(state, config.seed);
        Require(state.resources.FindAll(item => item.kind == "depot").TrueForAll(item => item.amount == 2), "own depots refresh two");
        state.tick = 1200; rules.Refresh(state, config.seed);
        Require(state.resources.Find(item => item.kind == "airdrop").amount == 4, "central airdrop independent stock");
        state.tick = 3000; rules.Refresh(state, config.seed);
        Require(state.resources.Find(item => item.kind == "central-depot").amount == 0, "central opens before first refresh");
        state.tick = 3250; rules.Refresh(state, config.seed);
        Require(state.resources.Find(item => item.kind == "central-depot").amount == 2, "central first refresh at 5:25");
        var transport = state.units[5]; var enemyStock = state.resources.Find(item => item.kind == "depot" && item.side == 1);
        transport.x = enemyStock.x; transport.y = enemyStock.y;
        Require(Start(rules, state, transport, "pickup") == "no-owned-materials-here", "R19 enemy depot access denied");
        state = Fresh(config, definition); state.tick = 1200; rules.Refresh(state, config.seed);
        var red = state.units[5]; var blue = state.units.Find(unit => unit.id == 105);
        red.x = blue.x = 47; red.y = blue.y = 47;
        state.tick = 1201; Start(rules, state, red, "pickup"); Start(rules, state, blue, "pickup");
        state.tick = 1202; rules.CompleteTasks(state, new List<string>());
        Require(red.materials == 4 && blue.materials == 0 && state.resources.Find(item => item.kind == "airdrop").amount == 0, "shared stock uses completion priority without duplicating");
        red.x = 8; red.y = 8; Start(rules, state, red, "unloadMaterials");
        state.tick++; rules.CompleteTasks(state, new List<string>());
        Require(red.materials == 0 && state.resources.Find(item => item.kind == "headquarters" && item.side == 0).amount == 4, "headquarters inventory accepts unload");

        state = Fresh(config, definition); state.scoreHalfPoints[1] = 1000; state.scoreHalfPoints[0] = 850; state.tick = 250; rules.Refresh(state, config.seed);
        Require(state.compensationCount[0] == 0, "exact fifteen percent does not compensate");
        state = Fresh(config, definition); state.scoreHalfPoints[1] = 1000; state.scoreHalfPoints[0] = 849;
        state.tick = 250; rules.Refresh(state, config.seed);
        Require(state.compensationCount[0] == 1 && state.resources.Find(item => item.id == 1004).amount == 3, "strict deficit adds one to least stock");
        state.tick = 500; rules.Refresh(state, config.seed); state.tick = 750; rules.Refresh(state, config.seed);
        Require(state.compensationCount[0] == 2, "compensation capped twice per side");

        TestMessages(config, definition, rules);
        TestSeries(config, definition);
        TestSchedule(config, definition);
        var hidden = new RelayMatchEngine(config, definition).GetObservation(0);
        Require(Array.TrueForAll(hidden.units, value => value.side == 0) && hidden.hubMaterials[1] == -1 && Array.TrueForAll(hidden.resources, value => value.side == 0), "initial observation hides enemy inventories and units");
        Console.WriteLine("FULL_RULE_CHECKS_PASSED assertions=" + assertions);
    }

    private static void TestSchedule(RelayConfig config, RelayMapData definition)
    {
        var state = Fresh(config, definition);
        var rules = new FullRuleSystem(definition);
        for (int tick = 1; tick <= 9000; tick++)
        {
            state.tick = tick;
            rules.Refresh(state, config.seed);
        }
        Require(state.messages.FindAll(value => value.kind == "plain").Count == 60, "full match spawns thirty plain pairs");
        Require(state.messages.FindAll(value => value.kind == "urgent").Count == 12, "full match spawns six urgent pairs");
        Require(state.messages.FindAll(value => value.kind == "secret").Count == 8, "full match spawns four secret pairs");
        Require(state.messages.FindAll(value => value.kind == "broadcast").Count == 6, "full match spawns three broadcast pairs");
        Require(state.messages.FindAll(value => value.kind == "assault").Count == 2, "full match spawns one assault pair");
        foreach (var message in state.messages.FindAll(value => value.side == 0 && value.sharedTaskId < 0))
        {
            var other = state.messages.Find(value => value.id == message.id + 1 && value.side == 1);
            if (other == null || other.createdTick != message.createdTick || other.x != 95 - message.x || other.y != 95 - message.y)
                throw new Exception("FULL_RULES_FAIL seeded task pair lost rotational symmetry");
        }
        Require(state.messages.FindAll(value => value.kind == "broadcast").TrueForAll(value => value.x != value.secondX || value.y != value.secondY),
            "broadcast always selects two distinct report points");
        Require(state.resources.FindAll(value => value.kind == "depot" || value.kind == "central-depot").TrueForAll(value => value.amount == 6),
            "full match resource refresh respects depot capacity");
    }

    private static void TestMessages(RelayConfig config, RelayMapData definition, FullRuleSystem rules)
    {
        var state = Fresh(config, definition); state.tick = 1800; FullMessageSystem.Spawn(state, definition, config.seed);
        var red = state.messages.Find(item => item.kind == "urgent" && item.side == 0);
        var blue = state.messages.Find(item => item.kind == "urgent" && item.side == 1);
        state.tick = 1801; Require(FullMessageSystem.Deliver(state, red), "urgent first arrival");
        state.tick = 1802; Require(FullMessageSystem.Deliver(state, blue) && state.scoreHalfPoints[0] == 90 && state.scoreHalfPoints[1] == 40, "urgent later arrival");
        Require(!FullMessageSystem.Deliver(state, red), "duplicate urgent delivery rejected");
        state = Fresh(config, definition); state.tick = 1800; FullMessageSystem.Spawn(state, definition, config.seed);
        state.tick = 1810;
        foreach (var message in state.messages) FullMessageSystem.Deliver(state, message);
        Require(state.scoreHalfPoints[0] == 90 && state.scoreHalfPoints[1] == 90, "same tick first arrival is fair");
        state = Fresh(config, definition); state.tick = 7200; FullMessageSystem.Spawn(state, definition, config.seed);
        state.tick = 7500;
        Require(FullMessageSystem.Deliver(state, state.messages.Find(item => item.kind == "assault" && item.side == 0)) && state.scoreHalfPoints[0] == 120, "assault deadline inclusive");
        state.tick = 7501; FullMessageSystem.ProcessNetwork(state);
        Require(state.messages.Find(item => item.kind == "assault" && item.side == 1).expired, "assault expires after deadline");

        state = Fresh(config, definition);
        var unit = state.units[0]; unit.stationActive = true; unit.messageIds.Add(200);
        var secret = new RelayMessage { id = 200, side = 0, x = 8, y = 8, kind = "secret", carrierId = 0, deadlineTicks = 900 };
        state.messages.Add(secret);
        RadioNetwork.Rebuild(state, definition);
        for (int tick = 0; tick < 50; tick++) { state.tick = tick; FullMessageSystem.ProcessNetwork(state); }
        Require(!secret.decoded, "secret must translate fifty full ticks");
        state.tick = 50; FullMessageSystem.ProcessNetwork(state);
        Require(secret.decoded && secret.deliveredTick == 50 && unit.messageIds.Count == 0 && state.scoreHalfPoints[0] == 75, "translation delivers decoded message through network");

        state = Fresh(config, definition); unit = state.units[0]; unit.stationActive = true; unit.messageIds.Add(200);
        secret = new RelayMessage { id = 200, side = 0, x = 8, y = 8, kind = "secret", carrierId = 0, deadlineTicks = 900 };
        state.messages.Add(secret);
        for (int tick = 0; tick <= 90; tick++)
        {
            state.tick = tick;
            if (tick == 20) unit.suppressedUntilTick = 40;
            RadioNetwork.Rebuild(state, definition); FullMessageSystem.ProcessNetwork(state);
            if (tick == 89) Require(!secret.decoded, "suppression resets incomplete translation");
        }
        Require(secret.deliveredTick == 90, "translation resumes from zero after suppression");

        state = Fresh(config, definition); unit = state.units[0]; unit.x = 30; unit.y = 20; unit.stationActive = true; unit.messageIds.Add(200);
        secret = new RelayMessage { id = 200, side = 0, kind = "secret", x = 30, y = 20, carrierId = 0, deadlineTicks = 900 };
        state.messages.Add(secret); RadioNetwork.Rebuild(state, definition);
        for (int tick = 0; tick <= 50; tick++) { state.tick = tick; FullMessageSystem.ProcessNetwork(state); }
        Require(secret.decoded && secret.deliveredTick < 0 && secret.stationId == unit.id && unit.messageIds.Count == 0, "disconnected decoding completes into station buffer");
        state.nodes.Find(value => value.id == unit.id).connectedToHome = true;
        state.tick = 51; FullMessageSystem.ProcessNetwork(state);
        Require(secret.deliveredTick == 51, "reconnected station forwards decoded buffer");

        state = Fresh(config, definition); unit = state.units[0]; unit.stationActive = true; unit.suppressedUntilTick = 40;
        secret = new RelayMessage { id = 200, side = 0, kind = "secret", x = unit.x, y = unit.y, stationId = unit.id, deadlineTicks = 900 };
        state.messages.Add(secret); state.tick = 10;
        RadioNetwork.Rebuild(state, definition); FullMessageSystem.ProcessNetwork(state);
        Require(secret.stationId == unit.id && secret.carrierId == -1 && !secret.decoded && secret.deliveredTick < 0,
            "suppressed station retains its buffered secret despite nearby home coverage");
        state.tick = 40; RadioNetwork.Rebuild(state, definition); FullMessageSystem.ProcessNetwork(state);
        state.tick = 89; FullMessageSystem.ProcessNetwork(state);
        Require(!secret.decoded, "retained secret requires fifty ticks after suppression ends");
        state.tick = 90; FullMessageSystem.ProcessNetwork(state);
        Require(secret.decoded && secret.deliveredTick == 90, "retained secret resumes translation at original station");

        state = Fresh(config, definition); unit = state.units[0]; unit.x = 30; unit.y = 20; unit.stationActive = true;
        secret = new RelayMessage { id = 200, side = 0, kind = "secret", decoded = true, x = 30, y = 20, stationId = unit.id, deadlineTicks = 900 };
        state.messages.Add(secret); RadioNetwork.Rebuild(state, definition);
        state.nodes.Add(new RelayRadioNode { id = 30000, side = 0, x = 40, y = 20, radius = 12, active = true, connectedToHome = true });
        FullMessageSystem.ProcessNetwork(state);
        Require(secret.stationId == unit.id && secret.deliveredTick < 0,
            "station buffer cannot bypass disconnected dual-radius link through ground collection");
        foreach (int age in new[] { 900, 901, 1200, 1201 })
        {
            state = Fresh(config, definition); state.tick = age;
            secret = new RelayMessage { id = 200, side = 0, kind = "secret", decoded = true, deadlineTicks = 900 };
            state.messages.Add(secret); FullMessageSystem.Deliver(state, secret);
            Require(state.scoreHalfPoints[0] == (age <= 900 ? 75 : age <= 1200 ? 50 : 25), "secret deadline boundary " + age);
        }
        state = Fresh(config, definition);
        var stock = state.resources.Find(value => value.side == 0 && value.kind == "depot"); stock.amount = 6;
        var carrier = state.units[5]; carrier.x = stock.x; carrier.y = stock.y; carrier.messageIds.Add(123);
        Require(Start(rules, state, carrier, "pickup") != null, "transport messages exclude material pickup");
        carrier.messageIds.Clear(); carrier.materials = 1;
        var plain = new RelayMessage { id = 0, side = 0, x = carrier.x, y = carrier.y }; state.messages.Add(plain);
        Require(FullMessageSystem.ValidateLoad(state, carrier, 0, definition) == "message-capacity", "transport materials exclude message loading");
        state = Fresh(config, definition); state.scoreHalfPoints[1] = 1000;
        foreach (var resource in state.resources) if (resource.kind == "depot") resource.amount = 6;
        state.tick = 250; rules.Refresh(state, config.seed);
        Require(state.compensationCount[0] == 0, "full depots do not consume compensation");

        state = Fresh(config, definition);
        var broadcast = new RelayMessage { id = 300, side = 0, kind = "broadcast", x = 10, y = 10, secondX = 20, secondY = 20, deadlineTicks = 600 };
        state.messages.Add(broadcast); state.nodes.Clear();
        state.nodes.Add(new RelayRadioNode { id = 0, side = 0, x = 10, y = 10, radius = 0, active = true, connectedToHome = true });
        state.nodes.Add(new RelayRadioNode { id = 1, side = 0, x = 20, y = 20, radius = 0, active = true, connectedToHome = true });
        for (int tick = 100; tick <= 151; tick++)
        {
            state.tick = tick;
            if (tick == 120) state.nodes[1].active = false;
            if (tick == 121) state.nodes[1].active = true;
            FullMessageSystem.ProcessNetwork(state);
            if (tick == 150) Require(broadcast.deliveredTick < 0, "broadcast requires continuous coverage after interruption");
        }
        Require(broadcast.deliveredTick == 151 && state.scoreHalfPoints[0] == 40, "broadcast completes once after thirty ticks");

        state = Fresh(config, definition); state.tick = 9000;
        for (int i = 0; i < 3; i++) { state.units[i].x = 47; state.units[i].y = 47; }
        state.hubMaterials[0] = 6;
        state.nodes.Clear(); state.nodes.Add(new RelayRadioNode { id = 0, side = 0, x = 47, y = 47, radius = 0, active = true, connectedToHome = true });
        state.units[3].materials = 3; state.units[5].materials = 6;
        state.resources.Find(item => item.kind == "headquarters" && item.side == 0).amount = 2;
        rules.FinalizeScores(state);
        Require(state.scoreHalfPoints[0] == 260 && state.ledger.Count == 4, "three hub components plus capped remainder");

        state = Fresh(config, definition);
        for (int i = 0; i < 24; i++)
        {
            state.tick = i >= 12 && i < 18 ? 601 : 100;
            var message = new RelayMessage { id = i, side = 0, kind = i < 18 ? "plain" : i < 21 ? "urgent" : i < 23 ? "secret" : "broadcast", decoded = true };
            state.messages.Add(message); FullMessageSystem.Deliver(state, message);
        }
        var engineer = state.units[3];
        int built = 0;
        foreach (var crossing in definition.crossings) if (crossing.kind == CrossingKind.BuiltBridge && built++ < 2)
        {
            engineer.x = crossing.fromX; engineer.y = crossing.fromY; engineer.materials = 3;
            Start(rules, state, engineer, "buildBridge", (crossing.fromX + crossing.toX) / 2, (crossing.fromY + crossing.toY) / 2);
            state.tick += 60; rules.CompleteTasks(state, new List<string>());
        }
        for (int i = 0; i < 2; i++)
        {
            engineer.x = 10 + i; engineer.y = 10; engineer.materials = 2;
            Start(rules, state, engineer, "buildRelay", engineer.x, engineer.y);
            state.tick += 50; rules.CompleteTasks(state, new List<string>());
        }
        state.tick = 9000; state.hubMaterials[0] = 6;
        for (int i = 0; i < 3; i++) { state.units[i].x = 47; state.units[i].y = 47; }
        state.nodes.Clear(); state.nodes.Add(new RelayRadioNode { side = 0, x = 47, y = 47, active = true, connectedToHome = true });
        state.units[5].materials = 6; engineer.materials = 2;
        rules.FinalizeScores(state);
        Require(state.scoreHalfPoints[0] == 1336, "appendix 668 score breakdown");
    }

    private static RelayGameOutcome Outcome(RelayConfig config, RelayMapData map, int side0Team, int red, int blue)
    {
        var state = Fresh(config, map); state.completed = true; state.scoreHalfPoints = new[] { red, blue };
        return new RelayGameOutcome { teamBySide = new[] { side0Team, 1 - side0Team }, result = new RelayResult {
            config = config.Copy(), mapHash = "same-map", finalSnapshot = state } };
    }

    private static void TestSeries(RelayConfig config, RelayMapData map)
    {
        var first = Outcome(config, map, 0, 10, 20); var second = Outcome(config, map, 1, 5, 30);
        var series = RelaySeries.Aggregate(first, second);
        Require(series.winnerTeam == 0 && series.totalScoreHalfPoints[0] == 40 && series.decision == "total-score", "series swaps team mapping");
        first = Outcome(config, map, 0, 30, 20); second = Outcome(config, map, 1, 20, 10);
        series = RelaySeries.Aggregate(first, second);
        Require(series.winnerTeam == 0 && series.decision == "best-game-score", "series single game tie breaker");
        first = Outcome(config, map, 0, 20, 20); second = Outcome(config, map, 1, 20, 20);
        first.result.finalSnapshot.ledger.Add(new RelayScoreEntry { side = 0, reason = "assault-delivery" });
        series = RelaySeries.Aggregate(first, second);
        Require(series.winnerTeam == 0 && series.decision == "urgent-deliveries", "assault included in urgent tie breaker");
        first.result.finalSnapshot.ledger.Clear();
        Require(RelaySeries.Aggregate(first, second).winnerTeam == -1, "complete tie is draw");
        first.forfeit = true; first.faultSide = 0;
        Require(RelaySeries.Aggregate(first, second).winnerTeam == 1, "fault loses entire paired match");
        second.forfeit = true; second.faultSide = 0;
        Require(RelaySeries.Aggregate(first, second).status == "both-forfeit", "both teams failing gives no winner");
        second.result.config.seed++;
        bool rejected = false; try { RelaySeries.Aggregate(first, second); } catch (ArgumentException) { rejected = true; }
        Require(rejected, "different seed rejected by series");
    }
}
