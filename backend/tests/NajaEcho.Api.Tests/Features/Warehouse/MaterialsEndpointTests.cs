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
using NajaEcho.Application.Features.Warehouse.Materials.GetMaterialFilters;
using NajaEcho.Application.Features.Warehouse.Materials.GetMaterials;
using NajaEcho.Application.Features.Warehouse.Materials.SearchCommodities;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Warehouse;

[Collection("ApiTests")]
public sealed class MaterialsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid QuartermasterId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    public MaterialsEndpointTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IExternalLoginService, MaterialsFakeLoginService>();

                services.RemoveAll<IMaterialInventoryRepository>();
                services.AddSingleton<FakeMaterialRepo>();
                services.AddSingleton<IMaterialInventoryRepository>(sp => sp.GetRequiredService<FakeMaterialRepo>());

                services.RemoveAll<ICommodityRepository>();
                services.AddSingleton<ICommodityRepository, MaterialsFakeCommodityRepo>();

                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository, MaterialsFakeUserRepo>();

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, MaterialsTestAuthHandler>(
                        MaterialsTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = MaterialsTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = MaterialsTestAuthHandler.SchemeName;
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

    // ── GET /api/warehouse/materials ────────────────────────────────────

    [Fact]
    public async Task GetMaterials_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/warehouse/materials");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMaterials_Authenticated_Returns200()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/materials");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetMaterials_Returns200WithRowsAndTwoDecimalQuantity()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/materials");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"rows\"");
        body.Should().Contain("\"quantity\":12.5");
    }

    [Fact]
    public async Task GetMaterials_WithFilterQueryParams_PassesThemThroughToRepository()
    {
        var fakeRepo = _factory.Services.GetRequiredService<FakeMaterialRepo>();

        var ownerId = Guid.NewGuid();
        var response = await CreateMemberClient().GetAsync(
            $"/api/warehouse/materials?material=titan&ownerUserId={ownerId}&location=Bay+1&qualityMin=100&qualityMax=900");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fakeRepo.CapturedMaterial.Should().Be("titan");
        fakeRepo.CapturedOwnerUserId.Should().Be(ownerId);
        fakeRepo.CapturedLocation.Should().Be("Bay 1");
        fakeRepo.CapturedQualityMin.Should().Be(100);
        fakeRepo.CapturedQualityMax.Should().Be(900);
    }

    // ── GET /api/warehouse/materials/filters ───────────────────────────────

    [Fact]
    public async Task GetMaterialFilters_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/warehouse/materials/filters");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMaterialFilters_Authenticated_Returns200WithOwnersAndLocations()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/materials/filters");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"owners\"");
        body.Should().Contain("\"locations\"");
    }

    // ── GET /api/warehouse/materials/catalog/search ───────────────────────

    [Fact]
    public async Task GetCatalogSearch_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/warehouse/materials/catalog/search");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCatalogSearch_NonQM_Returns403()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/materials/catalog/search");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCatalogSearch_Quartermaster_Returns200()
    {
        var response = await CreateQuartermasterClient().GetAsync("/api/warehouse/materials/catalog/search?search=titanium");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCatalogSearch_Admin_Returns200()
    {
        var response = await CreateAdminClient().GetAsync("/api/warehouse/materials/catalog/search?search=titanium");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /api/warehouse/materials ──────────────────────────────────────

    [Fact]
    public async Task PostMaterial_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/warehouse/materials",
            new { commodityId = FakeMaterialRepo.KnownCommodityId, location = "Bay 1", quantity = 1.5 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostMaterial_NonQM_Returns403()
    {
        var response = await CreateMemberClient().PostAsJsonAsync("/api/warehouse/materials",
            new { commodityId = FakeMaterialRepo.KnownCommodityId, location = "Bay 1", quantity = 1.5 });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostMaterial_Quartermaster_NewRow_Returns201()
    {
        var fakeRepo = _factory.Services.GetRequiredService<FakeMaterialRepo>();
        fakeRepo.NextAddIsNew = true;

        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/materials",
            new { commodityId = FakeMaterialRepo.KnownCommodityId, location = "Bay 1", quantity = 1.5 });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostMaterial_Quartermaster_IncrementExisting_Returns200()
    {
        var fakeRepo = _factory.Services.GetRequiredService<FakeMaterialRepo>();
        fakeRepo.NextAddIsNew = false;

        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/materials",
            new { commodityId = FakeMaterialRepo.KnownCommodityId, location = "Bay 1", quantity = 1.5 });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostMaterial_OutOfRangeQuality_Returns400()
    {
        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/materials",
            new { commodityId = FakeMaterialRepo.KnownCommodityId, location = "Bay 1", quantity = 1.5, quality = 1001 });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostMaterial_ZeroQuantity_Returns400()
    {
        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/materials",
            new { commodityId = FakeMaterialRepo.KnownCommodityId, location = "Bay 1", quantity = 0 });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostMaterial_UnknownCommodity_Returns404()
    {
        var commodityRepo = (MaterialsFakeCommodityRepo)_factory.Services.GetRequiredService<ICommodityRepository>();
        commodityRepo.CommodityExists = false;

        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/materials",
            new { commodityId = Guid.NewGuid(), location = "Bay 1", quantity = 1.5 });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        commodityRepo.CommodityExists = true;
    }

    [Fact]
    public async Task PostMaterial_UnknownOwner_Returns404()
    {
        var userRepo = (MaterialsFakeUserRepo)_factory.Services.GetRequiredService<IUserRepository>();
        userRepo.UserExists = false;

        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/materials",
            new { commodityId = FakeMaterialRepo.KnownCommodityId, ownerUserId = Guid.NewGuid(), location = "Bay 1", quantity = 1.5 });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        userRepo.UserExists = true;
    }

    // ── PUT /api/warehouse/materials/{id}/quantity ──────────────────────────

    [Fact]
    public async Task PutQuantity_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PutAsJsonAsync(
            $"/api/warehouse/materials/{FakeMaterialRepo.KnownRowId}/quantity", new { quantity = 5 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutQuantity_NonQM_Returns403()
    {
        var response = await CreateMemberClient().PutAsJsonAsync(
            $"/api/warehouse/materials/{FakeMaterialRepo.KnownRowId}/quantity", new { quantity = 5 });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutQuantity_Quartermaster_ValidQuantity_Returns200WithUpdatedQuantity()
    {
        var response = await CreateQuartermasterClient().PutAsJsonAsync(
            $"/api/warehouse/materials/{FakeMaterialRepo.KnownRowId}/quantity", new { quantity = 5 });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"quantity\":5");
    }

    [Fact]
    public async Task PutQuantity_ZeroQuantity_Returns400()
    {
        var response = await CreateQuartermasterClient().PutAsJsonAsync(
            $"/api/warehouse/materials/{FakeMaterialRepo.KnownRowId}/quantity", new { quantity = 0 });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutQuantity_UnknownId_Returns404()
    {
        var response = await CreateQuartermasterClient().PutAsJsonAsync(
            $"/api/warehouse/materials/{Guid.NewGuid()}/quantity", new { quantity = 5 });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/warehouse/materials/{id} ────────────────────────────────

    [Fact]
    public async Task DeleteMaterial_Unauthenticated_Returns401()
    {
        var response = await CreateClient().DeleteAsync($"/api/warehouse/materials/{FakeMaterialRepo.KnownRowId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteMaterial_NonQM_Returns403()
    {
        var response = await CreateMemberClient().DeleteAsync($"/api/warehouse/materials/{FakeMaterialRepo.KnownRowId}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteMaterial_Quartermaster_Returns204()
    {
        var response = await CreateQuartermasterClient().DeleteAsync($"/api/warehouse/materials/{FakeMaterialRepo.KnownRowId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteMaterial_UnknownId_Returns404()
    {
        var response = await CreateQuartermasterClient().DeleteAsync($"/api/warehouse/materials/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── Fakes ──────────────────────────────────────────────────────────────────

internal sealed class FakeMaterialRepo : IMaterialInventoryRepository
{
    public static readonly Guid KnownRowId = new("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid KnownCommodityId = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    public static readonly Guid KnownOwnerId = new("ffffffff-ffff-ffff-ffff-ffffffffffff");

    public bool NextAddIsNew { get; set; } = true;

    public string? CapturedMaterial { get; private set; }
    public Guid? CapturedOwnerUserId { get; private set; }
    public string? CapturedLocation { get; private set; }
    public int? CapturedQualityMin { get; private set; }
    public int? CapturedQualityMax { get; private set; }

    private static MaterialRowDto MakeRow(Guid id) =>
        new(id, KnownCommodityId, "Titanium", "TTAM", 12.5m, 500, KnownOwnerId, "Alice", "Bay 1");

    public Task<IReadOnlyList<MaterialRowDto>> GetMaterialsAsync(
        string? material, Guid? ownerUserId, string? location, int? qualityMin, int? qualityMax, CancellationToken ct)
    {
        CapturedMaterial = material;
        CapturedOwnerUserId = ownerUserId;
        CapturedLocation = location;
        CapturedQualityMin = qualityMin;
        CapturedQualityMax = qualityMax;
        return Task.FromResult<IReadOnlyList<MaterialRowDto>>([MakeRow(KnownRowId)]);
    }

    public Task<MaterialFiltersDto> GetMaterialFiltersAsync(CancellationToken ct) =>
        Task.FromResult(new MaterialFiltersDto(
            [new OwnerOption(KnownOwnerId, "Alice")],
            ["Bay 1"]));

    public Task<IReadOnlyList<CommodityResultDto>> SearchCommoditiesAsync(string? search, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CommodityResultDto>>([new CommodityResultDto(KnownCommodityId, "Titanium", "TTAM")]);

    public Task<(MaterialRowDto Row, bool IsNew)> AddOrIncrementAsync(
        Guid commodityId, Guid ownerUserId, string location, decimal quantity, int quality,
        Guid? locationId, string? locationType, CancellationToken ct) =>
        Task.FromResult((MakeRow(Guid.NewGuid()) with
        {
            CommodityId = commodityId,
            OwnerUserId = ownerUserId,
            Location = location,
            Quantity = quantity,
            Quality = quality,
        }, NextAddIsNew));

    public Task<MaterialRowDto> UpdateQuantityAsync(Guid id, decimal quantity, CancellationToken ct)
    {
        if (id != KnownRowId)
            throw new NajaEcho.Application.Features.Warehouse.Materials.ChangeMaterialQuantity.MaterialRowNotFoundException(id);
        return Task.FromResult(MakeRow(id) with { Quantity = quantity });
    }

    public Task RemoveAsync(Guid id, CancellationToken ct)
    {
        if (id != KnownRowId)
            throw new NajaEcho.Application.Features.Warehouse.Materials.ChangeMaterialQuantity.MaterialRowNotFoundException(id);
        return Task.CompletedTask;
    }

    public Task<MaterialRowDto> UpdateMaterialAsync(Guid id, Guid ownerUserId, Guid locationId, string locationType, decimal quantity, CancellationToken ct) => throw new NotImplementedException();
    public Task UpdateLocationAsync(Guid id, Guid locationId, string locationType, CancellationToken ct) => Task.CompletedTask;
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(id == KnownRowId);
}

internal sealed class MaterialsFakeCommodityRepo : ICommodityRepository
{
    public bool CommodityExists { get; set; } = true;

    public Task<NajaEcho.Domain.Commodities.Commodity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(CommodityExists ? (NajaEcho.Domain.Commodities.Commodity?)new NajaEcho.Domain.Commodities.Commodity
        {
            Id = id,
            Name = "Titanium",
            Code = "TTAM",
            Status = NajaEcho.Domain.Commodities.CommodityStatus.Active,
            RawData = System.Text.Json.JsonDocument.Parse("{}"),
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        } : null);

    public Task<(IReadOnlyList<NajaEcho.Application.Features.Commodities.GetCommodities.CommodityListItem> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<(int Inserted, int Updated, int Unchanged, int Restored, int SoftDeleted)> BulkUpsertAsync(
        IReadOnlyList<NajaEcho.Domain.Commodities.Commodity> incoming, CancellationToken ct = default) =>
        throw new NotImplementedException();
}

internal sealed class MaterialsFakeUserRepo : IUserRepository
{
    public bool UserExists { get; set; } = true;
    public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(UserExists);
    public Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
    public Task<IReadOnlyList<NajaEcho.Application.Features.Admin.Users.GetUsers.AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NajaEcho.Application.Features.Admin.Users.GetUsers.AdminUserDto>>([]);
    public Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}

internal sealed class MaterialsFakeLoginService : IExternalLoginService
{
    public Task<LocalUser> FindOrCreateAsync(NajaEcho.Domain.Users.DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<LocalUser?>(new LocalUser(userId, "Test", "test"));
}

internal sealed class MaterialsTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "MaterialsTestScheme";

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
