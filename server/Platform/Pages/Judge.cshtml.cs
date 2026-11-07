using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TuringSignalRelay.Evaluation;

namespace TuringSignalRelay.Platform.Pages;

[Authorize(Roles = "Judge")]
public sealed class JudgeModel(ContestDb db, ContestOperations operations, AccountAdmin accounts) : PageModel
{
    public List<Submission> Ready { get; private set; } = [];
    public Dictionary<string, string> Teams { get; private set; } = [];
    [BindProperty] public string FirstId { get; set; } = "";
    [BindProperty] public string SecondId { get; set; } = "";
    [BindProperty] public int Seed { get; set; } = 17;
    [BindProperty] public string RequestKey { get; set; } = Guid.NewGuid().ToString("N");
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string TeamName { get; set; } = "";
    public async Task OnGetAsync()
    {
        Ready = await db.Submissions.AsNoTracking().Where(x => x.Status == "ready").OrderByDescending(x => x.CreatedUtc).ToListAsync();
        Teams = await db.Teams.ToDictionaryAsync(x => x.Id, x => x.Name);
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
