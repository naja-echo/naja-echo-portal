using FluentAssertions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Infrastructure.Identity;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Identity;

public sealed class InMemoryUserSessionInvalidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UnknownUser_IsNotStale()
    {
        var invalidator = new InMemoryUserSessionInvalidator(new MutableClock(Now));

        invalidator.IsStaleSince(Guid.NewGuid(), Now).Should().BeFalse();
    }

    [Fact]
    public void SnapshotTakenBeforeTheChange_IsStale()
    {
        var clock = new MutableClock(Now);
        var invalidator = new InMemoryUserSessionInvalidator(clock);
        var userId = Guid.NewGuid();

        invalidator.Invalidate(userId);

        invalidator.IsStaleSince(userId, Now - TimeSpan.FromMinutes(5)).Should().BeTrue();
    }

    [Fact]
    public void SnapshotTakenAfterTheChange_IsFresh()
    {
        var clock = new MutableClock(Now);
        var invalidator = new InMemoryUserSessionInvalidator(clock);
        var userId = Guid.NewGuid();

        invalidator.Invalidate(userId);

        invalidator.IsStaleSince(userId, Now + TimeSpan.FromSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void InvalidationIsScopedToTheTargetUser()
    {
        var invalidator = new InMemoryUserSessionInvalidator(new MutableClock(Now));
        var target = Guid.NewGuid();
        var bystander = Guid.NewGuid();

        invalidator.Invalidate(target);

        invalidator.IsStaleSince(bystander, Now - TimeSpan.FromMinutes(5)).Should().BeFalse();
    }

    [Fact]
    public void EntriesOlderThanTheCookieLifetimeArePruned()
    {
        var clock = new MutableClock(Now);
        var invalidator = new InMemoryUserSessionInvalidator(clock);
        var stale = Guid.NewGuid();
        invalidator.Invalidate(stale);

        // A later invalidation for someone else triggers the prune sweep.
        clock.Now = Now + TimeSpan.FromHours(25);
        invalidator.Invalidate(Guid.NewGuid());

        invalidator.IsStaleSince(stale, Now - TimeSpan.FromMinutes(5)).Should().BeFalse();
    }

    private sealed class MutableClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now { get; set; } = now;

        public DateTimeOffset UtcNow => Now;
    }
}
