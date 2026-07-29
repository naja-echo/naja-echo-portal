namespace NajaEcho.Application.Abstractions;

/// <summary>
/// Signals that a user's authorization facts (currently roles) changed and any live session
/// holding a snapshot of them must refresh on its next request.
/// </summary>
/// <remarks>
/// The signal is process-local. On a multi-instance deployment an instance that did not handle
/// the admin request will not see the flag and will fall back to interval-based refresh instead.
/// This interface is the seam for a distributed implementation if that day comes.
/// </remarks>
public interface IUserSessionInvalidator
{
    /// <summary>Marks the user's cached authorization facts as stale as of now.</summary>
    void Invalidate(Guid userId);

    /// <summary>
    /// True when the user was invalidated at or after <paramref name="refreshedAt"/> — i.e. the
    /// caller's snapshot predates the change.
    /// </summary>
    bool IsStaleSince(Guid userId, DateTimeOffset refreshedAt);
}
