using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NajaEcho.Domain.Commodities;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Persistence;
using NajaEcho.Infrastructure.Warehouse;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Warehouse;

[Collection(PostgresCollection.Name)]
public sealed class MaterialInventoryRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;

    public MaterialInventoryRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

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

    private Commodity AddCommodity(string name = "Test Commodity", string? code = "TST")
    {
        var commodity = new Commodity
        {
            Id = Guid.NewGuid(),
            UexId = Random.Shared.Next(1, 100000),
            Name = name,
            Code = code,
            Status = CommodityStatus.Active,
            RawData = JsonDocument.Parse($"{{\"name\":\"{name}\"}}"),
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Commodities.Add(commodity);
        return commodity;
    }

    private MaterialInventoryRepository MakeRepo() => new(_db);

    // ── Constraints ──────────────────────────────────────────────────────

    [Fact]
    public async Task QuantityCheckConstraint_RejectsZeroOrNegative()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        Func<Task> act = () => _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO warehouse_material_inventory
                (id, commodity_id, owner_user_id, location, quantity, quality, created_at, updated_at)
            VALUES
                ({Guid.NewGuid()}, {commodity.Id}, {user.Id}, 'Bay 1', 0, 500, now(), now())
            """);

        await act.Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task QualityCheckConstraint_RejectsOutOfRange()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        Func<Task> act = () => _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO warehouse_material_inventory
                (id, commodity_id, owner_user_id, location, quantity, quality, created_at, updated_at)
            VALUES
                ({Guid.NewGuid()}, {commodity.Id}, {user.Id}, 'Bay 1', 1, 1001, now(), now())
            """);

        await act.Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task UniqueIndex_BlocksRawDuplicateInsert()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO warehouse_material_inventory
                (id, commodity_id, owner_user_id, location, quantity, quality, created_at, updated_at)
            VALUES
                ({Guid.NewGuid()}, {commodity.Id}, {user.Id}, 'Bay 1', 1, 500, now(), now())
            """);

        Func<Task> act = () => _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO warehouse_material_inventory
                (id, commodity_id, owner_user_id, location, quantity, quality, created_at, updated_at)
            VALUES
                ({Guid.NewGuid()}, {commodity.Id}, {user.Id}, 'Bay 1', 2, 500, now(), now())
            """);

        await act.Should().ThrowAsync<PostgresException>();
    }

    // ── GetMaterialsAsync — no filters / default sort ──────────────────────

    [Fact]
    public async Task GetMaterialsAsync_NoFilters_ReturnsAllRows()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);

        var result = await repo.GetMaterialsAsync(null, null, null, null, null, default);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetMaterialsAsync_DefaultSort_MaterialNameAsc_QualityDesc_OwnerNameAsc_LocationAsc()
    {
        var alice = AddUser("Alice");
        var bob = AddUser("Bob");
        var zeta = AddCommodity("Zeta Ore", "ZET");
        var alpha = AddCommodity("Alpha Ore", "ALP");
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        // Same commodity (Alpha), same owner (Alice), two qualities -> quality desc within name
        await repo.AddOrIncrementAsync(alpha.Id, alice.Id, "Bay 1", 1m, 300, null, null, CancellationToken.None);
        await repo.AddOrIncrementAsync(alpha.Id, alice.Id, "Bay 1", 1m, 700, null, null, CancellationToken.None);
        // Same commodity (Alpha), same quality, different owners -> owner name asc
        await repo.AddOrIncrementAsync(alpha.Id, bob.Id, "Bay 1", 1m, 300, null, null, CancellationToken.None);
        // Different commodity (Zeta) sorts after Alpha
        await repo.AddOrIncrementAsync(zeta.Id, alice.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);

        var result = await repo.GetMaterialsAsync(null, null, null, null, null, default);

        result.Should().HaveCount(4);
        result[0].MaterialName.Should().Be("Alpha Ore");
        result[0].Quality.Should().Be(700);
        result[1].MaterialName.Should().Be("Alpha Ore");
        result[1].Quality.Should().Be(300);
        result[1].OwnerDisplayName.Should().Be("Alice");
        result[2].MaterialName.Should().Be("Alpha Ore");
        result[2].Quality.Should().Be(300);
        result[2].OwnerDisplayName.Should().Be("Bob");
        result[3].MaterialName.Should().Be("Zeta Ore");
    }

    // ── GetMaterialsAsync — filters ─────────────────────────────────────

    [Fact]
    public async Task GetMaterialsAsync_MaterialFilter_MatchesNameOrCodeCaseInsensitively()
    {
        var user = AddUser();
        var titanium = AddCommodity("Titanium", "TTAM");
        var quantanium = AddCommodity("Quantanium", "QTM");
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        await repo.AddOrIncrementAsync(titanium.Id, user.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);
        await repo.AddOrIncrementAsync(quantanium.Id, user.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);

        var byName = await repo.GetMaterialsAsync("titan", null, null, null, null, default);
        byName.Should().ContainSingle().Which.MaterialName.Should().Be("Titanium");

        var byCode = await repo.GetMaterialsAsync("qtm", null, null, null, null, default);
        byCode.Should().ContainSingle().Which.MaterialName.Should().Be("Quantanium");
    }

    [Fact]
    public async Task GetMaterialsAsync_OwnerFilter_RestrictsToSingleOwner()
    {
        var alice = AddUser("Alice");
        var bob = AddUser("Bob");
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        await repo.AddOrIncrementAsync(commodity.Id, alice.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);
        await repo.AddOrIncrementAsync(commodity.Id, bob.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);

        var result = await repo.GetMaterialsAsync(null, alice.Id, null, null, null, default);

        result.Should().ContainSingle().Which.OwnerDisplayName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetMaterialsAsync_LocationFilter_RestrictsToSingleLocation()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);
        await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Dock 3", 1m, 600, null, null, CancellationToken.None);

        var result = await repo.GetMaterialsAsync(null, null, "Bay 1", null, null, default);

        result.Should().ContainSingle().Which.Location.Should().Be("Bay 1");
    }

    [Fact]
    public async Task GetMaterialsAsync_QualityRange_IsInclusiveBetween()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 100, null, null, CancellationToken.None);
        await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);
        await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 1000, null, null, CancellationToken.None);

        var result = await repo.GetMaterialsAsync(null, null, null, 100, 500, default);

        result.Should().HaveCount(2);
        result.Select(r => r.Quality).Should().BeEquivalentTo([100, 500]);
    }

    [Fact]
    public async Task GetMaterialsAsync_CombinedFilters_AppliesAndLogic()
    {
        var alice = AddUser("Alice");
        var bob = AddUser("Bob");
        var titanium = AddCommodity("Titanium", "TTAM");
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        await repo.AddOrIncrementAsync(titanium.Id, alice.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);
        await repo.AddOrIncrementAsync(titanium.Id, bob.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);
        await repo.AddOrIncrementAsync(titanium.Id, alice.Id, "Dock 3", 1m, 500, null, null, CancellationToken.None);

        var result = await repo.GetMaterialsAsync("titan", alice.Id, "Bay 1", 1, 1000, default);

        result.Should().ContainSingle();
        result[0].OwnerDisplayName.Should().Be("Alice");
        result[0].Location.Should().Be("Bay 1");
    }

    [Fact]
    public async Task GetMaterialsAsync_AllFiltersEmpty_ReturnsAllRows()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);

        var result = await repo.GetMaterialsAsync("", null, "", null, null, default);

        result.Should().HaveCount(1);
    }

    // ── AddOrIncrementAsync ──────────────────────────────────────────────

    [Fact]
    public async Task AddOrIncrementAsync_NewKey_InsertsRowAndReturnsIsNewTrue()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var (row, isNew) = await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1.5m, 500, null, null, CancellationToken.None);

        isNew.Should().BeTrue();
        row.Quantity.Should().Be(1.5m);
        row.Quality.Should().Be(500);
    }

    [Fact]
    public async Task AddOrIncrementAsync_SameKey_IncrementsQuantityAndReturnsIsNewFalse_NoDuplicateRow()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);
        var (row, isNew) = await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 2m, 500, null, null, CancellationToken.None);

        isNew.Should().BeFalse();
        row.Quantity.Should().Be(3m);

        var allRows = await repo.GetMaterialsAsync(null, null, null, null, null, default);
        allRows.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddOrIncrementAsync_QualityIsPartOfConflictKey_NeverAlteredByUpdate()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 300, null, null, CancellationToken.None);
        // Different quality -> distinct key, not an increment of the first row
        var (row, isNew) = await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 700, null, null, CancellationToken.None);

        isNew.Should().BeTrue();
        row.Quality.Should().Be(700);

        var allRows = await repo.GetMaterialsAsync(null, null, null, null, null, default);
        allRows.Should().HaveCount(2);
    }

    // ── UpdateQuantityAsync ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateQuantityAsync_SetsAbsoluteQuantity_LeavesOtherFieldsUnchanged()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var (row, _) = await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 700, null, null, CancellationToken.None);

        var updated = await repo.UpdateQuantityAsync(row.Id, 9.5m, default);

        updated.Quantity.Should().Be(9.5m);
        updated.Quality.Should().Be(700);
        updated.CommodityId.Should().Be(commodity.Id);
        updated.OwnerUserId.Should().Be(user.Id);
        updated.Location.Should().Be("Bay 1");
    }

    [Fact]
    public async Task UpdateQuantityAsync_UnknownId_ThrowsMaterialRowNotFoundException()
    {
        var act = () => MakeRepo().UpdateQuantityAsync(Guid.NewGuid(), 5m, default);
        await act.Should().ThrowAsync<NajaEcho.Application.Features.Warehouse.Materials.ChangeMaterialQuantity.MaterialRowNotFoundException>();
    }

    [Fact]
    public async Task UpdateQuantityAsync_RawUpdateToZeroOrNegative_RejectedByCheckConstraint()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var (row, _) = await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);

        Func<Task> act = () => _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE warehouse_material_inventory SET quantity = 0 WHERE id = {row.Id}
            """);

        await act.Should().ThrowAsync<PostgresException>();
    }

    // ── RemoveAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveAsync_DeletesRow_NoLongerAppearsInGetMaterialsAsync()
    {
        var user = AddUser();
        var commodity = AddCommodity();
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var (row, _) = await repo.AddOrIncrementAsync(commodity.Id, user.Id, "Bay 1", 1m, 500, null, null, CancellationToken.None);

        await repo.RemoveAsync(row.Id, default);

        var allRows = await repo.GetMaterialsAsync(null, null, null, null, null, default);
        allRows.Should().BeEmpty();
    }
}
