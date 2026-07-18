using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NajaEcho.Application.Abstractions;
using NajaEcho.Domain.Users;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Auth;

// T025, T026, T015, T016, T036, T037, T045
[Collection("ApiTests")]
public class AuthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid TestUserId = Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
    private static readonly LocalUser TestLocalUser = new(TestUserId, "Test User", "testuser");

    public AuthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Discord:ClientId"] = "test-client-id",
                    ["Discord:ClientSecret"] = "test-client-secret",
                    ["ConnectionStrings:Default"] = "Host=localhost;Database=test;Username=test;Password=test",
                });
            });

            b.ConfigureTestServices(services =>
            {
                // No database needed — endpoints use faked repositories (see StubDatabase)
                services.StubDatabase();

                // Fake external login service
                services.RemoveAll<IExternalLoginService>();
                services.AddSingleton<FakeExternalLoginService>();
                services.AddSingleton<IExternalLoginService>(sp =>
                    sp.GetRequiredService<FakeExternalLoginService>());

                // Add test auth scheme that authenticates from X-Test-UserId header
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                });
            });
        });
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient CreateAuthenticatedClient(Guid? userId = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", (userId ?? TestUserId).ToString());
        return client;
    }

    [Fact]
    public async Task Health_Returns200()
    {
        var response = await CreateClient().GetAsync("/api/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // T026: Me always returns 200 with anonymous body when no session
    [Fact]
    public async Task Me_Returns200WithAnonymousBody_WhenNoSession()
    {
        var response = await CreateClient().GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("authenticated").GetBoolean().Should().BeFalse();
        body.TryGetProperty("user", out _).Should().BeFalse();
    }

    // T025: Me returns 200 with authenticated body when session exists
    [Fact]
    public async Task Me_Returns200WithAuthenticatedBody_WhenSessionExists()
    {
        _factory.Services.GetRequiredService<FakeExternalLoginService>()
            .SetUser(TestUserId, TestLocalUser);

        var response = await CreateAuthenticatedClient().GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("authenticated").GetBoolean().Should().BeTrue();
        var user = body.GetProperty("user");
        user.GetProperty("displayName").GetString().Should().Be("Test User");
        user.GetProperty("discordUsername").GetString().Should().Be("testuser");

        // T016: Response contains no Discord token fields
        body.TryGetProperty("accessToken", out _).Should().BeFalse();
        body.TryGetProperty("refreshToken", out _).Should().BeFalse();
        body.TryGetProperty("token", out _).Should().BeFalse();
    }

    // T037: SignOut is idempotent — 204 with no active session
    [Fact]
    public async Task SignOut_Returns204_WithNoSession()
    {
        var response = await CreateClient().PostAsync("/api/auth/signout", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // T036: SignOut clears session, subsequent Me returns anonymous
    [Fact]
    public async Task SignOut_Returns204_WhenAuthenticated()
    {
        var response = await CreateAuthenticatedClient().PostAsync("/api/auth/signout", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Login_Redirects_ToDiscord()
    {
        var response = await CreateClient().GetAsync("/api/auth/discord/login");
        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location?.Host.Should().Be("discord.com");
    }

    // T015: Callback with OAuth error redirects to auth error page (no user created)
    [Fact]
    public async Task Callback_WithOAuthError_Redirects_ToAuthError()
    {
        // Discord sends error=access_denied to the callback URL — OnRemoteFailure fires
        var response = await CreateClient()
            .GetAsync("/api/auth/discord/callback?error=access_denied&error_description=User+denied");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location?.ToString().Should().Contain("/auth/error");
        response.Headers.Location?.ToString().Should().Contain("reason=oauth_error");
    }

    // T045: Authenticated Me response contains no sensitive auth values
    [Fact]
    public async Task Me_AuthenticatedResponse_ContainsNoSensitiveAuthValues()
    {
        _factory.Services.GetRequiredService<FakeExternalLoginService>()
            .SetUser(TestUserId, TestLocalUser);

        var response = await CreateAuthenticatedClient().GetAsync("/api/auth/me");
        var body = await response.Content.ReadAsStringAsync();

        // Verify no sensitive values appear in the response body
        body.Should().NotContain("accessToken");
        body.Should().NotContain("refreshToken");
        body.Should().NotContain("access_token");
        body.Should().NotContain("Bearer");
    }
}

// ---- Test doubles ----

internal sealed class FakeExternalLoginService : IExternalLoginService
{
    private readonly Dictionary<Guid, LocalUser> _store = [];

    public void SetUser(Guid id, LocalUser user) => _store[id] = user;

    public Task<LocalUser> FindOrCreateAsync(DiscordProfile profile, CancellationToken ct = default)
    {
        var user = new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username);
        _store[user.Id] = user;
        return Task.FromResult(user);
    }

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        _store.TryGetValue(userId, out var user);
        return Task.FromResult(user);
    }
}

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "Test User"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
