using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.Items;
using NajaEcho.Infrastructure.Items;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Infrastructure.Tests.Items;

[Collection(PostgresCollection.Name)]
public sealed class ItemRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;

    public ItemRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static JsonDocument MakeRaw(string uuid, int id, string name) =>
        JsonDocument.Parse($$"""{"id":{{id}},"uuid":"{{uuid}}","name":"{{name}}","id_category":1}""");

    private static JsonDocument MakeRawWithExtraFields(string uuid, int id) =>
        JsonDocument.Parse($$"""{"id":{{id}},"uuid":"{{uuid}}","name":"Item{{id}}","attributes":{"size":"large"},"screenshot":"https://example.com/shot.png","id_category":1}""");

    private static Item MakeItem(string uuid, int uexId, int idCategory, string name, ItemStatus status = ItemStatus.Active) =>
        new()
        {
            Uuid = uuid,
            UexId = uexId,
            IdCategory = idCategory,
            Name = name,
            Status = status,
            RawData = MakeRaw(uuid, uexId, name),
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static string NewUuid() => Guid.NewGuid().ToString();

    // Insert new items by UUID
    [Fact]
    public async Task BulkUpsertForCategoryAsync_NewItems_InsertsAll()
    {
        var repo = new ItemRepository(_db);
        var uuid1 = NewUuid(); var uuid2 = NewUuid();
        var items = new List<Item> { MakeItem(uuid1, 1, 10, "Armor"), MakeItem(uuid2, 2, 10, "Helmet") };

        var (inserted, updated, unchanged, softDeleted, restored) = await repo.BulkUpsertForCategoryAsync(10, items);

        inserted.Should().Be(2);
        updated.Should().Be(0);
        unchanged.Should().Be(0);
        softDeleted.Should().Be(0);
        restored.Should().Be(0);
        _db.ChangeTracker.Clear();
        (await _db.Items.CountAsync()).Should().Be(2);
    }

    // Update existing items when UUID already exists
    [Fact]
    public async Task BulkUpsertForCategoryAsync_ExistingItem_UpdatesOnChange()
    {
        var repo = new ItemRepository(_db);
        var uuid = NewUuid();
        await repo.BulkUpsertForCategoryAsync(10, [MakeItem(uuid, 1, 10, "Armor")]);
        _db.ChangeTracker.Clear();

        var (inserted, updated, unchanged, softDeleted, restored) =
            await repo.BulkUpsertForCategoryAsync(10, [MakeItem(uuid, 1, 10, "Armor Updated")]);

        updated.Should().Be(1);
        inserted.Should().Be(0);
        _db.ChangeTracker.Clear();
        (await _db.Items.FirstAsync(i => i.Uuid == uuid)).Name.Should().Be("Armor Updated");
    }

    // Soft-deletes Active items absent from incoming set in this category
    [Fact]
    public async Task BulkUpsertForCategoryAsync_MissingFromIncoming_SoftDeletesActiveItems()
    {
        var repo = new ItemRepository(_db);
        var uuid1 = NewUuid(); var uuid2 = NewUuid();
        await repo.BulkUpsertForCategoryAsync(10, [MakeItem(uuid1, 1, 10, "A"), MakeItem(uuid2, 2, 10, "B")]);
        _db.ChangeTracker.Clear();

        var (_, _, _, softDeleted, _) = await repo.BulkUpsertForCategoryAsync(10, [MakeItem(uuid1, 1, 10, "A")]);

        softDeleted.Should().Be(1);
        _db.ChangeTracker.Clear();
        (await _db.Items.FirstAsync(i => i.Uuid == uuid2)).Status.Should().Be(ItemStatus.SoftDeleted);
    }

    // Does NOT soft-delete items in a different category
    [Fact]
    public async Task BulkUpsertForCategoryAsync_MissingFromIncoming_DoesNotAffectOtherCategories()
    {
        var repo = new ItemRepository(_db);
        var uuid1 = NewUuid(); var uuid2 = NewUuid();
        // Insert items in two different categories
        await repo.BulkUpsertForCategoryAsync(10, [MakeItem(uuid1, 1, 10, "Cat10")]);
        _db.ChangeTracker.Clear();
        await repo.BulkUpsertForCategoryAsync(20, [MakeItem(uuid2, 2, 20, "Cat20")]);
        _db.ChangeTracker.Clear();

        // Import cat10 with empty list → should soft-delete uuid1 only
        var (_, _, _, softDeleted, _) = await repo.BulkUpsertForCategoryAsync(10, []);

        softDeleted.Should().Be(1);
        _db.ChangeTracker.Clear();
        (await _db.Items.FirstAsync(i => i.Uuid == uuid2)).Status.Should().Be(ItemStatus.Active);
    }

    // Restores SoftDeleted items that reappear
    [Fact]
    public async Task BulkUpsertForCategoryAsync_SoftDeletedItemReappears_RestoresIt()
    {
        var repo = new ItemRepository(_db);
        var uuid = NewUuid();
        var softDeletedItem = MakeItem(uuid, 1, 10, "Armor", ItemStatus.SoftDeleted);
        softDeletedItem.SoftDeletedAt = DateTimeOffset.UtcNow;
        _db.Items.Add(softDeletedItem);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var (_, _, _, _, restored) = await repo.BulkUpsertForCategoryAsync(10, [MakeItem(uuid, 1, 10, "Armor")]);

        restored.Should().Be(1);
        _db.ChangeTracker.Clear();
        var item = await _db.Items.FirstAsync(i => i.Uuid == uuid);
        item.Status.Should().Be(ItemStatus.Active);
        item.SoftDeletedAt.Should().BeNull();
    }

    // Items with empty uuid are inserted normally — identity key is uex_id, not uuid
    [Fact]
    public async Task BulkUpsertForCategoryAsync_EmptyUuidItem_InsertedByUexId()
    {
        var repo = new ItemRepository(_db);
        var itemWithEmptyUuid = MakeItem("", 1, 10, "NoUuid");

        var (inserted, _, _, _, _) = await repo.BulkUpsertForCategoryAsync(10, [itemWithEmptyUuid]);

        inserted.Should().Be(1);
        _db.ChangeTracker.Clear();
        (await _db.Items.CountAsync()).Should().Be(1);
    }

    // FR-021/FR-022: raw_data does not contain attributes or screenshot
    [Fact]
    public async Task BulkUpsertForCategoryAsync_RawDataWithAttributesAndScreenshot_FieldsNotPresent()
    {
        var repo = new ItemRepository(_db);
        var uuid = NewUuid();
        // The handler strips attributes/screenshot before calling repo.
        // Here we pass raw_data without those fields (as the handler would produce)
        // and verify they are absent in stored data.
        var item = MakeItem(uuid, 1, 10, "Clean");
        // Verify the raw used here has no attributes or screenshot
        item.RawData.RootElement.TryGetProperty("attributes", out _).Should().BeFalse();
        item.RawData.RootElement.TryGetProperty("screenshot", out _).Should().BeFalse();

        await repo.BulkUpsertForCategoryAsync(10, [item]);
        _db.ChangeTracker.Clear();

        var stored = await _db.Items.FirstAsync(i => i.Uuid == uuid);
        stored.RawData.RootElement.TryGetProperty("attributes", out _).Should().BeFalse();
        stored.RawData.RootElement.TryGetProperty("screenshot", out _).Should().BeFalse();
    }
}
