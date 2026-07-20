using System.Collections.Concurrent;
using NajaEcho.Application.Abstractions;

namespace NajaEcho.Infrastructure.Identity;

/// <summary>
/// Process-local invalidation signal, registered as a singleton. Checking a flag costs a
/// dictionary lookup, so <c>OnValidatePrincipal</c> can consult it on every request without
/// touching the database.
/// </summary>
public sealed class InMemoryUserSessionInvalidator(IClock clock) : IUserSessionInvalidator
{
    // Entries older than this cannot affect any live session — the auth cookie's absolute
    // lifetime is shorter — so they are pruned.
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<Guid, DateTimeOffset> invalidatedAt = new();

    public void Invalidate(Guid userId)
    {
        var now = clock.UtcNow;
        invalidatedAt[userId] = now;
        Prune(now);
    }

    public bool IsStaleSince(Guid userId, DateTimeOffset refreshedAt) =>
        invalidatedAt.TryGetValue(userId, out var invalidated) && invalidated >= refreshedAt;

    private void Prune(DateTimeOffset now)
    {
        var cutoff = now - Retention;
        foreach (var (userId, at) in invalidatedAt)
        {
            if (at < cutoff)
            {
                invalidatedAt.TryRemove(userId, out _);
            }
        }
    }
}
