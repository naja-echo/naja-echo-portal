using NajaEcho.Domain.Organizations;

namespace NajaEcho.Application.Abstractions;

public interface IOrganizationRepository
{
    /// <summary>Every organization a member may be assigned to, ordered by name.</summary>
    Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken ct);

    Task<bool> ExistsAsync(Guid organizationId, CancellationToken ct);

    /// <summary>The member's current organization, or null when they hold no current membership.</summary>
    Task<Organization?> GetCurrentForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Makes <paramref name="organizationId"/> the member's current organization, or clears it
    /// when null. Returns the organization that was current beforehand, or null if none was.
    /// </summary>
    /// <remarks>
    /// Must leave the member holding at most one current membership, with no window in which two
    /// are current (spec FR-004). Memberships are retained rather than deleted when cleared or
    /// superseded — a member's history is what makes multi-membership a later UI change rather
    /// than a later migration.
    ///
    /// The previous organization is returned rather than left for the caller to read separately:
    /// FR-018 requires it for the audit event, and a caller that queries for it before calling
    /// this method opens a window in which a concurrent assignment makes the logged "previous"
    /// value differ from the one actually replaced.
    /// </remarks>
    Task<Guid?> SetCurrentAsync(Guid userId, Guid? organizationId, CancellationToken ct);
}
