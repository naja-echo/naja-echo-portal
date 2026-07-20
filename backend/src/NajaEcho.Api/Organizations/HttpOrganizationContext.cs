using NajaEcho.Api.Authorization;
using NajaEcho.Application.Abstractions;

namespace NajaEcho.Api.Organizations;

/// <summary>
/// Resolves the acting member's organization from the session claim written at sign-in, falling
/// back to an explicitly declared <see cref="OrganizationScope"/> for work with no request.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately fails closed <i>within</i> a request. An absent claim (unauthenticated, or a member
/// with no current membership) and an unparseable one both yield null, which the query filter
/// treats as "match nothing" — never as "match everything". Throwing there would turn spec FR-021's
/// empty result into an error; returning a fallback organization would leak another tenant's rows.
/// </para>
/// <para>
/// Outside a request it fails <i>loudly</i> instead. Silently resolving to null would make every
/// organization-scoped read in a background job return zero rows while looking like success, which
/// is far harder to notice than an exception. Non-request code must declare its intent with
/// <see cref="OrganizationScope.Begin"/> or <see cref="OrganizationScope.BeginUnscoped"/>.
/// </para>
/// </remarks>
public sealed class HttpOrganizationContext(IHttpContextAccessor accessor) : IOrganizationContext
{
    public Guid? CurrentOrganizationId
    {
        get
        {
            // An explicit declaration wins: it is how non-request code states what it means, and
            // how a request-bound job can deliberately act outside its caller's organization.
            if (OrganizationScope.IsDeclared)
            {
                return OrganizationScope.DeclaredOrganizationId;
            }

            var httpContext = accessor.HttpContext
                ?? throw new InvalidOperationException(
                    "No HTTP request and no declared organization scope, so no organization could " +
                    "be resolved. Organization-scoped queries here would silently return nothing. " +
                    "Wrap this work in OrganizationScope.Begin(organizationId), or " +
                    "OrganizationScope.BeginUnscoped() if it is meant to span organizations.");

            var raw = httpContext.User.FindFirst(OrganizationClaims.OrganizationId)?.Value;
            return Guid.TryParse(raw, out var organizationId) ? organizationId : null;
        }
    }
}
