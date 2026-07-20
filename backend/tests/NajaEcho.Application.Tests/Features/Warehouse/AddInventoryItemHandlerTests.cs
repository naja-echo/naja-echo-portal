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

public sealed class AddInventoryItemHandlerTests
{
    private static readonly Guid KnownItemId = Guid.NewGuid();
    private static readonly Guid KnownOwnerId = Guid.NewGuid();
    private static readonly Guid KnownRowId = Guid.NewGuid();

    private sealed class FakeWarehouseRepo : IWarehouseInventoryRepository
    {
        public bool NextIsNew { get; set; } = true;
        private readonly InventoryRowDto _row;
        public FakeWarehouseRepo() => _row = new(KnownRowId, KnownItemId, "Test Item", null, null, 1, 500, KnownOwnerId, "Alice", "Bay 1");

        public Task<(InventoryRowDto Row, bool IsNew)> AddOrIncrementAsync(Guid itemId, Guid ownerUserId, string location, int quantity, int quality, Guid? locationId, string? locationType, CancellationToken ct) =>
            Task.FromResult((_row with { ItemId = itemId, OwnerUserId = ownerUserId, Location = location, Quantity = quantity, Quality = quality }, NextIsNew));

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
        public bool ItemExists { get; set; } = true;
        public int UexId { get; set; } = 42;
        public Task<Item?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(ItemExists ? (Item?)new Item
            {
                Id = id,
                Name = "Test Item",
                Status = ItemStatus.Active,
                Uuid = id.ToString(),
                UexId = UexId,
                IdCategory = 1,
                RawData = JsonDocument.Parse("{}"),
                ImportedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            } : null);
        public Task<(int, int, int, int, int)> BulkUpsertForCategoryAsync(int idCategory, IReadOnlyList<Item> incoming, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private sealed class FakeUserRepo : IUserRepository
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

    private sealed class FakeScRepo : IShipComponentRepository
    {
        public bool HasCache { get; set; }
        public bool SaveCalled { get; private set; }

        public Task<bool> HasCachedAttributesAsync(Guid id, CancellationToken ct) => Task.FromResult(HasCache);
        public Task SaveItemAttributesAsync(IReadOnlyList<ItemAttribute> attrs, CancellationToken ct) { SaveCalled = true; return Task.CompletedTask; }
        public Task<IReadOnlyList<ShipComponentRowDto>> GetShipComponentsAsync(GetShipComponentsQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<ShipComponentRowDto>>([]);
        public Task<ShipComponentFiltersDto> GetShipComponentFiltersAsync(CancellationToken ct) => Task.FromResult(new ShipComponentFiltersDto([], [], [], [], [], [], false, false, false));
        public Task<IReadOnlyList<SystemsCatalogItemDto>> SearchSystemsCatalogAsync(string? s, int l, CancellationToken ct) => Task.FromResult<IReadOnlyList<SystemsCatalogItemDto>>([]);
    }

    private sealed class FakeAttrClient : IUexItemAttributeClient
    {
        public bool ShouldThrow { get; set; }
        public int CallCount { get; private set; }
        public Task<IReadOnlyList<JsonDocument>> FetchItemAttributesAsync(int uexItemId, CancellationToken ct = default)
        {
            CallCount++;
            if (ShouldThrow) throw new HttpRequestException("UEX down");
            return Task.FromResult<IReadOnlyList<JsonDocument>>([]);
        }
    }

    private sealed class FakeStationRepo : ISpaceStationRepository
    {
        public Task<(int, int, int, int, int)> BulkUpsertAsync(IReadOnlyList<JsonDocument> records, IReadOnlyDictionary<int, Guid> starSystemMap, CancellationToken ct = default) =>
            Task.FromResult((0, 0, 0, 0, 0));
        public Task<IReadOnlyList<StationDto>> SearchActiveStationsAsync(string? search, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StationDto>>([]);
        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private static AddInventoryItemHandler MakeHandler(
        FakeWarehouseRepo? repo = null,
        FakeItemRepo? itemRepo = null,
        FakeUserRepo? userRepo = null,
        FakeScRepo? scRepo = null,
        FakeAttrClient? attrClient = null) =>
        new(
            repo ?? new FakeWarehouseRepo(),
            itemRepo ?? new FakeItemRepo(),
            userRepo ?? new FakeUserRepo(),
            scRepo ?? new FakeScRepo(),
            attrClient ?? new FakeAttrClient(),
            NullLogger<AddInventoryItemHandler>.Instance);

    [Fact]
    public async Task HandleAsync_NewRow_ReturnsCreated()
    {
        var repo = new FakeWarehouseRepo { NextIsNew = true };
        var (_, isNew) = await MakeHandler(repo).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 5), default);
        isNew.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_ExistingRow_ReturnsIncrement()
    {
        var repo = new FakeWarehouseRepo { NextIsNew = false };
        var (_, isNew) = await MakeHandler(repo).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 3), default);
        isNew.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_LocationTrimmed_PassesTrimmedToRepository()
    {
        var repo = new FakeWarehouseRepo();
        var (row, _) = await MakeHandler(repo).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "  Bay 1  ", 1), default);
        row.Location.Should().Be("Bay 1");
    }

    [Fact]
    public async Task HandleAsync_UnknownItem_ThrowsItemNotFoundException()
    {
        var itemRepo = new FakeItemRepo { ItemExists = false };
        var act = () => MakeHandler(itemRepo: itemRepo).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 1), default);
        await act.Should().ThrowAsync<ItemNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_UnknownOwner_ThrowsOwnerNotFoundException()
    {
        var userRepo = new FakeUserRepo { UserExists = false };
        var act = () => MakeHandler(userRepo: userRepo).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 1), default);
        await act.Should().ThrowAsync<OwnerNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_EmptyLocation_ThrowsArgumentException()
    {
        var act = () => MakeHandler().HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "", 1), default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HandleAsync_WhitespaceLocation_ThrowsArgumentException()
    {
        var act = () => MakeHandler().HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "   ", 1), default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HandleAsync_ZeroQuantity_ThrowsArgumentOutOfRangeException()
    {
        var act = () => MakeHandler().HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 0), default);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task HandleAsync_NegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        var act = () => MakeHandler().HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", -1), default);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // ── Attribute fetch tests ─────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_NoCachedAttributes_FetchesFromUex()
    {
        var attrClient = new FakeAttrClient();
        var scRepo = new FakeScRepo { HasCache = false };
        await MakeHandler(scRepo: scRepo, attrClient: attrClient).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 1), default);
        attrClient.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_CachedAttributesExist_SkipsFetch()
    {
        var attrClient = new FakeAttrClient();
        var scRepo = new FakeScRepo { HasCache = true };
        await MakeHandler(scRepo: scRepo, attrClient: attrClient).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 1), default);
        attrClient.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_UexFetchFails_InventoryStillCreated()
    {
        var repo = new FakeWarehouseRepo();
        var attrClient = new FakeAttrClient { ShouldThrow = true };
        var act = () => MakeHandler(repo: repo, attrClient: attrClient).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 1), default);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_ItemWithZeroUexId_SkipsFetch()
    {
        var itemRepo = new FakeItemRepo { UexId = 0 };
        var attrClient = new FakeAttrClient();
        await MakeHandler(itemRepo: itemRepo, attrClient: attrClient).HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 1), default);
        attrClient.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_QualityOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var act = () => MakeHandler().HandleAsync(
            new AddInventoryItemCommand(KnownItemId, KnownOwnerId, "Bay 1", 1, 1001), default);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
