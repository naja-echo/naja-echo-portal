using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Application.Abstractions;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Persistence;

/// <summary>
/// Proves the global query filter restricts organization-scoped entities by default (FR-020),
/// returns empty rather than erroring for an unassigned member (FR-021), grants no role an escape
/// (FR-024), leaves unscoped entities alone (FR-022), and — most importantly — re-evaluates per
/// context rather than baking one organization into EF's cached model.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OrganizationScopeTests : IAsyncLifetime
{
    private static readonly Guid OrgA = Guid.Parse("aaaaaaaa-0000-4000-a000-000000000001");
    private static readonly Guid OrgB = Guid.Parse("bbbbbbbb-0000-4000-a000-000000000002");

    private readonly PostgresFixture _fixture;

    // Built once and shared by every context in this class. This is load-bearing for
    // ModelCache_DoesNotBleedOneOrganizationIntoAnother — see the comment there.
    private DbContextOptions<ScopeTestDbContext> _options = null!;

    public OrganizationScopeTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _options = new DbContextOptionsBuilder<ScopeTestDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        // These tables are created after Respawn snapshotted the schema, so Respawn does not know
        // to reset them. Truncate explicitly instead of relying on ResetAsync.
        await using var db = NewContext(new StubOrganizationContext());
        await db.Database.ExecuteSqlRawAsync(ScopeTestDbContext.CreateTablesSql);
        await db.Database.ExecuteSqlRawAsync(ScopeTestDbContext.TruncateTablesSql);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ScopeTestDbContext NewContext(IOrganizationContext organizationContext) =>
        new(_options, organizationContext);

    private async Task SeedBothOrganizationsAsync()
    {
        // Written through a context with no current organization: the filter applies to reads, so
        // seeding rows for two organizations is only possible from outside either of them.
        await using var db = NewContext(new StubOrganizationContext());
        db.ScopedThings.AddRange(
            new ScopedThing { Id = Guid.NewGuid(), OrganizationId = OrgA, Label = "a-one" },
            new ScopedThing { Id = Guid.NewGuid(), OrganizationId = OrgA, Label = "a-two" },
            new ScopedThing { Id = Guid.NewGuid(), OrganizationId = OrgB, Label = "b-one" });
        await db.SaveChangesAsync();
    }

    // ── FR-020: default-on scoping ────────────────────────────────────────

    [Fact]
    public async Task WithCurrentOrganization_QueryStatingNoConditionReturnsOnlyThatOrganization()
    {
        await SeedBothOrganizationsAsync();

        await using var db = NewContext(new StubOrganizationContext(OrgA));

        // Deliberately no Where(t => t.OrganizationId == ...). That is the whole point: a developer
        // who forgets the condition must get nothing, not another organization's rows.
        var things = await db.ScopedThings.AsNoTracking().ToListAsync();

        things.Should().HaveCount(2);
        things.Select(t => t.Label).Should().BeEquivalentTo(["a-one", "a-two"]);
    }

    // ── FR-021 / SC-007: unassigned members get empty, not an error ───────

    [Fact]
    public async Task WithNoCurrentOrganization_ReturnsEmptyAndDoesNotThrow()
    {
        await SeedBothOrganizationsAsync();

        await using var db = NewContext(new StubOrganizationContext(currentOrganizationId: null));

        var act = async () => await db.ScopedThings.AsNoTracking().ToListAsync();

        var things = await act.Should().NotThrowAsync();
        things.Subject.Should().BeEmpty(
            "organization_id is non-nullable, so comparing it to null matches no row — the filter " +
            "fails closed for an unassigned member rather than erroring");
    }

    // ── FR-024: no role bypasses the boundary ─────────────────────────────

    [Fact]
    public async Task AdminActingInOneOrganization_SeesNoOtherOrganizationsRows()
    {
        await SeedBothOrganizationsAsync();

        // The filter keys on the organization context alone — it has no notion of roles, and there
        // is deliberately no IgnoreQueryFilters escape hatch anywhere in the codebase. An admin is
        // scoped exactly like any other member; administering members is a separate, unscoped path.
        await using var db = NewContext(new StubOrganizationContext(OrgA));

        var things = await db.ScopedThings.AsNoTracking().ToListAsync();

        things.Should().OnlyContain(t => t.OrganizationId == OrgA);
        things.Should().NotContain(t => t.Label == "b-one");
    }

    // ── FR-022 / SC-006: reference data is never scoped ───────────────────

    [Fact]
    public async Task UnscopedEntity_ReturnsEveryRowEvenWithNoCurrentOrganization()
    {
        await using (var seed = NewContext(new StubOrganizationContext()))
        {
            seed.UnscopedThings.AddRange(
                new UnscopedThing { Id = Guid.NewGuid(), Label = "catalog-one" },
                new UnscopedThing { Id = Guid.NewGuid(), Label = "catalog-two" });
            await seed.SaveChangesAsync();
        }

        await using var db = NewContext(new StubOrganizationContext(currentOrganizationId: null));

        var things = await db.UnscopedThings.AsNoTracking().ToListAsync();

        things.Should().HaveCount(2,
            "the filter is opt-in via IOrganizationScoped — ships, items, commodities and the rest " +
            "of the catalog must stay fully visible to a member belonging to no organization");
    }

    // ── SC-005: the assertion this whole feature turns on ─────────────────

    [Fact]
    public async Task ModelCache_DoesNotBleedOneOrganizationIntoAnother()
    {
        await SeedBothOrganizationsAsync();

        // Both contexts are built from the SAME DbContextOptions instance, so they share one
        // internal service provider and therefore one compiled-model cache. That sharing is what
        // gives this test its teeth: if ApplyOrganizationFilters captured the organization value at
        // model-build time, the first context's organization would be baked into the cached model
        // and the second context would read A's rows while believing it is B.
        //
        // Constructing separate options per organization would give each its own model cache and
        // hide exactly that bug — the test would pass while proving nothing.
        await using var asA = NewContext(new StubOrganizationContext(OrgA));
        var fromA = await asA.ScopedThings.AsNoTracking().Select(t => t.Label).ToListAsync();

        await using var asB = NewContext(new StubOrganizationContext(OrgB));
        var fromB = await asB.ScopedThings.AsNoTracking().Select(t => t.Label).ToListAsync();

        fromA.Should().BeEquivalentTo(["a-one", "a-two"]);
        fromB.Should().BeEquivalentTo(["b-one"],
            "the filter must re-evaluate per context; a cached model that froze organization A " +
            "would serve A's rows here and silently defeat the entire epic");
    }

    [Fact]
    public async Task ChangingTheOrganizationOnOneContext_ChangesWhatSubsequentQueriesSee()
    {
        await SeedBothOrganizationsAsync();

        // The same guarantee from the other direction: one context, one stub, value mutated between
        // two queries. EF must parameterize the organization per query rather than per model.
        var stub = new StubOrganizationContext(OrgA);
        await using var db = NewContext(stub);

        var first = await db.ScopedThings.AsNoTracking().Select(t => t.Label).ToListAsync();

        stub.CurrentOrganizationId = OrgB;
        var second = await db.ScopedThings.AsNoTracking().Select(t => t.Label).ToListAsync();

        first.Should().BeEquivalentTo(["a-one", "a-two"]);
        second.Should().BeEquivalentTo(["b-one"]);
    }
}
