using Microsoft.AspNetCore.Authorization;
using NajaEcho.Domain.Users;

namespace NajaEcho.Api.Authorization;

/// <summary>
/// Authorization policies, one per role in <see cref="Roles.All"/>.
/// </summary>
/// <remarks>
/// Policy names deliberately collide with role names: the policy for the Quartermaster role is
/// itself named "Quartermaster". So <c>RequireAuthorization(AuthorizationPolicies.Quartermaster)</c>
/// resolves to the <em>policy</em> — which requires Quartermaster <em>or</em> Admin — not to a bare
/// role check. The constants below exist so call sites read as policy references rather than
/// role literals.
/// </remarks>
public static class AuthorizationPolicies
{
    public const string Admin = Roles.Admin;
    public const string Quartermaster = Roles.Quartermaster;
    public const string CrewResourceOfficer = Roles.CrewResourceOfficer;

    public static AuthorizationOptions AddPolicies(this AuthorizationOptions options)
    {
        // Admin is an implicit superset of every other role.
        options.AddPolicy(Roles.Admin, policy => policy.RequireRole(Roles.Admin));

        foreach (var role in Roles.All.Where(r => !string.Equals(r, Roles.Admin, StringComparison.Ordinal)))
        {
            options.AddPolicy(role, policy => policy.RequireRole(role, Roles.Admin));
        }

        return options;
    }
}
