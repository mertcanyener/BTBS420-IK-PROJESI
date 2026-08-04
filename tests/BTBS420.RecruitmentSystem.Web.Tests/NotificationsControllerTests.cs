using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Controllers;
using BTBS420.RecruitmentSystem.Web.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class NotificationsControllerTests
{
    [Fact]
    public async Task Index_AnonimKullaniciyiReddederVeServisiCagirmaz()
    {
        var state = new FakeNotificationState();
        using var factory = CreateFactory(state);
        using var client = CreateClient(factory);

        var response = await client.GetAsync("/Notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(state.ObservedUserIds);
        Assert.Equal(0, state.GetNotificationsCallCount);
    }

    [Fact]
    public async Task Index_ClaimsKullanicisiniKullanirBadgeGosterirVeMetniEncodeEder()
    {
        var state = new FakeNotificationState
        {
            UnreadCount = 3,
            Notifications =
            [
                new NotificationListItem(
                    30,
                    "<script>alert('başlık')</script>",
                    "<img src=x onerror=alert('mesaj')>",
                    new DateTimeOffset(
                        2026,
                        7,
                        25,
                        10,
                        30,
                        0,
                        TimeSpan.Zero),
                    null)
            ]
        };
        using var factory = CreateFactory(state);
        using var client = CreateClient(factory);
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/Notifications",
            "candidate-kan30");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Bildirimler", content);
        Assert.Matches(
            "aria-label=\"Bildirimler, 3 [^\"]+\"",
            content);
        Assert.Contains("&lt;script&gt;", content);
        Assert.Contains("&lt;img", content);
        Assert.DoesNotContain("<script>alert", content);
        Assert.DoesNotContain("<img src=x", content);
        Assert.True(state.GetNotificationsCallCount > 0);
        Assert.True(state.GetUnreadCountCallCount > 0);
        Assert.All(
            state.ObservedUserIds,
            userId => Assert.Equal("candidate-kan30", userId));
    }

    [Fact]
    public async Task Layout_BildirimBaglantisiniYalnizAuthenticatedKullaniciyaGosterir()
    {
        var state = new FakeNotificationState { UnreadCount = 2 };
        using var factory = CreateFactory(state);
        using var client = CreateClient(factory);

        var anonymousResponse = await client.GetAsync("/");
        var anonymousContent =
            await anonymousResponse.Content.ReadAsStringAsync();

        using var authenticatedRequest = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/",
            "layout-user-kan30");
        var authenticatedResponse = await client.SendAsync(authenticatedRequest);
        var authenticatedContent =
            await authenticatedResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, anonymousResponse.StatusCode);
        Assert.DoesNotContain("href=\"/Notifications\"", anonymousContent);
        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
        Assert.Contains("href=\"/Notifications\"", authenticatedContent);
        Assert.Matches(
            "aria-label=\"Bildirimler, 2 [^\"]+\"",
            authenticatedContent);
        Assert.Contains("layout-user-kan30", state.ObservedUserIds);
    }

    [Fact]
    public async Task MarkAsRead_GecerliAntiforgeryIleTekBildirimiIsaretler()
    {
        var state = CreateStateWithUnreadNotification();
        using var factory = CreateFactory(state);
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(
            client,
            "reader-kan30");
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/Notifications/MarkAsRead/42",
            "reader-kan30");
        request.Content = CreateAntiforgeryContent(token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Notifications", response.Headers.Location?.OriginalString);
        Assert.Equal([42L], state.MarkAsReadIds);
        Assert.Equal("reader-kan30", state.MarkAsReadUserId);
    }

    [Fact]
    public async Task MarkAsRead_BaskaKullaniciyaAitVeyaBulunmayanKaydi404Yapar()
    {
        var state = CreateStateWithUnreadNotification();
        state.MarkAsReadResult = false;
        using var factory = CreateFactory(state);
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(
            client,
            "foreign-reader-kan30");
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/Notifications/MarkAsRead/9001",
            "foreign-reader-kan30");
        request.Content = CreateAntiforgeryContent(token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([9001L], state.MarkAsReadIds);
        Assert.Equal("foreign-reader-kan30", state.MarkAsReadUserId);
    }

    [Fact]
    public async Task MarkAsRead_ServiceFalseOldugundaNotFoundActionResultDondurur()
    {
        var state = new FakeNotificationState { MarkAsReadResult = false };
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(
                            ClaimTypes.NameIdentifier,
                            "not-found-user-kan30")
                    ],
                    TestAuthenticationHandler.SchemeName))
        };
        var service = new ClaimAwareFakeNotificationCenterService(
            new HttpContextAccessor { HttpContext = httpContext },
            state);
        var controller = new NotificationsController(service);

        var result = await controller.MarkAsRead(
            long.MaxValue,
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal([long.MaxValue], state.MarkAsReadIds);
    }

    [Fact]
    public async Task MarkAllAsRead_GecerliAntiforgeryIleCurrentUserIsleminiCagirir()
    {
        var state = CreateStateWithUnreadNotification();
        state.MarkAllAsReadResult = 4;
        using var factory = CreateFactory(state);
        using var client = CreateClient(factory);
        var token = await GetAntiforgeryTokenAsync(
            client,
            "mark-all-user-kan30");
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/Notifications/MarkAllAsRead",
            "mark-all-user-kan30");
        request.Content = CreateAntiforgeryContent(token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Notifications", response.Headers.Location?.OriginalString);
        Assert.Equal(1, state.MarkAllAsReadCallCount);
        Assert.Equal("mark-all-user-kan30", state.MarkAllAsReadUserId);
    }

    [Theory]
    [InlineData("/Notifications/MarkAsRead/42")]
    [InlineData("/Notifications/MarkAllAsRead")]
    public async Task BildirimMutationEndpointleri_AntiforgeryTokenOlmadanReddeder(
        string path)
    {
        var state = CreateStateWithUnreadNotification();
        using var factory = CreateFactory(state);
        using var client = CreateClient(factory);
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            path,
            "csrf-user-kan30");
        request.Content = new FormUrlEncodedContent([]);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(state.MarkAsReadIds);
        Assert.Equal(0, state.MarkAllAsReadCallCount);
    }

    [Fact]
    public void Controller_AuthorizationOwnershipVeAntiforgerySozlesmeleriniTasir()
    {
        var controllerType = typeof(NotificationsController);
        var index = controllerType.GetMethod(nameof(NotificationsController.Index))!;
        var markAsRead =
            controllerType.GetMethod(nameof(NotificationsController.MarkAsRead))!;
        var markAllAsRead =
            controllerType.GetMethod(nameof(NotificationsController.MarkAllAsRead))!;

        Assert.NotNull(controllerType.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(index.GetCustomAttribute<HttpGetAttribute>());
        var responseCache =
            index.GetCustomAttribute<ResponseCacheAttribute>();
        Assert.NotNull(responseCache);
        Assert.True(responseCache.NoStore);
        Assert.Equal(ResponseCacheLocation.None, responseCache.Location);

        Assert.NotNull(markAsRead.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(
            markAsRead.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal(
            ["id", "cancellationToken"],
            markAsRead.GetParameters().Select(parameter => parameter.Name));
        Assert.DoesNotContain(
            markAsRead.GetParameters(),
            parameter => parameter.ParameterType == typeof(string));

        Assert.NotNull(markAllAsRead.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(
            markAllAsRead.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal(
            ["cancellationToken"],
            markAllAsRead.GetParameters().Select(parameter => parameter.Name));
    }

    private WebApplicationFactory<Program> CreateFactory(
        FakeNotificationState state)
    {
        return new TestWebApplicationFactory(
            serviceProvider =>
                new ClaimAwareFakeNotificationCenterService(
                    serviceProvider.GetRequiredService<IHttpContextAccessor>(),
                    state));
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true
            });
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string path,
        string userId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(
            TestAuthenticationHandler.RoleHeaderName,
            SystemRoles.Candidate);
        request.Headers.Add(
            TestAuthenticationHandler.UserIdHeaderName,
            userId);
        return request;
    }

    private static async Task<string> GetAntiforgeryTokenAsync(
        HttpClient client,
        string userId)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/Notifications",
            userId);
        using var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokenMatch = Regex.Match(
            content,
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);
        Assert.True(tokenMatch.Success, "Antiforgery form alanı bulunamadı.");

        return WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
    }

    private static FormUrlEncodedContent CreateAntiforgeryContent(string token)
    {
        return new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            });
    }

    private static FakeNotificationState CreateStateWithUnreadNotification()
    {
        return new FakeNotificationState
        {
            UnreadCount = 1,
            Notifications =
            [
                new NotificationListItem(
                    42,
                    "Okunmamış bildirim",
                    "Bildirim metni",
                    new DateTimeOffset(
                        2026,
                        7,
                        25,
                        9,
                        0,
                        0,
                        TimeSpan.Zero),
                    null)
            ]
        };
    }

    private sealed class FakeNotificationState
    {
        public IReadOnlyList<NotificationListItem> Notifications { get; set; } = [];

        public int UnreadCount { get; set; }

        public bool MarkAsReadResult { get; set; } = true;

        public int MarkAllAsReadResult { get; set; }

        public int GetNotificationsCallCount { get; set; }

        public int GetUnreadCountCallCount { get; set; }

        public List<string?> ObservedUserIds { get; } = [];

        public List<long> MarkAsReadIds { get; } = [];

        public string? MarkAsReadUserId { get; set; }

        public int MarkAllAsReadCallCount { get; set; }

        public string? MarkAllAsReadUserId { get; set; }
    }

    private sealed class ClaimAwareFakeNotificationCenterService(
        IHttpContextAccessor httpContextAccessor,
        FakeNotificationState state) : INotificationCenterService
    {
        public Task<IReadOnlyList<NotificationListItem>> GetNotificationsAsync(
            CancellationToken cancellationToken = default)
        {
            state.GetNotificationsCallCount++;
            state.ObservedUserIds.Add(GetCurrentUserId());
            return Task.FromResult(state.Notifications);
        }

        public Task<int> GetUnreadCountAsync(
            CancellationToken cancellationToken = default)
        {
            state.GetUnreadCountCallCount++;
            state.ObservedUserIds.Add(GetCurrentUserId());
            return Task.FromResult(state.UnreadCount);
        }

        public Task<bool> MarkAsReadAsync(
            long notificationId,
            CancellationToken cancellationToken = default)
        {
            state.MarkAsReadIds.Add(notificationId);
            state.MarkAsReadUserId = GetCurrentUserId();
            return Task.FromResult(state.MarkAsReadResult);
        }

        public Task<int> MarkAllAsReadAsync(
            CancellationToken cancellationToken = default)
        {
            state.MarkAllAsReadCallCount++;
            state.MarkAllAsReadUserId = GetCurrentUserId();
            return Task.FromResult(state.MarkAllAsReadResult);
        }

        private string? GetCurrentUserId()
        {
            return httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}
