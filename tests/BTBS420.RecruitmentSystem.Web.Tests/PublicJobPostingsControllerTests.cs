using System.Reflection;
using BTBS420.RecruitmentSystem.Web.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class PublicJobPostingsControllerTests
{
    [Fact]
    public void Controller_AnonimErisimeIzinVerirVeYetkiGerektirmez()
    {
        var controllerType = typeof(PublicJobPostingsController);

        Assert.NotNull(controllerType.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(controllerType.GetCustomAttribute<AuthorizeAttribute>());
    }
}
