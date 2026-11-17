using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TuringSignalRelay.Evaluation;

namespace TuringSignalRelay.Platform.Pages;

[Authorize(Roles = "Judge")]
public sealed class JudgeModel(ContestDb db, ContestOperations operations, AccountAdmin accounts, ContestPaths paths, SubmissionReviewService reviews) : PageModel
{
    public List<Submission> Ready { get; private set; } = [];
    public Dictionary<string, string> Teams { get; private set; } = [];
    public List<Submission> RecentSubmissions { get; private set; } = [];
    public List<SubmissionReview> RecentReviews { get; private set; } = [];
    public Dictionary<string, SourceScan> Scans { get; } = [];
    [BindProperty] public string ReviewSubmissionId { get; set; } = "";
    [BindProperty] public string Decision { get; set; } = "approved";
    [BindProperty] public string ReviewReason { get; set; } = "";
    [BindProperty] public string FirstId { get; set; } = "";
    [BindProperty] public string SecondId { get; set; } = "";
    [BindProperty] public int Seed { get; set; } = 17;
    [BindProperty] public string RequestKey { get; set; } = Guid.NewGuid().ToString("N");
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string TeamName { get; set; } = "";
    public async Task OnGetAsync()
    {
        Ready = await db.Submissions.AsNoTracking().Where(x => x.Status == "ready" && db.Teams.Any(team => team.Id == x.TeamId && !team.Disqualified)).OrderByDescending(x => x.CreatedUtc).ToListAsync();
        Teams = await db.Teams.ToDictionaryAsync(x => x.Id, x => x.Name);
        RecentSubmissions = await db.Submissions.AsNoTracking().OrderByDescending(x => x.CreatedUtc).Take(20).ToListAsync();
        RecentReviews = await db.SubmissionReviews.AsNoTracking().OrderByDescending(x => x.CreatedUtc).Take(30).ToListAsync();
        foreach (var submission in RecentSubmissions)
        {
            byte[] source = await System.IO.File.ReadAllBytesAsync(paths.File("submissions", submission.Id, "ai.cpp"));
            if (ContestJson.Hash(source) == submission.SourceHash) Scans[submission.Id] = SourceScanner.Scan(source);
        }
    }
    public async Task<IActionResult> OnPostReviewAsync()
    {
        try
        {
            await reviews.ReviewAsync(ReviewSubmissionId, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value,
                Decision, ReviewReason, HttpContext.RequestAborted);
            TempData["Message"] = Decision == "disqualified" ? "复核结论已记录，该队伍相关比赛成绩已作废。" : "源码复核通过，结论已记录。";
            return RedirectToPage();
        }
        catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
        await OnGetAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostMatchAsync()
    {
        if (!ModelState.IsValid) { await OnGetAsync(); return Page(); }
        try
        {
            var match = await operations.CreateMatchAsync(FirstId, SecondId, Seed, RequestKey, HttpContext.RequestAborted);
            return RedirectToPage("/Results", new { id = match.Id });
        }
        catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
        await OnGetAsync(); return Page();
    }
    public async Task<IActionResult> OnPostAccountAsync()
    {
        if (!ModelState.IsValid) { await OnGetAsync(); return Page(); }
        var result = await accounts.CreateAsync(Username ?? "", Password ?? "", TeamName, false);
        Password = "";
        if (result.Succeeded) { TempData["Message"] = "队伍账号已创建。"; return RedirectToPage(); }
        foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        await OnGetAsync(); return Page();
    }
}
