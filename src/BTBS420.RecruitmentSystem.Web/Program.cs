using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Ai;
using BTBS420.RecruitmentSystem.Web.Ai.Evaluation;
using BTBS420.RecruitmentSystem.Web.Ai.Evaluation.PositionAnalysis;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Identity;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.Notifications;
using BTBS420.RecruitmentSystem.Web.PasswordReset;
using BTBS420.RecruitmentSystem.Web.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "SQL Server bağlantısı yapılandırılmadı. " +
            "ConnectionStrings__DefaultConnection ortam değişkenini ayarlayın.");
    }

    options.UseSqlServer(connectionString);
});
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});
builder.Services.AddScoped<ISecurityStampValidator, ApplicationSecurityStampValidator>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Error/403";
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddAuthorization(AuthorizationPolicies.Configure);
builder.Services.Configure<IdentityBootstrapOptions>(
    builder.Configuration.GetSection(IdentityBootstrapOptions.SectionName));
builder.Services.AddScoped<IdentityRoleSeeder>();
builder.Services.AddScoped<InitialAdminSeeder>();
builder.Services.AddScoped<SampleRecruiterSeeder>();
builder.Services.AddScoped<SampleHiringManagerSeeder>();
builder.Services.AddScoped<LookupDataSeeder>();
builder.Services.AddScoped<SampleCandidateSeeder>();
builder.Services.AddScoped<IIdentityDataSeeder, IdentityDataSeeder>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IActivityLogRedactor, ActivityLogRedactor>();
builder.Services.AddScoped<ICurrentActorAccessor, HttpContextCurrentActorAccessor>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<IPasswordResetSender, NoOpPasswordResetSender>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationPublisher>(
    serviceProvider => serviceProvider.GetRequiredService<NotificationService>());
builder.Services.AddScoped<INotificationCenterService>(
    serviceProvider => serviceProvider.GetRequiredService<NotificationService>());
builder.Services.Configure<CandidateDocumentStorageOptions>(
    builder.Configuration.GetSection(CandidateDocumentStorageOptions.SectionName));
builder.Services.AddScoped<ICandidateDocumentStorageService, FileSystemCandidateDocumentStorageService>();
builder.Services.AddScoped<IRecruitmentScopeService, RecruitmentScopeService>();
builder.Services.Configure<AiEvaluationOptions>(
    builder.Configuration.GetSection(AiEvaluationOptions.SectionName));
builder.Services.AddScoped<IAiEvaluationClient, NoOpAiEvaluationClient>();
builder.Services.AddScoped<IPositionAnalysisService, PositionAnalysisService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var identityDataSeeder =
        scope.ServiceProvider.GetRequiredService<IIdentityDataSeeder>();
    await identityDataSeeder.SeedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/500");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program;
