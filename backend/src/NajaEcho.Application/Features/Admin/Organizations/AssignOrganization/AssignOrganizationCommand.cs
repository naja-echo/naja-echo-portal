namespace NajaEcho.Application.Features.Admin.Organizations.AssignOrganization;

/// <summary>
/// Sets or clears a member's current organization.
/// </summary>
/// <param name="TargetUserId">The member whose organization is changing.</param>
/// <param name="OrganizationId">The organization to make current, or null to clear it.</param>
/// <param name="CallerId">
/// The acting admin. Carried on the command rather than read from the ambient context because
/// FR-018 requires the log event to identify who made the change, and an audit fact that depends
/// on ambient state is one that goes missing the first time this is called from anywhere else.
/// </param>
public sealed record AssignOrganizationCommand(
    Guid TargetUserId,
    Guid? OrganizationId,
    Guid? CallerId);
