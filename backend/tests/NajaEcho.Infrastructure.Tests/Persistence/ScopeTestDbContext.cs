using Microsoft.EntityFrameworkCore;
using NajaEcho.Application.Abstractions;
using NajaEcho.Domain.Organizations;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Infrastructure.Tests.Persistence;

/// <summary>An organization-scoped entity that exists only for these tests.</summary>
/// <remarks>
/// No production entity implements <see cref="IOrganizationScoped"/> in this feature — #32/#33/#34
/// opt theirs in later. The enforcement mechanism still has to be proven working now (spec FR-023),
/// so it is proven against this stand-in rather than left dormant until something real depends on it.
/// </remarks>
public sealed class ScopedThing : IOrganizationScoped
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>A deliberately unscoped entity, standing in for reference/catalog data (FR-022).</summary>
public sealed class UnscopedThing
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// A minimal context that applies the same <c>ApplyOrganizationFilters</c> extension the production
/// context does, over entities that exist only in tests.
/// </summary>
/// <remarks>
/// Its tables are created and dropped with explicit SQL rather than <c>EnsureCreated</c>: the shared
/// container already has a migrated database, and <c>EnsureCreated</c> is a no-op against a database
/// that exists — it would silently create nothing and every test would fail on a missing table.
/// </remarks>
public sealed class ScopeTestDbContext(
    DbContextOptions<ScopeTestDbContext> options,
    IOrganizationContext organizationContext) : DbContext(options)
{
    public Guid? CurrentOrganizationId => organizationContext.CurrentOrganizationId;

    public DbSet<ScopedThing> ScopedThings => Set<ScopedThing>();
    public DbSet<UnscopedThing> UnscopedThings => Set<UnscopedThing>();

    public const string CreateTablesSql = """
        CREATE TABLE IF NOT EXISTS scope_test_scoped_things (
            id uuid PRIMARY KEY,
            organization_id uuid NOT NULL,
            label text NOT NULL
        );
        CREATE TABLE IF NOT EXISTS scope_test_unscoped_things (
            id uuid PRIMARY KEY,
            label text NOT NULL
        );
        """;

    public const string TruncateTablesSql =
        "TRUNCATE scope_test_scoped_things, scope_test_unscoped_things;";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<ScopedThing>(b =>
        {
            b.ToTable("scope_test_scoped_things");
            b.HasKey(t => t.Id);
            b.Property(t => t.Id).HasColumnName("id");
            b.Property(t => t.OrganizationId).HasColumnName("organization_id");
            b.Property(t => t.Label).HasColumnName("label");
        });

        modelBuilder.Entity<UnscopedThing>(b =>
        {
            b.ToTable("scope_test_unscoped_things");
            b.HasKey(t => t.Id);
            b.Property(t => t.Id).HasColumnName("id");
            b.Property(t => t.Label).HasColumnName("label");
        });

        // The single line under test. Identical to the call in AppDbContext.
        //
        // Verified to have teeth: replacing this with a captured local
        // (`var captured = CurrentOrganizationId; ... (() => captured)`) makes
        // ModelCache_DoesNotBleedOneOrganizationIntoAnother fail, along with two others.
        modelBuilder.ApplyOrganizationFilters(() => CurrentOrganizationId);
    }
}
