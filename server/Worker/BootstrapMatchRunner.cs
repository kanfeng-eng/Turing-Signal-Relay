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
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
            throw new ArgumentOutOfRangeException(nameof(timeout));
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
        try
        {
            if (!process.Start())
                return new BootstrapRun(false, -1, false, output, null, "Judge process did not start.");
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            return new BootstrapRun(false, -1, false, output, null, "Cannot start judge: " + error.Message);
        }
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
            string hash = Convert.ToHexStringLower(await System.Security.Cryptography.SHA256.HashDataAsync(stream, cancellation));
            if (hash != root.GetProperty("replaySha256").GetString()) throw new InvalidDataException("Replay digest mismatch.");
            var options = new JsonSerializerOptions { IncludeFields = true };
            var config = root.GetProperty("config").Deserialize<MatchConfig>(options);
            var final = root.GetProperty("finalSnapshot").Deserialize<Snapshot>(options);
            if (config == null || final == null || BootstrapStateHash.Compute(config, final) != root.GetProperty("stateHash").GetString())
                throw new InvalidDataException("Final state hash mismatch.");
            stream.Position = 0;
            VerifyReplay(stream, root.GetProperty("stateHash").GetString()!, cancellation);
            return new BootstrapRun(true, 0, false, output, root.GetProperty("stateHash").GetString(), null);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException)
        {
            return new BootstrapRun(false, 0, false, output, null, error.Message);
        }
    }

    private static void VerifyReplay(Stream stream, string expectedFinalHash, CancellationToken cancellation)
    {
        using var reader = new StreamReader(stream);
        using var header = JsonDocument.Parse(reader.ReadLine() ?? throw new InvalidDataException("Missing replay header."));
        var root = header.RootElement;
        int version = root.GetProperty("replayVersion").GetInt32();
        if (root.GetProperty("type").GetString() != "bootstrap-replay" || version is not (1 or 2))
            throw new InvalidDataException("Unsupported replay version.");
        var options = new JsonSerializerOptions { IncludeFields = true };
        var config = root.GetProperty("config").Deserialize<MatchConfig>(options) ?? throw new InvalidDataException("Missing replay configuration.");
        config.Validate();
        long nextTick = 0;
        string? finalHash = null;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            cancellation.ThrowIfCancellationRequested();
            using var document = JsonDocument.Parse(line);
            var frame = document.RootElement;
            var snapshot = (version == 2 ? frame.GetProperty("snapshot") : frame).Deserialize<Snapshot>(options);
            if (snapshot == null || snapshot.tick != nextTick)
                throw new InvalidDataException("Replay sequence mismatch at tick " + nextTick);
            finalHash = BootstrapStateHash.Compute(config, snapshot);
            if (version == 2 && frame.GetProperty("stateHash").GetString() != finalHash)
                throw new InvalidDataException("Replay state hash mismatch at tick " + nextTick);
            nextTick++;
        }
        if (nextTick != (long)config.durationTicks + 1 || finalHash != expectedFinalHash)
            throw new InvalidDataException("Replay is incomplete or disagrees with the result.");
    }
}
