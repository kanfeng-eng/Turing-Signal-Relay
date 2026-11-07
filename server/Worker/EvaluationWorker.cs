using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using TuringSignalRelay.Evaluation;

namespace TuringSignalRelay.Worker;

public static class EvaluationWorker
{
    public static async Task RunAsync()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("正式评测Worker需要Linux沙箱环境。");
        var paths = new ContestPaths(); paths.Initialize();
        using var ownership = new FileStream(paths.File("worker.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; stop.Cancel(); };
        using var termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => { context.Cancel = true; stop.Cancel(); });
        var options = new DbContextOptionsBuilder<ContestDb>().UseSqlite(paths.ConnectionString).Options;
        var sandbox = new SandboxClient(paths);
        foreach (string directory in Directory.EnumerateDirectories("/var/lib/tsr-sandbox/runs"))
        {
            string token = Path.GetFileName(directory);
            if (Guid.TryParseExact(token, "N", out _)) await sandbox.InvokeAsync("release", token, null, null, stop.Token);
        }
        using (var initial = new ContestDb(options))
        {
            await new EvaluationProcessor(initial, paths, sandbox).RecoverAsync(stop.Token);
        }
        Console.WriteLine("EVALUATION_WORKER_READY");
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var db = new ContestDb(options);
                bool processed = await new EvaluationProcessor(db, paths, sandbox).ProcessOneAsync(stop.Token);
                if (!processed) await Task.Delay(1000, stop.Token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        Console.WriteLine("EVALUATION_WORKER_STOPPED");
    }
}
