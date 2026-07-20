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
using System.Text.Json;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Warehouse.GetInventory;
using NajaEcho.Application.Features.Warehouse.GetInventoryFilters;
using NajaEcho.Application.Features.Warehouse.SearchCatalogItems;
using NajaEcho.Application.Features.Warehouse.ShipComponents.GetShipComponentFilters;
using NajaEcho.Application.Features.Warehouse.ShipComponents.GetShipComponents;
using NajaEcho.Application.Features.Warehouse.ShipComponents.SearchSystemsCatalog;
using NajaEcho.Domain.Warehouse;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Warehouse;

[Collection("ApiTests")]
public sealed class WarehouseEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid QuartermasterId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    public WarehouseEndpointsTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IExternalLoginService, WarehouseFakeLoginService>();

                services.RemoveAll<IWarehouseInventoryRepository>();
                services.AddSingleton<FakeWarehouseRepo>();
                services.AddSingleton<IWarehouseInventoryRepository>(sp => sp.GetRequiredService<FakeWarehouseRepo>());

                services.RemoveAll<IItemRepository>();
                services.AddSingleton<IItemRepository, WarehouseFakeItemRepo>();

                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository, WarehouseFakeUserRepo>();

                services.RemoveAll<IShipComponentRepository>();
                services.AddSingleton<IShipComponentRepository, WarehouseFakeShipComponentRepo>();

                services.RemoveAll<IUexItemAttributeClient>();
                services.AddSingleton<IUexItemAttributeClient, WarehouseFakeUexAttrClient>();

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, WarehouseTestAuthHandler>(
                        WarehouseTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = WarehouseTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = WarehouseTestAuthHandler.SchemeName;
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

    // ── GET /api/warehouse/items ─────────────────────────────────────────

    [Fact]
    public async Task GetInventory_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/warehouse/items");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInventory_AuthenticatedNonQM_Returns200()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/items");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetInventory_Returns200WithItemsEnvelope()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/items");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("items");
    }

    // ── GET /api/warehouse/items/filters ────────────────────────────────

    [Fact]
    public async Task GetInventoryFilters_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/warehouse/items/filters");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInventoryFilters_AuthenticatedNonQM_Returns200()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/items/filters");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetInventoryFilters_Returns200WithTypesSubtypesOwners()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/items/filters");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("types");
        body.Should().Contain("subtypes");
        body.Should().Contain("owners");
    }

    // ── GET /api/warehouse/catalog/search ───────────────────────────────

    [Fact]
    public async Task GetCatalogSearch_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/warehouse/catalog/search");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCatalogSearch_NonQM_Returns403()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/catalog/search");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCatalogSearch_Quartermaster_Returns200()
    {
        var response = await CreateQuartermasterClient().GetAsync("/api/warehouse/catalog/search?search=laser");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCatalogSearch_Admin_Returns200()
    {
        var response = await CreateAdminClient().GetAsync("/api/warehouse/catalog/search?search=laser");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /api/warehouse/items ────────────────────────────────────────

    [Fact]
    public async Task PostInventory_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/warehouse/items",
            new { itemId = Guid.NewGuid(), location = "Bay 1", quantity = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostInventory_NonQM_Returns403()
    {
        var response = await CreateMemberClient().PostAsJsonAsync("/api/warehouse/items",
            new { itemId = Guid.NewGuid(), location = "Bay 1", quantity = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostInventory_Quartermaster_NewRow_Returns201()
    {
        var fakeRepo = _factory.Services.GetRequiredService<FakeWarehouseRepo>();
        fakeRepo.NextAddIsNew = true;

        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/items",
            new { itemId = Guid.NewGuid(), location = "Bay 1", quantity = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostInventory_Quartermaster_IncrementExisting_Returns200()
    {
        var fakeRepo = _factory.Services.GetRequiredService<FakeWarehouseRepo>();
        fakeRepo.NextAddIsNew = false;

        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/items",
            new { itemId = Guid.NewGuid(), location = "Bay 1", quantity = 5 });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostInventory_Admin_Returns201()
    {
        var fakeRepo = _factory.Services.GetRequiredService<FakeWarehouseRepo>();
        fakeRepo.NextAddIsNew = true;

        var response = await CreateAdminClient().PostAsJsonAsync("/api/warehouse/items",
            new { itemId = Guid.NewGuid(), location = "Bay 1", quantity = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostInventory_ExplicitQuality_PersistsInResponse()
    {
        var fakeRepo = _factory.Services.GetRequiredService<FakeWarehouseRepo>();
        fakeRepo.NextAddIsNew = true;

        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/items",
            new { itemId = Guid.NewGuid(), location = "Bay 1", quantity = 1, quality = 750 });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"quality\":750");
    }

    [Fact]
    public async Task PostInventory_OutOfRangeQuality_Returns400()
    {
        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/items",
            new { itemId = Guid.NewGuid(), location = "Bay 1", quantity = 1, quality = 1001 });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PUT /api/warehouse/items/{id}/quantity ────────────────────────────

    [Fact]
    public async Task PutQuantity_Unauthenticated_Returns401()
    {
        var response = await CreateClient()
            .PutAsJsonAsync($"/api/warehouse/items/{Guid.NewGuid()}/quantity", new { quantity = 5 });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutQuantity_NonQM_Returns403()
    {
        var response = await CreateMemberClient()
            .PutAsJsonAsync($"/api/warehouse/items/{Guid.NewGuid()}/quantity", new { quantity = 5 });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutQuantity_Quartermaster_Returns200()
    {
        var response = await CreateQuartermasterClient()
            .PutAsJsonAsync($"/api/warehouse/items/{FakeWarehouseRepo.KnownRowId}/quantity", new { quantity = 5 });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutQuantity_Admin_Returns200()
    {
        var response = await CreateAdminClient()
            .PutAsJsonAsync($"/api/warehouse/items/{FakeWarehouseRepo.KnownRowId}/quantity", new { quantity = 3 });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PutQuantity_MissingRow_Returns404()
    {
        var response = await CreateQuartermasterClient()
            .PutAsJsonAsync($"/api/warehouse/items/{Guid.NewGuid()}/quantity", new { quantity = 5 });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/warehouse/items/{id} ─────────────────────────────────

    [Fact]
    public async Task DeleteInventory_Unauthenticated_Returns401()
    {
        var response = await CreateClient().DeleteAsync($"/api/warehouse/items/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteInventory_NonQM_Returns403()
    {
        var response = await CreateMemberClient().DeleteAsync($"/api/warehouse/items/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteInventory_Quartermaster_Returns204()
    {
        var response = await CreateQuartermasterClient().DeleteAsync($"/api/warehouse/items/{FakeWarehouseRepo.KnownRowId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteInventory_Admin_Returns204()
    {
        var response = await CreateAdminClient().DeleteAsync($"/api/warehouse/items/{FakeWarehouseRepo.KnownRowId}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteInventory_MissingRow_Returns404()
    {
        var response = await CreateQuartermasterClient().DeleteAsync($"/api/warehouse/items/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── Fakes ──────────────────────────────────────────────────────────────────

internal sealed class FakeWarehouseRepo : IWarehouseInventoryRepository
{
    public static readonly Guid KnownRowId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid KnownItemId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid KnownOwnerId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public bool NextAddIsNew { get; set; } = true;

    private static InventoryRowDto MakeRow(Guid id) =>
        new(id, KnownItemId, "Test Item", "Weapons", "Laser", 5, 500, KnownOwnerId, "Alice", "Bay 1");

    public Task<IReadOnlyList<InventoryRowDto>> GetInventoryAsync(
        string? name, string? type, string? subtype, Guid? ownerUserId, string? location, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InventoryRowDto>>([MakeRow(KnownRowId)]);

    public Task<InventoryFiltersDto> GetInventoryFiltersAsync(CancellationToken ct) =>
        Task.FromResult(new InventoryFiltersDto(["Weapons"], ["Laser"], []));

    public Task<IReadOnlyList<CatalogItemResultDto>> SearchCatalogItemsAsync(string? search, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CatalogItemResultDto>>([new CatalogItemResultDto(KnownItemId, "Laser Mk1", "Weapons", "Laser")]);

    public Task<(InventoryRowDto Row, bool IsNew)> AddOrIncrementAsync(
        Guid itemId, Guid ownerUserId, string location, int quantity, int quality,
        Guid? locationId, string? locationType, CancellationToken ct) =>
        Task.FromResult((MakeRow(Guid.NewGuid()) with
        {
            ItemId = itemId,
            OwnerUserId = ownerUserId,
            Location = location,
            Quantity = quantity,
            Quality = quality,
        }, NextAddIsNew));

    public Task<InventoryRowDto> UpdateQuantityAsync(Guid id, int quantity, CancellationToken ct)
    {
        if (id != KnownRowId)
            throw new NajaEcho.Application.Features.Warehouse.ChangeInventoryQuantity.InventoryRowNotFoundException(id);
        return Task.FromResult(MakeRow(id) with { Quantity = quantity });
    }

    public Task RemoveAsync(Guid id, CancellationToken ct)
    {
        if (id != KnownRowId)
            throw new NajaEcho.Application.Features.Warehouse.ChangeInventoryQuantity.InventoryRowNotFoundException(id);
        return Task.CompletedTask;
    }

    public Task<InventoryRowDto> UpdateItemAsync(Guid id, Guid ownerUserId, Guid locationId, string locationType, int quantity, CancellationToken ct) =>
        throw new NotImplementedException();
    public Task UpdateLocationAsync(Guid id, Guid locationId, string locationType, CancellationToken ct) => Task.CompletedTask;
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(id == KnownRowId);
}

internal sealed class WarehouseFakeItemRepo : IItemRepository
{
    public Task<NajaEcho.Domain.Items.Item?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<NajaEcho.Domain.Items.Item?>(new NajaEcho.Domain.Items.Item
        {
            Id = id,
            Name = "Test Item",
            Status = NajaEcho.Domain.Items.ItemStatus.Active,
            Uuid = id.ToString(),
            UexId = 1,
            IdCategory = 1,
            RawData = System.Text.Json.JsonDocument.Parse("{}"),
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

    public Task<(int Inserted, int Updated, int Unchanged, int SoftDeleted, int Restored)> BulkUpsertForCategoryAsync(
        int idCategory, IReadOnlyList<NajaEcho.Domain.Items.Item> incoming, CancellationToken ct = default) =>
        throw new NotImplementedException();
}

internal sealed class WarehouseFakeUserRepo : IUserRepository
{
    public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(true);
    public Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
    public Task<IReadOnlyList<NajaEcho.Application.Features.Admin.Users.GetUsers.AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NajaEcho.Application.Features.Admin.Users.GetUsers.AdminUserDto>>([]);
    public Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}

internal sealed class WarehouseFakeLoginService : IExternalLoginService
{
    public Task<LocalUser> FindOrCreateAsync(NajaEcho.Domain.Users.DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<LocalUser?>(new LocalUser(userId, "Test", "test"));
}

internal sealed class WarehouseFakeShipComponentRepo : IShipComponentRepository
{
    public Task<bool> HasCachedAttributesAsync(Guid itemId, CancellationToken ct) => Task.FromResult(true);
    public Task SaveItemAttributesAsync(IReadOnlyList<ItemAttribute> attributes, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<ShipComponentRowDto>> GetShipComponentsAsync(GetShipComponentsQuery query, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ShipComponentRowDto>>([]);
    public Task<ShipComponentFiltersDto> GetShipComponentFiltersAsync(CancellationToken ct) =>
        Task.FromResult(new ShipComponentFiltersDto([], [], [], [], [], [], false, false, false));
    public Task<IReadOnlyList<SystemsCatalogItemDto>> SearchSystemsCatalogAsync(string? search, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SystemsCatalogItemDto>>([]);
}

internal sealed class WarehouseFakeUexAttrClient : IUexItemAttributeClient
{
    public Task<IReadOnlyList<JsonDocument>> FetchItemAttributesAsync(int uexItemId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<JsonDocument>>([]);
}

internal sealed class WarehouseTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "WarehouseTestScheme";

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
