using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Admin.Users.AddCharacterForUser;
using NajaEcho.Application.Features.Admin.Users.AssignRoles;
using NajaEcho.Application.Features.Admin.Users.GetUsers;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Infrastructure.Identity;

public sealed class UserRepository(AppDbContext db, UserManager<ApplicationUser> userManager) : IUserRepository
{
    public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id == userId, ct);

    public async Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct)
    {
        var users = await db.Users
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);
        return users.Select(u => (u.Id, u.DisplayName)).ToList();
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct)
    {
        var rows = await db.Database.SqlQuery<UserRoleCharacterRow>($"""
            SELECT
              u.id               AS user_id,
              u.display_name     AS auth_name,
              r.name             AS role_name,
              c.id               AS character_id,
              c.name             AS character_name,
              c.handle           AS character_handle
            FROM "AspNetUsers" u
            LEFT JOIN "AspNetUserRoles" ur ON ur.user_id = u.id
            LEFT JOIN "AspNetRoles"     r  ON r.id       = ur.role_id
            LEFT JOIN characters        c  ON c.owner_user_id = u.id
            ORDER BY u.display_name, r.name, c.name
            """).ToListAsync(ct);

        return rows
            .GroupBy(r => (r.UserId, r.AuthName))
            .Select(g => new AdminUserDto(
                g.Key.UserId,
                g.Key.AuthName,
                g.Where(r => r.RoleName is not null)
                  .Select(r => r.RoleName!)
                  .Distinct()
                  .ToList(),
                g.Where(r => r.CharacterId is not null)
                  .Select(r => new AdminUserCharacterDto(r.CharacterId!.Value, r.CharacterName!, r.CharacterHandle!))
                  .DistinctBy(c => c.Id)
                  .ToList()))
            .ToList();
    }

    public async Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            throw new UserNotFoundException(userId);
        }

        var current = await userManager.GetRolesAsync(user);
        var removal = await userManager.RemoveFromRolesAsync(user, current);
        if (!removal.Succeeded)
        {
            throw new RoleAssignmentFailedException(userId, Describe(removal));
        }

        if (roles.Count > 0)
        {
            var addition = await userManager.AddToRolesAsync(user, roles);
            if (!addition.Succeeded)
            {
                throw new RoleAssignmentFailedException(userId, Describe(addition));
            }
        }
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return [];
        }

        return (IReadOnlyList<string>)await userManager.GetRolesAsync(user);
    }

    private static string Describe(IdentityResult result) =>
        string.Join(", ", result.Errors.Select(e => e.Description));

    private sealed record UserRoleCharacterRow(
        Guid UserId,
        string AuthName,
        string? RoleName,
        Guid? CharacterId,
        string? CharacterName,
        string? CharacterHandle);
}
