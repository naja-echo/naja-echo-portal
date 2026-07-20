namespace NajaEcho.Domain.Organizations;

/// <summary>
/// The link between a member and an organization, and the sole source of truth for which
/// organization a member acts in.
/// </summary>
/// <remarks>
/// Modeled as a standalone relationship rather than a column on the user so that holding more
/// than one membership later is a presentation change rather than a data migration.
///
/// <see cref="IsCurrent"/> lives here rather than on the user precisely so there is only one
/// place the answer can live. At most one of a member's memberships is current at a time — an
/// invariant enforced by a partial unique index in the database, not by application checks, so
/// that it holds under concurrent writes. A member may have none current, which is a valid state.
/// </remarks>
public sealed class OrganizationMembership
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    public bool IsCurrent { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}
