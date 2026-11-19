using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TuringSignalRelay.Evaluation;
namespace TuringSignalRelay.Platform.Pages;
public sealed class LogoutModel(SignInManager<ContestUser> signIn) : PageModel
{
    public async Task<IActionResult> OnPostAsync() { await signIn.SignOutAsync(); return RedirectToPage("/Login"); }
}
