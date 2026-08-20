using System.Security.Claims;
using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using Microsoft.AspNetCore.Http;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class CurrentActorAccessorTests
{
    [Fact]
    public void GetUserId_AuthenticatedKullanicininNameIdentifierClaiminiDondurur()
    {
        var httpContext = new DefaultHttpContext
        {
            User = CreateAuthenticatedPrincipal(
                new Claim(ClaimTypes.NameIdentifier, "kan23-user"))
        };
        var accessor = new HttpContextCurrentActorAccessor(
            new HttpContextAccessor { HttpContext = httpContext });

        var userId = accessor.GetUserId();

        Assert.Equal("kan23-user", userId);
    }

    [Fact]
    public void GetUserId_NameIdentifierYoksaSubClaiminiDondurur()
    {
        var httpContext = new DefaultHttpContext
        {
            User = CreateAuthenticatedPrincipal(
                new Claim("sub", "kan23-subject"))
        };
        var accessor = new HttpContextCurrentActorAccessor(
            new HttpContextAccessor { HttpContext = httpContext });

        var userId = accessor.GetUserId();

        Assert.Equal("kan23-subject", userId);
    }

    [Fact]
    public void GetUserId_NameIdentifierBossaSubClaiminiDondurur()
    {
        var httpContext = new DefaultHttpContext
        {
            User = CreateAuthenticatedPrincipal(
                new Claim(ClaimTypes.NameIdentifier, " "),
                new Claim("sub", "kan23-subject"))
        };
        var accessor = new HttpContextCurrentActorAccessor(
            new HttpContextAccessor { HttpContext = httpContext });

        var userId = accessor.GetUserId();

        Assert.Equal("kan23-subject", userId);
    }

    [Fact]
    public void GetUserId_AnonymousVeyaHttpContextYoksaActorUretmez()
    {
        var anonymousContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        var anonymousAccessor = new HttpContextCurrentActorAccessor(
            new HttpContextAccessor { HttpContext = anonymousContext });
        var systemAccessor = new HttpContextCurrentActorAccessor(
            new HttpContextAccessor());

        Assert.Null(anonymousAccessor.GetUserId());
        Assert.Null(systemAccessor.GetUserId());
    }

    [Fact]
    public void GetUserId_AuthenticatedKullanicininGuvenilirKimligiYoksaHataVerir()
    {
        var httpContext = new DefaultHttpContext
        {
            User = CreateAuthenticatedPrincipal(
                new Claim(ClaimTypes.Email, "audit@example.invalid"))
        };
        var accessor = new HttpContextCurrentActorAccessor(
            new HttpContextAccessor { HttpContext = httpContext });

        var exception = Assert.Throws<InvalidOperationException>(
            accessor.GetUserId);

        Assert.Contains("kullanıcı kimliği claim'i", exception.Message);
        Assert.DoesNotContain("audit@example.invalid", exception.Message);
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(
        params Claim[] claims)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(
                claims,
                authenticationType: "KAN-23-Test"));
    }
}
