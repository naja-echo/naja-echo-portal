namespace NajaEcho.Application.Features.Admin.Users.AssignRoles;

/// <summary>
/// Identity rejected a role write. Previously these failures were silently discarded and
/// reported to the caller as success.
/// </summary>
public sealed class RoleAssignmentFailedException(Guid userId, string errors)
    : Exception($"Failed to update roles for user '{userId}': {errors}") { }
