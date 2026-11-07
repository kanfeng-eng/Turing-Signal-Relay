using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TuringSignalRelay.Evaluation;

namespace TuringSignalRelay.Platform.Pages;

public sealed class LoginModel(SignInManager<ContestUser> signIn) : PageModel
{
    [BindProperty, Required, StringLength(64)] public string Username { get; set; } = "";
    [BindProperty, Required, StringLength(256)] public string Password { get; set; } = "";
    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Index") : Page();
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await signIn.PasswordSignInAsync(Username, Password, false, true);
        Password = "";
        if (result.Succeeded) return RedirectToPage("/Index");
        ModelState.AddModelError("", result.IsLockedOut ? "登录尝试过多，请15分钟后重试。" : "用户名或密码错误。");
        return Page();
    }
}
