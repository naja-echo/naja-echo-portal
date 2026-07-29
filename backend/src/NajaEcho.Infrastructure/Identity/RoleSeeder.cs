using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NajaEcho.Domain.Users;

namespace NajaEcho.Infrastructure.Identity;

public sealed class RoleSeeder(RoleManager<IdentityRole<Guid>> roleManager, ILogger<RoleSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var role in Roles.All)
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            if (result.Succeeded)
            {
                logger.LogInformation("{Role} role seeded", role);
            }
            else
                logger.LogError("Failed to seed {Role} role: {Errors}", role,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
