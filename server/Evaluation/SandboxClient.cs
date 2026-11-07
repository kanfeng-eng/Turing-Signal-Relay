#nullable enable
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace TuringSignalRelay.Evaluation;

public sealed record CompilationResult(bool Success, string SourceHash, string BinaryHash, string Compiler, string Diagnostic, int ExitCode, bool TimedOut, string SdkHash);

public sealed class SandboxClient(ContestPaths paths)
{
    public async Task<JsonElement> InvokeAsync(string operation, string token, byte[]? source, string? submissionId, CancellationToken cancellation)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("正式沙箱需要已安装项目沙箱的Linux环境。");
        var start = new ProcessStartInfo("/usr/bin/sudo") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in new[] { "-n", paths.SandboxHelper, operation, token }) start.ArgumentList.Add(argument);
        if (submissionId != null) start.ArgumentList.Add(submissionId);
        using var process = Process.Start(start) ?? throw new IOException("Sandbox helper did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellation);
        Task<string> errors = process.StandardError.ReadToEndAsync(cancellation);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try
        {
            if (source != null) await process.StandardInput.BaseStream.WriteAsync(source, timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (operation == "compile") await InvokeAsync("cancel-compile", token, null, null, CancellationToken.None);
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(40));
            throw;
        }
        string text = await output;
        string diagnostic = await errors;
        if (process.ExitCode != 0) throw new IOException("Sandbox infrastructure failure " + diagnostic[..Math.Min(2000, diagnostic.Length)]);
        return text.Trim().Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public async Task<CompilationResult> CompileAsync(Submission submission, CancellationToken cancellation)
    {
        byte[] source = await File.ReadAllBytesAsync(paths.File("submissions", submission.Id, "ai.cpp"), cancellation);
        if (ContestJson.Hash(source) != submission.SourceHash) throw new InvalidDataException("Submission source digest changed.");
        var value = await InvokeAsync("compile", submission.Id, source, null, cancellation);
        return new CompilationResult(value.GetProperty("success").GetBoolean(), value.GetProperty("sourceHash").GetString()!,
            value.GetProperty("binaryHash").GetString()!, value.GetProperty("compiler").GetString()!, value.GetProperty("diagnostic").GetString()!,
            value.GetProperty("exitCode").GetInt32(), value.GetProperty("timedOut").GetBoolean(), value.GetProperty("sdkHash").GetString()!);
    }

    public async Task<SandboxRun> PrepareAsync(string submissionId, string directory, CancellationToken cancellation)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Sandbox runs require Linux.");
        string token = Guid.NewGuid().ToString("N");
        try
        {
            var result = await InvokeAsync("prepare", token, null, submissionId, cancellation);
            string resources = result.GetProperty("resourceDirectory").GetString()!;
            string launcher = Path.Combine(directory, "launch-" + token);
            if (paths.SandboxHelper != "/usr/local/libexec/tsr-sandbox") throw new InvalidOperationException("The installed sandbox helper path must match its audited sudoers entry.");
            await File.WriteAllTextAsync(launcher, "#!/bin/sh\nexec /usr/bin/sudo -n /usr/local/libexec/tsr-sandbox run " + token + "\n", new UTF8Encoding(false), cancellation);
            File.SetUnixFileMode(launcher, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return new SandboxRun(token, launcher, resources, this);
        }
        catch
        {
            await InvokeAsync("release", token, null, null, CancellationToken.None);
            throw;
        }
    }
}

public sealed record SandboxRun(string Token, string Launcher, string ResourceDirectory, SandboxClient Client)
{
    public AiProcessBridge Bridge() => new(Launcher, ResourceDirectory, () => Client.InvokeAsync("stop", Token, null, null, CancellationToken.None).GetAwaiter().GetResult());
    public Task ReleaseAsync() => Client.InvokeAsync("release", Token, null, null, CancellationToken.None);
}

public sealed class ServerAiJson : IRelayAiJson
{
    private static readonly JsonSerializerOptions Strict = new() { IncludeFields = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public string Initialization(RelayAiInit value) => JsonSerializer.Serialize(value, Strict);
    public string Frame(RelayObservation value) => JsonSerializer.Serialize(value, Strict);
    public RelayAiReady Ready(string text) => JsonSerializer.Deserialize<RelayAiReady>(text, Strict)!;
    public RelayCommandBatch Commands(string text) => JsonSerializer.Deserialize<RelayCommandBatch>(text, Strict)!;
}
