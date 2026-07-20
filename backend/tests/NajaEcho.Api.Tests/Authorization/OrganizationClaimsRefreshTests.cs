using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NajaEcho.Api.Authorization;
using NajaEcho.Api.Organizations;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Admin.Users.GetUsers;
using NajaEcho.Domain.Organizations;
using NajaEcho.Domain.Users;
using Xunit;

namespace NajaEcho.Api.Tests.Authorization;

/// <summary>
/// An organization change must reach the member on their next request, without a sign-out
/// (spec FR-014, SC-004), through the same mechanism that already refreshes roles.
/// </summary>
public sealed class OrganizationClaimsRefreshTests
{
    private static readonly Guid UserId = Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrgAId = Guid.Parse("aaaaaaaa-0000-4000-a000-00000000000a");
    private static readonly Guid OrgBId = Guid.Parse("bbbbbbbb-0000-4000-a000-00000000000b");

    [Fact]
    public async Task InvalidatedSession_PicksUpTheNewOrganizationWithoutRejectingThePrincipal()
    {
        var (refresher, repo, invalidator) = Make();
        repo.Organization = new Organization { Id = OrgBId, Name = "Org B" };
        invalidator.Stale = true;

        var ctx = Context(refreshedAt: Now - TimeSpan.FromMinutes(1), organizationId: OrgAId);

        await refresher.RefreshAsync(ctx);

        OrganizationOf(ctx.Principal).Should().Be(OrgBId);
        ctx.ShouldRenew.Should().BeTrue();
        ctx.Principal.Should().NotBeNull(
            "the principal is refreshed in place — an organization change must not sign the member out");
    }

    [Fact]
    public async Task ClearingAnOrganization_RemovesTheClaimEntirely()
    {
        var (refresher, repo, invalidator) = Make();
        repo.Organization = null;
        invalidator.Stale = true;

        var ctx = Context(refreshedAt: Now - TimeSpan.FromMinutes(1), organizationId: OrgAId);

        await refresher.RefreshAsync(ctx);

        ctx.Principal!.FindFirst(OrganizationClaims.OrganizationId).Should().BeNull(
            "a stale claim left behind would keep an unassigned member acting inside their old " +
            "organization, which is the exact failure this refresh exists to prevent");
    }

    [Fact]
    public async Task AssigningAPreviouslyUnassignedMember_AddsTheClaim()
    {
        var (refresher, repo, invalidator) = Make();
        repo.Organization = new Organization { Id = OrgAId, Name = "Org A" };
        invalidator.Stale = true;

        var ctx = Context(refreshedAt: Now - TimeSpan.FromMinutes(1), organizationId: null);

        await refresher.RefreshAsync(ctx);

        OrganizationOf(ctx.Principal).Should().Be(OrgAId);
    }

    [Fact]
    public async Task RefreshPreservesNonRoleNonOrganizationClaims()
    {
        var (refresher, repo, invalidator) = Make();
        repo.Organization = new Organization { Id = OrgBId, Name = "Org B" };
        invalidator.Stale = true;

        var ctx = Context(refreshedAt: Now - TimeSpan.FromMinutes(1), organizationId: OrgAId);

        await refresher.RefreshAsync(ctx);

        ctx.Principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(UserId.ToString());
        ctx.Principal.FindFirst(ClaimTypes.Name)!.Value.Should().Be("Test Pilot");
    }

    [Fact]
    public async Task NotStaleAndRecentlyRefreshed_DoesNotQueryTheOrganization()
    {
        var (refresher, repo, _) = Make();

        var ctx = Context(refreshedAt: Now - TimeSpan.FromMinutes(1), organizationId: OrgAId);

        await refresher.RefreshAsync(ctx);

        repo.GetOrganizationCallCount.Should().Be(0,
            "the organization is read in the same pass as roles — it must not add a per-request query");
        OrganizationOf(ctx.Principal).Should().Be(OrgAId);
    }

    [Fact]
    public async Task RolesAndOrganizationRefreshTogetherInOnePass()
    {
        var (refresher, repo, invalidator) = Make();
        repo.Organization = new Organization { Id = OrgBId, Name = "Org B" };
        invalidator.Stale = true;

        var ctx = Context(refreshedAt: Now - TimeSpan.FromMinutes(1), organizationId: OrgAId);

        await refresher.RefreshAsync(ctx);

        OrganizationOf(ctx.Principal).Should().Be(OrgBId);
        repo.GetOrganizationCallCount.Should().Be(1,
            "the organization is read once, in the same pass as roles");
    }

    // ── The seam: claim → HttpOrganizationContext → DbContext ─────────────

    [Fact]
    public void OrganizationContextResolvesTheRefreshedClaimFromThePrincipal()
    {
        // T010, T034 and T035 each handle one leg of the journey from database to query filter;
        // none of them proves the legs connect. This asserts the join: a principal carrying the
        // claim yields that organization through the same IOrganizationContext AppDbContext reads.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(OrganizationClaims.OrganizationId, OrgBId.ToString())], "TestScheme"));

        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };

