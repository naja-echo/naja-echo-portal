using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.Organizations;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Organizations;

/// <summary>
/// Covers the seed and backfill the <c>AddOrganizations</c> migration performs (FR-007, FR-008,
/// FR-009, SC-001).
/// </summary>
/// <remarks>
/// That the migration itself applies cleanly is already proven by <see cref="PostgresFixture"/>,
/// which runs the real migration chain against the container before any test executes — the
/// <c>organizations</c> table these tests query would not exist otherwise.
///
/// What that setup cannot prove is the data outcome, because Respawn truncates row data between
/// tests: by the time a test runs, the seeded organization and every backfilled membership are
/// gone, and asserting "every user has a membership" against zero users passes vacuously. So each
/// test re-executes <see cref="OrganizationSeedSql"/> — the exact statements the migration runs —
/// against a known set of users.
/// </remarks>
[Collection(PostgresCollection.Name)]
public sealed class MigrationBackfillTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;

    public MigrationBackfillTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private ApplicationUser AddUser(string name)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = name,
            DiscordUsername = name.ToLowerInvariant(),
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        _db.Set<ApplicationUser>().Add(user);
        return user;
    }

    private Task SeedAsync() => _db.Database.ExecuteSqlRawAsync(OrganizationSeedSql.SeedDefaultOrganization);

    private Task BackfillAsync() => _db.Database.ExecuteSqlRawAsync(OrganizationSeedSql.BackfillMemberships);

    // ── FR-007: the default organization ──────────────────────────────────

    [Fact]
    public async Task Seed_CreatesExactlyOneOrganizationNamedNajaEcho()
    {
        await SeedAsync();

        var organizations = await _db.Organizations.AsNoTracking().ToListAsync();

        organizations.Should().ContainSingle();
        organizations[0].Id.Should().Be(DefaultOrganization.Id,
            "the migration's literal and DefaultOrganization.Id are a matched pair — a mismatch " +
            "creates a second organization on every deploy while the constant points at nothing");
        organizations[0].Name.Should().Be(DefaultOrganization.Name);
    }

    // ── FR-008 / SC-001: every existing member is backfilled ──────────────

    [Fact]
    public async Task Backfill_GivesEveryExistingUserOneCurrentMembershipInTheDefaultOrganization()
    {
        var users = new[] { AddUser("alice"), AddUser("bob"), AddUser("charlie") };
        await _db.SaveChangesAsync();

        await SeedAsync();
        await BackfillAsync();

        var memberships = await _db.OrganizationMemberships.AsNoTracking().ToListAsync();

        memberships.Should().HaveCount(users.Length);
        memberships.Should().OnlyContain(m => m.OrganizationId == DefaultOrganization.Id);
        memberships.Should().OnlyContain(m => m.IsCurrent);
        memberships.Select(m => m.UserId).Should().BeEquivalentTo(users.Select(u => u.Id));
    }

    [Fact]
    public async Task Backfill_WithNoUsers_StillLeavesTheDefaultOrganization()
    {
        await SeedAsync();
        await BackfillAsync();

        (await _db.Organizations.AsNoTracking().CountAsync()).Should().Be(1);
        (await _db.OrganizationMemberships.AsNoTracking().CountAsync()).Should().Be(0);
    }

    // ── FR-009: safe to apply more than once ──────────────────────────────

    [Fact]
    public async Task SeedAndBackfill_AppliedTwice_ChangeNothingTheSecondTime()
    {
        AddUser("alice");
        AddUser("bob");
        await _db.SaveChangesAsync();

        await SeedAsync();
        await BackfillAsync();

        var organizationsAfterFirst = await _db.Organizations.AsNoTracking().ToListAsync();
        var membershipsAfterFirst = await _db.OrganizationMemberships.AsNoTracking()
            .OrderBy(m => m.UserId).ToListAsync();

        await SeedAsync();
        await BackfillAsync();

        var organizationsAfterSecond = await _db.Organizations.AsNoTracking().ToListAsync();
        var membershipsAfterSecond = await _db.OrganizationMemberships.AsNoTracking()
            .OrderBy(m => m.UserId).ToListAsync();

        organizationsAfterSecond.Should().BeEquivalentTo(organizationsAfterFirst);
        membershipsAfterSecond.Should().BeEquivalentTo(membershipsAfterFirst,
            "re-running must not create duplicates, and must alter no membership id or currency");
    }

    [Fact]
    public async Task Backfill_DoesNotResurrectAMembershipAnAdminCleared()
    {
        var user = AddUser("alice");
        await _db.SaveChangesAsync();

        await SeedAsync();
        await BackfillAsync();

        // An admin subsequently clears the member's organization: the row is retained, not deleted.
        var membership = await _db.OrganizationMemberships.SingleAsync(m => m.UserId == user.Id);
        membership.IsCurrent = false;
        await _db.SaveChangesAsync();

        await BackfillAsync();

        var after = await _db.OrganizationMemberships.AsNoTracking()
            .Where(m => m.UserId == user.Id).ToListAsync();

        after.Should().ContainSingle();
        after[0].IsCurrent.Should().BeFalse(
            "a re-run must not silently undo an administrative decision to unassign a member");
    }
}
