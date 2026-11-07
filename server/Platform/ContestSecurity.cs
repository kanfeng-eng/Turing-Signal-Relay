using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TuringSignalRelay.Evaluation;

namespace TuringSignalRelay.Platform;

public sealed class AccountAdmin(ContestDb db, UserManager<ContestUser> users, RoleManager<IdentityRole> roles)
{
    public async Task<IdentityResult> CreateAsync(string username, string password, string? teamName, bool judge)
    {
        if (username.Length is < 3 or > 64 || password.Length > 256 || (!judge && (string.IsNullOrWhiteSpace(teamName) || teamName.Length > 80)))
            return IdentityResult.Failed(new IdentityError { Description = "用户名须为3—64字符，队伍名称须为1—80字符。" });
        await using var transaction = await db.Database.BeginTransactionAsync();
        Team? team = null;
        if (!judge)
        {
            teamName = teamName!.Trim();
            team = await db.Teams.SingleOrDefaultAsync(x => x.Name == teamName);
            if (team == null)
            {
                team = new Team { Name = teamName }; db.Teams.Add(team); await db.SaveChangesAsync();
            }
        }
        var account = new ContestUser { UserName = username, TeamId = team?.Id };
        var result = await users.CreateAsync(account, password);
        if (!result.Succeeded) return result;
        if (judge)
        {
            if (!await roles.RoleExistsAsync("Judge"))
            {
                result = await roles.CreateAsync(new IdentityRole("Judge"));
                if (!result.Succeeded) return result;
            }
            result = await users.AddToRoleAsync(account, "Judge");
            if (!result.Succeeded) return result;
        }
        await transaction.CommitAsync();
        return result;
    }
}
