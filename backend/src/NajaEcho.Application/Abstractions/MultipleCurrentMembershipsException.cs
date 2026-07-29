namespace NajaEcho.Application.Abstractions;

/// <summary>
/// A member holds more than one current organization membership, which
/// <c>ux_organization_memberships_user_current</c> is supposed to make impossible (spec FR-004).
/// </summary>
/// <remarks>
/// Reaching this means the partial unique index is not doing its job — dropped during maintenance,
/// or never created because the database was provisioned outside the migration chain. It is not a
/// condition application code can recover from, so it carries the diagnosis rather than a retry:
/// which member, and which organizations they are simultaneously current in.
/// </remarks>
public sealed class MultipleCurrentMembershipsException(Guid userId, IReadOnlyList<Guid> organizationIds)
    : Exception(
        $"Member '{userId}' holds {organizationIds.Count} current memberships " +
        $"({string.Join(", ", organizationIds)}). At most one is permitted — verify that the " +
        "ux_organization_memberships_user_current index exists on organization_memberships.")
{
    public Guid UserId { get; } = userId;

    public IReadOnlyList<Guid> OrganizationIds { get; } = organizationIds;
}
