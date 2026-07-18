using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.Items;
using NajaEcho.Domain.Warehouse;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Persistence;
using NajaEcho.Infrastructure.Warehouse;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Warehouse;

[Collection(PostgresCollection.Name)]
public sealed class WarehouseInventoryRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;

    public WarehouseInventoryRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

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

    private Item AddItem(string name = "Test Item", string? section = null, string? category = null)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            UexId = Random.Shared.Next(1, 100000),
            IdCategory = 1,
            Name = name,
            Status = ItemStatus.Active,
            RawData = JsonDocument.Parse($"{{\"name\":\"{name}\"}}"),
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Items.Add(item);
        return item;
    }

    private WarehouseInventoryRepository MakeRepo() => new(_db);

    // ── Add-or-Increment ─────────────────────────────────────────────────

    [Fact]
    public async Task AddOrIncrementAsync_NewEntry_CreatesRow()
    {
        var user = AddUser();
        var item = AddItem();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var (row, isNew) = await repo.AddOrIncrementAsync(item.Id, user.Id, "Bay 1", 3, 700, null, null, CancellationToken.None);

        isNew.Should().BeTrue();
        row.Quantity.Should().Be(3);
        row.Quality.Should().Be(700);
        row.Location.Should().Be("Bay 1");
    }

    [Fact]
    public async Task AddOrIncrementAsync_ExistingEntry_IncrementsQuantity()
    {
        var user = AddUser();
        var item = AddItem();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        await repo.AddOrIncrementAsync(item.Id, user.Id, "Bay 1", 3, 500, null, null, CancellationToken.None);
        var (row, isNew) = await repo.AddOrIncrementAsync(item.Id, user.Id, "Bay 1", 2, 650, null, null, CancellationToken.None);

        isNew.Should().BeFalse();
        row.Quantity.Should().Be(5);
        row.Quality.Should().Be(650);
    }

    [Fact]
    public async Task AddOrIncrementAsync_DifferentLocation_CreatesSeparateRow()
    {
        var user = AddUser();
        var item = AddItem();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var (row1, isNew1) = await repo.AddOrIncrementAsync(item.Id, user.Id, "Bay 1", 1, 500, null, null, CancellationToken.None);
        var (row2, isNew2) = await repo.AddOrIncrementAsync(item.Id, user.Id, "Bay 2", 1, 500, null, null, CancellationToken.None);

        isNew1.Should().BeTrue();
        isNew2.Should().BeTrue();
        row1.Id.Should().NotBe(row2.Id);
    }

    [Fact]
    public async Task AddOrIncrementAsync_DifferentOwner_CreatesSeparateRow()
    {
        var user1 = AddUser("Alice");
        var user2 = AddUser("Bob");
        var item = AddItem();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var (row1, isNew1) = await repo.AddOrIncrementAsync(item.Id, user1.Id, "Bay 1", 1, 500, null, null, CancellationToken.None);
        var (row2, isNew2) = await repo.AddOrIncrementAsync(item.Id, user2.Id, "Bay 1", 1, 500, null, null, CancellationToken.None);

        isNew1.Should().BeTrue();
        isNew2.Should().BeTrue();
        row1.Id.Should().NotBe(row2.Id);
    }

    [Fact]
    public async Task AddOrIncrementAsync_UniqueConstraintEnforced()
    {
        var user = AddUser();
        var item = AddItem();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        await repo.AddOrIncrementAsync(item.Id, user.Id, "Bay 1", 1, 500, null, null, CancellationToken.None);

        // Second call must not create a second row — must increment
        await repo.AddOrIncrementAsync(item.Id, user.Id, "Bay 1", 4, 500, null, null, CancellationToken.None);

        var count = await _db.WarehouseInventory.CountAsync();
        count.Should().Be(1);
    }

    // ── Update Quantity ──────────────────────────────────────────────────

    [Fact]
    public async Task UpdateQuantityAsync_ExistingRow_ReplacesQuantity()
    {
        var user = AddUser();
        var item = AddItem();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var (created, _) = await repo.AddOrIncrementAsync(item.Id, user.Id, "Bay 1", 5, 500, null, null, CancellationToken.None);
        var updated = await repo.UpdateQuantityAsync(created.Id, 12, default);

        updated.Quantity.Should().Be(12);
    }

    [Fact]
    public async Task UpdateQuantityAsync_MissingRow_Throws()
    {
        var repo = MakeRepo();
        Func<Task> act = () => repo.UpdateQuantityAsync(Guid.NewGuid(), 5, default);
        await act.Should().ThrowAsync<Exception>();
    }

    // ── Remove ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_ExistingRow_DeletesRow()
    {
        var user = AddUser();
        var item = AddItem();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var (created, _) = await repo.AddOrIncrementAsync(item.Id, user.Id, "Bay 1", 1, 500, null, null, CancellationToken.None);
        await repo.RemoveAsync(created.Id, default);

        var exists = await _db.WarehouseInventory.AnyAsync(w => w.Id == created.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_DoesNotDeleteCatalogItem()
    {
        var user = AddUser();
        var item = AddItem();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var (created, _) = await repo.AddOrIncrementAsync(item.Id, user.Id, "Bay 1", 1, 500, null, null, CancellationToken.None);
        await repo.RemoveAsync(created.Id, default);

        var itemStillExists = await _db.Items.AnyAsync(i => i.Id == item.Id);
        itemStillExists.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAsync_MissingRow_Throws()
    {
        var repo = MakeRepo();
        Func<Task> act = () => repo.RemoveAsync(Guid.NewGuid(), default);
        await act.Should().ThrowAsync<Exception>();
    }
}
