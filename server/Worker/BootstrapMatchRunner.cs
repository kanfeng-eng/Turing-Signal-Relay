using System.Diagnostics;
using System.Text.Json;

namespace TuringSignalRelay.Worker;

public sealed record BootstrapRun(bool Succeeded, int ExitCode, bool TimedOut, string Directory, string? StateHash, string? Error);

// The executable is a trusted Unity judge. Contestant AI lifecycle belongs to the sandbox adapter.
public sealed class BootstrapMatchRunner
{
    public async Task<BootstrapRun> RunAsync(string judge, string outputDirectory, TimeSpan timeout, CancellationToken cancellation)
    {
        string executable = Path.GetFullPath(judge);
        if (!File.Exists(executable)) throw new FileNotFoundException("Trusted judge executable missing.", executable);
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        string output = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Output directory already exists.");
        cancellation.ThrowIfCancellationRequested();
        Directory.CreateDirectory(output);
        using var ownership = new FileStream(Path.Combine(output, ".runner-owner"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        string replay = Path.Combine(output, "bootstrap.jsonl");
        string result = Path.Combine(output, "bootstrap.result.json");
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = output,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (string argument in new[] { "-batchmode", "-nographics", "-logFile", Path.Combine(output, "judge.log"),
                     "--bootstrap-replay", replay, "--bootstrap-result", result }) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(timeout);
        process.Start();
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) when (process.HasExited) { }
            await process.WaitForExitAsync(CancellationToken.None);
            if (cancellation.IsCancellationRequested) throw;
            return new BootstrapRun(false, process.ExitCode, true, output, null, "Judge timeout.");
        }
        if (process.ExitCode != 0)
            return new BootstrapRun(false, process.ExitCode, false, output, null, "Judge exited with an error.");
        try
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(result, cancellation));
            var root = document.RootElement;
            if (root.GetProperty("type").GetString() != "bootstrap-result" || root.GetProperty("resultVersion").GetInt32() != 1 ||
                root.GetProperty("status").GetString() != "bootstrap-completed" ||
                !root.GetProperty("finalSnapshot").GetProperty("completed").GetBoolean())
                throw new InvalidDataException("Unsupported bootstrap result.");
            using var stream = File.OpenRead(replay);
            string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream));
            if (hash != root.GetProperty("replaySha256").GetString()) throw new InvalidDataException("Replay digest mismatch.");
            var options = new JsonSerializerOptions { IncludeFields = true };
            var config = root.GetProperty("config").Deserialize<MatchConfig>(options);
            var final = root.GetProperty("finalSnapshot").Deserialize<Snapshot>(options);
            if (config == null || final == null || BootstrapStateHash.Compute(config, final) != root.GetProperty("stateHash").GetString())
                throw new InvalidDataException("Final state hash mismatch.");
            return new BootstrapRun(true, 0, false, output, root.GetProperty("stateHash").GetString(), null);
        }
        catch (Exception error) when (error is IOException or JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return new BootstrapRun(false, 0, false, output, null, error.Message);
        }
    }
}
