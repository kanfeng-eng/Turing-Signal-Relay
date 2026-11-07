using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TuringSignalRelay;

internal static class AiBridgeChecks
{
    private static string Executable(string directory, string name) { return Path.Combine(directory, name + (OperatingSystem.IsWindows() ? ".exe" : "")); }
    private static string Frame(int tick) { return "{\"type\":\"relay-frame\",\"protocolVersion\":2,\"side\":0,\"tick\":" + tick + ",\"completed\":false}"; }
    private static int Tick(string text) { return System.Text.Json.JsonSerializer.Deserialize<RelayCommandBatch>(text, Program.Json).tick; }
    private static async Task Ready(AiProcessBridge bridge)
    {
        string reply = await bridge.ExchangeAsync("{\"type\":\"relay-init\",\"protocolVersion\":2,\"side\":0}", -1, null, 2000, false, CancellationToken.None);
        if (reply == null) throw new Exception("Fault fixture failed to initialize.");
    }

    internal static async Task Run(string directory)
    {
        int cases = 0;
        foreach (string name in new[] { "malformed", "oversize", "crash", "memory", "future-tick" })
        {
            using var bridge = new AiProcessBridge(Executable(directory, name));
            await Ready(bridge);
            bool rejected = false;
            try { await bridge.ExchangeAsync(Frame(0), 0, Tick, 2000, false, CancellationToken.None); }
            catch (Exception) { rejected = true; }
            if (!rejected) throw new Exception("Invalid AI output accepted: " + name);
            cases++;
        }
        using (var bridge = new AiProcessBridge(Executable(directory, "timeout")))
        {
            await Ready(bridge);
            var clock = Stopwatch.StartNew();
            for (int tick = 0; tick < 3; tick++)
                if (await bridge.ExchangeAsync(Frame(tick), tick, Tick, 100, false, CancellationToken.None) != null) throw new Exception("Timeout fixture replied early.");
            if (clock.ElapsedMilliseconds > 1500) throw new Exception("Timeout handling was unbounded.");
            cases++;
        }
        using (var bridge = new AiProcessBridge(Executable(directory, "late-once")))
        {
            await Ready(bridge);
            if (await bridge.ExchangeAsync(Frame(0), 0, Tick, 100, false, CancellationToken.None) != null) throw new Exception("Late reply did not time out.");
            string recovered = await bridge.ExchangeAsync(Frame(1), 1, Tick, 500, false, CancellationToken.None);
            if (recovered == null || Tick(recovered) != 1) throw new Exception("Late reply resynchronization failed.");
            bridge.Complete(); cases++;
        }
        var stderr = new AiProcessBridge(Executable(directory, "stderr"));
        try
        {
            await Ready(stderr);
            if (await stderr.ExchangeAsync(Frame(0), 0, Tick, 2000, false, CancellationToken.None) == null) throw new Exception("Stderr flood blocked stdout.");
            stderr.Complete();
        }
        finally { stderr.Dispose(); }
        if (stderr.Diagnostics.Length != 1024 * 1024) throw new Exception("Stderr capture did not stop at the byte limit.");
        cases++;
        using (var bridge = new AiProcessBridge(Executable(directory, "timeout")))
        {
            await Ready(bridge);
            using var cancellation = new CancellationTokenSource(30);
            bool cancelled = false;
            try { await bridge.ExchangeAsync(Frame(0), 0, Tick, 1000, false, cancellation.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            if (!cancelled) throw new Exception("Cancellation was ignored.");
            cases++;
        }
        if (OperatingSystem.IsLinux())
        {
            using var bridge = new AiProcessBridge(Executable(directory, "cpu"));
            await Ready(bridge);
            for (int tick = 0; tick < 3; tick++)
            {
                if (await bridge.ExchangeAsync(Frame(tick), tick, Tick, 500, true, CancellationToken.None) != null || bridge.LastCpuMilliseconds <= 10)
                    throw new Exception("CPU budget violation was not detected.");
                Console.WriteLine("CPU_VIOLATION_REJECTED milliseconds=" + bridge.LastCpuMilliseconds);
            }
            cases++;
        }
        Console.WriteLine("AI_BRIDGE_CHECKS_PASSED cases=" + cases);
    }
}
