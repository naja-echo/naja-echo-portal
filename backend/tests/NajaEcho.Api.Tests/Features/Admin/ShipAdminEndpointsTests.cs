using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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
using NajaEcho.Domain.Ships;
using NajaEcho.Domain.Users;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Admin;

[Collection("ApiTests")]
public class ShipAdminEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid AdminUserId = Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
    private static readonly Guid RegularUserId = Guid.Parse("b1ffcd00-0d1c-4ef9-cc7e-7cc0ce491b22");

    public ShipAdminEndpointsTests(WebApplicationFactory<Program> factory)
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
                services.StubDatabase();

                services.RemoveAll<IExternalLoginService>();
                services.AddSingleton<IExternalLoginService, FakeShipExternalLoginService>();

                services.RemoveAll<IUexVehicleClient>();
                services.AddSingleton<FakeUexVehicleClient>();
                services.AddSingleton<IUexVehicleClient>(sp => sp.GetRequiredService<FakeUexVehicleClient>());

                services.RemoveAll<IImportCoordinator>();
                services.AddSingleton<FakeImportCoordinator>();
                services.AddSingleton<IImportCoordinator>(sp => sp.GetRequiredService<FakeImportCoordinator>());

                // Replace real ShipRepository (which uses transactions incompatible with InMemory)
                services.RemoveAll<IShipRepository>();
                services.AddSingleton<FakeApiShipRepository>();
                services.AddSingleton<IShipRepository>(sp => sp.GetRequiredService<FakeApiShipRepository>());

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, AdminTestAuthHandler>(
                        AdminTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = AdminTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = AdminTestAuthHandler.SchemeName;
                });
            });
        });
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", AdminUserId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        return client;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", RegularUserId.ToString());
        return client;
    }

    // T021: /api/auth/me returns roles for admin user
    [Fact]
    public async Task Me_AdminUser_ReturnsRolesArray()
    {
        var client = CreateAdminClient();
        var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("roles");
        body.Should().Contain("Admin");
    }

    // T021: /api/auth/me returns empty roles for non-admin
    [Fact]
    public async Task Me_RegularUser_ReturnsEmptyRoles()
    {
        var client = CreateAuthenticatedClient();
        var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("\"roles\":[]");
    }

    // T029: 401 unauthenticated
    [Fact]
    public async Task ImportShips_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PostAsync("/api/admin/ships/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // T029: 403 non-admin
    [Fact]
    public async Task ImportShips_NonAdmin_Returns403()
    {
        var response = await CreateAuthenticatedClient().PostAsync("/api/admin/ships/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T029: 200 with counts on success
    [Fact]
    public async Task ImportShips_AdminWithRecords_Returns200WithCounts()
    {
        _factory.Services.GetRequiredService<FakeUexVehicleClient>()
            .SetRecords([MakeRecord(1, "100i"), MakeRecord(2, "Avenger")]);
        _factory.Services.GetRequiredService<FakeImportCoordinator>().Held = false;

        var response = await CreateAdminClient().PostAsync("/api/admin/ships/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("added");
    }

    // T029: 409 when already in progress
    [Fact]
    public async Task ImportShips_WhenAlreadyInProgress_Returns409()
    {
        _factory.Services.GetRequiredService<FakeImportCoordinator>().Held = true;

        var response = await CreateAdminClient().PostAsync("/api/admin/ships/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // T029: 502 when feed fails
    [Fact]
    public async Task ImportShips_WhenFeedFails_Returns502()
    {
        _factory.Services.GetRequiredService<FakeUexVehicleClient>().ShouldThrow = true;
        _factory.Services.GetRequiredService<FakeImportCoordinator>().Held = false;

        var response = await CreateAdminClient().PostAsync("/api/admin/ships/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    // T046: GET /api/admin/ships — 401 unauthenticated
    [Fact]
    public async Task GetShips_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/admin/ships");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // T046: GET /api/admin/ships — 403 non-admin
    [Fact]
    public async Task GetShips_NonAdmin_Returns403()
    {
        var response = await CreateAuthenticatedClient().GetAsync("/api/admin/ships");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T046: GET /api/admin/ships — returns paged envelope
    [Fact]
    public async Task GetShips_Admin_ReturnsPagedEnvelope()
    {
        var response = await CreateAdminClient().GetAsync("/api/admin/ships");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("items");
        body.Should().Contain("totalCount");
        body.Should().Contain("page");
    }

    // T057: GET /api/admin/ships/{id} — 401 unauthenticated
    [Fact]
    public async Task GetShipById_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync($"/api/admin/ships/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // T057: GET /api/admin/ships/{id} — 404 unknown id
    [Fact]
    public async Task GetShipById_UnknownId_Returns404()
    {
        var response = await CreateAdminClient().GetAsync($"/api/admin/ships/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static JsonDocument MakeRecord(int id, string name) =>
        JsonDocument.Parse($$"""{"id":{{id}},"name":"{{name}}","uuid":null,"name_full":null,"company_name":null}""");
}

internal sealed class FakeApiShipRepository : IShipRepository
{
    public Task<(IReadOnlyList<Ship>, int)> GetPagedAsync(int page, int pageSize, CancellationToken ct)
    {
        IReadOnlyList<Ship> items = [];
        return Task.FromResult((items, 0));
    }

    public Task<Ship?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult<Ship?>(null);

    public Task<Ship?> GetByUexIdAsync(int uexId, CancellationToken ct) =>
        Task.FromResult<Ship?>(null);

    public Task<(int, int, int, int)> BulkUpsertAsync(IReadOnlyList<Ship> incoming, CancellationToken ct) =>
        Task.FromResult((incoming.Count, 0, 0, 0));

    public Task<IReadOnlyList<int>> GetAllActiveUexIdsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<int>>([]);
}

internal sealed class FakeShipExternalLoginService : IExternalLoginService
{
    public Task<LocalUser> FindOrCreateAsync(DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<LocalUser?>(new LocalUser(userId, "Test", "test"));
}

internal sealed class FakeUexVehicleClient : IUexVehicleClient
{
    private IReadOnlyList<JsonDocument> _records = [];
    public bool ShouldThrow { get; set; }
    public void SetRecords(IReadOnlyList<JsonDocument> records) => _records = records;

    public Task<IReadOnlyList<JsonDocument>> FetchAllVehiclesAsync(CancellationToken ct = default)
    {
        if (ShouldThrow) throw new HttpRequestException("Feed error");
        return Task.FromResult(_records);
    }
}

internal sealed class FakeImportCoordinator : IImportCoordinator
{
    public bool Held { get; set; }
    public bool TryAcquire() { if (Held) return false; Held = true; return true; }
    public void Release() => Held = false;
}

internal sealed class AdminTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AdminTestScheme";

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

        if (Request.Headers.TryGetValue("X-Test-Role", out var role))
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
