namespace NajaEcho.Domain.Organizations;

/// <summary>
/// Marks an entity as belonging to an organization. Every entity carrying this interface is
/// automatically restricted to the acting member's current organization by a global query filter.
/// </summary>
/// <remarks>
/// No entity implements this today. It is the contract that features #32 (warehouse inventory),
/// #33 (hangar and fleet), and #34 (loot ledger and economy) opt into — each adds the interface
/// and an <c>organization_id</c> column, and inherits the filter without hand-writing any wiring.
///
/// The guarantee is real but partial: the filter covers LINQ queries rooted on an entity type. It
/// does NOT cover <c>Database.SqlQuery</c> calls, which project to non-entity records. Any raw SQL
/// against a scoped entity must carry an explicit organization predicate and a test proving it
/// cannot return another organization's rows (spec FR-020a).
///
/// References only <see cref="System.Guid"/>, so Domain acquires no framework dependency.
/// </remarks>
public interface IOrganizationScoped
{
    Guid OrganizationId { get; set; }
}
