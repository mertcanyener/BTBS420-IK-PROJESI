using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Identity;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
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
builder.Services.AddAuthorization(AuthorizationPolicies.Configure);
builder.Services.Configure<IdentityBootstrapOptions>(
    builder.Configuration.GetSection(IdentityBootstrapOptions.SectionName));
builder.Services.AddScoped<IdentityRoleSeeder>();
builder.Services.AddScoped<InitialAdminSeeder>();
builder.Services.AddScoped<IIdentityDataSeeder, IdentityDataSeeder>();

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
