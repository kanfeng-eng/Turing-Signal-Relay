using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text;
using System.Text.Json;
using TuringSignalRelay;
using TuringSignalRelay.Evaluation;

internal static class RequirementChecks
{
    internal static async Task RunAsync(string output)
    {
        void Check(bool value, string name) { if (!value) throw new Exception("REQUIREMENTS_FAIL " + name); }
        var defaults = new RelayRuleParameters();
        Check(EvaluationProcessor.MatchTimeLimit(9000) > TimeSpan.FromSeconds(904), "full frame budget and initialization");
        Check(FullRuleSystem.NextEventTick(0, 9000, defaults, true) == 200, "initial countdown");
        Check(FullRuleSystem.NextEventTick(200, 9000, defaults, true) == 250, "countdown after event");
        Check(FullRuleSystem.NextEventTick(2399, 9000, defaults, true) == 2400, "simultaneous events");
        Check(FullRuleSystem.NextEventTick(9000, 9000, defaults, true) == -1, "terminal countdown");
        var config = new RelayConfig { featureSet = "relay-full-v1", rulesHash = RelayConfig.ApprovedRulesHash,
            supplementalRulesHash = "dd49f601362b03585fb677833f72382ee8ce69bcccf71ca0c89472bab583b2ed", parameters = defaults };
        config.Validate();
        var copied = config.Copy();
        copied.parameters.plainFirstTick = 100;
        Check(config.parameters.plainFirstTick == 200, "isolated configuration copy");
        bool rejected = false;
        try { copied.Validate(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "modified parameters require digest");
        copied.parametersHash = RelayStateHash.Parameters(copied.parameters);
        copied.Validate();
        Check(FullRuleSystem.NextEventTick(0, 9000, copied.parameters, true) == 100, "configured countdown");
        Check(GridPathfinder.EnterTicks(TerrainKind.Road, UnitRole.Communicator, defaults) == 9, "default movement");
        copied.parameters.communicatorSpeed = 200;
        Check(GridPathfinder.EnterTicks(TerrainKind.Road, UnitRole.Communicator, copied.parameters) == 5, "configured movement");
        Check(SourceScanner.Scan(Encoding.UTF8.GetBytes("int main(){}" )).Findings.Length == 0, "clean source");
        SourceScanner.Scan(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("moveTo(", 30000))));
        var scan = SourceScanner.Scan(Encoding.UTF8.GetBytes("fopen(\"x\",\"r\");\nsocket(1,2,3);\nstd::thread t;"));
        Check(scan.Findings.Any(x => x.Category == "file" && x.Line == 1) && scan.Findings.Any(x => x.Category == "network") &&
            scan.Findings.Any(x => x.Category == "thread"), "scan categories and line numbers");

        string originalData = Environment.GetEnvironmentVariable("TSR_DATA");
        string data = Path.Combine(output, "requirements-data");
        Environment.SetEnvironmentVariable("TSR_DATA", data);
        try
        {
            var paths = new ContestPaths(); paths.Initialize();
            var options = new DbContextOptionsBuilder<ContestDb>().UseSqlite(paths.ConnectionString).Options;
            await using var db = new ContestDb(options);
            await db.GetService<IMigrator>().MigrateAsync(db.Database.GetMigrations().First());
            await db.Database.ExecuteSqlRawAsync("DROP TABLE __EFMigrationsHistory;");
            await ContestDatabase.UpgradeAsync(db, paths);
            await ContestDatabase.UpgradeAsync(db, paths);
            Check(await db.SubmissionReviews.CountAsync() == 0, "initial schema adoption and upgrade");
            db.Teams.AddRange(new Team { Id = "alpha", Name = "alpha" }, new Team { Id = "beta", Name = "beta" });
            await db.SaveChangesAsync();
            var operations = new ContestOperations(db, paths);
            using var firstSource = new MemoryStream(Encoding.UTF8.GetBytes("int main(){}"));
            using var secondSource = new MemoryStream(Encoding.UTF8.GetBytes("int main(){return 0;}"));
            var first = await operations.SubmitAsync("alpha", "", firstSource, default);
            var second = await operations.SubmitAsync("beta", "", secondSource, default);
            first.Status = second.Status = "ready";
            await db.SaveChangesAsync();
            Check(File.Exists(paths.File("submissions", first.Id, "source-scan.json")), "scan stored on submission");
            var match = await operations.CreateMatchAsync(first.Id, second.Id, 17, Guid.NewGuid().ToString("N"), default);
            var reviews = new SubmissionReviewService(db, paths);
            await reviews.ReviewAsync(first.Id, "test-judge", "approved", "source reviewed", default);
            Check(!await db.Teams.Where(x => x.Id == "alpha").Select(x => x.Disqualified).SingleAsync(), "approval preserves eligibility");
            await reviews.ReviewAsync(first.Id, "test-judge", "disqualified", "confirmed violation", default);
            db.ChangeTracker.Clear();
            Check(await db.Matches.Where(x => x.Id == match.Id).Select(x => x.Status).SingleAsync() == "disqualified", "match results voided");
            Check(await db.SubmissionReviews.CountAsync() == 2, "append-only review audit");
            rejected = false;
            try { await operations.CreateMatchAsync(first.Id, second.Id, 17, Guid.NewGuid().ToString("N"), default); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "disqualified team cannot enter match");
            rejected = false;
            try { using var source = new MemoryStream(Encoding.UTF8.GetBytes("int main(){}")); await operations.SubmitAsync("alpha", "", source, default); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "disqualified team cannot submit");
        }
        finally { Environment.SetEnvironmentVariable("TSR_DATA", originalData); }
        await File.WriteAllTextAsync(Path.Combine(output, "requirements.json"), JsonSerializer.Serialize(new { status = "passed",
            deadlineSeconds = EvaluationProcessor.MatchTimeLimit(9000).TotalSeconds, scan = "passed", reviewAndMigration = "passed", countdown = "passed", parameters = "passed" }));
        Console.WriteLine("REQUIREMENTS_CHECKS_PASSED");
    }
}
