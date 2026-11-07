using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TuringSignalRelay.Evaluation;
using TuringSignalRelay.Platform;

var builder = WebApplication.CreateBuilder(args);
var paths = new ContestPaths(); paths.Initialize();
string keys = paths.File("keys"); Directory.CreateDirectory(keys);
if (OperatingSystem.IsLinux()) File.SetUnixFileMode(keys, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
builder.Services.AddSingleton(paths);
builder.Services.AddDbContext<ContestDb>(options => options.UseSqlite(paths.ConnectionString));
builder.Services.AddIdentity<ContestUser, IdentityRole>(options => {
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<ContestDb>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options => {
    options.Cookie.Name = "tsr.session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.LoginPath = "/Login";
    options.Events.OnRedirectToLogin = context => {
        if (context.Request.Path.StartsWithSegments("/api")) context.Response.StatusCode = 401;
        else context.Response.Redirect("/Login");
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddDataProtection().SetApplicationName("TuringSignalRelay.Contest").PersistKeysToFileSystem(new DirectoryInfo(keys));
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddRazorPages(options => { options.Conventions.AuthorizeFolder("/"); options.Conventions.AllowAnonymousToPage("/Login"); })
    .AddMvcOptions(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddScoped<ContestOperations>();
builder.Services.AddScoped<AccountAdmin>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.IncludeFields = true);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 300 * 1024);
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ContestDb>();
    await ContestDatabase.UpgradeAsync(db, paths);
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    if (args.Length == 1 && args[0] == "migrate")
    {
        Console.WriteLine("CONTEST_DATABASE_MIGRATED");
        return;
    }
    if (args.Length == 2 && args[0] == "create-judge")
    {
        string password = Console.ReadLine() ?? "";
        var result = await scope.ServiceProvider.GetRequiredService<AccountAdmin>().CreateAsync(args[1], password, null, true);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        Console.WriteLine("JUDGE_ACCOUNT_CREATED " + args[1]);
        return;
    }
    if (args.Length == 2 && args[0] == "backup")
    {
        string destination = Path.GetFullPath(args[1]);
        if (File.Exists(destination)) throw new IOException("Use a new backup file.");
        await db.Database.OpenConnectionAsync();
        using var backup = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = destination }.ToString());
        backup.Open();
        ((Microsoft.Data.Sqlite.SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(backup);
        Console.WriteLine("DATABASE_BACKUP_COMPLETED");
        return;
    }
}
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; script-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
ContestApi.Map(app);
app.Run();
