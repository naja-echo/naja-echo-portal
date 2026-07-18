using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.Commodities;
using NajaEcho.Domain.Items;
using NajaEcho.Infrastructure.Commodities;
using NajaEcho.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace NajaEcho.Infrastructure.Tests.Commodities;

public sealed class CommodityRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithDatabase("najaecho_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        _db = new AppDbContext(opts);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private static JsonDocument MakeRaw(int id, string name) =>
        JsonDocument.Parse($$"""{"id":{{id}},"name":"{{name}}"}""");

    private static Commodity MakeCommodity(int uexId, string name, CommodityStatus status = CommodityStatus.Active)
    {
        var now = DateTimeOffset.UtcNow;
        return new Commodity
        {
            Id = Guid.NewGuid(),
            UexId = uexId,
            Name = name,
            Status = status,
            RawData = MakeRaw(uexId, name),
            ImportedAt = now,
            UpdatedAt = now,
        };
    }

    private async Task SeedItemAsync(int uexId, string uuid, string? slug, DateTimeOffset? softDeletedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        _db.Items.Add(new Item
        {
            Id = Guid.NewGuid(),
            UexId = uexId,
            Uuid = uuid,
            Slug = slug,
            Name = $"Item {uexId}",
            RawData = JsonDocument.Parse("{}"),
            ImportedAt = now,
            UpdatedAt = now,
            SoftDeletedAt = softDeletedAt,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task BulkUpsertAsync_LinkedItemExists_ResolvesUuidAndSlug()
    {
        await SeedItemAsync(5848, "agri-uuid", "agricium");
        var repo = new CommodityRepository(_db);

        var commodity = MakeCommodity(1, "Agricium");
        commodity.IdItem = 5848;
        commodity.Uuid = null;
        commodity.Slug = null;

        await repo.BulkUpsertAsync([commodity]);
        _db.ChangeTracker.Clear();

        var stored = await _db.Commodities.FirstAsync(c => c.UexId == 1);
        stored.Uuid.Should().Be("agri-uuid");
        stored.Slug.Should().Be("agricium");
    }

    [Fact]
    public async Task BulkUpsertAsync_NoLinkedItem_LeavesUuidAndSlugNull()
    {
        var repo = new CommodityRepository(_db);

        var commodity = MakeCommodity(1, "Agricium");
        commodity.IdItem = 9999; // no matching item

        await repo.BulkUpsertAsync([commodity]);
        _db.ChangeTracker.Clear();

        var stored = await _db.Commodities.FirstAsync(c => c.UexId == 1);
        stored.Uuid.Should().BeNull();
        stored.Slug.Should().BeNull();
    }

    [Fact]
    public async Task BulkUpsertAsync_LinkedItemSoftDeleted_LeavesUuidAndSlugNull()
    {
        await SeedItemAsync(5848, "agri-uuid", "agricium", softDeletedAt: DateTimeOffset.UtcNow);
        var repo = new CommodityRepository(_db);

        var commodity = MakeCommodity(1, "Agricium");
        commodity.IdItem = 5848;

        await repo.BulkUpsertAsync([commodity]);
        _db.ChangeTracker.Clear();

        var stored = await _db.Commodities.FirstAsync(c => c.UexId == 1);
        stored.Uuid.Should().BeNull();
        stored.Slug.Should().BeNull();
    }

    [Fact]
    public async Task BulkUpsertAsync_LinkedItemAppearsOnReimport_ResolvesAndCountsUpdated()
    {
        var repo = new CommodityRepository(_db);

        var commodity = MakeCommodity(1, "Agricium");
        commodity.IdItem = 5848;

        // First import: item not yet present → unresolved.
        await repo.BulkUpsertAsync([commodity]);
        _db.ChangeTracker.Clear();
        (await _db.Commodities.FirstAsync(c => c.UexId == 1)).Uuid.Should().BeNull();

        // Item now imported.
        await SeedItemAsync(5848, "agri-uuid", "agricium");

        var reimport = MakeCommodity(1, "Agricium");
        reimport.IdItem = 5848;
        var (_, upd, unch, _, _) = await repo.BulkUpsertAsync([reimport]);

        upd.Should().Be(1);
        unch.Should().Be(0);
        _db.ChangeTracker.Clear();

        var stored = await _db.Commodities.FirstAsync(c => c.UexId == 1);
        stored.Uuid.Should().Be("agri-uuid");
        stored.Slug.Should().Be("agricium");
    }

    [Fact]
    public async Task BulkUpsertAsync_NewCommodities_InsertsAll()
    {
        var repo = new CommodityRepository(_db);
        var commodities = new[] { MakeCommodity(1, "Agricium"), MakeCommodity(2, "Laranite") };

        var (ins, upd, unch, res, sd) = await repo.BulkUpsertAsync(commodities);

        ins.Should().Be(2);
        upd.Should().Be(0);
        unch.Should().Be(0);
        res.Should().Be(0);
        sd.Should().Be(0);

        var stored = await _db.Commodities.ToListAsync();
        stored.Should().HaveCount(2);
        stored.Select(c => c.UexId).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task BulkUpsertAsync_ExistingCommodity_UpdatesFields()
    {
        var repo = new CommodityRepository(_db);
        await repo.BulkUpsertAsync([MakeCommodity(1, "OldName")]);
        _db.ChangeTracker.Clear();

        await repo.BulkUpsertAsync([MakeCommodity(1, "NewName")]);
        _db.ChangeTracker.Clear();

        var commodity = await _db.Commodities.FirstAsync(c => c.UexId == 1);
        commodity.Name.Should().Be("NewName");
    }

    [Fact]
    public async Task BulkUpsertAsync_AbsentFromFeed_SoftDeletesActiveCommodity()
    {
        var repo = new CommodityRepository(_db);
        await repo.BulkUpsertAsync([MakeCommodity(1, "Agricium"), MakeCommodity(2, "Laranite")]);
        _db.ChangeTracker.Clear();

        var (_, _, _, _, sd) = await repo.BulkUpsertAsync([MakeCommodity(2, "Laranite")]);

        sd.Should().Be(1);
        _db.ChangeTracker.Clear();

        var deleted = await _db.Commodities.FirstAsync(c => c.UexId == 1);
        deleted.Status.Should().Be(CommodityStatus.SoftDeleted);
        deleted.SoftDeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkUpsertAsync_SoftDeletedReappearsInFeed_Restores()
    {
        var repo = new CommodityRepository(_db);
        await repo.BulkUpsertAsync([MakeCommodity(1, "Agricium")]);
        _db.ChangeTracker.Clear();

        // Remove → soft-delete
        await repo.BulkUpsertAsync([MakeCommodity(2, "Laranite")]);
        _db.ChangeTracker.Clear();

        // Reappear → restore
        var (ins, upd, unch, res, sd) = await repo.BulkUpsertAsync([MakeCommodity(1, "Agricium"), MakeCommodity(2, "Laranite")]);

        res.Should().Be(1);
        sd.Should().Be(0);
        _db.ChangeTracker.Clear();

        var restored = await _db.Commodities.FirstAsync(c => c.UexId == 1);
        restored.Status.Should().Be(CommodityStatus.Active);
        restored.SoftDeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task BulkUpsertAsync_DuplicateUexIdInFeed_DoesNotThrowAndUpsertsOnce()
    {
        var repo = new CommodityRepository(_db);

        // Two records share uex_id 1; the later one should win.
        var (ins, _, _, _, _) = await repo.BulkUpsertAsync(
        [
            MakeCommodity(1, "First"),
            MakeCommodity(1, "Second"),
        ]);

        ins.Should().Be(1);
        _db.ChangeTracker.Clear();

        var stored = await _db.Commodities.Where(c => c.UexId == 1).ToListAsync();
        stored.Should().ContainSingle();
        stored[0].Name.Should().Be("Second");
    }

    [Fact]
    public async Task BulkUpsertAsync_UnchangedCommodity_CountsUnchangedAndDoesNotBumpUpdatedAt()
    {
        var repo = new CommodityRepository(_db);
        await repo.BulkUpsertAsync([MakeCommodity(1, "Agricium")]);
        _db.ChangeTracker.Clear();

        var before = await _db.Commodities.AsNoTracking().FirstAsync(c => c.UexId == 1);
        _db.ChangeTracker.Clear();

        // Re-import an identical record.
        var (_, upd, unch, _, _) = await repo.BulkUpsertAsync([MakeCommodity(1, "Agricium")]);

        upd.Should().Be(0);
        unch.Should().Be(1);
        _db.ChangeTracker.Clear();

        var after = await _db.Commodities.AsNoTracking().FirstAsync(c => c.UexId == 1);
        after.UpdatedAt.Should().Be(before.UpdatedAt);
    }

    [Fact]
    public async Task BulkUpsertAsync_PersistsIntegerArrayColumns()
    {
        var repo = new CommodityRepository(_db);
        var commodity = MakeCommodity(1, "Ore");
        commodity.IdsStarSystems = [10, 20, 30];
        commodity.IdsStarSystemsRaw = "10,20,30";

        await repo.BulkUpsertAsync([commodity]);
        _db.ChangeTracker.Clear();

        var stored = await _db.Commodities.FirstAsync(c => c.UexId == 1);
        stored.IdsStarSystems.Should().Equal(10, 20, 30);
        stored.IdsStarSystemsRaw.Should().Be("10,20,30");
    }

    [Fact]
    public async Task BulkUpsertAsync_PersistsBooleanFlags()
    {
        var repo = new CommodityRepository(_db);
        var commodity = MakeCommodity(1, "Fuel");
        commodity.IsFuel = true;
        commodity.IsAvailable = false;
        commodity.IsIllegal = true;

        await repo.BulkUpsertAsync([commodity]);
        _db.ChangeTracker.Clear();

        var stored = await _db.Commodities.FirstAsync(c => c.UexId == 1);
        stored.IsFuel.Should().BeTrue();
        stored.IsAvailable.Should().BeFalse();
        stored.IsIllegal.Should().BeTrue();
    }

    [Fact]
    public async Task BulkUpsertAsync_JsonbRoundTrip_PreservesRawData()
    {
        var repo = new CommodityRepository(_db);
        var raw = JsonDocument.Parse("""{"id":99,"name":"Test","extra_field":"some_value","nested":{"key":"val"}}""");
        var commodity = MakeCommodity(99, "Test");
        commodity.RawData = raw;

        await repo.BulkUpsertAsync([commodity]);
        _db.ChangeTracker.Clear();

        var stored = await _db.Commodities.FirstAsync(c => c.UexId == 99);
        stored.RawData.RootElement.GetProperty("extra_field").GetString().Should().Be("some_value");
    }

    [Fact]
    public async Task BulkUpsertAsync_PersistsDualTimestamps()
    {
        var repo = new CommodityRepository(_db);
        const long unixTs = 1700000000L;
        var commodity = MakeCommodity(1, "Ore");
        commodity.SourceDateAdded = unixTs;
        commodity.SourceDateAddedUtc = DateTimeOffset.FromUnixTimeSeconds(unixTs);

        await repo.BulkUpsertAsync([commodity]);
        _db.ChangeTracker.Clear();

        var stored = await _db.Commodities.FirstAsync(c => c.UexId == 1);
        stored.SourceDateAdded.Should().Be(unixTs);
        stored.SourceDateAddedUtc.Should().BeCloseTo(DateTimeOffset.FromUnixTimeSeconds(unixTs), TimeSpan.FromSeconds(1));
    }
}
