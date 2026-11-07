using System.Text.Json;
using TuringSignalRelay.Worker;

if (args.Length == 1 && args[0] == "serve")
{
    await EvaluationWorker.RunAsync();
    return 0;
}

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: Worker <trusted judge executable> <new output directory> <timeout seconds>");
    return 2;
}
try
{
    var output = Path.GetFullPath(args[1]);
    if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Use a new output directory.");
    int timeout = int.Parse(args[2]);
    if (timeout <= 0 || timeout > 3600) throw new ArgumentException("Timeout must be 1..3600 seconds.");
    var runner = new BootstrapMatchRunner();
    var results = new List<BootstrapRun>();
    for (int game = 0; game < 2; game++)
        results.Add(await runner.RunAsync(args[0], Path.Combine(output, "game-" + game),
            TimeSpan.FromSeconds(timeout), CancellationToken.None));
    await File.WriteAllTextAsync(Path.Combine(output, "runs.json"), JsonSerializer.Serialize(results,
        new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("BOOTSTRAP_PAIR_FINISHED games=2 succeeded=" + results.Count(result => result.Succeeded));
    return results.All(result => result.Succeeded) ? 0 : 1;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
