using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Warehouse.AddInventoryItem;
using NajaEcho.Application.Features.Warehouse.GetInventory;
using NajaEcho.Application.Features.Warehouse.GetInventoryFilters;
using NajaEcho.Application.Features.Warehouse.SearchCatalogItems;
using NajaEcho.Application.Features.Warehouse.ShipComponents.GetShipComponentFilters;
using NajaEcho.Application.Features.Warehouse.ShipComponents.GetShipComponents;
using NajaEcho.Application.Features.Warehouse.ShipComponents.SearchSystemsCatalog;
using NajaEcho.Domain.Items;
using NajaEcho.Domain.Warehouse;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Warehouse;

public sealed class AddInventoryItemLocationTests
{
    private static readonly Guid KnownItemId = Guid.NewGuid();
    private static readonly Guid KnownOwnerId = Guid.NewGuid();
    private static readonly Guid KnownRowId = Guid.NewGuid();
    private static readonly Guid KnownLocationId = Guid.NewGuid();

    private sealed class CapturingWarehouseRepo : IWarehouseInventoryRepository
    {
        public Guid? CapturedLocationId { get; private set; }
        public string? CapturedLocationType { get; private set; }

        public Task<(InventoryRowDto Row, bool IsNew)> AddOrIncrementAsync(
            Guid itemId, Guid ownerUserId, string location, int quantity, int quality, Guid? locationId, string? locationType, CancellationToken ct)
        {
            CapturedLocationId = locationId;
            CapturedLocationType = locationType;
            var row = new InventoryRowDto(KnownRowId, itemId, "Test Item", null, null, quantity, quality, ownerUserId, "Alice", location);
            return Task.FromResult((row, true));
        }

        public Task<IReadOnlyList<InventoryRowDto>> GetInventoryAsync(string? name, string? type, string? subtype, Guid? ownerUserId, string? location, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<InventoryRowDto>>([]);
        public Task<InventoryFiltersDto> GetInventoryFiltersAsync(CancellationToken ct) =>
            Task.FromResult(new InventoryFiltersDto([], [], []));
        public Task<IReadOnlyList<CatalogItemResultDto>> SearchCatalogItemsAsync(string? search, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CatalogItemResultDto>>([]);
        public Task<InventoryRowDto> UpdateQuantityAsync(Guid id, int quantity, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<InventoryRowDto> UpdateItemAsync(Guid id, Guid ownerUserId, Guid locationId, string locationType, int quantity, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task UpdateLocationAsync(Guid id, Guid locationId, string locationType, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task RemoveAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeItemRepo : IItemRepository
    {
        public Task<Item?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Item?>(new Item
            {
                Id = id,
                Name = "Test Item",
                Status = ItemStatus.Active,
                Uuid = id.ToString(),
                UexId = 42,
                IdCategory = 1,
                RawData = JsonDocument.Parse("{}"),
                ImportedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        public Task<(int, int, int, int, int)> BulkUpsertForCategoryAsync(int idCategory, IReadOnlyList<Item> incoming, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class FakeUserRepo : IUserRepository
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

    private sealed class FakeScRepo : IShipComponentRepository
    {
        public Task<bool> HasCachedAttributesAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task SaveItemAttributesAsync(IReadOnlyList<ItemAttribute> attrs, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<ShipComponentRowDto>> GetShipComponentsAsync(GetShipComponentsQuery q, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ShipComponentRowDto>>([]);
        public Task<ShipComponentFiltersDto> GetShipComponentFiltersAsync(CancellationToken ct) =>
            Task.FromResult(new ShipComponentFiltersDto([], [], [], [], [], [], false, false, false));
        public Task<IReadOnlyList<SystemsCatalogItemDto>> SearchSystemsCatalogAsync(string? s, int l, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SystemsCatalogItemDto>>([]);
    }

    private sealed class FakeAttrClient : IUexItemAttributeClient
    {
        public Task<IReadOnlyList<JsonDocument>> FetchItemAttributesAsync(int uexItemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<JsonDocument>>([]);
    }

    private static AddInventoryItemHandler MakeHandler(CapturingWarehouseRepo? repo = null) =>
        new(
            repo ?? new CapturingWarehouseRepo(),
            new FakeItemRepo(),
            new FakeUserRepo(),
            new FakeScRepo(),
            new FakeAttrClient(),
            NullLogger<AddInventoryItemHandler>.Instance);

    [Fact]
    public async Task AddItem_WithStationLocationId_PersistsLocationId()
    {
        var repo = new CapturingWarehouseRepo();

        await MakeHandler(repo).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 1, LocationId: KnownLocationId, LocationType: "Station"), default);

        repo.CapturedLocationId.Should().Be(KnownLocationId);
        repo.CapturedLocationType.Should().Be("Station");
    }

    [Fact]
    public async Task AddItem_WithCityLocationId_PersistsLocationId()
    {
        var repo = new CapturingWarehouseRepo();

        await MakeHandler(repo).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 1, LocationId: KnownLocationId, LocationType: "City"), default);

        repo.CapturedLocationId.Should().Be(KnownLocationId);
        repo.CapturedLocationType.Should().Be("City");
    }

    [Fact]
    public async Task AddItem_WithNullLocationId_PersistsNull()
    {
        var repo = new CapturingWarehouseRepo();

        await MakeHandler(repo).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 1, LocationId: null, LocationType: null), default);

        repo.CapturedLocationId.Should().BeNull();
        repo.CapturedLocationType.Should().BeNull();
    }
}
