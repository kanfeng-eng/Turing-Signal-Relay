using System;
using System.Collections.Generic;
using System.Text.Json;
using TuringSignalRelay;

internal static class RelayChecks
{
    private static int cases;
    private static void Require(bool value, string name)
    {
        if (!value) throw new Exception("FAIL " + name);
        cases++;
    }
    private static void Reject(Action action, string name)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Require(rejected, name);
    }
    private static RelayCommandBatch Batch(int tick, int side, params RelayCommand[] actions)
    {
        return new RelayCommandBatch { protocolVersion = 2, type = "relay-commands", tick = tick, side = side, actions = actions };
    }
    private static RelayCommand Move(int id, int x, int y) { return new RelayCommand { unitId = id, action = "moveTo", x = x, y = y }; }

    internal static void Run(RelayConfig config, RelayMapData data)
    {
        var map = new RelayMap(data);
        Require(map.Grid.Width == 96 && map.Grid.Height == 96, "map dimensions");
        var changed = map.Export(); changed.rows[0] = "3" + changed.rows[0].Substring(1);
        Reject(() => new RelayMap(changed), "asymmetric terrain");
        changed = map.Export(); changed.sites[1].id = changed.sites[0].id;
        Reject(() => new RelayMap(changed), "duplicate map ids");
        changed = map.Export(); changed.crossings[0].toY++;
        Reject(() => new RelayMap(changed), "invalid crossing");
        changed = map.Export(); Array.Reverse(changed.sites); Array.Reverse(changed.crossings);
        Require(new RelayMap(changed).Hash == map.Hash, "map canonical ordering");
        Require(!RelayMap.FerryOpen(2399) && RelayMap.FerryOpen(2400) && RelayMap.FerryOpen(2549) && !RelayMap.FerryOpen(2550) && RelayMap.FerryOpen(2850), "ferry boundaries");

        var engine = new RelayMatchEngine(config, data);
        string before = engine.StateHash();
        var bad = Batch(0, 1); bad.protocolVersion = 99;
        Reject(() => engine.Step(Batch(0, 0, Move(0, 9, 8)), bad), "envelope rejection");
        Require(engine.Tick == 0 && before == engine.StateHash(), "bad envelope is atomic");
        var snapshot = engine.GetSnapshot(); snapshot.units[0].x = 90;
        Require(engine.GetSnapshot().units[0].x == 8, "snapshot isolation");
        var observation = engine.GetObservation(0);
        Require(observation.units.Length == 7 && observation.visibleEnemies.Length == 0, "filtered initial frame");
        observation.units[0].messageIds.Add(99);
        Require(engine.GetSnapshot().units[0].messageIds.Count == 0, "observation isolation");
        engine.Step(Batch(0, 0, new RelayCommand { unitId = 0, action = "invalid" }, Move(0, 9, 8), Move(100, 86, 87)), null);
        snapshot = engine.GetSnapshot();
        Require(!snapshot.commandResults[0].accepted && snapshot.commandResults[1].reason == "duplicate-unit-command" && snapshot.commandResults[2].reason == "unit-not-owned", "first command and ownership");
        Require(snapshot.units[0].task == "idle", "invalid first action does not fall through");
        engine.Step(Batch(1, 0, Move(0, 9, 8)), null);
        snapshot = engine.GetSnapshot();
        Require(snapshot.units[0].x == 8 && snapshot.units[0].edgeCompleteTick == 11, "move starts next tick with full duration");
        engine.Step(Batch(2, 0, Move(0, 9, 8)), null);
        Require(engine.GetSnapshot().units[0].edgeCompleteTick == 11, "repeated move preserves progress");
        engine.Step(Batch(3, 0, Move(0, -1, 8)), null);
        Require(engine.GetSnapshot().units[0].task == "move", "invalid move preserves task");
        while (engine.Tick < 11) engine.Step(null, null);
        Require(engine.GetSnapshot().units[0].x == 9 && engine.GetSnapshot().units[0].task == "idle", "exact road arrival");
        var tooMany = new RelayCommand[129];
        for (int i = 0; i < tooMany.Length; i++) tooMany[i] = Move(0, 10, 8);
        before = engine.StateHash();
        Reject(() => engine.Step(Batch(engine.Tick, 0, tooMany), null), "action count bound");
        Require(before == engine.StateHash(), "oversized batch leaves state unchanged");
        var invalidConfig = config.Copy(); invalidConfig.rulesHash = new string('0', 64);
        Reject(() => new RelayMatchEngine(invalidConfig, data), "approved digest is pinned");

        var state = new RelayState();
        state.units.Add(new RelayUnit { id = 0, side = 0, x = 8, y = 8 });
        state.units.Add(new RelayUnit { id = 100, side = 1, x = 10, y = 8 });
        MovementSystem.MoveTo(map, state.units[0], 9, 8); MovementSystem.MoveTo(map, state.units[1], 9, 8);
        state.tick = 1; MovementSystem.Step(map, state);
        Require(state.units[1].edgeCompleteTick == 10 && state.units[0].edgeCompleteTick == 0, "odd tick gives blue conflict priority");
        state = new RelayState();
        state.units.Add(new RelayUnit { id = 0, side = 0, x = 9, y = 8 });
        state.units.Add(new RelayUnit { id = 100, side = 1, x = 10, y = 8 });
        MovementSystem.MoveTo(map, state.units[0], 10, 8); MovementSystem.MoveTo(map, state.units[1], 9, 8);
        state.tick = 2; MovementSystem.Step(map, state);
        Require(state.units.TrueForAll(unit => unit.edgeCompleteTick == 0), "head on swaps blocked");
        for (int tick = 3; tick <= 12; tick++) { state.tick = tick; MovementSystem.Step(map, state); }
        Require(state.units.TrueForAll(unit => unit.blockedTicks < 10), "blocked paths replan without unbounded retries");
        state = new RelayState();
        var rerouting = new RelayUnit { id = 0, side = 0, x = 8, y = 8 };
        state.units.Add(rerouting); state.units.Add(new RelayUnit { id = 100, side = 1, x = 9, y = 8 });
        MovementSystem.MoveTo(map, rerouting, 10, 8);
        for (int tick = 1; tick <= 11; tick++) { state.tick = tick; MovementSystem.Step(map, state); }
        Require(rerouting.edgeCompleteTick > 11 && (rerouting.edgeX != 9 || rerouting.edgeY != 8), "ten blocked ticks cause a usable detour");
        state = new RelayState();
        state.units.Add(new RelayUnit { id = 0, side = 0, x = 7, y = 8 });
        state.units.Add(new RelayUnit { id = 1, side = 0, x = 9, y = 8 });
        foreach (var unit in state.units) MovementSystem.MoveTo(map, unit, 8, 8);
        state.tick = 2; MovementSystem.Step(map, state);
        Require(state.units.TrueForAll(unit => unit.edgeCompleteTick == 11), "headquarters allows shared arrival reservations");

        var rope = Array.Find(data.crossings, value => value.kind == CrossingKind.RopeBridge && value.fromX == 20);
        state = new RelayState();
        var first = new RelayUnit { id = 0, side = 0, x = rope.fromX, y = rope.fromY };
        var older = new RelayUnit { id = 1, side = 0, x = rope.fromX, y = rope.fromY };
        state.units.Add(first); state.units.Add(older);
        MovementSystem.MoveTo(map, first, rope.toX, rope.toY + 1);
        MovementSystem.MoveTo(map, older, rope.toX, rope.toY + 1);
        older.waitingSinceTick = 0;
        state.tick = 1; MovementSystem.Step(map, state);
        Require(older.crossingId == rope.id && first.edgeCompleteTick == 0, "crossing FIFO beats unit id priority");
        for (int tick = 2; tick <= 30; tick++)
        {
            state.tick = tick; MovementSystem.Step(map, state);
            if (state.units.FindAll(unit => unit.crossingId == rope.id && unit.edgeCompleteTick > 0).Count > 1)
                throw new Exception("Overlapping crossing occupants");
        }
        Require(first.crossingId == rope.id && first.edgeCompleteTick == 50, "queue advances after crossing exit clears");

        var ferry = Array.Find(data.crossings, value => value.kind == CrossingKind.Ferry);
        state = new RelayState();
        var transport = new RelayUnit { id = 5, side = 0, role = UnitRole.Transporter, x = ferry.fromX, y = ferry.fromY };
        state.units.Add(transport); MovementSystem.MoveTo(map, transport, ferry.toX, ferry.toY);
        state.tick = 2399; MovementSystem.Step(map, state);
        Require(transport.edgeCompleteTick == 0 && transport.waitingSinceTick == 2399, "closed ferry waits");
        state.tick = 2549; MovementSystem.Step(map, state);
        Require(transport.edgeCompleteTick == 2609, "transport ferry duration doubled");
        MovementSystem.Wait(transport, 2550, 1);
        state.tick = 2550; MovementSystem.Step(map, state);
        Require(transport.x == ferry.fromX && transport.y == ferry.fromY, "cancellation cannot teleport out of transit");
        state.tick = 2609; MovementSystem.Step(map, state);
        Require(transport.x == ferry.toX && transport.y == ferry.toY, "entered ferry completes after closing");

        state = new RelayState { tick = 200 };
        PlainMessageSystem.Spawn(state, map.Reports(0), 17);
        Require(state.messages.Count == 2 && state.messages[0].x == 95 - state.messages[1].x && state.messages[0].y == 95 - state.messages[1].y, "paired seeded reports");
        var message = state.messages[0];
        var courier = new RelayUnit { id = 0, side = 0, x = message.x, y = message.y };
        var receiver = new RelayUnit { id = 5, side = 0, role = UnitRole.Transporter, x = message.x, y = message.y };
        state.units.Add(courier); state.units.Add(receiver);
        Require(PlainMessageSystem.Load(state, courier, message.id) == null && message.carrierId == 0, "plain pickup");
        Require(PlainMessageSystem.Load(state, receiver, message.id) != null, "single owner pickup");
        Require(PlainMessageSystem.Transfer(state, courier, receiver, message.id) == null && courier.messageIds.Count == 0, "same side transfer");
        state.tick = 800; PlainMessageSystem.Deliver(state, receiver);
        Require(state.scoreHalfPoints[0] == 30 && state.ledger.Count == 1 && state.ledger[0].ageTicks == 600, "deadline delivery");
        PlainMessageSystem.Deliver(state, receiver);
        Require(state.scoreHalfPoints[0] == 30 && state.ledger.Count == 1, "duplicate delivery cannot score");
        Require(PlainMessageSystem.PlainHalfPoints(600) == 30 && PlainMessageSystem.PlainHalfPoints(601) == 20 &&
            PlainMessageSystem.PlainHalfPoints(900) == 20 && PlainMessageSystem.PlainHalfPoints(901) == 10, "score band endpoints");
        Reject(() => PlainMessageSystem.PlainHalfPoints(-1), "negative report age");
        state = new RelayState { tick = 300 };
        for (int i = 0; i < 4; i++) state.messages.Add(new RelayMessage { id = i, side = 0, x = 16, y = 12, createdTick = 200 });
        var engineer = new RelayUnit { id = 3, side = 0, role = UnitRole.Engineer, x = 16, y = 12 };
        transport = new RelayUnit { id = 5, side = 0, role = UnitRole.Transporter, x = 16, y = 12 };
        Require(PlainMessageSystem.ValidateLoad(state, engineer, 0) == "message-capacity", "engineer cannot carry reports");
        Require(PlainMessageSystem.Load(state, transport, 1) == null && transport.messageIds.Count == 3 && state.messages[3].carrierId == -1, "transporter bulk loads three only");
        Require(PlainMessageSystem.Load(state, transport, 3) == "message-capacity", "full transporter rejects additional report");

        engine = new RelayMatchEngine(config, data);
        var spawned = new RelayState { tick = 200 };
        PlainMessageSystem.Spawn(spawned, map.Reports(0), config.seed);
        var destination = spawned.messages[0];
        engine.Step(Batch(0, 0, Move(0, destination.x, destination.y)), null);
        while (engine.Tick < 200 || engine.GetSnapshot().units[0].task != "idle") engine.Step(null, null);
        engine.Step(Batch(engine.Tick, 0, new RelayCommand { unitId = 0, action = "loadMsg", messageId = 0 }), null);
        Require(engine.GetSnapshot().messages[0].carrierId == -1, "pickup consumes a full tick");
        engine.Step(null, null);
        Require(engine.GetSnapshot().messages[0].carrierId == 0, "pickup completes on its boundary");
        engine.Step(Batch(engine.Tick, 0, new RelayCommand { unitId = 0, action = "deliverMsg" }), null);
        Require(engine.GetSnapshot().commandResults[0].reason == "not-at-headquarters", "remote delivery rejected");
        engine.Step(Batch(engine.Tick, 0, Move(0, 8, 8)), null);
        while (engine.GetSnapshot().units[0].task != "idle") engine.Step(null, null);
        engine.Step(Batch(engine.Tick, 0, new RelayCommand { unitId = 0, action = "deliverMsg" }), null);
        Require(engine.GetSnapshot().ledger.Count == 0, "delivery consumes a full tick");
        engine.Step(Batch(engine.Tick, 0, new RelayCommand { unitId = 0, action = "deliverMsg" }), null);
        Require(engine.GetSnapshot().ledger.Count == 1 && engine.GetSnapshot().scoreHalfPoints[0] == PlainMessageSystem.PlainHalfPoints(engine.Tick - 200), "repeated delivery does not restart pending action");
        engine.Step(Batch(engine.Tick, 0, new RelayCommand { unitId = 0, action = "deliverMsg" }), null);
        Require(engine.GetSnapshot().ledger.Count == 1 && engine.GetSnapshot().commandResults[0].reason == "no-carried-message", "completed delivery cannot repeat");
        var shortConfig = config.Copy(); shortConfig.durationTicks = 3;
        var replay = RelayScenario.Run(shortConfig, data);
        var verified = RelayReplay.Verify(replay);
        Require(verified.Completed && verified.Tick == 3, "replay round trip");
        bool stopped = false;
        try { verified.Step(null, null); } catch (InvalidOperationException) { stopped = true; }
        Require(stopped, "completed scenario cannot advance");
        string finalHash = replay.frames[2].stateHash;
        replay.frames[2].stateHash = new string('0', 64);
        Reject(() => RelayReplay.Verify(replay), "tampered replay");
        replay.frames[2].stateHash = finalHash;
        replay.frames[1].tick = 3;
        Reject(() => RelayReplay.Verify(replay), "skipped replay frame");
        Console.WriteLine("RELAY_LOGIC_CHECKS_PASSED cases=" + cases);
    }
}
