using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.Loot;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Loot;
using NajaEcho.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Loot;

public sealed class LootLedgerRepositoryTests : IAsyncLifetime
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

    private ApplicationUser AddUser(string displayName = "Test User")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            DiscordUsername = displayName.ToLower().Replace(" ", "_"),
            UserName = displayName,
            NormalizedUserName = displayName.ToUpper(),
            Email = $"{displayName.Replace(" ", ".")}@test.com",
            NormalizedEmail = $"{displayName.Replace(" ", ".")}@test.com".ToUpper(),
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        _db.Set<ApplicationUser>().Add(user);
        return user;
    }

    private LootLedgerRepository MakeRepo() => new(_db);

    // ── Per-member ledger read (T019) ────────────────────────────────────

    [Fact]
    public async Task GetMemberLedgerAsync_ReturnsEntriesNewestFirst()
    {
        var member = AddUser("Member One");
        var actor = AddUser("Actor User");
        await _db.SaveChangesAsync();

        var older = new LootLedgerEntry { Id = Guid.NewGuid(), MemberId = member.Id, Kind = LootLedgerKind.OrgPoints, Amount = 10, Reason = "First", ActorId = actor.Id, CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) };
        var newer = new LootLedgerEntry { Id = Guid.NewGuid(), MemberId = member.Id, Kind = LootLedgerKind.LootPoints, Amount = 5, Reason = "Second", ActorId = actor.Id, CreatedAt = DateTimeOffset.UtcNow };
        _db.LootLedger.AddRange(older, newer);
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var result = await repo.GetMemberLedgerAsync(member.Id, default);

        result.Should().NotBeNull();
        result!.Entries.Should().HaveCount(2);
        result.Entries[0].Reason.Should().Be("Second");
        result.Entries[1].Reason.Should().Be("First");
    }

    [Fact]
    public async Task GetMemberLedgerAsync_UnknownMember_ReturnsNull()
    {
        var repo = MakeRepo();
        var result = await repo.GetMemberLedgerAsync(Guid.NewGuid(), default);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMemberLedgerAsync_EmptyMember_ReturnsEmptyEntries()
    {
        var member = AddUser("Empty Member");
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var result = await repo.GetMemberLedgerAsync(member.Id, default);

        result.Should().NotBeNull();
        result!.Entries.Should().BeEmpty();
    }

    // ── Distribution (T035) ──────────────────────────────────────────────

    [Fact]
    public async Task GetDistributionAsync_IncludesMembersWithZeroEntries()
    {
        var member1 = AddUser("Rich Member");
        var member2 = AddUser("Zero Member");
        await _db.SaveChangesAsync();

        var entry = new LootLedgerEntry { Id = Guid.NewGuid(), MemberId = member1.Id, Kind = LootLedgerKind.OrgPoints, Amount = 100, Reason = "Points", ActorId = member1.Id, CreatedAt = DateTimeOffset.UtcNow };
        _db.LootLedger.Add(entry);
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var rows = await repo.GetDistributionAsync(default);

        rows.Should().Contain(r => r.MemberId == member2.Id);
        var zeroRow = rows.First(r => r.MemberId == member2.Id);
        zeroRow.OrgPointsTotal.Should().Be(0);
        zeroRow.LootPointsTotal.Should().Be(0);
        zeroRow.ClaimPriority.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public async Task GetDistributionAsync_SortedByClaimPriorityAscending()
    {
        var member1 = AddUser("High Priority Member");
        var member2 = AddUser("Low Priority Member");
        await _db.SaveChangesAsync();

        // member1: org=100, loot=200 → priority=0.5
        // member2: org=100, loot=10 → priority=10
        _db.LootLedger.AddRange(
            new LootLedgerEntry { Id = Guid.NewGuid(), MemberId = member1.Id, Kind = LootLedgerKind.OrgPoints, Amount = 100, Reason = "Org", ActorId = member1.Id, CreatedAt = DateTimeOffset.UtcNow },
            new LootLedgerEntry { Id = Guid.NewGuid(), MemberId = member1.Id, Kind = LootLedgerKind.LootPoints, Amount = 200, Reason = "Loot", ActorId = member1.Id, CreatedAt = DateTimeOffset.UtcNow },
            new LootLedgerEntry { Id = Guid.NewGuid(), MemberId = member2.Id, Kind = LootLedgerKind.OrgPoints, Amount = 100, Reason = "Org", ActorId = member2.Id, CreatedAt = DateTimeOffset.UtcNow },
            new LootLedgerEntry { Id = Guid.NewGuid(), MemberId = member2.Id, Kind = LootLedgerKind.LootPoints, Amount = 10, Reason = "Loot", ActorId = member2.Id, CreatedAt = DateTimeOffset.UtcNow }
        );
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var rows = await repo.GetDistributionAsync(default);

        var list = rows.ToList();
        var idx1 = list.FindIndex(r => r.MemberId == member1.Id);
        var idx2 = list.FindIndex(r => r.MemberId == member2.Id);
        idx1.Should().BeLessThan(idx2);
    }
}
