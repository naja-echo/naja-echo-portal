using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NajaEcho.Api.Authorization;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Admin.Users.GetUsers;
using NajaEcho.Domain.Users;
using Xunit;

namespace NajaEcho.Api.Tests.Authorization;

public sealed class RoleClaimsRefresherTests
{
    private static readonly Guid UserId = Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecentlyRefreshedAndNotInvalidated_DoesNotQueryTheDatabase()
    {
        var (refresher, repo, _) = Make();
        var ctx = Context(refreshedAt: Now - TimeSpan.FromMinutes(1), roles: [Roles.Quartermaster]);

        await refresher.RefreshAsync(ctx);

        repo.GetRolesCallCount.Should().Be(0);
        RolesOf(ctx.Principal).Should().Equal(Roles.Quartermaster);
        ctx.ShouldRenew.Should().BeFalse();
    }

    [Fact]
    public async Task UserInvalidated_ReplacesRoleClaimsWithCurrentRoles()
    {
        var (refresher, repo, invalidator) = Make();
        repo.Roles = [Roles.CrewResourceOfficer];
        invalidator.Stale = true;
        var ctx = Context(refreshedAt: Now - TimeSpan.FromMinutes(1), roles: [Roles.Quartermaster]);

        await refresher.RefreshAsync(ctx);

        repo.GetRolesCallCount.Should().Be(1);
        RolesOf(ctx.Principal).Should().Equal(Roles.CrewResourceOfficer);
        ctx.ShouldRenew.Should().BeTrue();
    }

    [Fact]
    public async Task RevokedRole_LeavesPrincipalWithNoRoles()
    {
        var (refresher, repo, invalidator) = Make();
        repo.Roles = [];
        invalidator.Stale = true;
        var ctx = Context(refreshedAt: Now, roles: [Roles.Quartermaster]);

        await refresher.RefreshAsync(ctx);

        RolesOf(ctx.Principal).Should().BeEmpty();
        ctx.Principal!.IsInRole(Roles.Quartermaster).Should().BeFalse();
    }

    [Fact]
    public async Task FallbackIntervalElapsed_RefreshesEvenWithoutAnInvalidationSignal()
    {
        var (refresher, repo, _) = Make();
        repo.Roles = [Roles.Admin];
        var ctx = Context(
            refreshedAt: Now - RoleClaimsRefresher.FallbackInterval - TimeSpan.FromSeconds(1),
            roles: []);

        await refresher.RefreshAsync(ctx);

        repo.GetRolesCallCount.Should().Be(1);
        RolesOf(ctx.Principal).Should().Equal(Roles.Admin);
    }

    [Fact]
    public async Task SessionIssuedBeforeThisFeature_HasNoStampAndRefreshesOnce()
    {
        var (refresher, repo, _) = Make();
        repo.Roles = [Roles.Quartermaster];
        var ctx = Context(refreshedAt: null, roles: []);

        await refresher.RefreshAsync(ctx);

        repo.GetRolesCallCount.Should().Be(1);
        RolesOf(ctx.Principal).Should().Equal(Roles.Quartermaster);
        ctx.Properties.Items[RoleClaimsRefresher.RefreshedAtKey].Should().Be(Now.ToString("O"));
    }

    [Fact]
    public async Task Refresh_PreservesIdentityClaimsSetAtSignIn()
    {
        var (refresher, repo, invalidator) = Make();
        repo.Roles = [Roles.Admin];
        invalidator.Stale = true;
        var ctx = Context(refreshedAt: Now, roles: []);

        await refresher.RefreshAsync(ctx);

        var principal = ctx.Principal;
        principal.Should().NotBeNull();
        principal!.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(UserId.ToString());
        principal.FindFirstValue(ClaimTypes.Name).Should().Be("Test Pilot");
    }

    [Fact]
    public async Task PrincipalWithoutAUsableUserId_IsLeftAlone()
    {
        var (refresher, repo, invalidator) = Make();
        invalidator.Stale = true;
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "Test Pilot")], "TestScheme");
        var ctx = Context(refreshedAt: null, roles: [], principal: new ClaimsPrincipal(identity));

        await refresher.RefreshAsync(ctx);

        repo.GetRolesCallCount.Should().Be(0);
    }

    private static (RoleClaimsRefresher, FakeUserRepo, FakeInvalidator) Make()
    {
        var repo = new FakeUserRepo();
        var invalidator = new FakeInvalidator();
        var refresher = new RoleClaimsRefresher(
            repo, invalidator, new FixedClock(Now), NullLogger<RoleClaimsRefresher>.Instance);
        return (refresher, repo, invalidator);
    }

    private static CookieValidatePrincipalContext Context(
        DateTimeOffset? refreshedAt,
        string[] roles,
        ClaimsPrincipal? principal = null)
    {
        principal ??= new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                new Claim(ClaimTypes.Name, "Test Pilot"),
                .. roles.Select(r => new Claim(ClaimTypes.Role, r)),
            ],
            "TestScheme"));

        var properties = new AuthenticationProperties();
        if (refreshedAt is not null)
        {
            properties.Items[RoleClaimsRefresher.RefreshedAtKey] = refreshedAt.Value.ToString("O");
        }

        var scheme = new AuthenticationScheme("TestScheme", null, typeof(CookieAuthenticationHandler));
        return new CookieValidatePrincipalContext(
            new DefaultHttpContext(),
            scheme,
            new CookieAuthenticationOptions(),
            new AuthenticationTicket(principal, properties, scheme.Name));
    }

    private static string[] RolesOf(ClaimsPrincipal? principal) =>
        principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class FakeInvalidator : IUserSessionInvalidator
    {
        public bool Stale { get; set; }

        public void Invalidate(Guid userId) => Stale = true;

        public bool IsStaleSince(Guid userId, DateTimeOffset refreshedAt) => Stale;
    }

    private sealed class FakeUserRepo : IUserRepository
    {
        public int GetRolesCallCount { get; private set; }
        public IReadOnlyList<string> Roles { get; set; } = [];

        public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct)
        {
            GetRolesCallCount++;
            return Task.FromResult(Roles);
        }

        public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(true);
        public Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
        public Task<IReadOnlyList<AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdminUserDto>>([]);
        public Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct)
            => Task.CompletedTask;
    }
}
