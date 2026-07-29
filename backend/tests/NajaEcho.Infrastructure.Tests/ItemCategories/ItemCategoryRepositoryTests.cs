using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.ItemCategories;
using NajaEcho.Infrastructure.ItemCategories;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Infrastructure.Tests.ItemCategories;

[Collection(PostgresCollection.Name)]
public sealed class ItemCategoryRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;

    public ItemCategoryRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static JsonDocument MakeRaw(int id, string name) =>
        JsonDocument.Parse($$"""{"id":{{id}},"type":"item","name":"{{name}}"}""");

    private static ItemCategory MakeCategory(int uexId, string name, string type = "item") =>
        new()
        {
            UexId = uexId,
            Type = type,
            Name = name,
            IsGameRelated = true,
            IsMining = false,
            RawData = MakeRaw(uexId, name),
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    // (a) Insert new categories
    [Fact]
    public async Task BulkUpsertAsync_NewCategories_InsertsAll()
    {
        var repo = new ItemCategoryRepository(_db);
        var incoming = new List<ItemCategory> { MakeCategory(1, "Armor"), MakeCategory(2, "Weapons") };

        var (inserted, updated, unchanged) = await repo.BulkUpsertAsync(incoming);

        inserted.Should().Be(2);
        updated.Should().Be(0);
        unchanged.Should().Be(0);
        _db.ChangeTracker.Clear();
        (await _db.ItemCategories.CountAsync()).Should().Be(2);
    }

    // (b) Update existing categories when fields change
    [Fact]
    public async Task BulkUpsertAsync_ChangedCategories_UpdatesCorrectly()
    {
        var repo = new ItemCategoryRepository(_db);
        await repo.BulkUpsertAsync([MakeCategory(1, "Armor")]);
        _db.ChangeTracker.Clear();

        var updated = MakeCategory(1, "Armor Updated");
        var (inserted, updatedCount, unchanged) = await repo.BulkUpsertAsync([updated]);

        updatedCount.Should().Be(1);
        inserted.Should().Be(0);
        unchanged.Should().Be(0);
        _db.ChangeTracker.Clear();
        (await _db.ItemCategories.FirstAsync(c => c.UexId == 1)).Name.Should().Be("Armor Updated");
    }

    // (c) raw_data roundtrip — attributes excluded but other fields preserved
    [Fact]
    public async Task BulkUpsertAsync_JsonbRoundTrip_PreservesFields()
    {
        var repo = new ItemCategoryRepository(_db);
        var raw = JsonDocument.Parse("""{"id":42,"type":"item","name":"Armor","section":"Combat","custom":"value"}""");
        var cat = new ItemCategory
        {
            UexId = 42,
            Type = "item",
            Name = "Armor",
            RawData = raw,
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await repo.BulkUpsertAsync([cat]);
        _db.ChangeTracker.Clear();

        var loaded = await _db.ItemCategories.FirstAsync(c => c.UexId == 42);
        loaded.RawData.RootElement.GetProperty("custom").GetString().Should().Be("value");
    }

    // (d) Transaction rollback — duplicate uex_id should leave data unchanged
    [Fact]
    public async Task BulkUpsertAsync_TransactionalRollback_LeavesDataUnchanged()
    {
        var existing = MakeCategory(1, "Existing");
        _db.ItemCategories.Add(existing);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Trigger a mid-transaction failure by inserting a row that violates the
        // NOT NULL constraint on "name". The first category is valid; the second is not.
        // If the operation is transactional, neither should be persisted.
        var badBatch = new List<ItemCategory>
        {
            new() { UexId = 2, Type = "item", Name = "Valid", RawData = MakeRaw(2, "Valid"), ImportedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { UexId = 3, Type = "item", Name = null!, RawData = MakeRaw(3, "Invalid"), ImportedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
        };

        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var freshDb = new AppDbContext(opts, new StubOrganizationContext());
        var repo2 = new ItemCategoryRepository(freshDb);

        await repo2.Invoking(r => r.BulkUpsertAsync(badBatch)).Should().ThrowAsync<Exception>();

        _db.ChangeTracker.Clear();
        (await _db.ItemCategories.CountAsync()).Should().Be(1);
    }

    // (e) GetEligibleAsync — only returns categories where type = "item"
    [Fact]
    public async Task GetEligibleAsync_FiltersToItemTypeOnly()
    {
        var repo = new ItemCategoryRepository(_db);
        await repo.BulkUpsertAsync([
            MakeCategory(10, "Armor", "item"),
            MakeCategory(11, "Components", "item"),
            MakeCategory(20, "Ships", "vehicle"),
            MakeCategory(21, "Services", "service"),
        ]);
        _db.ChangeTracker.Clear();

        var eligible = await repo.GetEligibleAsync();

        eligible.Should().HaveCount(2);
        eligible.Should().AllSatisfy(c => c.Type.Should().Be("item"));
        eligible.Select(c => c.UexId).Should().BeEquivalentTo([10, 11]);
    }
}