        new HttpOrganizationContext(accessor).CurrentOrganizationId.Should().Be(OrgBId);
    }

    [Fact]
    public void OrganizationContextIsNullForAPrincipalWithNoOrganizationClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, UserId.ToString())], "TestScheme"));

        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };

        new HttpOrganizationContext(accessor).CurrentOrganizationId.Should().BeNull(
            "an unassigned member must resolve to null so the query filter returns empty (FR-021)");
    }

    [Fact]
    public void OrganizationContextIsNullForAnUnparseableClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(OrganizationClaims.OrganizationId, "not-a-guid")], "TestScheme"));

        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };

        new HttpOrganizationContext(accessor).CurrentOrganizationId.Should().BeNull(
            "a malformed claim must fail closed, never fall back to some default organization");
    }

    [Fact]
    public void OrganizationContextThrowsWhenThereIsNoRequestAndNoDeclaredScope()
    {
        var context = new HttpOrganizationContext(new HttpContextAccessor { HttpContext = null });

        var act = () => context.CurrentOrganizationId;

        // Silently resolving to null here would make every organization-scoped read in a
        // background job return zero rows while looking like success.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OrganizationScope*");
    }

    [Fact]
    public void DeclaredScopeSuppliesTheOrganizationOutsideARequest()
    {
        var context = new HttpOrganizationContext(new HttpContextAccessor { HttpContext = null });

        using (OrganizationScope.Begin(OrgAId))
        {
            context.CurrentOrganizationId.Should().Be(OrgAId);
        }
    }

    [Fact]
    public void UnscopedDeclarationYieldsNullWithoutThrowing()
    {
        var context = new HttpOrganizationContext(new HttpContextAccessor { HttpContext = null });

        using (OrganizationScope.BeginUnscoped())
        {
            context.CurrentOrganizationId.Should().BeNull(
                "spanning organizations is legitimate when stated explicitly");
        }
    }

    [Fact]
    public void DeclaredScopeUnwindsOnDispose()
    {
        var context = new HttpOrganizationContext(new HttpContextAccessor { HttpContext = null });

        using (OrganizationScope.Begin(OrgAId))
        {
            using (OrganizationScope.Begin(OrgBId))
            {
                context.CurrentOrganizationId.Should().Be(OrgBId);
            }

            context.CurrentOrganizationId.Should().Be(OrgAId, "the inner scope must not leak out");
        }

        OrganizationScope.IsDeclared.Should().BeFalse();
    }

    [Fact]
    public void DeclaredScopeOverridesTheRequestClaim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(OrganizationClaims.OrganizationId, OrgAId.ToString())], "TestScheme"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        var context = new HttpOrganizationContext(accessor);

        using (OrganizationScope.Begin(OrgBId))
        {
            context.CurrentOrganizationId.Should().Be(OrgBId,
                "an explicit declaration is how request-bound work deliberately acts elsewhere");
        }

        context.CurrentOrganizationId.Should().Be(OrgAId);
    }

    // ── Harness ───────────────────────────────────────────────────────────

    private static (RoleClaimsRefresher Refresher, FakeOrganizationRepo Repo, FakeInvalidator Invalidator) Make()
    {
        var userRepo = new FakeUserRepo();
        var organizationRepo = new FakeOrganizationRepo();
        var invalidator = new FakeInvalidator();
        var refresher = new RoleClaimsRefresher(
            userRepo, organizationRepo, invalidator, new FixedClock(Now),
            NullLogger<RoleClaimsRefresher>.Instance);
        return (refresher, organizationRepo, invalidator);
    }

    private static CookieValidatePrincipalContext Context(DateTimeOffset? refreshedAt, Guid? organizationId)
    {
        Claim[] organizationClaims = organizationId is { } id
            ? [new Claim(OrganizationClaims.OrganizationId, id.ToString())]
            : [];

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
                new Claim(ClaimTypes.Name, "Test Pilot"),
                .. organizationClaims,
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

    private static Guid? OrganizationOf(ClaimsPrincipal? principal)
    {
        var raw = principal?.FindFirst(OrganizationClaims.OrganizationId)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

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

    private sealed class FakeOrganizationRepo : IOrganizationRepository
    {
        public int GetOrganizationCallCount { get; private set; }
        public Organization? Organization { get; set; }

        public Task<Organization?> GetCurrentForUserAsync(Guid userId, CancellationToken ct)
        {
            GetOrganizationCallCount++;
            return Task.FromResult(Organization);
        }

        public Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Organization>>([]);
        public Task<bool> ExistsAsync(Guid organizationId, CancellationToken ct) => Task.FromResult(true);
        public Task<Guid?> SetCurrentAsync(Guid userId, Guid? organizationId, CancellationToken ct)
            => Task.FromResult<Guid?>(null);
    }

    private sealed class FakeUserRepo : IUserRepository
    {
        public int GetRolesCallCount { get; private set; }
        public int GetOrganizationCallCount { get; private set; }
        public IReadOnlyList<string> Roles { get; set; } = [];
        public Organization? Organization { get; set; }

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
