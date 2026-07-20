using NajaEcho.Application.Features.Admin.Users.GetUsers;

namespace NajaEcho.Application.Abstractions;

public interface IUserRepository
{
    Task<bool> ExistsAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct);
    Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct);

    /// <summary>Current role names for the user, or an empty list if the user no longer exists.</summary>
    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct);
}
