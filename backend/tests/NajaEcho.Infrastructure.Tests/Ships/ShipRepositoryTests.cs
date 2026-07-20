using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.Ships;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Infrastructure.Tests.Ships;

[Collection(PostgresCollection.Name)]
public sealed class ShipRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;

    public ShipRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static JsonDocument MakeRaw(int id, string name) =>
        JsonDocument.Parse($$"""{"id":{{id}},"name":"{{name}}","uuid":null,"name_full":null,"company_name":null}""");

    private static Ship MakeShip(int uexId, string name, ShipStatus status = ShipStatus.Active)
    {
        var now = DateTimeOffset.UtcNow;
        return new Ship
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

    [Fact]
    public async Task GetPagedAsync_ReturnsPaginatedResultsOrderedByName()
    {
        var repo = new NajaEcho.Infrastructure.Ships.ShipRepository(_db);
        _db.Ships.AddRange(MakeShip(3, "Zebra"), MakeShip(1, "Alpha"), MakeShip(2, "Bravo"));
        await _db.SaveChangesAsync();

        var (items, total) = await repo.GetPagedAsync(1, 2);

        total.Should().Be(3);
        items.Should().HaveCount(2);
        items[0].Name.Should().Be("Alpha");
        items[1].Name.Should().Be("Bravo");
    }

    [Fact]
    public async Task BulkUpsertAsync_JsonbRoundTrip_PreservesAllFields()
    {
        var repo = new NajaEcho.Infrastructure.Ships.ShipRepository(_db);
        var raw = JsonDocument.Parse("""{"id":42,"name":"Gladius","company_name":"Aegis","custom_field":"some_value","nested":{"key":"value"}}""");
        var ship = new Ship { UexId = 42, Name = "Gladius", RawData = raw, Status = ShipStatus.Active, ImportedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

        await repo.BulkUpsertAsync([ship]);
        _db.ChangeTracker.Clear();

        var loaded = await _db.Ships.FirstAsync(s => s.UexId == 42);
        loaded.RawData.RootElement.GetProperty("custom_field").GetString().Should().Be("some_value");
    }

    [Fact]
    public async Task BulkUpsertAsync_TransactionalRollback_LeavesDataUnchanged()
    {
        var existing = MakeShip(1, "Existing");
        _db.Ships.Add(existing);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Trigger a mid-transaction failure by inserting a row that violates the
        // NOT NULL constraint on "name". The first ship is valid; the second is not.
        // If the operation is transactional, neither should be persisted.
        var badBatch = new List<Ship>
        {
            new() { UexId = 2, Name = "Valid", RawData = MakeRaw(2, "Valid"), Status = ShipStatus.Active, ImportedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            new() { UexId = 3, Name = null!, RawData = MakeRaw(3, "Invalid"), Status = ShipStatus.Active, ImportedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
        };

        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        using var freshDb = new AppDbContext(opts, new StubOrganizationContext());
        var repo2 = new NajaEcho.Infrastructure.Ships.ShipRepository(freshDb);

        await repo2.Invoking(r => r.BulkUpsertAsync(badBatch)).Should().ThrowAsync<Exception>();

        _db.ChangeTracker.Clear();
        var count = await _db.Ships.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task BulkUpsertAsync_SoftDeleteAndReactivate_WorksCorrectly()
    {
        var repo = new NajaEcho.Infrastructure.Ships.ShipRepository(_db);
        var ship = MakeShip(1, "Gladius");
        await repo.BulkUpsertAsync([ship]);
        _db.ChangeTracker.Clear();

        // Remove from feed → soft-delete
        await repo.BulkUpsertAsync([MakeShip(2, "Avenger")]);
        _db.ChangeTracker.Clear();

        var softDeleted = await _db.Ships.FirstAsync(s => s.UexId == 1);
        softDeleted.Status.Should().Be(ShipStatus.SoftDeleted);
        softDeleted.SoftDeletedAt.Should().NotBeNull();

        // Reappear in feed → reactivate
        await repo.BulkUpsertAsync([MakeShip(1, "Gladius"), MakeShip(2, "Avenger")]);
        _db.ChangeTracker.Clear();

        var reactivated = await _db.Ships.FirstAsync(s => s.UexId == 1);
        reactivated.Status.Should().Be(ShipStatus.Active);
        reactivated.SoftDeletedAt.Should().BeNull();
    }
}
