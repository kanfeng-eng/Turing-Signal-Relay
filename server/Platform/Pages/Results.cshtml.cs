using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TuringSignalRelay.Evaluation;

namespace TuringSignalRelay.Platform.Pages;

public sealed class ResultsModel(ContestDb db, UserManager<ContestUser> users, ContestPaths paths, IDataProtectionProvider protection) : PageModel
{
    public List<ContestMatch> Matches { get; private set; } = [];
    public Dictionary<string, string> Teams { get; private set; } = [];
    public ContestMatch? Selected { get; private set; }
    public TuringSignalRelay.RelaySeriesResult? Series { get; private set; }
    public string SpectatorToken { get; private set; } = "";
    public string? ViewerTeamId { get; private set; }
    public string[] FaultReasons { get; } = new string[2];
    public async Task<IActionResult> OnGetAsync(string? id)
    {
        var account = await users.GetUserAsync(User);
        ViewerTeamId = account?.TeamId;
        var allowed = db.Matches.AsNoTracking().Where(x => User.IsInRole("Judge") || x.TeamAId == account!.TeamId || x.TeamBId == account.TeamId);
        Matches = await allowed.OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync();
        Teams = await db.Teams.ToDictionaryAsync(x => x.Id, x => x.Name);
        if (id == null) return Page();
        Selected = await allowed.SingleOrDefaultAsync(x => x.Id == id);
        if (Selected == null) return NotFound();
        if (Selected.Status == "completed")
        {
            Series = ContestJson.Read<TuringSignalRelay.RelaySeriesResult>(paths.File(Selected.ResultDirectory, "series.result.json"));
            for (int game = 0; game < 2; game++) if (Series.games[game].forfeit)
                FaultReasons[game] = ContestJson.Read<TuringSignalRelay.RelayRecording>(paths.File(Selected.ResultDirectory, "game-" + (game + 1), "relay.replay.json")).terminationReason;
        }
        return Page();
    }
    public async Task<IActionResult> OnPostTokenAsync(string id)
    {
        var result = await OnGetAsync(id);
        if (Selected == null) return result;
        SpectatorToken = protection.CreateProtector("relay-snapshot-v1").ToTimeLimitedDataProtector().Protect(id, TimeSpan.FromHours(1));
        return Page();
    }
    public static string StatusLabel(string status) => status switch { "queued" => "等待评测", "running" => "正在比赛", "completed" => "评测完成", "failed" => "需要裁判处理", _ => status };
}
