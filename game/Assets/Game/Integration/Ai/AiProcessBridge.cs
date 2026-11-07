using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TuringSignalRelay
{
    // Local bridge for trusted programs. Submission isolation belongs to the sandbox worker.
    public sealed class AiProcessBridge : IDisposable
    {
        public const int MaximumFrameBytes = 2 * 1024 * 1024;
        public const int MaximumReplyBytes = 64 * 1024;
        private readonly Process process;
        private readonly byte[] buffer = new byte[4096];
        private int offset, available;
        private Task<string> pendingRead;
        private Task pendingWrite;
        private readonly Task stderrPump;
        private readonly MemoryStream diagnostics = new MemoryStream();
        private bool disposed;
        public long ObservedPeakBytes { get; private set; }
        public double LastCpuMilliseconds { get; private set; }
        public bool PreciseCpuAvailable { get; private set; }

        public AiProcessBridge(string executable)
        {
            string path = Path.GetFullPath(executable);
            if (!File.Exists(path)) throw new FileNotFoundException("AI executable missing.", path);
            process = new Process { StartInfo = new ProcessStartInfo(path) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(path)
            } };
            if (!process.Start()) throw new IOException("AI process did not start.");
            PreciseCpuAvailable = File.Exists("/proc/" + process.Id + "/schedstat");
            stderrPump = DrainDiagnostics();
        }

        public async Task<string> ExchangeAsync(string json, int expectedTick, Func<string, int> replyTick,
            int wallMilliseconds, bool requireCpuBudget, CancellationToken cancellation)
        {
            if (disposed) throw new ObjectDisposedException("AiProcessBridge");
            if (requireCpuBudget && !PreciseCpuAvailable) throw new PlatformNotSupportedException("Precise Linux CPU accounting is required.");
            byte[] payload = Encoding.UTF8.GetBytes(json + "\n");
            if (payload.Length > MaximumFrameBytes) throw new ArgumentException("Input frame too large.");
            var clock = Stopwatch.StartNew();
            double cpu = CpuMilliseconds();
            // A timed-out write/read is retained so late replies can be drained without concurrent pipe reads.
            if (pendingWrite != null && !await FinishBefore(pendingWrite, clock, wallMilliseconds, cancellation)) return null;
            pendingWrite = process.StandardInput.BaseStream.WriteAsync(payload, 0, payload.Length, cancellation);
            if (!await FinishBefore(pendingWrite, clock, wallMilliseconds, cancellation)) return null;
            pendingWrite = null;
            while (true)
            {
                if (pendingRead == null) pendingRead = ReadReply();
                if (!await FinishBefore(pendingRead, clock, wallMilliseconds, cancellation)) return null;
                string reply = await pendingRead;
                pendingRead = null;
                int tick = expectedTick < 0 ? expectedTick : replyTick(reply);
                if (tick > expectedTick) throw new InvalidDataException("AI replied for a future tick.");
                if (tick < expectedTick) continue;
                LastCpuMilliseconds = Math.Max(0, CpuMilliseconds() - cpu);
                process.Refresh();
                ObservedPeakBytes = Math.Max(ObservedPeakBytes, Math.Max(process.WorkingSet64, process.PeakWorkingSet64));
                if (ObservedPeakBytes > 256L * 1024 * 1024) throw new InvalidDataException("AI observed memory limit exceeded.");
                if (requireCpuBudget && LastCpuMilliseconds > 10) return null;
                return reply;
            }
        }

        private static async Task<bool> FinishBefore(Task task, Stopwatch clock, int budget, CancellationToken cancellation)
        {
            int remaining = budget - (int)clock.ElapsedMilliseconds;
            if (remaining <= 0) return false;
            Task completed = await Task.WhenAny(task, Task.Delay(remaining, cancellation));
            cancellation.ThrowIfCancellationRequested();
            if (completed != task) return false;
            await task;
            return true;
        }

        private async Task<string> ReadReply()
        {
            using (var line = new MemoryStream())
            {
                while (true)
                {
                    if (offset == available)
                    {
                        available = await process.StandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                        offset = 0;
                        if (available == 0) throw new EndOfStreamException("AI stdout closed.");
                    }
                    byte value = buffer[offset++];
                    if (value == 10)
                    {
                        byte[] bytes = line.ToArray();
                        int length = bytes.Length > 0 && bytes[bytes.Length - 1] == 13 ? bytes.Length - 1 : bytes.Length;
                        return new UTF8Encoding(false, true).GetString(bytes, 0, length);
                    }
                    if (line.Length >= MaximumReplyBytes) throw new InvalidDataException("AI stdout line exceeds limit.");
                    line.WriteByte(value);
                }
            }
        }

        private double CpuMilliseconds()
        {
            if (PreciseCpuAvailable)
            {
                string value = File.ReadAllText("/proc/" + process.Id + "/schedstat").Split(' ')[0];
                return long.Parse(value, CultureInfo.InvariantCulture) / 1000000.0;
            }
            process.Refresh();
            return process.TotalProcessorTime.TotalMilliseconds;
        }

        private async Task DrainDiagnostics()
        {
            var bytes = new byte[4096];
            int read;
            while ((read = await process.StandardError.BaseStream.ReadAsync(bytes, 0, bytes.Length)) > 0)
                lock (diagnostics)
                {
                    int keep = Math.Min(read, 1024 * 1024 - (int)diagnostics.Length);
                    if (keep > 0) diagnostics.Write(bytes, 0, keep);
                }
        }

        public string Diagnostics { get { lock (diagnostics) return Encoding.UTF8.GetString(diagnostics.ToArray()); } }

        public void Complete()
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(1000)) process.Kill();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { }
            process.WaitForExit(3000);
            try { stderrPump.Wait(3000); } catch (AggregateException) { }
            if (pendingRead != null) pendingRead.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            if (pendingWrite != null) pendingWrite.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            process.Dispose();
        }
    }
}
