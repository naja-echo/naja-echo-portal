using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
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
using NajaEcho.Application.Features.Blueprints.GetBlueprints;
using NajaEcho.Application.Features.Blueprints.ImportBlueprints;
using NajaEcho.Domain.Blueprints;

namespace NajaEcho.Api.Tests.Features.Admin.Blueprints;

[Collection("ApiTests")]
public class BlueprintAdminEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid AdminUserId = Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
    private static readonly Guid RegularUserId = Guid.Parse("b1ffcd00-0d1c-4ef9-cc7e-7cc0ce491b22");

    private const string ValidDataset = """
    {
      "version": "1.4.0",
      "meta": { "totalBlueprints": 1, "totalProducts": 1, "totalResources": 1, "totalItems": 0 },
      "dismantle": { "efficiency": 0.5, "dismantleTimeSeconds": 60, "blacklistedResources": [], "blacklistedEntityClasses": [] },
      "properties": {},
      "resources": ["Steel"],
      "items": [],
      "blueprints": [
        {
          "guid": "11111111-1111-1111-1111-111111111111", "tag": "BP",
          "productEntityClass": "22222222-2222-2222-2222-222222222222", "gear": "Weapon",
          "type": null, "subtype": null, "productName": "Widget", "manufacturer": "ACME",
          "tiers": [ { "craftTimeSeconds": 1, "slots": [ { "name": "S", "options": [ { "type": "resource", "quantity": 1, "minQuality": 0, "resourceName": "Steel" } ], "modifiers": null } ] } ]
        }
      ]
    }
    """;

    public BlueprintAdminEndpointsTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IExternalLoginService, FakeBlueprintLoginService>();

                services.RemoveAll<IImportCoordinator>();
                services.AddSingleton<FakeBlueprintCoordinator>();
                services.AddSingleton<IImportCoordinator>(sp => sp.GetRequiredService<FakeBlueprintCoordinator>());

                services.RemoveAll<IBlueprintRepository>();
                services.AddSingleton<FakeBlueprintRepository>();
                services.AddSingleton<IBlueprintRepository>(sp => sp.GetRequiredService<FakeBlueprintRepository>());

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, BlueprintTestAuthHandler>(
                        BlueprintTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = BlueprintTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = BlueprintTestAuthHandler.SchemeName;
                });
            });
        });
    }

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", AdminUserId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        return client;
    }

    private HttpClient NonAdminClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", RegularUserId.ToString());
        return client;
    }

    private HttpClient AnonymousClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private void ResetFakes()
    {
        _factory.Services.GetRequiredService<FakeBlueprintCoordinator>().Held = false;
        _factory.Services.GetRequiredService<FakeBlueprintRepository>().Reset();
    }

    // ── Access control (US3, SC-008) ─────────────────────────────────────────

    [Fact]
    public async Task Import_Unauthenticated_Returns401()
    {
        var response = await AnonymousClient().PostAsync("/api/admin/blueprints/import", Json(ValidDataset));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Import_NonAdmin_Returns403()
    {
        var response = await NonAdminClient().PostAsync("/api/admin/blueprints/import", Json(ValidDataset));
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Unauthenticated_Returns401()
    {
        var response = await AnonymousClient().GetAsync("/api/admin/blueprints");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_NonAdmin_Returns403()
    {
        var response = await NonAdminClient().GetAsync("/api/admin/blueprints");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Import (US1, US5) ────────────────────────────────────────────────────

    [Fact]
    public async Task Import_Admin_ValidDataset_Returns200WithExpectedShape()
    {
        ResetFakes();
        _factory.Services.GetRequiredService<FakeBlueprintRepository>().Counts = new BlueprintImportCounts(1, 0);

        var response = await AdminClient().PostAsync("/api/admin/blueprints/import", Json(ValidDataset));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ImportResponseShape>();
        body.Should().NotBeNull();
        body!.Version.Should().Be("1.4.0");
        body.Blueprints.Read.Should().Be(1);
        body.Blueprints.Inserted.Should().Be(1);
        body.Resources.Read.Should().Be(1);
        body.ReferenceDataReplaced.Should().BeTrue();
        body.Warnings.Should().NotBeNull();
        body.Rejections.Should().NotBeNull();
    }

    [Fact]
    public async Task Import_Admin_InvalidDocument_Returns400NothingStored()
    {
        ResetFakes();
        const string missingDismantle = """
        { "version": "1.0.0", "meta": { "totalBlueprints": 0, "totalProducts": 0, "totalResources": 0, "totalItems": 0 },
          "properties": {}, "resources": [], "items": [], "blueprints": [] }
        """;

        var response = await AdminClient().PostAsync("/api/admin/blueprints/import", Json(missingDismantle));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _factory.Services.GetRequiredService<FakeBlueprintRepository>().ImportCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Import_WhenAlreadyInProgress_Returns409()
    {
        ResetFakes();
        _factory.Services.GetRequiredService<FakeBlueprintCoordinator>().Held = true;

        var response = await AdminClient().PostAsync("/api/admin/blueprints/import", Json(ValidDataset));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Listing (US2) ────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_Admin_ReturnsListOrderedByDisplayName()
    {
        ResetFakes();
        _factory.Services.GetRequiredService<FakeBlueprintRepository>().List =
        [
            new BlueprintListItemDto(Guid.NewGuid(), "Zeta", "productName", "Zeta", "TAGZ", null),
            new BlueprintListItemDto(Guid.NewGuid(), "Alpha", "tag", null, "Alpha", null),
        ];

        var response = await AdminClient().GetAsync("/api/admin/blueprints");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ListResponseShape>();
        body!.Blueprints.Should().HaveCount(2);
        body.Blueprints[0].DisplayName.Should().Be("Zeta");
        body.Blueprints[0].NameSource.Should().Be("productName");
    }

    // ── Response DTOs for assertions ─────────────────────────────────────────

    private sealed record CountsShape(int Read, int Inserted, int Updated, int Rejected);

    private sealed record ImportResponseShape(
        string Version, CountsShape Blueprints, CountsShape Resources, CountsShape Items,
        CountsShape Properties, bool ReferenceDataReplaced, List<string> Warnings, List<object> Rejections);

    private sealed record ListItemShape(Guid Guid, string DisplayName, string NameSource);

    private sealed record ListResponseShape(List<ListItemShape> Blueprints);
}

internal sealed class FakeBlueprintCoordinator : IImportCoordinator
{
    public bool Held { get; set; }

    public bool TryAcquire()
    {
        if (Held)
        {
            return false;
        }

        Held = true;
        return true;
    }

    public void Release() => Held = false;
}

internal sealed class FakeBlueprintRepository : IBlueprintRepository
{
    public bool ImportCalled { get; private set; }
    public BlueprintImportCounts Counts { get; set; } = new(0, 0);
    public IReadOnlyList<BlueprintListItemDto> List { get; set; } = [];

    public void Reset()
    {
        ImportCalled = false;
        Counts = new BlueprintImportCounts(0, 0);
        List = [];
    }

    public Task<IReadOnlyDictionary<string, MaterialMatch>> ResolveMaterialsAsync(
        IReadOnlyCollection<string> names, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, MaterialMatch>>(new Dictionary<string, MaterialMatch>());

    public Task<BlueprintImportCounts> ImportAsync(ParsedBlueprintDataset dataset, CancellationToken ct = default)
    {
        ImportCalled = true;
        return Task.FromResult(Counts);
    }

    public Task<IReadOnlyList<BlueprintListItemDto>> GetListAsync(CancellationToken ct = default) =>
        Task.FromResult(List);

    public Task<IReadOnlyList<NajaEcho.Application.Features.Blueprints.SearchBlueprints.BlueprintSearchResultDto>> SearchAsync(
        string term, int limit = 20, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<NajaEcho.Application.Features.Blueprints.SearchBlueprints.BlueprintSearchResultDto>>([]);
}

internal sealed class FakeBlueprintLoginService : IExternalLoginService
{
    public Task<LocalUser> FindOrCreateAsync(NajaEcho.Domain.Users.DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<LocalUser?>(new LocalUser(userId, "Test", "test"));
}

internal sealed class BlueprintTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "BlueprintTestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "Test User"),
        };

        if (Request.Headers.TryGetValue("X-Test-Role", out var role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
