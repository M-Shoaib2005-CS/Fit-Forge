using FitForge.BL; using FitForge.DL; using FitForge.Services;
var builder = WebApplication.CreateBuilder(args);
string cs = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection missing");
DB.Configure(cs);
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
// BL
builder.Services.AddScoped<UserBL>(); builder.Services.AddScoped<WorkoutBL>();
builder.Services.AddScoped<ProgramBL>(); builder.Services.AddScoped<SkillBL>();
builder.Services.AddScoped<ProfileBL>(); builder.Services.AddScoped<AchievementBL>();
// Services
builder.Services.AddScoped<IEmailService, EmailService>();
var app = builder.Build();
if(!app.Environment.IsDevelopment()){app.UseExceptionHandler("/Home/Error");app.UseHsts();}
else{app.UseDeveloperExceptionPage();}
app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseRouting(); app.UseSession(); app.UseAuthorization();
app.MapControllerRoute("default","{controller=Account}/{action=Login}/{id?}");
app.Run();
