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
using NajaEcho.Application.Features.Admin.Users.GetUsers;
using NajaEcho.Domain.Organizations;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Admin.Organizations;

[Collection("ApiTests")]
public sealed class OrganizationAdminEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid TargetUserId = Guid.NewGuid();
    private static readonly Guid KnownOrgId = Guid.Parse("9b8ac811-3cec-421c-8cfb-cc56f775ad5a");

    public OrganizationAdminEndpointTests(WebApplicationFactory<Program> factory)
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

                services.RemoveAll<IUserRepository>();
                services.AddSingleton<OrgAdminFakeUserRepo>();
                services.AddSingleton<IUserRepository>(sp => sp.GetRequiredService<OrgAdminFakeUserRepo>());

                services.RemoveAll<IOrganizationRepository>();
                services.AddSingleton<OrgAdminFakeOrgRepo>();
                services.AddSingleton<IOrganizationRepository>(sp => sp.GetRequiredService<OrgAdminFakeOrgRepo>());

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, OrgAdminTestAuthHandler>(
                        OrgAdminTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = OrgAdminTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = OrgAdminTestAuthHandler.SchemeName;
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

    private void Reset()
    {
        _factory.Services.GetRequiredService<OrgAdminFakeUserRepo>().Exists = true;
        var orgRepo = _factory.Services.GetRequiredService<OrgAdminFakeOrgRepo>();
        orgRepo.Known = [new Organization { Id = KnownOrgId, Name = "Naja Echo" }];
        orgRepo.LastSet = null;
    }

    // ── GET /api/admin/organizations ──────────────────────────────────────

    [Fact]
    public async Task GetOrganizations_Unauthenticated_Returns401()
    {
        Reset();
        var response = await CreateClient().GetAsync("/api/admin/organizations");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrganizations_NonAdmin_Returns403()
    {
        Reset();
        var response = await CreateMemberClient().GetAsync("/api/admin/organizations");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetOrganizations_Admin_Returns200WithTheDefaultOrganization()
    {
        Reset();

        var response = await CreateAdminClient().GetAsync("/api/admin/organizations");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("organizations").And.Contain("Naja Echo");
    }

    // ── PUT /api/admin/users/{userId}/organization ────────────────────────

    [Fact]
    public async Task AssignOrganization_Unauthenticated_Returns401()
    {
        Reset();
        var response = await CreateClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/organization", new { organizationId = KnownOrgId });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AssignOrganization_NonAdmin_Returns403()
    {
        Reset();
        var response = await CreateMemberClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/organization", new { organizationId = KnownOrgId });

        // FR-013: refused regardless of how the attempt is made.
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignOrganization_Admin_Returns204AndWritesTheAssignment()
    {
        Reset();

        var response = await CreateAdminClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/organization", new { organizationId = KnownOrgId });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var orgRepo = _factory.Services.GetRequiredService<OrgAdminFakeOrgRepo>();
        orgRepo.LastSet.Should().NotBeNull();
        orgRepo.LastSet!.Value.UserId.Should().Be(TargetUserId);
        orgRepo.LastSet.Value.OrganizationId.Should().Be(KnownOrgId);
    }

    [Fact]
    public async Task AssignOrganization_WithNullOrganizationId_ClearsAndReturns204()
    {
        Reset();

        var response = await CreateAdminClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/organization", new { organizationId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var orgRepo = _factory.Services.GetRequiredService<OrgAdminFakeOrgRepo>();
        orgRepo.LastSet!.Value.OrganizationId.Should().BeNull(
            "a null organizationId is the documented way to unassign a member (FR-012)");
    }

    [Fact]
    public async Task AssignOrganization_UnknownUser_Returns404WithUserNotFoundType()
    {
        Reset();
        _factory.Services.GetRequiredService<OrgAdminFakeUserRepo>().Exists = false;

        var response = await CreateAdminClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/organization", new { organizationId = KnownOrgId });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("urn:najaecho:error:user-not-found");
    }

    [Fact]
    public async Task AssignOrganization_UnknownOrganization_Returns404WithOrganizationNotFoundType()
    {
        Reset();

        var response = await CreateAdminClient().PutAsJsonAsync(
            $"/api/admin/users/{TargetUserId}/organization", new { organizationId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("urn:najaecho:error:organization-not-found");
    }

    // ── FR-015: no path exists to create, rename, or delete an organization ──

    [Theory]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public async Task OrganizationWriteRoutes_DoNotExist(string method)
    {
        Reset();

        var request = new HttpRequestMessage(new HttpMethod(method), "/api/admin/organizations")
        {
            Content = JsonContent.Create(new { name = "Second Org" }),
        };
        var response = await CreateAdminClient().SendAsync(request);

        // FR-015 makes the absence of these routes a requirement, not an omission. Asserted from
        // the outside because "we did not write that endpoint" is not something code can state.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }
}

// ── Fakes ─────────────────────────────────────────────────────────────────────

internal sealed class OrgAdminFakeUserRepo : IUserRepository
{
    public bool Exists { get; set; } = true;

    public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(Exists);
    public Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
    public Task<IReadOnlyList<AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<AdminUserDto>>([]);
    public Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct)
        => Task.CompletedTask;
    public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>([]);
}

internal sealed class OrgAdminFakeOrgRepo : IOrganizationRepository
{
    public IReadOnlyList<Organization> Known { get; set; } = [];
    public (Guid UserId, Guid? OrganizationId)? LastSet { get; set; }

    public Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken ct) => Task.FromResult(Known);
    public Task<bool> ExistsAsync(Guid organizationId, CancellationToken ct)
        => Task.FromResult(Known.Any(o => o.Id == organizationId));
    public Task<Organization?> GetCurrentForUserAsync(Guid userId, CancellationToken ct)
        => Task.FromResult<Organization?>(null);
    public Task<Guid?> SetCurrentAsync(Guid userId, Guid? organizationId, CancellationToken ct)
    {
        LastSet = (userId, organizationId);
        return Task.FromResult<Guid?>(null);
    }
}

internal sealed class OrgAdminTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "OrgAdminTestScheme";

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
