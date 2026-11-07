#nullable enable
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TuringSignalRelay.Evaluation;

public static class ContestDatabase
{
    public static async Task UpgradeAsync(ContestDb db, ContestPaths paths)
    {
        await db.Database.OpenConnectionAsync();
        paths.ShareDatabaseFiles();
        var schema = await SchemaAsync(db);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
        bool hasHistory = Convert.ToInt64(await command.ExecuteScalarAsync()) != 0;
        if (!hasHistory && schema.Length > 0)
        {
            // Adopt the exact initial schema created by the earlier EnsureCreated build.
            var options = new DbContextOptionsBuilder<ContestDb>().UseSqlite("Data Source=:memory:").Options;
            await using var reference = new ContestDb(options);
            await reference.Database.OpenConnectionAsync();
            await reference.Database.MigrateAsync();
            if (schema != await SchemaAsync(reference)) throw new InvalidDataException("Existing database schema does not match the initial contest schema. Restore a verified backup before upgrading.");
            var history = db.GetService<IHistoryRepository>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
            await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(db.Database.GetMigrations().First(), "10.0.14")));
            await transaction.CommitAsync();
        }
        await db.Database.MigrateAsync();
        paths.ShareDatabaseFiles();
    }

    private static async Task<string> SchemaAsync(ContestDb db)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT type,name,sql FROM sqlite_master WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%' AND substr(name,1,4) != '__EF' ORDER BY type,name";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync()) rows.Add(reader.GetString(0) + "|" + reader.GetString(1) + "|" + Regex.Replace(reader.GetString(2), @"\s+", " ").Trim());
        return string.Join("\n", rows);
    }
}
