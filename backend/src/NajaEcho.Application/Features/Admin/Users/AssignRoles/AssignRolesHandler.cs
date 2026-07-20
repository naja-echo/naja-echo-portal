using Microsoft.Extensions.Logging;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Admin.Users.AddCharacterForUser;
using NajaEcho.Domain.Users;

namespace NajaEcho.Application.Features.Admin.Users.AssignRoles;

public sealed class AssignRolesHandler(
    IUserRepository userRepository,
    IUserSessionInvalidator sessionInvalidator,
    ILogger<AssignRolesHandler> logger)
{
    private static readonly IReadOnlySet<string> ValidRoles =
        new HashSet<string>(Roles.All, StringComparer.Ordinal);

    public async Task HandleAsync(AssignRolesCommand command, CancellationToken ct)
    {
        foreach (var role in command.Roles)
        {
            if (!ValidRoles.Contains(role))
            {
                throw new InvalidRoleException(role);
            }
        }

        var userExists = await userRepository.ExistsAsync(command.TargetUserId, ct);
        if (!userExists)
        {
            throw new UserNotFoundException(command.TargetUserId);
        }

        await userRepository.SetRolesAsync(command.TargetUserId, command.Roles, ct);

        // The target's live session holds role claims from sign-in; flag it to refresh.
        sessionInvalidator.Invalidate(command.TargetUserId);

        logger.LogInformation(
            "AssignRoles targetUserId={TargetUserId} roles=[{Roles}] outcome=success",
            command.TargetUserId, string.Join(", ", command.Roles));
    }
}
