using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TuringSignalRelay.Evaluation;

namespace TuringSignalRelay.Platform;

public sealed record MatchRequest(string TeamASubmissionId, string TeamBSubmissionId, int Seed, string RequestKey);

public static class ContestApi
{
    private static bool CanView(ClaimsPrincipal user, ContestUser? account, ContestMatch match) =>
        user.Identity?.IsAuthenticated == true && (user.IsInRole("Judge") || account?.TeamId == match.TeamAId || account?.TeamId == match.TeamBId);

    public static void Map(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "relay-contest" }));
        app.MapGet("/api/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken })).RequireAuthorization();
        app.MapGet("/api/submissions", async (ClaimsPrincipal user, UserManager<ContestUser> users, ContestDb db) => {
            var account = await users.GetUserAsync(user);
            return Results.Ok(await db.Submissions.AsNoTracking().Where(x => user.IsInRole("Judge") || x.TeamId == account!.TeamId).OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync());
        }).RequireAuthorization();
        app.MapPost("/api/submissions", async (HttpContext context, UserManager<ContestUser> users, IAntiforgery antiforgery, ContestOperations operations) => {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
                var account = await users.GetUserAsync(context.User);
                if (account?.TeamId == null) return Results.Forbid();
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                var file = form.Files.GetFile("source");
                if (form.Files.Count != 1 || file == null || file.FileName != "ai.cpp") return Results.BadRequest(new { error = "请上传一个名为ai.cpp的源文件。" });
                await using var input = file.OpenReadStream();
                var result = await operations.SubmitAsync(account.TeamId, form["note"].ToString(), input, context.RequestAborted);
                return Results.Accepted("/api/submissions/" + result.Id, result);
            }
            catch (AntiforgeryValidationException) { return Results.BadRequest(new { error = "请刷新页面后重试。" }); }
            catch (ArgumentException error) { return Results.BadRequest(new { error = error.Message }); }
        }).RequireAuthorization().DisableAntiforgery();
        app.MapGet("/api/submissions/{id}", async (string id, ClaimsPrincipal user, UserManager<ContestUser> users, ContestDb db) => {
            var item = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            var account = await users.GetUserAsync(user);
            return item == null || (!user.IsInRole("Judge") && item.TeamId != account?.TeamId) ? Results.NotFound() : Results.Ok(item);
        }).RequireAuthorization();
        app.MapGet("/api/submissions/{id}/source", async (string id, ClaimsPrincipal user, UserManager<ContestUser> users, ContestDb db, ContestPaths paths) => {
            var item = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            var account = await users.GetUserAsync(user);
            if (item == null || (!user.IsInRole("Judge") && item.TeamId != account?.TeamId)) return Results.NotFound();
            byte[] bytes = await File.ReadAllBytesAsync(paths.File("submissions", item.Id, "ai.cpp"));
            if (ContestJson.Hash(bytes) != item.SourceHash) return Results.Problem("源码校验失败。");
            return Results.File(bytes, "text/plain; charset=utf-8", "ai.cpp");
        }).RequireAuthorization();
        app.MapPost("/api/matches", async (MatchRequest request, HttpContext context, IAntiforgery antiforgery, ContestOperations operations) => {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
                var match = await operations.CreateMatchAsync(request.TeamASubmissionId, request.TeamBSubmissionId, request.Seed, request.RequestKey, context.RequestAborted);
                return Results.Accepted("/api/matches/" + match.Id, match);
            }
            catch (AntiforgeryValidationException) { return Results.BadRequest(new { error = "请刷新页面后重试。" }); }
            catch (ArgumentException error) { return Results.BadRequest(new { error = error.Message }); }
        }).RequireAuthorization(policy => policy.RequireRole("Judge"));
        app.MapGet("/api/matches", async (ClaimsPrincipal user, UserManager<ContestUser> users, ContestDb db) => {
            var account = await users.GetUserAsync(user);
            return Results.Ok(await db.Matches.AsNoTracking().Where(x => user.IsInRole("Judge") || x.TeamAId == account!.TeamId || x.TeamBId == account.TeamId)
                .OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync());
        }).RequireAuthorization();
        app.MapGet("/api/matches/{id}", async (string id, ClaimsPrincipal user, UserManager<ContestUser> users, ContestDb db, ContestPaths paths) => {
            var match = await db.Matches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (match == null || !CanView(user, await users.GetUserAsync(user), match)) return Results.NotFound();
            object? result = match.Status == "completed" ? ContestJson.Read<System.Text.Json.JsonElement>(paths.File(match.ResultDirectory, "series.result.json")) : null;
            return Results.Ok(new { match, result });
        }).RequireAuthorization();
        app.MapGet("/api/matches/{id}/artifacts", async (string id, ClaimsPrincipal user, UserManager<ContestUser> users, ContestDb db) => {
            var match = await db.Matches.SingleOrDefaultAsync(x => x.Id == id);
            if (match == null || !CanView(user, await users.GetUserAsync(user), match)) return Results.NotFound();
            return Results.Ok(await db.Artifacts.Where(x => x.MatchId == id).Select(x => new { x.Name, x.Sha256, x.Bytes }).ToListAsync());
        }).RequireAuthorization();
        app.MapGet("/api/matches/{id}/artifacts/{name}", async (string id, string name, ClaimsPrincipal user, UserManager<ContestUser> users, ContestDb db, ContestPaths paths) => {
            var match = await db.Matches.SingleOrDefaultAsync(x => x.Id == id);
            if (match == null || !CanView(user, await users.GetUserAsync(user), match)) return Results.NotFound();
            var artifact = await db.Artifacts.SingleOrDefaultAsync(x => x.MatchId == id && x.Name == name);
            if (artifact == null) return Results.NotFound();
            byte[] bytes = await File.ReadAllBytesAsync(paths.File(artifact.RelativePath));
            if (bytes.LongLength != artifact.Bytes || ContestJson.Hash(bytes) != artifact.Sha256) return Results.Problem("比赛产物校验失败。");
            return Results.File(bytes, "application/json", name + ".json");
        }).RequireAuthorization();
        app.MapPost("/api/matches/{id}/spectator-token", async (string id, HttpContext context, UserManager<ContestUser> users, ContestDb db, IAntiforgery antiforgery, IDataProtectionProvider protection) => {
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException) { return Results.BadRequest(); }
            var match = await db.Matches.SingleOrDefaultAsync(x => x.Id == id);
            if (match == null || !CanView(context.User, await users.GetUserAsync(context.User), match)) return Results.NotFound();
            string token = protection.CreateProtector("relay-snapshot-v1").ToTimeLimitedDataProtector().Protect(id, TimeSpan.FromHours(1));
            return Results.Ok(new { token, expiresInSeconds = 3600 });
        }).RequireAuthorization();
        app.MapGet("/api/matches/{id}/logs/{game:int}/{side:int}", async (string id, int game, int side, ClaimsPrincipal user, UserManager<ContestUser> users, ContestDb db, ContestPaths paths) => {
            var match = await db.Matches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            var account = await users.GetUserAsync(user);
            if (match == null || !CanView(user, account, match)) return Results.NotFound();
            if (game is < 1 or > 2 || side is < 0 or > 1) return Results.BadRequest();
            int ownSide = account?.TeamId == match.TeamAId ? game - 1 : 2 - game;
            if (!user.IsInRole("Judge") && side != ownSide) return Results.Forbid();
            string file = paths.File(match.ResultDirectory, "game-" + game, "ai-" + side + ".stderr.txt");
            if (match.ResultDirectory.Length == 0 || !File.Exists(file)) return Results.NotFound();
            return Results.File(await File.ReadAllBytesAsync(file), "text/plain; charset=utf-8", "game-" + game + "-ai-" + side + ".stderr.txt");
        }).RequireAuthorization();
        app.MapGet("/api/matches/{id}/snapshot", async (string id, int? game, HttpContext context, UserManager<ContestUser> users, ContestDb db, ContestPaths paths, IDataProtectionProvider protection) => {
            var match = await db.Matches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            if (match == null) return Results.NotFound();
            bool allowed = CanView(context.User, await users.GetUserAsync(context.User), match);
            string bearer = context.Request.Headers.Authorization.ToString();
            if (!allowed && bearer.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                try { allowed = protection.CreateProtector("relay-snapshot-v1").ToTimeLimitedDataProtector().Unprotect(bearer[7..]) == id; }
                catch (System.Security.Cryptography.CryptographicException) { }
            }
            if (!allowed) return Results.Unauthorized();
            if (game is < 1 or > 2) return Results.BadRequest();
            string file = paths.File(match.ResultDirectory, "game-" + (game ?? 1), "snapshot.json");
            if (match.ResultDirectory.Length == 0 || !File.Exists(file)) return Results.Accepted();
            return Results.Json(ContestJson.Read<TuringSignalRelay.ObserverSnapshotResponse>(file), ContestJson.Options);
        });
    }
}
