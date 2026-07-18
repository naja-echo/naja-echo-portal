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
using NajaEcho.Application.Features.Loot.GetDistribution;
using NajaEcho.Application.Features.Loot.GetMemberLedger;
using NajaEcho.Domain.Loot;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Loot;

[Collection("ApiTests")]
public sealed class LootEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid CroId = Guid.NewGuid();
    private static readonly Guid QuartermasterId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    public LootEndpointsTests(WebApplicationFactory<Program> factory)
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
                services.ReplaceWithInMemoryDb("LootTestDb_" + Guid.NewGuid());

                services.RemoveAll<IExternalLoginService>();
                services.AddSingleton<IExternalLoginService, LootFakeLoginService>();

                services.RemoveAll<ILootLedgerRepository>();
                services.AddSingleton<ILootLedgerRepository>(new LootFakeRepo(MemberId));

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, LootTestAuthHandler>(
                        LootTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = LootTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = LootTestAuthHandler.SchemeName;
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

    private HttpClient CreateCroClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", CroId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "CrewResourceOfficer");
        return client;
    }

    private HttpClient CreateQuartermasterClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", QuartermasterId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Quartermaster");
        return client;
    }

    private HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", AdminId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return client;
    }

    // ── GET /api/loot/me ─────────────────────────────────────────────────

    [Fact]
    public async Task GetMyLedger_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/loot/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyLedger_Authenticated_Returns200()
    {
        var response = await CreateMemberClient().GetAsync("/api/loot/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMyLedger_Returns_MemberLedgerResponseShape()
    {
        var response = await CreateMemberClient().GetAsync("/api/loot/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("memberId");
        body.Should().Contain("orgPoints");
        body.Should().Contain("lootPoints");
        body.Should().Contain("claimPriority");
    }

    // ── GET /api/loot/distribution ───────────────────────────────────────

    [Fact]
    public async Task GetDistribution_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/loot/distribution");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDistribution_Authenticated_Returns200()
    {
        var response = await CreateMemberClient().GetAsync("/api/loot/distribution");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("members");
    }

    // ── GET /api/loot/{userId} ────────────────────────────────────────────

    [Fact]
    public async Task GetMemberLedger_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync($"/api/loot/{MemberId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMemberLedger_KnownMember_Returns200()
    {
        var response = await CreateMemberClient().GetAsync($"/api/loot/{MemberId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMemberLedger_UnknownMember_Returns404()
    {
        var response = await CreateMemberClient().GetAsync($"/api/loot/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/loot/{userId}/org-points ───────────────────────────────

    [Fact]
    public async Task AddOrgPoints_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PostAsJsonAsync($"/api/loot/{MemberId}/org-points",
            new { amount = 100, reason = "Test" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddOrgPoints_NonCro_Returns403()
    {
        var response = await CreateMemberClient().PostAsJsonAsync($"/api/loot/{MemberId}/org-points",
            new { amount = 100, reason = "Test" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddOrgPoints_Quartermaster_Returns403()
    {
        var response = await CreateQuartermasterClient().PostAsJsonAsync($"/api/loot/{MemberId}/org-points",
            new { amount = 100, reason = "Test" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddOrgPoints_CRO_Returns201()
    {
        var response = await CreateCroClient().PostAsJsonAsync($"/api/loot/{MemberId}/org-points",
            new { amount = 100, reason = "Great raid participation" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddOrgPoints_Admin_Returns201()
    {
        var response = await CreateAdminClient().PostAsJsonAsync($"/api/loot/{MemberId}/org-points",
            new { amount = 50, reason = "Admin award" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddOrgPoints_EmptyReason_Returns422()
    {
        var response = await CreateCroClient().PostAsJsonAsync($"/api/loot/{MemberId}/org-points",
            new { amount = 100, reason = "" });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── POST /api/loot/{userId}/loot-points ──────────────────────────────

    [Fact]
    public async Task AwardLootPoints_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PostAsJsonAsync($"/api/loot/{MemberId}/loot-points",
            new { amount = 100, reason = "Test" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AwardLootPoints_NonQm_Returns403()
    {
        var response = await CreateMemberClient().PostAsJsonAsync($"/api/loot/{MemberId}/loot-points",
            new { amount = 100, reason = "Test" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AwardLootPoints_CRO_Returns403()
    {
        var response = await CreateCroClient().PostAsJsonAsync($"/api/loot/{MemberId}/loot-points",
            new { amount = 100, reason = "Test" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AwardLootPoints_Quartermaster_Returns201()
    {
        var response = await CreateQuartermasterClient().PostAsJsonAsync($"/api/loot/{MemberId}/loot-points",
            new { amount = 300, reason = "Distributed bounty" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AwardLootPoints_EmptyReason_Returns422()
    {
        var response = await CreateQuartermasterClient().PostAsJsonAsync($"/api/loot/{MemberId}/loot-points",
            new { amount = 100, reason = "" });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

// ── Fakes ──────────────────────────────────────────────────────────────────

internal sealed class LootFakeRepo(Guid knownMemberId) : ILootLedgerRepository
{
    public Task AddEntryAsync(LootLedgerEntry entry, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<DistributionRowDto>> GetDistributionAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DistributionRowDto>>([
            new DistributionRowDto(knownMemberId, "Test Member", 100, 50, 2.0),
        ]);

    public Task<MemberLedgerData?> GetMemberLedgerAsync(Guid memberId, CancellationToken ct) =>
        Task.FromResult(memberId == knownMemberId
            ? (MemberLedgerData?)new MemberLedgerData(memberId, "Test Member", [])
            : null);
}

internal sealed class LootFakeLoginService : IExternalLoginService
{
    public Task<LocalUser> FindOrCreateAsync(NajaEcho.Domain.Users.DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<LocalUser?>(new LocalUser(userId, "Test", "test"));
}

internal sealed class LootTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "LootTestScheme";

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
