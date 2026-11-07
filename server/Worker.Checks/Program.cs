using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using TuringSignalRelay;
using TuringSignalRelay.Worker;

if (args.Contains("--child"))
{
    await File.WriteAllTextAsync("child.pid", Environment.ProcessId.ToString());
    await Task.Delay(60000);
    return 0;
}
if (args.Contains("-batchmode")) return await Fixture(args);
if (args.Length != 1 || Directory.Exists(args[0]) || File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: WorkerChecks <new output directory>");
    return 2;
}
string output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
string fixture = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, OperatingSystem.IsWindows() ? "WorkerChecks.exe" : "WorkerChecks");
var runner = new BootstrapMatchRunner();
var passed = new List<string>();
try
{
    foreach (string scenario in new[] { "valid", "legacy", "empty", "exit7", "malformed-result", "bad-type", "bad-digest", "bad-final-hash", "header-config", "replay-tamper", "replay-duplicate", "replay-truncated", "replay-final-mismatch", "replay-version", "config-invalid", "wrong-result-number" })
    {
        Environment.SetEnvironmentVariable("TSR_WORKER_FIXTURE", scenario);
        var result = await runner.RunAsync(fixture, Path.Combine(output, scenario), TimeSpan.FromSeconds(30), CancellationToken.None);
        bool expected = scenario is "valid" or "legacy";
        Require(result.Succeeded == expected, scenario + " was classified incorrectly.");
        Require(expected || !string.IsNullOrWhiteSpace(result.Error), scenario + " has no diagnostic.");
        Record(scenario);
    }
    string invalid = Path.Combine(output, OperatingSystem.IsWindows() ? "invalid.exe" : "invalid-executable");
    await File.WriteAllTextAsync(invalid, "invalid executable");
    var launch = await runner.RunAsync(invalid, Path.Combine(output, "start-error"), TimeSpan.FromSeconds(2), CancellationToken.None);
    Require(!launch.Succeeded && launch.ExitCode == -1 && launch.Error != null, "Start failure was not reported.");
    Record("start-error");
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel();
        string directory = Path.Combine(output, "pre-cancel");
        await ExpectCancelled(runner.RunAsync(fixture, directory, TimeSpan.FromSeconds(30), cancelled.Token));
        Require(!Directory.Exists(directory), "Pre-cancel created artifacts.");
        Record("pre-cancel");
    }
    Environment.SetEnvironmentVariable("TSR_WORKER_FIXTURE", "slow");
    string timeoutDirectory = Path.Combine(output, "timeout");
    var timeout = await runner.RunAsync(fixture, timeoutDirectory, TimeSpan.FromSeconds(2), CancellationToken.None);
    Require(timeout.TimedOut && !timeout.Succeeded, "Timeout was not reported.");
    CheckStopped(timeoutDirectory);
    Record("timeout-tree");
    using (var cancelled = new CancellationTokenSource())
    {
        string directory = Path.Combine(output, "running-cancel");
        Task<BootstrapRun> run = runner.RunAsync(fixture, directory, TimeSpan.FromSeconds(30), cancelled.Token);
        await WaitUntil(() => File.Exists(Path.Combine(directory, "child.pid")), "Fixture child did not start.");
        cancelled.Cancel();
        await ExpectCancelled(run);
        CheckStopped(directory);
        Record("running-cancel-tree");
    }
    Environment.SetEnvironmentVariable("TSR_WORKER_FIXTURE", "large");
    using (var cancelled = new CancellationTokenSource())
    {
        string directory = Path.Combine(output, "after-exit-cancel");
        Task<BootstrapRun> run = runner.RunAsync(fixture, directory, TimeSpan.FromSeconds(60), cancelled.Token);
        await WaitUntil(() => File.Exists(Path.Combine(directory, "ready")), "Large fixture was not produced.");
        int pid = int.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "parent.pid")));
        await WaitUntil(() => !Alive(pid), "Fixture process did not exit.");
        Require(!run.IsCompleted, "Validation finished before cancellation could be exercised.");
        cancelled.Cancel();
        await ExpectCancelled(run);
        Record("after-exit-cancel");
    }
    Environment.SetEnvironmentVariable("TSR_WORKER_FIXTURE", "valid");
    string shared = Path.Combine(output, "concurrent");
    using (var gate = new ManualResetEventSlim())
    {
        var runs = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            gate.Wait();
            try { return await runner.RunAsync(fixture, shared, TimeSpan.FromSeconds(30), CancellationToken.None); }
            catch (IOException) { return null; }
        })).ToArray();
        gate.Set();
        var results = await Task.WhenAll(runs);
        Require(results.Count(result => result?.Succeeded == true) == 1 && results.Count(result => result == null) == 1, "Concurrent jobs did not have one owner.");
    }
    Record("concurrent-owner");
    byte[] before = SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(shared, "bootstrap.jsonl")));
    bool rejected = false;
    try { await runner.RunAsync(fixture, shared, TimeSpan.FromSeconds(30), CancellationToken.None); }
    catch (IOException) { rejected = true; }
    byte[] after = SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(shared, "bootstrap.jsonl")));
    Require(rejected && before.SequenceEqual(after), "Existing artifacts were overwritten.");
    Record("existing-output-preserved");
    bool invalidTimeout = false;
    string invalidDirectory = Path.Combine(output, "invalid-timeout");
    try { await runner.RunAsync(fixture, invalidDirectory, TimeSpan.MaxValue, CancellationToken.None); }
    catch (ArgumentOutOfRangeException) { invalidTimeout = true; }
    Require(invalidTimeout && !Directory.Exists(invalidDirectory), "Invalid timeout created partial output.");
    Record("invalid-timeout");
    await File.WriteAllTextAsync(Path.Combine(output, "CHECKS.json"), JsonSerializer.Serialize(new { status = "passed", cases = passed }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("WORKER_CHECKS_PASSED cases=" + passed.Count);
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 1;
}
finally { Environment.SetEnvironmentVariable("TSR_WORKER_FIXTURE", null); }

void Record(string name) { passed.Add(name); Console.WriteLine("PASS: " + name); }
static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
static async Task ExpectCancelled(Task<BootstrapRun> run)
{
    try { await run; }
    catch (OperationCanceledException) { return; }
    throw new Exception("Cancellation was swallowed.");
}
static async Task WaitUntil(Func<bool> condition, string message)
{
    var watch = Stopwatch.StartNew();
    while (!condition())
    {
        if (watch.Elapsed > TimeSpan.FromSeconds(30)) throw new Exception(message);
        await Task.Delay(10);
    }
}
static bool Alive(int pid)
{
    try
    {
        using var process = Process.GetProcessById(pid);
        if (process.HasExited) return false;
        if (OperatingSystem.IsLinux())
        {
            string stat = File.ReadAllText("/proc/" + pid + "/stat");
            if (stat[(stat.LastIndexOf(')') + 2)..].StartsWith("Z")) return false;
        }
        return true;
    }
    catch (ArgumentException) { return false; }
    catch (DirectoryNotFoundException) { return false; }
    catch (FileNotFoundException) { return false; }
}
static void CheckStopped(string directory)
{
    foreach (string file in Directory.GetFiles(directory, "*.pid"))
        Require(!Alive(int.Parse(File.ReadAllText(file))), "Fixture process survived cleanup.");
}
static async Task<int> Fixture(string[] arguments)
{
    string scenario = Environment.GetEnvironmentVariable("TSR_WORKER_FIXTURE") ?? "valid";
    await File.WriteAllTextAsync("parent.pid", Environment.ProcessId.ToString());
    if (scenario == "exit7") return 7;
    if (scenario == "empty") return 0;
    if (scenario == "slow")
    {
        string executable = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, OperatingSystem.IsWindows() ? "WorkerChecks.exe" : "WorkerChecks");
        using var child = Process.Start(new ProcessStartInfo(executable, "--child") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        await Task.Delay(60000);
        return 0;
    }
    string replay = arguments[Array.IndexOf(arguments, "--bootstrap-replay") + 1];
    string result = arguments[Array.IndexOf(arguments, "--bootstrap-result") + 1];
    if (scenario == "malformed-result") { await File.WriteAllTextAsync(result, "{broken"); return 0; }
    var options = new JsonSerializerOptions { IncludeFields = true };
    var config = new MatchConfig { durationTicks = scenario == "large" ? 200000 : 3, seed = 7 };
    var replayConfig = new MatchConfig { durationTicks = config.durationTicks, seed = scenario == "header-config" ? 8 : 7 };
    int version = scenario == "legacy" ? 1 : scenario == "replay-version" ? 99 : 2;
    using (var writer = new StreamWriter(replay))
    {
        writer.WriteLine(JsonSerializer.Serialize(new { type = "bootstrap-replay", replayVersion = version, config = replayConfig }, options));
        for (int tick = 0; tick <= config.durationTicks; tick++)
        {
            if (scenario == "replay-truncated" && tick == config.durationTicks) break;
            int frameTick = scenario == "replay-duplicate" && tick == 2 ? 1 : tick;
            var snapshot = new Snapshot { tick = frameTick, completed = frameTick == config.durationTicks, rulesVersion = config.rulesVersion };
            if (scenario == "replay-final-mismatch" && tick == config.durationTicks) snapshot.scoreHalfPoints[0] = 10;
            string hash = BootstrapStateHash.Compute(replayConfig, snapshot);
            if (scenario == "replay-tamper" && tick == 1) snapshot.scoreHalfPoints[0]++;
            writer.WriteLine(version == 1 ? JsonSerializer.Serialize(snapshot, options) : JsonSerializer.Serialize(new { snapshot, stateHash = hash }, options));
        }
    }
    var final = new Snapshot { tick = config.durationTicks, completed = true, rulesVersion = config.rulesVersion };
    var document = JsonSerializer.SerializeToNode(new
    {
        type = "bootstrap-result", resultVersion = 1, status = "bootstrap-completed", config,
        finalSnapshot = final, stateHash = BootstrapStateHash.Compute(config, final),
        replaySha256 = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(replay)))
    }, options)!;
    if (scenario == "bad-type") document["type"] = "unsupported";
    if (scenario == "bad-digest") document["replaySha256"] = "bad";
    if (scenario == "bad-final-hash") document["stateHash"] = "bad";
    if (scenario == "config-invalid") document["config"]!["durationTicks"] = 0;
    if (scenario == "wrong-result-number") document["resultVersion"] = long.MaxValue;
    await File.WriteAllTextAsync(result, document.ToJsonString());
    if (scenario == "large") await File.WriteAllTextAsync("ready", "ready");
    return 0;
}
