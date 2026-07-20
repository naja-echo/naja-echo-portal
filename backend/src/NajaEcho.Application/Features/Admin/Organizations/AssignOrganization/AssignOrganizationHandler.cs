using Microsoft.Extensions.Logging;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Admin.Users.AddCharacterForUser;

namespace NajaEcho.Application.Features.Admin.Organizations.AssignOrganization;

/// <summary>
/// Sets or clears a member's current organization, and makes the change visible to them without
/// requiring a sign-out.
/// </summary>
public sealed class AssignOrganizationHandler(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IUserSessionInvalidator sessionInvalidator,
    ILogger<AssignOrganizationHandler> logger)
{
    public async Task HandleAsync(AssignOrganizationCommand command, CancellationToken ct)
    {
        if (!await userRepository.ExistsAsync(command.TargetUserId, ct))
        {
            throw new UserNotFoundException(command.TargetUserId);
        }

        if (command.OrganizationId is { } organizationId
            && !await organizationRepository.ExistsAsync(organizationId, ct))
        {
            throw new OrganizationNotFoundException(organizationId);
        }

        // The write reports what it replaced. FR-018 needs the previous organization for the audit
        // event, and reading it separately beforehand would leave a window in which a concurrent
        // assignment makes the logged "previous" value differ from the one actually superseded.
        var previousOrganizationId = await organizationRepository.SetCurrentAsync(
            command.TargetUserId, command.OrganizationId, ct);

        // The target's live session carries their organization as a claim from sign-in; flag it so
        // the next request picks up the change rather than waiting for the session to expire (FR-014).
        sessionInvalidator.Invalidate(command.TargetUserId);

        // FR-018: who changed whom, from where to where, and when — the timestamp comes from the
        // log pipeline. FR-019: no tokens, cookies, or authorization headers, only identifiers.
        logger.LogInformation(
            "AssignOrganization caller={CallerId} targetUserId={TargetUserId} previousOrganizationId={PreviousOrganizationId} newOrganizationId={NewOrganizationId} outcome=success",
            command.CallerId,
            command.TargetUserId,
            previousOrganizationId,
            command.OrganizationId);
    }
}
