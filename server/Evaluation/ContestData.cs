#nullable enable
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TuringSignalRelay.Evaluation;

public sealed class ContestUser : IdentityUser
{
    public string? TeamId { get; set; }
}

public sealed class Team
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
}

public sealed class Submission
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TeamId { get; set; } = "";
    public string Note { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string BinaryHash { get; set; } = "";
    public string Compiler { get; set; } = "";
    public string Status { get; set; } = "queued";
    public string Diagnostic { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int Attempts { get; set; }
}

public sealed class ContestMatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TeamASubmissionId { get; set; } = "";
    public string TeamBSubmissionId { get; set; } = "";
    public string TeamAId { get; set; } = "";
    public string TeamBId { get; set; } = "";
    public int Seed { get; set; }
    public string RequestKey { get; set; } = "";
    public string Status { get; set; } = "queued";
    public string Diagnostic { get; set; } = "";
    public string ResultDirectory { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int Attempts { get; set; }
}

public sealed class MatchArtifact
{
    public string MatchId { get; set; } = "";
    public string Name { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Bytes { get; set; }
}

public sealed class ContestDb(DbContextOptions<ContestDb> options) : IdentityDbContext<ContestUser>(options)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<ContestMatch> Matches => Set<ContestMatch>();
    public DbSet<MatchArtifact> Artifacts => Set<MatchArtifact>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Team>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<ContestUser>().HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Submission>().HasOne<Team>().WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Submission>().HasIndex(x => new { x.Status, x.CreatedUtc });
        builder.Entity<ContestMatch>().HasIndex(x => x.RequestKey).IsUnique();
        builder.Entity<ContestMatch>().HasOne<Submission>().WithMany().HasForeignKey(x => x.TeamASubmissionId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<ContestMatch>().HasOne<Submission>().WithMany().HasForeignKey(x => x.TeamBSubmissionId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<MatchArtifact>().HasKey(x => new { x.MatchId, x.Name });
        builder.Entity<MatchArtifact>().HasOne<ContestMatch>().WithMany().HasForeignKey(x => x.MatchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ContestPaths
{
    public string Data { get; } = Path.GetFullPath(Environment.GetEnvironmentVariable("TSR_DATA") ?? "runtime-data/contest");
    public string Repository { get; } = Path.GetFullPath(Environment.GetEnvironmentVariable("TSR_REPOSITORY") ?? ".");
    public string SandboxHelper { get; } = Environment.GetEnvironmentVariable("TSR_SANDBOX_HELPER") ?? "/usr/local/libexec/tsr-sandbox";
    public string Database => Path.Combine(Data, "contest.db");
    public string ConnectionString => new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Database, DefaultTimeout = 30 }.ToString();
    public string File(params string[] parts) => Path.Combine(new[] { Data }.Concat(parts).ToArray());
    public void Initialize() { Directory.CreateDirectory(Data); Directory.CreateDirectory(File("submissions")); Directory.CreateDirectory(File("matches")); }
    public void ShareDatabaseFiles()
    {
        if (!OperatingSystem.IsLinux()) return;
        var permissions = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite;
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            string path = Database + suffix;
            if (System.IO.File.Exists(path) && System.IO.File.GetUnixFileMode(path) != permissions) System.IO.File.SetUnixFileMode(path, permissions);
        }
    }
}

public static class ContestJson
{
    public static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
    public static T Read<T>(string path) => JsonSerializer.Deserialize<T>(System.IO.File.ReadAllText(path), Options) ?? throw new InvalidDataException("Missing JSON data.");
    public static void Write(string path, object value)
    {
        string temporary = path + ".tmp";
        System.IO.File.WriteAllText(temporary, JsonSerializer.Serialize(value, value.GetType(), Options));
        System.IO.File.Move(temporary, path, true);
    }
    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

public sealed class ContestOperations(ContestDb db, ContestPaths paths)
{
    public async Task<Submission> SubmitAsync(string teamId, string note, Stream input, CancellationToken cancellation)
    {
        if (note.Length > 200) throw new ArgumentException("版本说明最多200字。");
        using var memory = new MemoryStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, cancellation)) > 0)
        {
            if (memory.Length + count > 256 * 1024) throw new ArgumentException("源码最多256 KiB。");
            memory.Write(buffer, 0, count);
        }
        byte[] bytes = memory.ToArray();
        if (bytes.Length == 0 || bytes.Contains((byte)0)) throw new ArgumentException("请上传非空UTF-8源码。");
        try { new UTF8Encoding(false, true).GetString(bytes); } catch (DecoderFallbackException) { throw new ArgumentException("源码必须使用UTF-8编码。"); }
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellation);
        if (await db.Submissions.CountAsync(x => x.TeamId == teamId && (x.Status == "queued" || x.Status == "compiling"), cancellation) >= 5)
            throw new ArgumentException("本队最多同时保留5个待编译提交。");
        var submission = new Submission { TeamId = teamId, Note = note, SourceHash = ContestJson.Hash(bytes) };
        string directory = paths.File("submissions", submission.Id);
        Directory.CreateDirectory(directory);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(directory, "ai.cpp"), bytes, cancellation);
        db.Submissions.Add(submission);
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        return submission;
    }

    public async Task<ContestMatch> CreateMatchAsync(string firstId, string secondId, int seed, string requestKey, CancellationToken cancellation)
    {
        if (!Guid.TryParseExact(requestKey, "N", out var requestId)) throw new ArgumentException("请刷新裁判页面后重试。");
        requestKey = requestId.ToString("N");
        var existing = await db.Matches.SingleOrDefaultAsync(x => x.RequestKey == requestKey, cancellation);
        if (existing != null) return VerifySameRequest(existing, firstId, secondId, seed);
        var first = await db.Submissions.SingleOrDefaultAsync(x => x.Id == firstId && x.Status == "ready", cancellation);
        var second = await db.Submissions.SingleOrDefaultAsync(x => x.Id == secondId && x.Status == "ready", cancellation);
        if (first == null || second == null || first.TeamId == second.TeamId) throw new ArgumentException("请选择两支不同队伍已编译的提交。");
        var match = new ContestMatch { TeamASubmissionId = first.Id, TeamBSubmissionId = second.Id, TeamAId = first.TeamId, TeamBId = second.TeamId, Seed = seed, RequestKey = requestKey };
        db.Matches.Add(match);
        try { await db.SaveChangesAsync(cancellation); }
        catch (DbUpdateException)
        {
            db.Entry(match).State = EntityState.Detached;
            return VerifySameRequest(await db.Matches.SingleAsync(x => x.RequestKey == requestKey, cancellation), firstId, secondId, seed);
        }
        return match;
    }

    private static ContestMatch VerifySameRequest(ContestMatch match, string first, string second, int seed)
    {
        if (match.TeamASubmissionId != first || match.TeamBSubmissionId != second || match.Seed != seed)
            throw new ArgumentException("该请求编号已用于其他比赛，请刷新页面后重试。");
        return match;
    }
}
