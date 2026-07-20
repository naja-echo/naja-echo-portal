using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.Organizations;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Organizations;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Organizations;

/// <summary>
/// Covers the membership state transitions in data-model.md: assign, reassign, reactivate, clear.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OrganizationRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;
    private OrganizationRepository _repo = null!;

    private Guid _userId;
    private Guid _orgAId;
    private Guid _orgBId;

    public OrganizationRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
        _repo = new OrganizationRepository(_db);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = "alice",
            DiscordUsername = "alice",
            UserName = "alice",
            NormalizedUserName = "ALICE",
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        _db.Set<ApplicationUser>().Add(user);
        _userId = user.Id;

        var orgA = new Organization { Id = Guid.NewGuid(), Name = "Org A", CreatedAt = DateTimeOffset.UtcNow };
        var orgB = new Organization { Id = Guid.NewGuid(), Name = "Org B", CreatedAt = DateTimeOffset.UtcNow };
        _db.Organizations.AddRange(orgA, orgB);
        _orgAId = orgA.Id;
        _orgBId = orgB.Id;

        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private Task<List<OrganizationMembership>> MembershipsAsync() =>
        _db.OrganizationMemberships.AsNoTracking().Where(m => m.UserId == _userId).ToListAsync();

    // ── Assign ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetCurrent_ForUnassignedUser_CreatesOneCurrentMembership()
    {
        await _repo.SetCurrentAsync(_userId, _orgAId, CancellationToken.None);

        var memberships = await MembershipsAsync();

        memberships.Should().ContainSingle();
        memberships[0].OrganizationId.Should().Be(_orgAId);
        memberships[0].IsCurrent.Should().BeTrue();
    }

    // ── Reassign ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SetCurrent_ReassigningFromAToB_LeavesExactlyOneCurrentMembershipInB()
    {
        await _repo.SetCurrentAsync(_userId, _orgAId, CancellationToken.None);
        await _repo.SetCurrentAsync(_userId, _orgBId, CancellationToken.None);

        var memberships = await MembershipsAsync();

        memberships.Should().HaveCount(2, "the membership in A is retained, not deleted");
        memberships.Should().ContainSingle(m => m.IsCurrent);
        memberships.Single(m => m.IsCurrent).OrganizationId.Should().Be(_orgBId);
        memberships.Single(m => m.OrganizationId == _orgAId).IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task SetCurrent_ToAnOrganizationTheUserAlreadyHeld_ReactivatesRatherThanDuplicates()
    {
        await _repo.SetCurrentAsync(_userId, _orgAId, CancellationToken.None);
        await _repo.SetCurrentAsync(_userId, _orgBId, CancellationToken.None);

        // Back to A: the dormant A row must be reactivated. Inserting a second one would violate
        // ux_organization_memberships_user_org.
        await _repo.SetCurrentAsync(_userId, _orgAId, CancellationToken.None);

        var memberships = await MembershipsAsync();

        memberships.Should().HaveCount(2);
        memberships.Should().ContainSingle(m => m.IsCurrent);
        memberships.Single(m => m.IsCurrent).OrganizationId.Should().Be(_orgAId);
    }

    [Fact]
    public async Task SetCurrent_ToTheOrganizationAlreadyCurrent_IsANoOp()
    {
        await _repo.SetCurrentAsync(_userId, _orgAId, CancellationToken.None);
        var before = await MembershipsAsync();

        await _repo.SetCurrentAsync(_userId, _orgAId, CancellationToken.None);
        var after = await MembershipsAsync();

        after.Should().BeEquivalentTo(before, "assigning the current organization again changes nothing");
    }

    // ── Clear ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetCurrent_ToNull_RetainsTheRowAndLeavesNoCurrentMembership()
    {
        await _repo.SetCurrentAsync(_userId, _orgAId, CancellationToken.None);

        await _repo.SetCurrentAsync(_userId, null, CancellationToken.None);

        var memberships = await MembershipsAsync();

        memberships.Should().ContainSingle("clearing retains membership history rather than deleting it");
        memberships[0].IsCurrent.Should().BeFalse();
        (await _repo.GetCurrentForUserAsync(_userId, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task SetCurrent_ToNull_ForAlreadyUnassignedUser_IsANoOp()
    {
        await _repo.SetCurrentAsync(_userId, null, CancellationToken.None);

        (await MembershipsAsync()).Should().BeEmpty();
    }

    // ── Composition into a larger unit of work ────────────────────────────

    [Fact]
    public async Task SetCurrent_JoinsAnAmbientTransactionRatherThanThrowing()
    {
        // #32–#34 will want to assign a member and stamp their rows atomically. An unconditional
        // BeginTransaction inside the repository makes that impossible.
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var act = async () => await _repo.SetCurrentAsync(_userId, _orgAId, CancellationToken.None);

        await act.Should().NotThrowAsync();

        await transaction.CommitAsync();

        var memberships = await MembershipsAsync();
        memberships.Should().ContainSingle();
        memberships[0].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task SetCurrent_InsideAnAmbientTransaction_IsRolledBackWithIt()
    {
        // The corollary: having joined the caller's transaction, the write must live and die by
        // the caller's decision rather than having quietly committed itself.
        await using (var transaction = await _db.Database.BeginTransactionAsync())
        {
            await _repo.SetCurrentAsync(_userId, _orgAId, CancellationToken.None);
            await transaction.RollbackAsync();
        }

        await using var verify = _fixture.CreateContext();
        var memberships = await verify.OrganizationMemberships.AsNoTracking()
            .Where(m => m.UserId == _userId).ToListAsync();

        memberships.Should().BeEmpty();
    }

    [Fact]
    public async Task SetCurrent_ReturnsThePreviousOrganization()
    {
        (await _repo.SetCurrentAsync(_userId, _orgAId, CancellationToken.None))
            .Should().BeNull("the member belonged to no organization beforehand");

        (await _repo.SetCurrentAsync(_userId, _orgBId, CancellationToken.None))
            .Should().Be(_orgAId);

        (await _repo.SetCurrentAsync(_userId, null, CancellationToken.None))
            .Should().Be(_orgBId, "clearing reports what it cleared, for the FR-018 audit event");
    }

    // ── Reads ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentForUser_ReturnsTheCurrentOrganization()
    {
        await _repo.SetCurrentAsync(_userId, _orgBId, CancellationToken.None);

        var current = await _repo.GetCurrentForUserAsync(_userId, CancellationToken.None);

        current.Should().NotBeNull();
        current!.Id.Should().Be(_orgBId);
        current.Name.Should().Be("Org B");
    }

    [Fact]
    public async Task GetAll_ReturnsEveryOrganizationOrderedByName()
    {
        var organizations = await _repo.GetAllAsync(CancellationToken.None);

        organizations.Select(o => o.Name).Should().ContainInOrder("Org A", "Org B");
    }

    [Fact]
    public async Task Exists_IsTrueForAKnownOrganizationAndFalseOtherwise()
    {
        (await _repo.ExistsAsync(_orgAId, CancellationToken.None)).Should().BeTrue();
        (await _repo.ExistsAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeFalse();
    }
}
