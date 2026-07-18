using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Warehouse.ChangeInventoryQuantity;
using NajaEcho.Application.Features.Warehouse.GetInventory;
using NajaEcho.Application.Features.Warehouse.GetInventoryFilters;
using NajaEcho.Application.Features.Warehouse.Materials.ChangeMaterialQuantity;
using NajaEcho.Application.Features.Warehouse.Materials.GetMaterialFilters;
using NajaEcho.Application.Features.Warehouse.Materials.GetMaterials;
using NajaEcho.Application.Features.Warehouse.Materials.SearchCommodities;
using NajaEcho.Application.Features.Warehouse.SearchCatalogItems;
using NajaEcho.Domain.Users;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Warehouse;

[Collection("ApiTests")]
public sealed class WarehouseTransferEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid QuartermasterId = Guid.NewGuid();
    private static readonly Guid KnownRowId = Guid.NewGuid();
    private static readonly Guid KnownStationId = Guid.NewGuid();

    public WarehouseTransferEndpointsTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IExternalLoginService, TransferFakeLoginService>();

                services.RemoveAll<IWarehouseInventoryRepository>();
                services.AddSingleton<FakeTransferWarehouseRepo>();
                services.AddSingleton<IWarehouseInventoryRepository>(sp => sp.GetRequiredService<FakeTransferWarehouseRepo>());

                services.RemoveAll<IMaterialInventoryRepository>();
                services.AddSingleton<FakeTransferMaterialRepo>();
                services.AddSingleton<IMaterialInventoryRepository>(sp => sp.GetRequiredService<FakeTransferMaterialRepo>());

                services.RemoveAll<ISpaceStationRepository>();
                services.AddSingleton<FakeTransferStationRepo>();
                services.AddSingleton<ISpaceStationRepository>(sp => sp.GetRequiredService<FakeTransferStationRepo>());

                services.AddAuthentication()
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, WarehouseTestAuthHandler>(
                        WarehouseTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(opts =>
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

    // ── PUT /api/warehouse/items/{id}/station ────────────────────────────

    [Fact]
    public async Task TransferItem_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PutAsJsonAsync(
            $"/api/warehouse/items/{KnownRowId}/location",
            new { locationId = KnownStationId, locationType = "Station" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TransferItem_AuthenticatedNonQuartermaster_Returns403()
    {
        var response = await CreateMemberClient().PutAsJsonAsync(
            $"/api/warehouse/items/{KnownRowId}/location",
            new { locationId = KnownStationId, locationType = "Station" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TransferItem_AsQuartermaster_Returns204()
    {
        var repo = _factory.Services.GetRequiredService<FakeTransferWarehouseRepo>();
        repo.RowExists = true;
        _factory.Services.GetRequiredService<FakeTransferStationRepo>().StationExists = true;

        var response = await CreateQuartermasterClient().PutAsJsonAsync(
            $"/api/warehouse/items/{KnownRowId}/location",
            new { locationId = KnownStationId, locationType = "Station" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TransferItem_UnknownRowId_Returns404()
    {
        var repo = _factory.Services.GetRequiredService<FakeTransferWarehouseRepo>();
        repo.RowExists = false;
        _factory.Services.GetRequiredService<FakeTransferStationRepo>().StationExists = true;

        var response = await CreateQuartermasterClient().PutAsJsonAsync(
            $"/api/warehouse/items/{Guid.NewGuid()}/location",
            new { locationId = KnownStationId, locationType = "Station" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TransferItem_AsQuartermaster_NewLocation_Returns204()
    {
        var repo = _factory.Services.GetRequiredService<FakeTransferWarehouseRepo>();
        repo.RowExists = true;

        var response = await CreateQuartermasterClient().PutAsJsonAsync(
            $"/api/warehouse/items/{KnownRowId}/location",
            new { locationId = Guid.NewGuid(), locationType = "City" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── PUT /api/warehouse/materials/{id}/station ─────────────────────────

    [Fact]
    public async Task TransferMaterial_AsQuartermaster_Returns204()
    {
        var repo = _factory.Services.GetRequiredService<FakeTransferMaterialRepo>();
        repo.RowExists = true;
        _factory.Services.GetRequiredService<FakeTransferStationRepo>().StationExists = true;

        var response = await CreateQuartermasterClient().PutAsJsonAsync(
            $"/api/warehouse/materials/{KnownRowId}/location",
            new { locationId = KnownStationId, locationType = "Station" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task TransferMaterial_UnknownRowId_Returns404()
    {
        var repo = _factory.Services.GetRequiredService<FakeTransferMaterialRepo>();
        repo.RowExists = false;
        _factory.Services.GetRequiredService<FakeTransferStationRepo>().StationExists = true;

        var response = await CreateQuartermasterClient().PutAsJsonAsync(
            $"/api/warehouse/materials/{Guid.NewGuid()}/location",
            new { locationId = KnownStationId, locationType = "Station" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

internal sealed class FakeTransferWarehouseRepo : IWarehouseInventoryRepository
{
    public bool RowExists { get; set; } = true;

    public Task<(InventoryRowDto Row, bool IsNew)> AddOrIncrementAsync(
        Guid itemId, Guid ownerUserId, string location, int quantity, int quality,
        Guid? locationId, string? locationType, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<InventoryRowDto>> GetInventoryAsync(
        string? name, string? type, string? subtype, Guid? ownerUserId, string? location, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<InventoryRowDto>>([]);

    public Task<InventoryFiltersDto> GetInventoryFiltersAsync(CancellationToken ct)
        => Task.FromResult(new InventoryFiltersDto([], [], []));

    public Task<IReadOnlyList<CatalogItemResultDto>> SearchCatalogItemsAsync(string? search, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CatalogItemResultDto>>([]);

    public Task<InventoryRowDto> UpdateQuantityAsync(Guid id, int quantity, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<InventoryRowDto> UpdateItemAsync(Guid id, Guid ownerUserId, Guid locationId, string locationType, int quantity, CancellationToken ct)
        => throw new NotImplementedException();

    public Task UpdateLocationAsync(Guid id, Guid locationId, string locationType, CancellationToken ct)
    {
        if (!RowExists) throw new InventoryRowNotFoundException(id);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct)
        => Task.FromResult(RowExists);

    public Task RemoveAsync(Guid id, CancellationToken ct)
        => throw new NotImplementedException();
}

internal sealed class FakeTransferMaterialRepo : IMaterialInventoryRepository
{
    public bool RowExists { get; set; } = true;

    public Task<(MaterialRowDto Row, bool IsNew)> AddOrIncrementAsync(
        Guid commodityId, Guid ownerUserId, string location, decimal quantity, int quality,
        Guid? locationId, string? locationType, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<MaterialRowDto>> GetMaterialsAsync(
        string? material, Guid? ownerUserId, string? location, int? qualityMin, int? qualityMax, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MaterialRowDto>>([]);

    public Task<MaterialFiltersDto> GetMaterialFiltersAsync(CancellationToken ct)
        => Task.FromResult(new MaterialFiltersDto([], []));

    public Task<IReadOnlyList<CommodityResultDto>> SearchCommoditiesAsync(string? search, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<CommodityResultDto>>([]);

    public Task<MaterialRowDto> UpdateQuantityAsync(Guid id, decimal quantity, CancellationToken ct)
        => throw new NotImplementedException();

    public Task<MaterialRowDto> UpdateMaterialAsync(Guid id, Guid ownerUserId, Guid locationId, string locationType, decimal quantity, CancellationToken ct)
        => throw new NotImplementedException();

    public Task UpdateLocationAsync(Guid id, Guid locationId, string locationType, CancellationToken ct)
    {
        if (!RowExists) throw new MaterialRowNotFoundException(id);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct)
        => Task.FromResult(RowExists);

    public Task RemoveAsync(Guid id, CancellationToken ct)
        => throw new NotImplementedException();
}

internal sealed class FakeTransferStationRepo : ISpaceStationRepository
{
    public bool StationExists { get; set; } = true;

    public Task<(int, int, int, int, int)> BulkUpsertAsync(
        IReadOnlyList<JsonDocument> records, IReadOnlyDictionary<int, Guid> starSystemMap, CancellationToken ct = default)
        => Task.FromResult((0, 0, 0, 0, 0));

    public Task<IReadOnlyList<StationDto>> SearchActiveStationsAsync(string? search, int limit, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<StationDto>>([]);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(StationExists);
}

internal sealed class TransferFakeLoginService : IExternalLoginService
{
    public Task<LocalUser> FindOrCreateAsync(DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<LocalUser?>(new LocalUser(userId, "Test", "test"));
}
