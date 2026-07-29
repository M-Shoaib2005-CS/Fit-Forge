using FitForge.BL; using FitForge.DL; using FitForge.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;

// Must run before CreateBuilder(): stops the config system from creating a
// FileSystemWatcher (inotify instance) to watch appsettings.json for live edits.
// We don't need hot-reload in production, and on shared hosts like Render the
// inotify instance cap (128) is shared across every tenant on the physical
// machine — so whether this app can even start depends on what else happens
// to be running on that host at that moment. That's why it crashed on some
// restarts/redeploys and not others. Removing the watcher removes the failure
// mode entirely, rather than hoping there's room under someone else's limit.
Environment.SetEnvironmentVariable("DOTNET_hostBuilder:reloadConfigOnChange", "false");

var builder = WebApplication.CreateBuilder(args);
string cs = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection missing");
DB.Configure(cs);
builder.Services.AddDataProtection().SetApplicationName("FitForge");
builder.Services.Configure<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(o =>
    o.XmlRepository = new DbXmlRepository());
// Persistent login: the user's identity is encrypted straight into the cookie,
// so login survives server restarts/redeploys (unlike the old Session-only approach).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o => {
        o.LoginPath = "/Account/Login";
        o.ExpireTimeSpan = TimeSpan.FromDays(60);
        o.SlidingExpiration = true; // resets the 60-day clock on activity
        o.Cookie.HttpOnly = true;
        o.Cookie.IsEssential = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o=>o.JsonSerializerOptions.PropertyNamingPolicy=System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o=>{o.IdleTimeout=TimeSpan.FromHours(8);o.Cookie.HttpOnly=true;o.Cookie.IsEssential=true;});
builder.Logging.ClearProviders(); builder.Logging.AddConsole(); builder.Logging.AddDebug();
builder.Services.AddHttpContextAccessor();
// DL
builder.Services.AddScoped<UserDL>(); builder.Services.AddScoped<ExerciseDL>();
builder.Services.AddScoped<ProgramDL>(); builder.Services.AddScoped<ScheduleDL>();
builder.Services.AddScoped<WorkoutDL>(); builder.Services.AddScoped<AdaptiveDL>();
builder.Services.AddScoped<PersonalRecordDL>(); builder.Services.AddScoped<StreakDL>();
builder.Services.AddScoped<SkillDL>(); builder.Services.AddScoped<InjuryDL>();
builder.Services.AddScoped<WaterDL>(); builder.Services.AddScoped<MeasurementDL>();
builder.Services.AddScoped<AchievementDL>(); builder.Services.AddScoped<CalendarDL>();
builder.Services.AddScoped<NotificationDL>();
// BL
builder.Services.AddScoped<UserBL>(); builder.Services.AddScoped<WorkoutBL>();
builder.Services.AddScoped<ProgramBL>(); builder.Services.AddScoped<SkillBL>();
builder.Services.AddScoped<ProfileBL>(); builder.Services.AddScoped<AchievementBL>();
builder.Services.AddScoped<NotificationBL>();
// Services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
// NotificationBackgroundService (in-process timer) was removed — on free-tier hosts the
// whole process (and any in-memory timer with it) gets suspended after ~15 min idle, so an
// in-process loop can't be relied on to ever fire. Replaced by NotificationsController.Tick,
// triggered by an external scheduler (e.g. cron-job.org) hitting the app's public URL —
// works the same way regardless of hosting tier, since each hit both wakes the app (if
// asleep) and runs the check.
builder.Services.AddHttpClient<GeminiService>(c => c.Timeout = TimeSpan.FromSeconds(25));
var app = builder.Build();

// One-time visible-at-startup check — tells you immediately on deploy whether the Vapid
// keys actually loaded, instead of only finding out 1-2 background ticks later.
using(var scope = app.Services.CreateScope())
{
    var pushCheck = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
    var startupLog = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    if(pushCheck.IsConfigured)
        startupLog.LogInformation("Push notifications: configured, public key ends '...{Tail}'",
            pushCheck.PublicKey.Length>=6?pushCheck.PublicKey[^6..]:pushCheck.PublicKey);
    else
        startupLog.LogWarning("Push notifications: NOT configured — Vapid:PublicKey/PrivateKey missing or empty. Check Vapid__PublicKey / Vapid__PrivateKey env vars.");
}
app.UseForwardedHeaders(new ForwardedHeadersOptions{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    // Render's proxy IPs aren't fixed, so clear the default restriction that would otherwise ignore the header
    KnownNetworks = { },
    KnownProxies = { }
});
if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/Home/Error");app.UseHsts();}
else{app.UseDeveloperExceptionPage();}
app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseRouting(); app.UseSession(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllerRoute("default","{controller=Account}/{action=Login}/{id?}");
app.Run();
