using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Application.Features.Characters.VerifyCharacter;
using NajaEcho.Domain.Characters;
using NajaEcho.Infrastructure.Characters;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Characters;

[Collection(PostgresCollection.Name)]
public sealed class CharacterRegistrationIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;

    public CharacterRegistrationIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private ApplicationUser AddUser(string name = "Test")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = name,
            DiscordUsername = name.ToLower(),
            UserName = name,
            NormalizedUserName = name.ToUpper(),
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        _db.Set<ApplicationUser>().Add(user);
        return user;
    }

    private CharacterRepository MakeCharRepo() => new(_db);
    private PendingRegistrationRepository MakePendingRepo() => new(_db);

    // ── Constraint: lower(handle) unique index ────────────────────────────

    [Fact]
    public async Task DuplicateHandleDifferentCase_ViolatesUniqueIndex()
    {
        var user1 = AddUser("User1");
        var user2 = AddUser("User2");
        await _db.SaveChangesAsync();

        var charRepo = MakeCharRepo();
        var char1 = new Character { Id = Guid.NewGuid(), OwnerUserId = user1.Id, Name = "Alpha", Handle = "G8R", CreatedAt = DateTimeOffset.UtcNow };
        await charRepo.AddAsync(char1, CancellationToken.None);

        var char2 = new Character { Id = Guid.NewGuid(), OwnerUserId = user2.Id, Name = "Beta", Handle = "g8r", CreatedAt = DateTimeOffset.UtcNow };

        var act = () => charRepo.AddAsync(char2, CancellationToken.None);
        await act.Should().ThrowAsync<HandleAlreadyClaimedException>();
    }

    // ── Constraint: one pending per owner ─────────────────────────────────

    [Fact]
    public async Task SecondPendingForSameOwner_OverwritesExisting()
    {
        var user = AddUser();
        await _db.SaveChangesAsync();

        var repo = MakePendingRepo();
        var p1 = PendingCharacterRegistration.Create(user.Id, DateTimeOffset.UtcNow);
        await repo.UpsertAsync(p1, CancellationToken.None);

        var p2 = PendingCharacterRegistration.Create(user.Id, DateTimeOffset.UtcNow);
        await repo.UpsertAsync(p2, CancellationToken.None);

        var stored = await repo.GetByOwnerAsync(user.Id, CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.Token.Should().Be(p2.Token);
    }

    // ── Happy path: add and list ───────────────────────────────────────────

    [Fact]
    public async Task AddAndGetByOwner_ReturnsCharacter()
    {
        var user = AddUser();
        await _db.SaveChangesAsync();

        var charRepo = MakeCharRepo();
        var character = new Character { Id = Guid.NewGuid(), OwnerUserId = user.Id, Name = "TestName", Handle = "testhandle", CreatedAt = DateTimeOffset.UtcNow };
        await charRepo.AddAsync(character, CancellationToken.None);

        var results = await charRepo.GetByOwnerAsync(user.Id, CancellationToken.None);
        results.Should().HaveCount(1);
        results[0].Handle.Should().Be("testhandle");
    }

    // ── FR-007: Multiple characters per owner ─────────────────────────────

    [Fact]
    public async Task OwnerWithTwoCharacters_GetByOwnerReturnsAll()
    {
        var user = AddUser();
        await _db.SaveChangesAsync();

        var charRepo = MakeCharRepo();
        var c1 = new Character { Id = Guid.NewGuid(), OwnerUserId = user.Id, Name = "Alpha", Handle = "alpha", CreatedAt = DateTimeOffset.UtcNow };
        var c2 = new Character { Id = Guid.NewGuid(), OwnerUserId = user.Id, Name = "Beta", Handle = "beta", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1) };
        await charRepo.AddAsync(c1, CancellationToken.None);
        await charRepo.AddAsync(c2, CancellationToken.None);

        var results = await charRepo.GetByOwnerAsync(user.Id, CancellationToken.None);
        results.Should().HaveCount(2);
    }
}
