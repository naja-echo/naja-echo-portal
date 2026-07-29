using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Domain.Organizations;

namespace NajaEcho.Infrastructure.Persistence;

/// <summary>
/// Applies the organization boundary to every entity marked <see cref="IOrganizationScoped"/>.
/// </summary>
public static class OrganizationScopeExtensions
{
    /// <summary>
    /// Adds a global query filter restricting every <see cref="IOrganizationScoped"/> entity to the
    /// organization the current request is acting in.
    /// </summary>
    /// <param name="modelBuilder">The model being built.</param>
    /// <param name="currentOrganizationAccessor">
    /// Reads the current organization. <b>Must</b> be a member access on the DbContext instance —
    /// pass <c>() =&gt; CurrentOrganizationId</c> from inside <c>OnModelCreating</c>. See remarks.
    /// </param>
    /// <remarks>
    /// <para>
    /// Applied by convention rather than per entity: #32/#33/#34 opt an entity in by implementing
    /// the interface and adding a column, and inherit the filter without hand-writing wiring that
    /// each of them could forget.
    /// </para>
    /// <para>
    /// <b>The accessor must read a DbContext instance member, never a captured local.</b> EF Core
    /// parameterizes context-member access inside a query filter, so the compiled model stays
    /// cacheable while the value varies per request. Passing a local — <c>var id = ...;
    /// () =&gt; id</c> — captures whichever organization happened to be current when the model was
    /// first built and bakes it into EF's cached model, which every later context then reuses. The
    /// result is one tenant's data served to all of them, with every single-organization test still
    /// passing. <c>OrganizationScopeTests.ModelCache_DoesNotBleedOneOrganizationIntoAnother</c>
    /// exists to fail if that regression is ever introduced.
    /// </para>
    /// <para>
    /// <b>This covers LINQ only.</b> Query filters apply to queries rooted on an entity type.
    /// <c>Database.SqlQuery&lt;T&gt;</c> projects to non-entity records and is not filtered — any
    /// raw SQL against a scoped entity needs an explicit organization predicate and a test proving
    /// it cannot cross the boundary (spec FR-020a).
    /// </para>
    /// <para>
    /// A null current organization matches nothing, because <c>organization_id</c> is non-nullable.
    /// Unassigned members and unauthenticated requests therefore get an empty result rather than an
    /// error (FR-021) — the filter fails closed by construction, not by a special case.
    /// </para>
    /// </remarks>
    public static void ApplyOrganizationFilters(
        this ModelBuilder modelBuilder,
        Expression<Func<Guid?>> currentOrganizationAccessor)
    {
        var scopedTypes = modelBuilder.Model.GetEntityTypes()
            // BaseType: a TPH hierarchy is filtered once, at its root.
            // IsOwned: owned types also have a null BaseType, but calling modelBuilder.Entity() on
            // one re-declares it as an independent entity — changing its table mapping or failing
            // model building outright. An owned type is filtered through the entity that owns it.
            .Where(e => e.BaseType is null
                        && !e.IsOwned()
                        && e.ClrType.IsAssignableTo(typeof(IOrganizationScoped)))
            .Select(e => e.ClrType);

        foreach (var clrType in scopedTypes)
        {
            var entity = Expression.Parameter(clrType, "e");

            // e.OrganizationId == currentOrganization
            // The left side is Guid and the right Guid?, so lift the left to match.
            var organizationId = Expression.Property(entity, nameof(IOrganizationScoped.OrganizationId));
            var comparison = Expression.Equal(
                Expression.Convert(organizationId, typeof(Guid?)),
                currentOrganizationAccessor.Body);

            modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(comparison, entity));
        }
    }
}
