using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Application.Features.Warehouse.ShipComponents.GetShipComponents;
using NajaEcho.Domain.Items;
using NajaEcho.Domain.Warehouse;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Persistence;
using NajaEcho.Infrastructure.Warehouse;

namespace NajaEcho.Infrastructure.Tests.Warehouse;

[Collection(PostgresCollection.Name)]
public sealed class ShipComponentRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;

    public ShipComponentRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private ApplicationUser AddUser(string displayName = "Test User")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            DiscordUsername = displayName.ToLower().Replace(" ", ""),
            UserName = displayName,
            NormalizedUserName = displayName.ToUpper(),
            Email = $"{displayName}@test.com",
            NormalizedEmail = $"{displayName}@test.com".ToUpper(),
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        _db.Set<ApplicationUser>().Add(user);
        return user;
    }

    private Item AddItem(string name = "Test Item", string? section = "Systems", string? category = "Shield",
        ItemStatus status = ItemStatus.Active)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            UexId = Random.Shared.Next(1, 100000),
            IdCategory = 1,
            Name = name,
            Section = section,
            Category = category,
            Status = status,
            RawData = JsonDocument.Parse($"{{\"name\":\"{name}\"}}"),
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Items.Add(item);
        return item;
    }

    private WarehouseInventoryEntry AddInventoryEntry(Guid itemId, Guid ownerId, string location = "Bay 1", int qty = 1)
    {
        var entry = new WarehouseInventoryEntry
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            OwnerUserId = ownerId,
            Location = location,
            Quantity = qty,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.WarehouseInventory.Add(entry);
        return entry;
    }

    private ItemAttribute AddAttr(Guid itemId, string name, string? value, int uexCategoryAttributeId)
    {
        var attr = new ItemAttribute
        {
            Id = Guid.NewGuid(),
            ItemId = itemId,
            UexItemId = Random.Shared.Next(1, 100000),
            UexCategoryAttributeId = uexCategoryAttributeId,
            AttributeName = name,
            Value = value,
            FetchedAt = DateTimeOffset.UtcNow,
        };
        _db.ItemAttributes.Add(attr);
        return attr;
    }

    private ShipComponentRepository MakeRepo() => new(_db);

    // ── Section-scoped list ───────────────────────────────────────────────

    [Fact]
    public async Task GetShipComponentsAsync_ReturnsSystemsAndVehicleWeaponsRows()
    {
        var user = AddUser();
        var systemsItem = AddItem("Shield A", section: "Systems");
        var vehicleWeaponsItem = AddItem("Laser B", section: "Vehicle Weapons");
        var otherItem = AddItem("Cargo C", section: "Cargo");
        AddInventoryEntry(systemsItem.Id, user.Id);
        AddInventoryEntry(vehicleWeaponsItem.Id, user.Id);
        AddInventoryEntry(otherItem.Id, user.Id);
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var query = new GetShipComponentsQuery(null, null, null, null, null, null, null, false, false, false);
        var rows = await repo.GetShipComponentsAsync(query, default);

        rows.Should().HaveCount(2);
        rows.Select(r => r.Name).Should().BeEquivalentTo(["Shield A", "Laser B"]);
    }

    [Fact]
    public async Task GetShipComponentsAsync_AttributesLeftJoined_NullWhenAbsent()
    {
        var user = AddUser();
        var item = AddItem("Shield X", section: "Systems");
        AddInventoryEntry(item.Id, user.Id);
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var query = new GetShipComponentsQuery(null, null, null, null, null, null, null, false, false, false);
        var rows = await repo.GetShipComponentsAsync(query, default);

        rows.Should().HaveCount(1);
        rows[0].Class.Should().BeNull();
        rows[0].Size.Should().BeNull();
        rows[0].Grade.Should().BeNull();
    }

    [Fact]
    public async Task GetShipComponentsAsync_WithAttributes_ReturnsClassSizeGrade()
    {
        var user = AddUser();
        var item = AddItem("Shield Z", section: "Systems");
        AddInventoryEntry(item.Id, user.Id);
        AddAttr(item.Id, "Class", "Military", 1);
        AddAttr(item.Id, "Size", "3", 2);
        AddAttr(item.Id, "Grade", "A", 3);
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var query = new GetShipComponentsQuery(null, null, null, null, null, null, null, false, false, false);
        var rows = await repo.GetShipComponentsAsync(query, default);

        rows.Should().HaveCount(1);
        rows[0].Class.Should().Be("Military");
        rows[0].Size.Should().Be(3);
        rows[0].Grade.Should().Be("A");
    }

    // ── Unique constraint on item_attributes ─────────────────────────────

    [Fact]
    public async Task SaveItemAttributesAsync_DuplicateCategoryAttributeId_Upserts()
    {
        var item = AddItem("Gun Y");
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var attr1 = new ItemAttribute
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            UexItemId = item.UexId,
            UexCategoryAttributeId = 42,
            AttributeName = "Size",
            Value = "1",
            FetchedAt = DateTimeOffset.UtcNow,
        };
        await repo.SaveItemAttributesAsync([attr1], default);

        var attr2 = new ItemAttribute
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            UexItemId = item.UexId,
            UexCategoryAttributeId = 42,
            AttributeName = "Size",
            Value = "2",
            FetchedAt = DateTimeOffset.UtcNow,
        };

        var act = async () => await repo.SaveItemAttributesAsync([attr2], default);
        await act.Should().NotThrowAsync("upsert semantics should handle duplicate");
    }

    // ── View derivation (sc.ship_component_attributes) ───────────────────

    [Fact]
    public async Task ShipComponentAttributesView_DerivesClassSizeGradeFromRawAttributes()
    {
        var item = AddItem("Missile M");
        AddAttr(item.Id, "Class", "Civilian", 10);
        AddAttr(item.Id, "Size", "2", 11);
        AddAttr(item.Id, "Grade", "B", 12);
        await _db.SaveChangesAsync();

        var projection = await _db.ShipComponentAttributes.FirstOrDefaultAsync(s => s.ItemId == item.Id);

        projection.Should().NotBeNull();
        projection!.Class.Should().Be("Civilian");
        projection.Size.Should().Be(2);
        projection.Grade.Should().Be("B");
    }

    [Fact]
    public async Task ShipComponentAttributesView_NonNumericSize_DerivesNull()
    {
        var item = AddItem("Rocket R");
        AddAttr(item.Id, "Size", "large", 20);
        await _db.SaveChangesAsync();

        var projection = await _db.ShipComponentAttributes.FirstOrDefaultAsync(s => s.ItemId == item.Id);

        projection.Should().NotBeNull();
        projection!.Size.Should().BeNull();
    }

    [Fact]
    public async Task HasCachedAttributesAsync_ReturnsFalseWhenNoAttributes()
    {
        var item = AddItem("Empty E");
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var result = await repo.HasCachedAttributesAsync(item.Id, default);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasCachedAttributesAsync_ReturnsTrueWhenAttributesExist()
    {
        var item = AddItem("Cached C");
        await _db.SaveChangesAsync();

        _db.ItemAttributes.Add(new ItemAttribute
        {
            Id = Guid.NewGuid(),
            ItemId = item.Id,
            UexItemId = item.UexId,
            UexCategoryAttributeId = 1,
            AttributeName = "Class",
            Value = "A",
            FetchedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var result = await repo.HasCachedAttributesAsync(item.Id, default);

        result.Should().BeTrue();
    }

    // ── Filter integration: name partial match ───────────────────────────

    [Fact]
    public async Task GetShipComponentsAsync_NameFilter_ReturnsMatchingRows()
    {
        var user = AddUser();
        var item1 = AddItem("Alpha Shield", section: "Systems");
        var item2 = AddItem("Beta Gun", section: "Systems");
        AddInventoryEntry(item1.Id, user.Id);
        AddInventoryEntry(item2.Id, user.Id);
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var query = new GetShipComponentsQuery("shield", null, null, null, null, null, null, false, false, false);
        var rows = await repo.GetShipComponentsAsync(query, default);

        rows.Should().HaveCount(1);
        rows[0].Name.Should().Be("Alpha Shield");
    }

    // ── Filter integration: unknownClass sentinel ─────────────────────────

    [Fact]
    public async Task GetShipComponentsAsync_UnknownClassFilter_ReturnsNullClassRows()
    {
        var user = AddUser();
        var item1 = AddItem("No-class Item", section: "Systems");
        var item2 = AddItem("Classed Item", section: "Systems");
        AddInventoryEntry(item1.Id, user.Id);
        AddInventoryEntry(item2.Id, user.Id);
        AddAttr(item2.Id, "Class", "Military", 1);
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var query = new GetShipComponentsQuery(null, null, null, null, null, null, null, UnknownClass: true, false, false);
        var rows = await repo.GetShipComponentsAsync(query, default);

        rows.Should().HaveCount(1);
        rows[0].Name.Should().Be("No-class Item");
    }

    // ── Filter: empty params returns all rows ─────────────────────────────

    [Fact]
    public async Task GetShipComponentsAsync_NoFilters_ReturnsAllSystemsRows()
    {
        var user = AddUser();
        for (var i = 0; i < 3; i++)
        {
            var item = AddItem($"Item {i}", section: "Systems");
            AddInventoryEntry(item.Id, user.Id);
        }
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var query = new GetShipComponentsQuery(null, null, null, null, null, null, null, false, false, false);
        var rows = await repo.GetShipComponentsAsync(query, default);

        rows.Should().HaveCount(3);
    }

    // ── Filter: multi-value type OR ───────────────────────────────────────

    [Fact]
    public async Task GetShipComponentsAsync_MultiTypeFilter_ReturnsMatchingRows()
    {
        var user = AddUser();
        var shield = AddItem("Shield A", section: "Systems", category: "Shield");
        var gun = AddItem("Gun A", section: "Systems", category: "Gun");
        var missile = AddItem("Missile A", section: "Systems", category: "Missile");
        AddInventoryEntry(shield.Id, user.Id);
        AddInventoryEntry(gun.Id, user.Id);
        AddInventoryEntry(missile.Id, user.Id);
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var query = new GetShipComponentsQuery(null, ["Shield", "Gun"], null, null, null, null, null, false, false, false);
        var rows = await repo.GetShipComponentsAsync(query, default);

        rows.Should().HaveCount(2);
        rows.Select(r => r.Type).Should().BeEquivalentTo(["Shield", "Gun"]);
    }

    // ── Filter: multi-field AND (type + class) ────────────────────────────

    [Fact]
    public async Task GetShipComponentsAsync_TypeAndClassFilter_AppliesAndSemantics()
    {
        var user = AddUser();
        var item1 = AddItem("Shield Military", section: "Systems", category: "Shield");
        var item2 = AddItem("Shield Civilian", section: "Systems", category: "Shield");
        var item3 = AddItem("Gun Military", section: "Systems", category: "Gun");
        AddInventoryEntry(item1.Id, user.Id);
        AddInventoryEntry(item2.Id, user.Id);
        AddInventoryEntry(item3.Id, user.Id);
        AddAttr(item1.Id, "Class", "Military", 1);
        AddAttr(item2.Id, "Class", "Civilian", 1);
        AddAttr(item3.Id, "Class", "Military", 1);
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var query = new GetShipComponentsQuery(null, ["Shield"], ["Military"], null, null, null, null, false, false, false);
        var rows = await repo.GetShipComponentsAsync(query, default);

        rows.Should().HaveCount(1);
        rows[0].Name.Should().Be("Shield Military");
    }

    [Fact]
    public async Task SearchSystemsCatalogAsync_IncludesSystemsAndVehicleWeaponsOnly()
    {
        AddItem("Shield A", section: "Systems");
        AddItem("Laser B", section: "Vehicle Weapons");
        AddItem("Cargo C", section: "Cargo");
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var rows = await repo.SearchSystemsCatalogAsync(null, 25, default);

        rows.Select(r => r.Name).Should().BeEquivalentTo(["Shield A", "Laser B"]);
    }
}
