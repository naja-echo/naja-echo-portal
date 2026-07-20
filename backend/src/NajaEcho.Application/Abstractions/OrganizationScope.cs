namespace NajaEcho.Application.Abstractions;

/// <summary>
/// Declares which organization a non-request code path acts in.
/// </summary>
/// <remarks>
/// <para>
/// The organization normally comes from the acting member's session claim. Background jobs,
/// startup seeding, imports and CLI tasks have no session, so without this they would resolve to
/// "no organization" — and because the query filter compares <c>organization_id</c> to null, every
/// organization-scoped read would return zero rows rather than failing. A nightly rollup would
/// process nothing and report success.
/// </para>
/// <para>
/// So non-request code must say what it means, explicitly:
/// <code>
/// using (OrganizationScope.Begin(organizationId))
/// {
///     // scoped reads inside here see that organization
/// }
/// </code>
/// Use <see cref="BeginUnscoped"/> for work that genuinely spans organizations — a cross-tenant
/// report, or an import that stamps rows itself. It reads as a decision rather than an omission.
/// </para>
/// <para>
/// Flows across async calls via <see cref="AsyncLocal{T}"/> and unwinds on dispose, so concurrent
/// jobs in one process do not see each other's scope.
/// </para>
/// </remarks>
public static class OrganizationScope
{
    private static readonly AsyncLocal<ScopeState?> Current = new();

    /// <summary>True when non-request code has declared a scope.</summary>
    public static bool IsDeclared => Current.Value is not null;

    /// <summary>The declared organization, or null inside a <see cref="BeginUnscoped"/> block.</summary>
    public static Guid? DeclaredOrganizationId => Current.Value?.OrganizationId;

    /// <summary>Acts as a member of <paramref name="organizationId"/> until disposed.</summary>
    public static IDisposable Begin(Guid organizationId) => new Handle(new ScopeState(organizationId));

    /// <summary>
    /// Declares that this work deliberately spans organizations and expects no scoping.
    /// </summary>
    public static IDisposable BeginUnscoped() => new Handle(new ScopeState(null));

    private sealed record ScopeState(Guid? OrganizationId);

    private sealed class Handle : IDisposable
    {
        private readonly ScopeState? _previous;
        private bool _disposed;

        public Handle(ScopeState state)
        {
            _previous = Current.Value;
            Current.Value = state;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Current.Value = _previous;
        }
    }
}
