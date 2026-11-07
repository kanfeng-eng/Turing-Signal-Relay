using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TuringSignalRelay.Evaluation;

namespace TuringSignalRelay.Platform.Pages;

public sealed class WorkspaceModel(ContestDb db, UserManager<ContestUser> users, ContestOperations operations) : PageModel
{
    public List<Submission> Submissions { get; private set; } = [];
    public string TeamName { get; private set; } = "";
    [BindProperty] public IFormFile? Source { get; set; }
    [BindProperty] public string Note { get; set; } = "";
    public async Task OnGetAsync()
    {
        var account = await users.GetUserAsync(User);
        TeamName = account?.TeamId == null ? "裁判账号" : (await db.Teams.SingleAsync(x => x.Id == account.TeamId)).Name;
        Submissions = await db.Submissions.AsNoTracking().Where(x => User.IsInRole("Judge") || x.TeamId == account!.TeamId)
            .OrderByDescending(x => x.CreatedUtc).Take(100).ToListAsync();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        var account = await users.GetUserAsync(User);
        if (account?.TeamId == null) return Forbid();
        if (Source == null || Source.FileName != "ai.cpp") ModelState.AddModelError("", "请选择名为ai.cpp的源文件。");
        if (ModelState.IsValid)
        {
            try
            {
                await using var input = Source!.OpenReadStream();
                await operations.SubmitAsync(account.TeamId, Note ?? "", input, HttpContext.RequestAborted);
                return RedirectToPage("/History");
            }
            catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
        }
        await OnGetAsync(); return Page();
    }
    public static string StatusLabel(string status) => status switch {
        "queued" => "等待编译", "compiling" => "正在编译", "ready" => "可参加比赛", "failed" => "编译失败", _ => status };
}
