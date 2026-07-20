namespace NajaEcho.Application.Abstractions;

/// <summary>
/// The organization the current request is acting in, resolved without the member selecting or
/// supplying it.
/// </summary>
/// <remarks>
/// Null when the request is unauthenticated, or when the member holds no current membership.
/// Both are valid states: the global query filter compares against this value, and a null
/// never matches a non-nullable <c>organization_id</c>, so scoped retrievals return empty
/// rather than erroring (spec FR-005, FR-021).
///
/// This is an Application-layer port. Its HTTP-reading implementation lives in the API layer,
/// which already owns HttpContext concerns, so Infrastructure depends only on this abstraction.
/// </remarks>
public interface IOrganizationContext
{
    Guid? CurrentOrganizationId { get; }
}
