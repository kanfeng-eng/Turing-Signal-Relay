#nullable enable
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace TuringSignalRelay.Evaluation;

public sealed class EvaluationProcessor(ContestDb db, ContestPaths paths, SandboxClient sandbox)
{
    public async Task RecoverAsync(CancellationToken cancellation)
    {
        foreach (var submission in await db.Submissions.Where(x => x.Status == "compiling").ToListAsync(cancellation))
        {
            await sandbox.InvokeAsync("cancel-compile", submission.Id, null, null, cancellation);
            submission.Status = submission.Attempts < 2 ? "queued" : "failed";
            submission.Diagnostic = "编译服务重启，已恢复记录。";
        }
        foreach (var match in await db.Matches.Where(x => x.Status == "running").ToListAsync(cancellation))
        {
            match.Status = match.Attempts < 2 ? "queued" : "failed";
            match.Diagnostic = "评测服务中断，按基础设施重试上限恢复。";
        }
        await db.SaveChangesAsync(cancellation);
    }

    public async Task<bool> ProcessOneAsync(CancellationToken cancellation)
    {
        var submission = await db.Submissions.OrderBy(x => x.CreatedUtc).FirstOrDefaultAsync(x => x.Status == "queued", cancellation);
        if (submission != null)
        {
            submission.Status = "compiling"; submission.Attempts++;
            await db.SaveChangesAsync(cancellation);
            try
            {
                var result = await sandbox.CompileAsync(submission, cancellation);
                if (result.SourceHash != submission.SourceHash) throw new InvalidDataException("Sandbox source digest mismatch.");
                submission.Status = result.Success ? "ready" : "failed";
                submission.BinaryHash = result.BinaryHash; submission.Compiler = result.Compiler;
                submission.Diagnostic = result.TimedOut ? "编译超过30秒限制。\n" + result.Diagnostic : result.Diagnostic;
                ContestJson.Write(paths.File("submissions", submission.Id, "compilation.json"), result);
            }
            catch (Exception error) when (error is not OperationCanceledException || !cancellation.IsCancellationRequested)
            {
                submission.Status = submission.Attempts < 2 ? "queued" : "failed";
                submission.Diagnostic = "编译基础设施故障 " + error.Message;
            }
            await db.SaveChangesAsync(cancellation);
            return true;
        }
        var match = await db.Matches.OrderBy(x => x.CreatedUtc).FirstOrDefaultAsync(x => x.Status == "queued", cancellation);
        if (match == null) return false;
        match.Status = "running"; match.Attempts++;
        match.ResultDirectory = Path.Combine("matches", match.Id, "attempt-" + match.Attempts);
        await db.SaveChangesAsync(cancellation);
        try
        {
            await RunMatchAsync(match, cancellation);
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellation.IsCancellationRequested)
        {
            match.Status = match.Attempts < 2 ? "queued" : "failed";
            match.Diagnostic = "评测基础设施故障 " + error.Message;
        }
        await db.SaveChangesAsync(cancellation);
        return true;
    }

