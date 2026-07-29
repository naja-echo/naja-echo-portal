using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.Hangar;
using NajaEcho.Domain.Ships;
using NajaEcho.Infrastructure.Hangar;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Hangar;

[Collection(PostgresCollection.Name)]
public sealed class HangarRepositoryGetMyHangarTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;

    public HangarRepositoryGetMyHangarTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static readonly Guid UserId1 = Guid.NewGuid();
    private static readonly Guid UserId2 = Guid.NewGuid();

    private static JsonDocument MakeRaw(string? urlPhoto = null, string? scu = null, string? crew = null) =>
        JsonDocument.Parse($$"""{"url_photo":{{(urlPhoto is null ? "null" : $"\"{urlPhoto}\"")}},"scu":{{(scu is null ? "null" : $"\"{scu}\"")}},"crew":{{(crew is null ? "null" : $"\"{crew}\"")}},"name":"test"}""");

    private Ship AddShip(string name, string? urlPhoto = null, ShipStatus status = ShipStatus.Active)
    {
        var ship = new Ship
        {
            Id = Guid.NewGuid(),
            UexId = Random.Shared.Next(1, 100000),
            Name = name,
            CompanyName = "Aegis",
            Status = status,
            RawData = MakeRaw(urlPhoto, "10", "1"),
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Ships.Add(ship);
        return ship;
    }

    private HangarEntry AddEntry(Guid userId, Guid shipId) =>
        _db.HangarEntries.Add(new HangarEntry { Id = Guid.NewGuid(), UserId = userId, ShipId = shipId, AddedAt = DateTimeOffset.UtcNow }).Entity;

    [Fact]
    public async Task GetMyHangarAsync_ReturnsOnlyCurrentUserShips()
    {
        var ship1 = AddShip("Gladius", "https://img.test/g.jpg");
        var ship2 = AddShip("Avenger");
        var ship3 = AddShip("Carrack");
        AddEntry(UserId1, ship1.Id);
        AddEntry(UserId1, ship2.Id);
        AddEntry(UserId2, ship3.Id);
        await _db.SaveChangesAsync();

        var repo = new HangarRepository(_db);
        var result = await repo.GetMyHangarAsync(UserId1, null, 1, 25, default);

        result.Items.Should().HaveCount(2);
        result.Items.Select(c => c.Name).Should().Contain(["Gladius", "Avenger"]);
        result.Items.Select(c => c.Name).Should().NotContain("Carrack");
    }

    [Fact]
    public async Task GetMyHangarAsync_ExtractsUrlPhotoFromRawData()
    {
        var ship = AddShip("Gladius", "https://img.test/g.jpg");
        AddEntry(UserId1, ship.Id);
        await _db.SaveChangesAsync();

        var repo = new HangarRepository(_db);
        var result = await repo.GetMyHangarAsync(UserId1, null, 1, 25, default);

        result.Items.Single().UrlPhoto.Should().Be("https://img.test/g.jpg");
    }

    [Fact]
    public async Task GetMyHangarAsync_ExtractsScuAndCrewFromRawData()
    {
        var ship = AddShip("Gladius");
        AddEntry(UserId1, ship.Id);
        await _db.SaveChangesAsync();

        var repo = new HangarRepository(_db);
        var result = await repo.GetMyHangarAsync(UserId1, null, 1, 25, default);

        var card = result.Items.Single();
        card.Scu.Should().Be(10m);
        card.Crew.Should().Be("1");
    }

    [Fact]
    public async Task GetMyHangarAsync_FiltersByNameCaseInsensitive()
    {
        var ship1 = AddShip("Gladius");
        var ship2 = AddShip("Avenger");
        AddEntry(UserId1, ship1.Id);
        AddEntry(UserId1, ship2.Id);
        await _db.SaveChangesAsync();

        var repo = new HangarRepository(_db);
        var result = await repo.GetMyHangarAsync(UserId1, "GLAD", 1, 25, default);

        result.Items.Should().HaveCount(1);
        result.Items.Single().Name.Should().Be("Gladius");
    }

    [Fact]
    public async Task GetMyHangarAsync_PagesResults()
    {
        for (var i = 0; i < 30; i++)
        {
            var ship = AddShip($"Ship {i:D2}");
            AddEntry(UserId1, ship.Id);
        }
        await _db.SaveChangesAsync();

        var repo = new HangarRepository(_db);
        var page1 = await repo.GetMyHangarAsync(UserId1, null, 1, 25, default);
        var page2 = await repo.GetMyHangarAsync(UserId1, null, 2, 25, default);

        page1.Items.Should().HaveCount(25);
        page2.Items.Should().HaveCount(5);
        page1.TotalCount.Should().Be(30);
        page1.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetMyHangarAsync_DoesNotIncludeSoftDeletedShips()
    {
        var activeShip = AddShip("Gladius");
        var softDeleted = AddShip("DeletedShip", status: ShipStatus.SoftDeleted);
        AddEntry(UserId1, activeShip.Id);
        AddEntry(UserId1, softDeleted.Id);
        await _db.SaveChangesAsync();

        var repo = new HangarRepository(_db);
        var result = await repo.GetMyHangarAsync(UserId1, null, 1, 25, default);

        result.Items.Should().HaveCount(1);
        result.Items.Single().Name.Should().Be("Gladius");
    }
}
