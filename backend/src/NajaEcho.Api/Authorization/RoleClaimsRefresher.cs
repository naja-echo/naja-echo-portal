using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using NajaEcho.Application.Abstractions;

namespace NajaEcho.Api.Authorization;

/// <summary>
/// Keeps the role and organization claims in the auth cookie in step with the database.
/// </summary>
/// <remarks>
/// Roles and the current organization are snapshotted into the cookie at sign-in. Without this, an
/// admin's change had no effect until the session expired — up to the 7-day absolute cap. Rather
/// than re-reading them on every request, this consults a process-local invalidation flag (a
/// dictionary lookup) and only hits the database when a user was actually changed, or when the
/// fallback interval has elapsed so a missed signal self-heals.
///
/// Claims are refreshed in place; the principal is never rejected, so neither a role change nor an
/// organization change signs the user out (spec FR-014).
///
/// The class name predates organizations. It covers both facts now — both are authorization state
/// snapshotted at sign-in, invalidated by the same signal, and refreshed in the same pass, so
/// splitting them would mean two database round-trips and two staleness stamps for one event.
/// </remarks>
public sealed class RoleClaimsRefresher(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
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

        var ct = ctx.HttpContext.RequestAborted;
        var roles = await userRepository.GetRolesAsync(userId, ct);
        var organization = await organizationRepository.GetCurrentForUserAsync(userId, ct);

        ctx.ReplacePrincipal(WithRolesAndOrganization(principal, roles, organization?.Id));
        ctx.Properties.Items[RefreshedAtKey] = now.ToString("O");
        ctx.ShouldRenew = true;

        logger.LogInformation(
            "Role claims refreshed userId={UserId} roles=[{Roles}] organizationId={OrganizationId}",
            userId, string.Join(", ", roles), organization?.Id);
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
    /// Rebuilds the principal with fresh role and organization claims, preserving every other claim
    /// (notably the user id and display name set at sign-in).
    /// </summary>
    /// <remarks>
    /// Both claim types are dropped and rebuilt rather than merged. A member who has just been
    /// unassigned has no organization at all, so the refreshed principal must carry no organization
    /// claim — merging would leave the stale one in place and keep them acting in an organization
    /// they no longer belong to, which is precisely the bug this whole mechanism exists to prevent.
    /// </remarks>
    private static ClaimsPrincipal WithRolesAndOrganization(
        ClaimsPrincipal principal,
        IReadOnlyList<string> roles,
        Guid? organizationId)
    {
        var preserved = principal.Claims.Where(c =>
            c.Type != ClaimTypes.Role && c.Type != OrganizationClaims.OrganizationId);

        var organizationClaims = organizationId is { } id
            ? new[] { new Claim(OrganizationClaims.OrganizationId, id.ToString()) }
            : [];

        var identity = new ClaimsIdentity(
            [.. preserved, .. roles.Select(r => new Claim(ClaimTypes.Role, r)), .. organizationClaims],
            principal.Identity?.AuthenticationType);

        return new ClaimsPrincipal(identity);
    }
}
