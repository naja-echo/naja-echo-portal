using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using NajaEcho.Application.Abstractions;

namespace NajaEcho.Api.Authorization;

/// <summary>
/// Keeps the role claims in the auth cookie in step with the database.
/// </summary>
/// <remarks>
/// Roles are snapshotted into the cookie at sign-in. Without this, an admin's role change had no
/// effect until the session expired — up to the 7-day absolute cap. Rather than re-reading roles
/// on every request, this consults a process-local invalidation flag (a dictionary lookup) and
/// only hits the database when a user was actually changed, or when the fallback interval has
/// elapsed so a missed signal self-heals.
///
/// Claims are refreshed in place; the principal is never rejected, so a role change does not
/// sign the user out.
/// </remarks>
public sealed class RoleClaimsRefresher(
    IUserRepository userRepository,
    IUserSessionInvalidator invalidator,
    IClock clock,
    ILogger<RoleClaimsRefresher> logger)
{
    /// <summary>Key under which the last refresh time is stashed in the cookie's properties.</summary>
    public const string RefreshedAtKey = "roles.refreshed_at";

    /// <summary>
    /// Upper bound on staleness when the invalidation signal is missed — for example on another
    /// instance, or after a restart cleared the in-memory flags.
    /// </summary>
    public static readonly TimeSpan FallbackInterval = TimeSpan.FromMinutes(15);

    public async Task RefreshAsync(CookieValidatePrincipalContext ctx)
    {
        var principal = ctx.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId))
        {
            return;
        }

        var now = clock.UtcNow;
        var refreshedAt = ReadRefreshedAt(ctx.Properties);

        // A session issued before this feature shipped has no stamp — refresh it once.
        var due = refreshedAt is null
            || now - refreshedAt.Value >= FallbackInterval
            || invalidator.IsStaleSince(userId, refreshedAt.Value);

        if (!due)
        {
            return;
        }

        var roles = await userRepository.GetRolesAsync(userId, ctx.HttpContext.RequestAborted);

        ctx.ReplacePrincipal(WithRoles(principal, roles));
        ctx.Properties.Items[RefreshedAtKey] = now.ToString("O");
        ctx.ShouldRenew = true;

        logger.LogInformation(
            "Role claims refreshed userId={UserId} roles=[{Roles}]", userId, string.Join(", ", roles));
    }

    /// <summary>Stamps a freshly issued cookie so the first request does not trigger a refresh.</summary>
    public void StampRefreshed(AuthenticationProperties properties) =>
        properties.Items[RefreshedAtKey] = clock.UtcNow.ToString("O");

    private static DateTimeOffset? ReadRefreshedAt(AuthenticationProperties properties) =>
        properties.Items.TryGetValue(RefreshedAtKey, out var raw)
        && DateTimeOffset.TryParse(raw, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Rebuilds the principal with fresh role claims, preserving every non-role claim (notably
    /// the user id and display name set at sign-in).
    /// </summary>
    private static ClaimsPrincipal WithRoles(ClaimsPrincipal principal, IReadOnlyList<string> roles)
    {
        var preserved = principal.Claims.Where(c => c.Type != ClaimTypes.Role);
        var identity = new ClaimsIdentity(
            [.. preserved, .. roles.Select(r => new Claim(ClaimTypes.Role, r))],
            principal.Identity?.AuthenticationType);

        return new ClaimsPrincipal(identity);
    }
}
