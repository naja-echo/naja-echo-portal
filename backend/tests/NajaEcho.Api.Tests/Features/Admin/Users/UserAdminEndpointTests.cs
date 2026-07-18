using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Admin.Users.AddCharacterForUser;
using NajaEcho.Application.Features.Admin.Users.AssignRoles;
using NajaEcho.Application.Features.Admin.Users.GetUsers;
using NajaEcho.Application.Features.Characters.VerifyCharacter;
using NajaEcho.Domain.Characters;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Admin.Users;

[Collection("ApiTests")]
public sealed class UserAdminEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid TargetUserId = Guid.NewGuid();

    public UserAdminEndpointTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IExternalLoginService, UserAdminFakeLoginService>();

                services.RemoveAll<IUserRepository>();
                services.AddSingleton<UserAdminFakeUserRepo>();
                services.AddSingleton<IUserRepository>(sp => sp.GetRequiredService<UserAdminFakeUserRepo>());

                services.RemoveAll<ICharacterRepository>();
                services.AddSingleton<UserAdminFakeCharacterRepo>();
                services.AddSingleton<ICharacterRepository>(sp => sp.GetRequiredService<UserAdminFakeCharacterRepo>());

                services.RemoveAll<IRsiCitizenClient>();
                services.AddSingleton<UserAdminFakeRsiClient>();
                services.AddSingleton<IRsiCitizenClient>(sp => sp.GetRequiredService<UserAdminFakeRsiClient>());

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, UserAdminTestAuthHandler>(
                        UserAdminTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = UserAdminTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = UserAdminTestAuthHandler.SchemeName;
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
        client.DefaultRequestHeaders.Add("X-Test-Roles", "");
        return client;
    }

    private HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", AdminId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return client;
    }

    // ── GET /api/admin/users ──────────────────────────────────────────────

    [Fact]
    public async Task GetAdminUsers_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAdminUsers_NonAdmin_Returns403()
    {
        var response = await CreateMemberClient().GetAsync("/api/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAdminUsers_Admin_Returns200WithUsers()
    {
        var repo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        repo.Users =
        [
            new AdminUserDto(Guid.NewGuid(), "testuser", [], []),
        ];

        var response = await CreateAdminClient().GetAsync("/api/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("users");
    }

    // ── POST /api/admin/users/{userId}/characters ─────────────────────────

    [Fact]
    public async Task AddCharacterForUser_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PostAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/characters", new { handle = "somehandle" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddCharacterForUser_NonAdmin_Returns403()
    {
        var response = await CreateMemberClient().PostAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/characters", new { handle = "somehandle" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddCharacterForUser_BlankHandle_Returns400()
    {
        var repo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        repo.ExistsResult = true;

        var response = await CreateAdminClient().PostAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/characters", new { handle = "" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddCharacterForUser_UserNotFound_Returns404WithCorrectType()
    {
        var repo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        repo.ExistsResult = false;

        var response = await CreateAdminClient().PostAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/characters", new { handle = "somehandle" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("urn:najaecho:error:user-not-found");
    }

    [Fact]
    public async Task AddCharacterForUser_AlreadyClaimed_Returns409()
    {
        var userRepo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        userRepo.ExistsResult = true;
        var charRepo = _factory.Services.GetRequiredService<UserAdminFakeCharacterRepo>();
        charRepo.HandleExists = true;

        var response = await CreateAdminClient().PostAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/characters", new { handle = "claimedhandle" });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        charRepo.HandleExists = false;
    }

    [Fact]
    public async Task AddCharacterForUser_RsiNotFound_Returns404WithRsiType()
    {
        var userRepo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        userRepo.ExistsResult = true;
        var rsiClient = _factory.Services.GetRequiredService<UserAdminFakeRsiClient>();
        rsiClient.OverrideResult = new RsiProfileNotFound();

        var response = await CreateAdminClient().PostAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/characters", new { handle = "unknownhandle" });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("urn:najaecho:error:rsi-handle-not-found");

        rsiClient.Reset();
    }

    [Fact]
    public async Task AddCharacterForUser_RsiUnreachable_Returns502()
    {
        var userRepo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        userRepo.ExistsResult = true;
        var rsiClient = _factory.Services.GetRequiredService<UserAdminFakeRsiClient>();
        rsiClient.OverrideResult = new RsiUnreachable();

        var response = await CreateAdminClient().PostAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/characters", new { handle = "somehandle" });
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        rsiClient.Reset();
    }

    [Fact]
    public async Task AddCharacterForUser_NameNotExtractable_Returns422()
    {
        var userRepo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        userRepo.ExistsResult = true;
        var rsiClient = _factory.Services.GetRequiredService<UserAdminFakeRsiClient>();
        rsiClient.OverrideResult = new RsiCitizenPage("some content", null);

        var response = await CreateAdminClient().PostAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/characters", new { handle = "somehandle" });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        rsiClient.Reset();
    }

    [Fact]
    public async Task AddCharacterForUser_Success_Returns201WithCharacter()
    {
        var userRepo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        userRepo.ExistsResult = true;
        var rsiClient = _factory.Services.GetRequiredService<UserAdminFakeRsiClient>();
        rsiClient.OverrideResult = new RsiCitizenPage("content", "MyCharName");

        var response = await CreateAdminClient().PostAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/characters", new { handle = "newhandle" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("MyCharName");

        rsiClient.Reset();
    }

    // ── PUT /api/admin/users/{userId}/roles ───────────────────────────────

    [Fact]
    public async Task AssignRoles_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/roles", new { roles = new[] { "Admin" } });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignRoles_NonAdmin_Returns403()
    {
        var response = await CreateMemberClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/roles", new { roles = new[] { "Admin" } });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignRoles_UserNotFound_Returns404()
    {
        var repo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        repo.ExistsResult = false;

        var response = await CreateAdminClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/roles", new { roles = new[] { "Admin" } });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("urn:najaecho:error:user-not-found");

        repo.ExistsResult = true;
    }

    [Fact]
    public async Task AssignRoles_InvalidRole_Returns400()
    {
        var repo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        repo.ExistsResult = true;

        var response = await CreateAdminClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/roles", new { roles = new[] { "NotARealRole" } });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignRoles_ValidRoles_Returns204()
    {
        var repo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        repo.ExistsResult = true;

        var response = await CreateAdminClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/roles", new { roles = new[] { "Admin", "Quartermaster" } });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssignRoles_EmptyRoles_Returns204()
    {
        var repo = _factory.Services.GetRequiredService<UserAdminFakeUserRepo>();
        repo.ExistsResult = true;

        var response = await CreateAdminClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/roles", new { roles = Array.Empty<string>() });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

// ── Fakes ─────────────────────────────────────────────────────────────────────

internal sealed class UserAdminFakeUserRepo : IUserRepository
{
    public bool ExistsResult { get; set; } = true;
    public IReadOnlyList<AdminUserDto> Users { get; set; } = [];
    public IReadOnlyList<string>? LastSetRoles { get; private set; }

    public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(ExistsResult);
    public Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
    public Task<IReadOnlyList<AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct)
        => Task.FromResult(Users);
    public Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct)
    {
        if (!ExistsResult) throw new UserNotFoundException(userId);
        LastSetRoles = roles;
        return Task.CompletedTask;
    }
}

internal sealed class UserAdminFakeCharacterRepo : ICharacterRepository
{
    public bool HandleExists { get; set; }

    public Task<bool> HandleExistsAsync(string handle, CancellationToken ct) => Task.FromResult(HandleExists);
    public Task AddAsync(Character character, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<Character>> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Character>>([]);
}

internal sealed class UserAdminFakeRsiClient : IRsiCitizenClient
{
    public object? OverrideResult { get; set; }
    public void Reset() => OverrideResult = null;
    public Task<object> FetchCitizenAsync(string handle, CancellationToken ct)
        => Task.FromResult(OverrideResult ?? (object)new RsiCitizenPage("content", "DefaultName"));
}

internal sealed class UserAdminFakeLoginService : IExternalLoginService
{
    public Task<NajaEcho.Application.Abstractions.LocalUser> FindOrCreateAsync(
        NajaEcho.Domain.Users.DiscordProfile profile, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<NajaEcho.Application.Abstractions.LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default)
        => throw new NotImplementedException();
}

internal sealed class UserAdminTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "UserAdminTestScheme";

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

        if (Request.Headers.TryGetValue("X-Test-Roles", out var rolesHeader))
        {
            foreach (var role in rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
