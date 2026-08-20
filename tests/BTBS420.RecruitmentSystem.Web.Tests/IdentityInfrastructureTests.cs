using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class IdentityInfrastructureTests :
    IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public IdentityInfrastructureTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ApplicationUser_VarsayilanOlarakAktiftir()
    {
        var user = new ApplicationUser();

        Assert.True(user.IsActive);
    }

    [Fact]
    public void ApplicationDbContext_IdentityModeliniIcerir()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var userEntity = context.Model.FindEntityType(typeof(ApplicationUser));
        var roleEntity = context.Model.FindEntityType(typeof(IdentityRole));
        var isActiveProperty = userEntity?.FindProperty(nameof(ApplicationUser.IsActive));

        Assert.NotNull(userEntity);
        Assert.Equal("AspNetUsers", userEntity.GetTableName());
        Assert.NotNull(roleEntity);
        Assert.Equal("AspNetRoles", roleEntity.GetTableName());
        Assert.NotNull(isActiveProperty);
        Assert.False(isActiveProperty.IsNullable);
    }

    [Fact]
    public void ApplicationDbContext_AddIdentityInfrastructureMigrationiniIcerir()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Contains(
            context.Database.GetMigrations(),
            migration => migration.EndsWith(
                "_AddIdentityInfrastructure",
                StringComparison.Ordinal));
    }
}