    private async Task RunMatchAsync(ContestMatch match, CancellationToken cancellation)
    {
        string output = paths.File(match.ResultDirectory);
        Directory.CreateDirectory(output);
        var first = await db.Submissions.SingleAsync(x => x.Id == match.TeamASubmissionId, cancellation);
        var second = await db.Submissions.SingleAsync(x => x.Id == match.TeamBSubmissionId, cancellation);
        if (first.Status != "ready" || second.Status != "ready") throw new InvalidDataException("A match requires compiled submissions.");
        var config = ContestJson.Read<RelayConfig>(Path.Combine(paths.Repository, "fixtures/protocol/relay-full-config.json"));
        var map = ContestJson.Read<RelayMapData>(Path.Combine(paths.Repository, "fixtures/world/map-v1.json"));
        config.seed = match.Seed;
        var outcomes = new RelayGameOutcome[2];
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(6));
        for (int number = 0; number < 2; number++)
        {
            var ordered = number == 0 ? new[] { first, second } : new[] { second, first };
            string directory = Path.Combine(output, "game-" + (number + 1));
            Directory.CreateDirectory(directory);
            var runs = new List<SandboxRun>();
            try
            {
                foreach (var item in ordered) runs.Add(await sandbox.PrepareAsync(item.Id, directory, deadline.Token));
                var builder = new ObserverSnapshotBuilder(config, map);
                var game = await RelayAiSession.RunAsync(config, map, runs.Select(x => x.Launcher).ToArray(), new ServerAiJson(), true,
                    deadline.Token, bridgeFactory: side => runs[side].Bridge(), capture: engine => {
                        ContestJson.Write(Path.Combine(directory, "snapshot.json"), new ObserverSnapshotResponse { protocolVersion = 1,
                            tick = engine.Tick, status = engine.Completed ? "completed" : "running", snapshot = builder.Create(engine.GetSnapshot()) });
                    });
                ContestJson.Write(Path.Combine(directory, "snapshot.json"), new ObserverSnapshotResponse { protocolVersion = 1,
                    tick = game.result.finalSnapshot.tick, status = game.recording.forfeit ? "forfeit" : "completed", snapshot = builder.Create(game.result.finalSnapshot) });
                game.recording.aiSourceHashes = ordered.Select(x => x.SourceHash).ToArray();
                game.recording.aiBinaryHashes = ordered.Select(x => x.BinaryHash).ToArray();
                var audits = await Task.WhenAll(runs.Select(run => sandbox.InvokeAsync("inspect", run.Token, null, null, CancellationToken.None)));
                if (audits.Any(audit => audit.GetProperty("infrastructureFailure").GetBoolean()))
                    throw new IOException("The trusted sandbox supervisor reported an infrastructure failure.");
                ContestJson.Write(Path.Combine(directory, "sandbox.audit.json"), audits);
                var compilationA = ContestJson.Read<CompilationResult>(paths.File("submissions", first.Id, "compilation.json"));
                var compilationB = ContestJson.Read<CompilationResult>(paths.File("submissions", second.Id, "compilation.json"));
                if (compilationA.SdkHash != compilationB.SdkHash) throw new InvalidDataException("Submissions use different SDK versions.");
                game.recording.sdkSourceHash = compilationA.SdkHash;
                var verified = RelayReplay.Verify(game.recording);
                if (verified.StateHash() != game.result.stateHash) throw new InvalidDataException("Evaluation replay digest mismatch.");
                ContestJson.Write(Path.Combine(directory, "relay.replay.json"), game.recording);
                ContestJson.Write(Path.Combine(directory, "relay.result.json"), game.result);
                ContestJson.Write(Path.Combine(directory, "ai.report.json"), new { game.cpuBudgetEnforced, game.timeouts,
                    game.failures, game.observedPeakBytes, resources = runs.Select(x => new {
                        cpu = File.ReadAllText(Path.Combine(x.ResourceDirectory, "cpu.stat")),
                        memory = File.ReadAllText(Path.Combine(x.ResourceDirectory, "memory.events")),
                        pids = File.ReadAllText(Path.Combine(x.ResourceDirectory, "pids.events")) }).ToArray() });
                for (int side = 0; side < 2; side++) await File.WriteAllTextAsync(Path.Combine(directory, "ai-" + side + ".stderr.txt"), game.diagnostics[side] ?? "", deadline.Token);
                outcomes[number] = new RelayGameOutcome { result = game.result, forfeit = game.recording.forfeit, faultSide = game.recording.faultSide,
                    teamBySide = number == 0 ? new[] { 0, 1 } : new[] { 1, 0 } };
            }
            finally
            {
                await Task.WhenAll(runs.Select(run => run.ReleaseAsync()));
            }
        }
        var series = RelaySeries.Aggregate(outcomes[0], outcomes[1]);
        ContestJson.Write(Path.Combine(output, "series.result.json"), series);
        var artifacts = new List<MatchArtifact>();
        foreach (var artifact in new[] { ("result", "series.result.json"), ("replay-1", "game-1/relay.replay.json"), ("replay-2", "game-2/relay.replay.json") })
        {
            string file = Path.Combine(output, artifact.Item2);
            artifacts.Add(new MatchArtifact { MatchId = match.Id, Name = artifact.Item1,
                RelativePath = Path.Combine(match.ResultDirectory, artifact.Item2), Sha256 = ContestJson.Hash(await File.ReadAllBytesAsync(file, cancellation)), Bytes = new FileInfo(file).Length });
        }
        db.Artifacts.AddRange(artifacts);
        match.Status = "completed";
        match.Diagnostic = series.status == "completed" ? "双局评测完成。" : "双局评测完成，存在选手故障。";
    }
}
