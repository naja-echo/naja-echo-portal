namespace NajaEcho.Api.Authorization;

/// <summary>
/// Claim types carrying organization membership in the auth cookie.
/// </summary>
/// <remarks>
/// The current organization travels as a session claim rather than being looked up per request.
/// It is written at sign-in and kept fresh by <see cref="RoleClaimsRefresher"/>, which already
/// solves exactly this problem for roles — reusing it avoids inventing a second invalidation path
/// and keeps the "no sign-out required" guarantee (spec FR-014) on one mechanism.
/// </remarks>
public static class OrganizationClaims
{
    /// <summary>The member's current organization id, absent when they hold no current membership.</summary>
    public const string OrganizationId = "najaecho:organization_id";
}
