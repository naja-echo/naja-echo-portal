using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.Organizations;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Organizations;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Organizations;

/// <summary>
/// The at-most-one-current-membership invariant (FR-004, SC-008), enforced by the partial unique
/// index <c>ux_organization_memberships_user_current</c> rather than by application checks.
/// </summary>
/// <remarks>
/// The distinction matters: an application-level "check then write" cannot hold under two admins
/// acting at once, because the check and the write are separate operations. A database constraint
/// is the only thing that makes two current memberships genuinely unrepresentable — so these tests
/// go at the constraint directly, not through the repository's happy path.
/// </remarks>
[Collection(PostgresCollection.Name)]
public sealed class MembershipInvariantTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;

    private Guid _userId;
    private Guid _orgAId;
    private Guid _orgBId;

    public MembershipInvariantTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();

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

    private OrganizationMembership Membership(Guid organizationId, bool isCurrent) => new()
    {
        Id = Guid.NewGuid(),
        UserId = _userId,
        OrganizationId = organizationId,
        IsCurrent = isCurrent,
        JoinedAt = DateTimeOffset.UtcNow,
    };

    // ── FR-004: two current memberships are unrepresentable ───────────────

    [Fact]
    public async Task InsertingASecondCurrentMembership_IsRejectedByThePartialUniqueIndex()
    {
        _db.OrganizationMemberships.Add(Membership(_orgAId, isCurrent: true));
        await _db.SaveChangesAsync();

        _db.OrganizationMemberships.Add(Membership(_orgBId, isCurrent: true));

        var act = async () => await _db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("ux_organization_memberships_user_current");
    }

    [Fact]
    public async Task ASecondNonCurrentMembership_IsAllowed()
    {
        _db.OrganizationMemberships.Add(Membership(_orgAId, isCurrent: true));
        _db.OrganizationMemberships.Add(Membership(_orgBId, isCurrent: false));

        var act = async () => await _db.SaveChangesAsync();

        await act.Should().NotThrowAsync(
            "the index is filtered on is_current — dormant memberships are the history the join " +
            "table exists to keep");
    }

    [Fact]
    public async Task TwoMembershipsInTheSameOrganization_AreRejectedByTheUserOrgIndex()
    {
        _db.OrganizationMemberships.Add(Membership(_orgAId, isCurrent: false));
        await _db.SaveChangesAsync();

        _db.OrganizationMemberships.Add(Membership(_orgAId, isCurrent: false));

        var act = async () => await _db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("ux_organization_memberships_user_org");
    }

    // ── SC-008: the invariant holds under concurrent admins ───────────────

    [Fact]
    public async Task TwoOverlappingCurrentWrites_CannotBothCommit()
    {
        // Two admins assigning the same member at the same moment, on separate connections with
        // genuinely overlapping transactions — the second writes while the first is still open.
        //
        // This goes at the index rather than through OrganizationRepository on purpose. The
        // repository manages its own transaction, so driving it from an outer one only proves EF
        // rejects nested transactions. The invariant under test belongs to the database, and this
        // is the interleaving that would break it if the guarantee lived in application code.
        await using var dbOne = _fixture.CreateContext();
        await using var dbTwo = _fixture.CreateContext();

        await using var txOne = await dbOne.Database.BeginTransactionAsync();
        dbOne.OrganizationMemberships.Add(Membership(_orgAId, isCurrent: true));
        await dbOne.SaveChangesAsync();

        await using var txTwo = await dbTwo.Database.BeginTransactionAsync();
        dbTwo.OrganizationMemberships.Add(Membership(_orgBId, isCurrent: true));

        // Blocks on the uncommitted row rather than failing immediately: Postgres cannot know
        // whether the first writer will commit until it does.
        var second = dbTwo.SaveChangesAsync();

        await txOne.CommitAsync();

        var act = async () => await second;

        (await act.Should().ThrowAsync<DbUpdateException>(
            "once the first assignment commits, the second must lose to the unique index rather " +
            "than leaving the member holding two current memberships"))
            .Which.InnerException!.Message.Should().Contain("ux_organization_memberships_user_current");

        await using var verify = _fixture.CreateContext();
        var currentCount = await verify.OrganizationMemberships.AsNoTracking()
            .CountAsync(m => m.UserId == _userId && m.IsCurrent);

        currentCount.Should().Be(1);
    }

    [Fact]
    public async Task SequentialReassignmentsThroughTheRepository_NeverLeaveTwoCurrent()
    {
        // The repository's own clear-before-set ordering, exercised across a run of reassignments.
        // Each call must land the member in exactly one organization.
        await using var db = _fixture.CreateContext();
        var repo = new OrganizationRepository(db);

        foreach (var target in new[] { _orgAId, _orgBId, _orgAId, _orgBId })
        {
            await repo.SetCurrentAsync(_userId, target, CancellationToken.None);

            var current = await db.OrganizationMemberships.AsNoTracking()
                .Where(m => m.UserId == _userId && m.IsCurrent).ToListAsync();

            current.Should().ContainSingle();
            current[0].OrganizationId.Should().Be(target);
        }
    }
}
