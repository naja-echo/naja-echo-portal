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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NajaEcho.Domain.Organizations;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Admin.Organizations;

/// <summary>
/// Drives the assignment endpoints against a real migrated Postgres, with no faked repositories.
/// </summary>
/// <remarks>
/// <para>
/// Every other API test in this feature replaces the repositories with fakes and stubs the database
/// out entirely — fast, and right for asserting status codes and authorization. But a suite built
/// entirely that way can be green while the endpoint, the handler, the repository and the schema
/// disagree with each other, because nothing ever makes all four meet. This is the test that makes
/// them meet, and the constitution's Development Workflow rule requires at least one.
/// </para>
/// <para>
/// It owns its own container rather than joining <c>PostgresFixture</c>, which lives in the
/// Infrastructure test project and is shared by a serial collection there.
/// </para>
/// </remarks>
[Collection("ApiTests")]
public sealed class OrganizationAssignmentEndToEndTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithDatabase("najaecho_api_e2e")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private Guid _adminId;
    private Guid _targetUserId;
    private Guid _organizationId;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Discord:ClientId"] = "test-id",
                    ["Discord:ClientSecret"] = "test-secret",
                    ["ConnectionStrings:Default"] = _pg.GetConnectionString(),
                });
            });

            // Deliberately no StubDatabase() and no repository fakes — that is the entire point.
            b.ConfigureTestServices(services =>
            {
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, E2ETestAuthHandler>(
                        E2ETestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = E2ETestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = E2ETestAuthHandler.SchemeName;
                });
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // The migration seeds the default organization and backfills every existing member; these
        // two users are created afterwards, so they start unassigned.
        _organizationId = (await db.Organizations.AsNoTracking().SingleAsync()).Id;

        _adminId = AddUser(db, "e2e-admin");
        _targetUserId = AddUser(db, "e2e-target");
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private static Guid AddUser(AppDbContext db, string name)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = name,
            DiscordUsername = name,
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        db.Set<ApplicationUser>().Add(user);
        return user.Id;
    }

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", _adminId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return client;
    }

    private async Task<List<OrganizationMembership>> MembershipsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OrganizationMemberships.AsNoTracking()
            .Where(m => m.UserId == _targetUserId).ToListAsync();
    }

    [Fact]
    public async Task AssignThenClear_MovesRealRowsAndIsVisibleThroughTheUsersEndpoint()
    {
        var client = AdminClient();

        // The organization list comes from the seeded database, not a fake.
        var list = await client.GetFromJsonAsync<OrganizationListDto>("/api/admin/organizations");
        list!.Organizations.Should().ContainSingle().Which.Name.Should().Be("Naja Echo");

        // ── Assign ────────────────────────────────────────────────────────
        var assign = await client.PutAsJsonAsync(
            $"/api/admin/users/{_targetUserId}/organization", new { organizationId = _organizationId });
        assign.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterAssign = await MembershipsAsync();
        afterAssign.Should().ContainSingle();
        afterAssign[0].IsCurrent.Should().BeTrue();
        afterAssign[0].OrganizationId.Should().Be(_organizationId);

        // The admin list must reflect it — this is the read path the Members page uses, and it is
        // hand-authored SQL, so nothing but a real query proves the join is right.
        var users = await client.GetFromJsonAsync<AdminUserListDto>("/api/admin/users");
        var target = users!.Users.Single(u => u.Id == _targetUserId);
        target.Organization.Should().NotBeNull();
        target.Organization!.Name.Should().Be("Naja Echo");

        // ── Clear ─────────────────────────────────────────────────────────
        var clear = await client.PutAsJsonAsync(
            $"/api/admin/users/{_targetUserId}/organization", new { organizationId = (Guid?)null });
        clear.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterClear = await MembershipsAsync();
        afterClear.Should().ContainSingle("clearing retains the membership row");
        afterClear[0].IsCurrent.Should().BeFalse();

        var usersAfterClear = await client.GetFromJsonAsync<AdminUserListDto>("/api/admin/users");
        usersAfterClear!.Users.Single(u => u.Id == _targetUserId).Organization.Should().BeNull();
    }

    [Fact]
    public async Task AssigningAnUnknownOrganization_Returns404AndWritesNothing()
    {
        var response = await AdminClient().PutAsJsonAsync(
            $"/api/admin/users/{_targetUserId}/organization", new { organizationId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("urn:najaecho:error:organization-not-found");

        (await MembershipsAsync()).Should().BeEmpty(
            "a rejected assignment must leave no trace in the database");
    }

    [Fact]
    public async Task AssigningAnUnknownUser_Returns404()
    {
        var response = await AdminClient().PutAsJsonAsync(
            $"/api/admin/users/{Guid.NewGuid()}/organization", new { organizationId = _organizationId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("urn:najaecho:error:user-not-found");
    }

    [Fact]
    public async Task ReassignmentThroughTheApi_NeverLeavesTwoCurrentMemberships()
    {
        var client = AdminClient();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Organizations.Add(new Organization
            {
                Id = Guid.NewGuid(), Name = "Second Org", CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        Guid secondOrgId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            secondOrgId = (await db.Organizations.AsNoTracking()
                .SingleAsync(o => o.Name == "Second Org")).Id;
        }

        foreach (var target in new[] { _organizationId, secondOrgId, _organizationId })
        {
            var response = await client.PutAsJsonAsync(
                $"/api/admin/users/{_targetUserId}/organization", new { organizationId = target });
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var memberships = await MembershipsAsync();
            memberships.Count(m => m.IsCurrent).Should().Be(1);
            memberships.Single(m => m.IsCurrent).OrganizationId.Should().Be(target);
        }
    }

    // ── Response shapes, kept minimal on purpose ──────────────────────────

    private sealed record OrganizationSummaryDto(Guid Id, string Name);
    private sealed record OrganizationListDto(List<OrganizationSummaryDto> Organizations);
    private sealed record AdminUserDtoShape(Guid Id, string AuthName, OrganizationSummaryDto? Organization);
    private sealed record AdminUserListDto(List<AdminUserDtoShape> Users);
}

internal sealed class E2ETestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "E2ETestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "E2E User"),
        };

        if (Request.Headers.TryGetValue("X-Test-Roles", out var rolesHeader))
        {
            foreach (var role in rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
