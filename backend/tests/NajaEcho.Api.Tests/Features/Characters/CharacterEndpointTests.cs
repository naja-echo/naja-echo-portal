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
using NajaEcho.Application.Features.Characters.VerifyCharacter;
using NajaEcho.Domain.Characters;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Characters;

[Collection("ApiTests")]
public sealed class CharacterEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid MemberId = Guid.NewGuid();

    public CharacterEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Discord:ClientId"] = "test-id",
                    ["Discord:ClientSecret"] = "test-secret",
                    ["ConnectionStrings:Default"] = "Host=localhost;Database=test;Username=test;Password=test",
                });
            });

            b.ConfigureTestServices(services =>
            {
                services.StubDatabase();

                services.RemoveAll<IExternalLoginService>();
                services.AddSingleton<IExternalLoginService, CharFakeLoginService>();

                services.RemoveAll<IPendingRegistrationRepository>();
                services.AddSingleton<CharFakePendingRepo>();
                services.AddSingleton<IPendingRegistrationRepository>(sp => sp.GetRequiredService<CharFakePendingRepo>());

                services.RemoveAll<ICharacterRepository>();
                services.AddSingleton<CharFakeCharacterRepo>();
                services.AddSingleton<ICharacterRepository>(sp => sp.GetRequiredService<CharFakeCharacterRepo>());

                services.RemoveAll<IRsiCitizenClient>();
                services.AddSingleton<CharFakeRsiClient>(sp =>
                    new CharFakeRsiClient(sp.GetRequiredService<CharFakePendingRepo>()));
                services.AddSingleton<IRsiCitizenClient>(sp => sp.GetRequiredService<CharFakeRsiClient>());

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, CharTestAuthHandler>(
                        CharTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = CharTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = CharTestAuthHandler.SchemeName;
                });
            });
        });
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient CreateMemberClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", MemberId.ToString());
        return client;
    }

    // ── GET /api/characters/registration ──────────────────────────────────

    [Fact]
    public async Task GetRegistration_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/characters/registration");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRegistration_Authenticated_Returns200()
    {
        var response = await CreateMemberClient().GetAsync("/api/characters/registration");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /api/characters/registration ─────────────────────────────────

    [Fact]
    public async Task PostRegistration_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PostAsync("/api/characters/registration", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostRegistration_Authenticated_Returns200WithTokenShape()
    {
        var response = await CreateMemberClient().PostAsync("/api/characters/registration", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("token");
        body.Should().Contain("expiresAt");
    }

    // ── POST /api/characters/verify ───────────────────────────────────────

    [Fact]
    public async Task PostVerify_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/characters/verify", new { handle = "g8r" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostVerify_EmptyHandle_Returns400()
    {
        var response = await CreateMemberClient().PostAsJsonAsync("/api/characters/verify", new { handle = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostVerify_TokenExpired_Returns409()
    {
        var pendingRepo = _factory.Services.GetRequiredService<CharFakePendingRepo>();
        pendingRepo.SetExpired();

        var response = await CreateMemberClient().PostAsJsonAsync("/api/characters/verify", new { handle = "g8r" });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostVerify_HandleAlreadyClaimed_Returns409()
    {
        var pendingRepo = _factory.Services.GetRequiredService<CharFakePendingRepo>();
        pendingRepo.SetValid();
        var charRepo = _factory.Services.GetRequiredService<CharFakeCharacterRepo>();
        charRepo.HandleExists = true;

        var response = await CreateMemberClient().PostAsJsonAsync("/api/characters/verify", new { handle = "g8r" });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        charRepo.HandleExists = false;
    }

    [Fact]
    public async Task PostVerify_RsiProfileNotFound_Returns404()
    {
        var pendingRepo = _factory.Services.GetRequiredService<CharFakePendingRepo>();
        pendingRepo.SetValid();
        var rsiClient = _factory.Services.GetRequiredService<CharFakeRsiClient>();
        rsiClient.OverrideResult = new RsiProfileNotFound();

        var response = await CreateMemberClient().PostAsJsonAsync("/api/characters/verify", new { handle = "g8r" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        rsiClient.Reset();
    }

    [Fact]
    public async Task PostVerify_RsiUnreachable_Returns502()
    {
        var pendingRepo = _factory.Services.GetRequiredService<CharFakePendingRepo>();
        pendingRepo.SetValid();
        var rsiClient = _factory.Services.GetRequiredService<CharFakeRsiClient>();
        rsiClient.OverrideResult = new RsiUnreachable();

        var response = await CreateMemberClient().PostAsJsonAsync("/api/characters/verify", new { handle = "g8r" });
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        rsiClient.Reset();
    }

    // ── GET /api/characters/ ──────────────────────────────────────────────

    [Fact]
    public async Task GetCharacters_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/characters/");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCharacters_Authenticated_Returns200WithCharactersShape()
    {
        var response = await CreateMemberClient().GetAsync("/api/characters/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("characters");
    }
}

// ── Fakes ─────────────────────────────────────────────────────────────────────

internal sealed class CharFakePendingRepo : IPendingRegistrationRepository
{
    private PendingCharacterRegistration? _stored;
    public string? CurrentToken => _stored?.Token;

    public void SetValid()
    {
        _stored = PendingCharacterRegistration.Create(Guid.NewGuid(), DateTimeOffset.UtcNow);
    }

    public void SetExpired()
    {
        _stored = PendingCharacterRegistration.Create(Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(-2));
    }

    public Task<PendingCharacterRegistration?> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct)
        => Task.FromResult(_stored);

    public Task UpsertAsync(PendingCharacterRegistration pending, CancellationToken ct)
    {
        _stored = pending;
        return Task.CompletedTask;
    }

    public Task RemoveByOwnerAsync(Guid ownerUserId, CancellationToken ct)
    {
        _stored = null;
        return Task.CompletedTask;
    }
}

internal sealed class CharFakeCharacterRepo : ICharacterRepository
{
    public bool HandleExists { get; set; }

    public Task<bool> HandleExistsAsync(string handle, CancellationToken ct) => Task.FromResult(HandleExists);

    public Task AddAsync(Character character, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<Character>> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Character>>([]);
}

internal sealed class CharFakeRsiClient(CharFakePendingRepo pendingRepo) : IRsiCitizenClient
{
    public object? OverrideResult { get; set; }

    public void Reset() => OverrideResult = null;

    public Task<object> FetchCitizenAsync(string handle, CancellationToken ct)
    {
        var result = OverrideResult
            ?? new RsiCitizenPage($"page content with {pendingRepo.CurrentToken} embedded", "DisplayMoniker");
        return Task.FromResult(result);
    }
}

internal sealed class CharFakeLoginService : IExternalLoginService
{
    public Task<NajaEcho.Application.Abstractions.LocalUser> FindOrCreateAsync(
        NajaEcho.Domain.Users.DiscordProfile profile, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<NajaEcho.Application.Abstractions.LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => throw new NotImplementedException();
}

internal sealed class CharTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "CharTestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "Test User"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
