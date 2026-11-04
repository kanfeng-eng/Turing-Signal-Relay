using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TuringSignalRelay.Platform.Pages;

public sealed record SubmissionSummary(string Id, string Note, string SubmittedAt, string Status, string Diagnostic);
public sealed record GameSummary(int Number, string RedTeam, int RedScoreHalfPoints, string BlueTeam, int BlueScoreHalfPoints, string Status);

public sealed class DemoPageModel : PageModel
{
    public IReadOnlyList<SubmissionSummary> Submissions { get; } = new[]
    {
        new SubmissionSummary("DEMO-003", "跨河路线调整", "2026-11-04 10:30", "等待评测", "队列中"),
        new SubmissionSummary("DEMO-002", "基础移动", "2026-11-04 10:00", "编译失败", "示例诊断将在此处显示")
    };
    public IReadOnlyList<GameSummary> Games { get; } = new[]
    {
        new GameSummary(1, "A", 130, "B", 100, "演示完成"),
        new GameSummary(2, "B", 120, "A", 110, "演示完成")
    };
    public decimal TeamATotal => Games.Sum(game => game.RedTeam == "A" ? game.RedScoreHalfPoints : game.BlueScoreHalfPoints) / 2m;
    public decimal TeamBTotal => Games.Sum(game => game.RedTeam == "B" ? game.RedScoreHalfPoints : game.BlueScoreHalfPoints) / 2m;
    public void OnGet() { }
}
