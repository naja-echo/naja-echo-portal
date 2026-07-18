using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Hangar;
using NajaEcho.Application.Features.Hangar.GetMyHangar;
using NajaEcho.Application.Features.Hangar.GetOrgHangar;
using NajaEcho.Application.Features.Hangar.GetOwningMembers;
using NajaEcho.Application.Features.Hangar.SearchCatalogShips;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Hangar;

[Collection("ApiTests")]
public sealed class OrgHangarEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid OtherMemberId = Guid.NewGuid();
    private static readonly Guid SharedShipId = Guid.NewGuid();

    private readonly WebApplicationFactory<Program> _factory;

    public OrgHangarEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Discord:ClientId"] = "test-id",
                    ["Discord:ClientSecret"] = "test-secret",
                    ["ConnectionStrings:Default"] = "Host=localhost;Database=test;Username=test;Password=test",
                });
            });

            b.ConfigureTestServices(services =>
            {
                services.StubDatabase();

                services.RemoveAll<IExternalLoginService>();
                services.AddSingleton<IExternalLoginService, FakeHangarLoginService>();

                services.RemoveAll<IHangarRepository>();
                services.AddSingleton<IHangarRepository>(new OrgTestHangarRepo(
                    MemberId, OtherMemberId, SharedShipId));

                services.AddAuthentication()
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, HangarTestAuthHandler>(
                        HangarTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = HangarTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = HangarTestAuthHandler.SchemeName;
                });
            });
        });
    }

    private HttpClient CreateMemberClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", MemberId.ToString());
        return client;
    }

    private HttpClient CreateAnonymousClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    // ── GET /api/hangar/org ─────────────────────────────────────────────

    [Fact]
    public async Task GetOrg_Unauthenticated_Returns401()
    {
        var response = await CreateAnonymousClient().GetAsync("/api/hangar/org");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrg_Authenticated_Returns200WithPagedEnvelope()
    {
        var response = await CreateMemberClient().GetAsync("/api/hangar/org");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("items");
        body.Should().Contain("totalCount");
    }

    [Fact]
    public async Task GetOrg_WithMineParam_Returns200()
    {
        var response = await CreateMemberClient().GetAsync("/api/hangar/org?mine=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOrg_WithMemberIdParam_Returns200()
    {
        var response = await CreateMemberClient().GetAsync($"/api/hangar/org?memberId={OtherMemberId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOrg_EmptyResult_Returns200WithEmptyItems()
    {
        var emptyFactory = _factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHangarRepository>();
                services.AddSingleton<IHangarRepository>(new EmptyOrgHangarRepo());
            });
        });

        var client = emptyFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", MemberId.ToString());

        var response = await client.GetAsync("/api/hangar/org");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"items\":[]");
    }

    [Fact]
    public async Task GetOrg_GroupingContainsOwners_InResponseBody()
    {
        var response = await CreateMemberClient().GetAsync("/api/hangar/org");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("ownerCount");
        body.Should().Contain("owners");
    }

    // ── GET /api/hangar/org/members ─────────────────────────────────────

    [Fact]
    public async Task GetOrgMembers_Unauthenticated_Returns401()
    {
        var response = await CreateAnonymousClient().GetAsync("/api/hangar/org/members");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrgMembers_Authenticated_Returns200WithMemberArray()
    {
        var response = await CreateMemberClient().GetAsync("/api/hangar/org/members");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("userId");
        body.Should().Contain("displayName");
    }

    [Fact]
    public async Task GetOrgMembers_OnlyOwningMembersReturned()
    {
        var response = await CreateMemberClient().GetAsync("/api/hangar/org/members");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Alice");
        body.Should().Contain("Bob");
    }
}

// ── Fakes ──────────────────────────────────────────────────────────────────

internal sealed class OrgTestHangarRepo : IHangarRepository
{
    private readonly Guid _memberId;
    private readonly Guid _otherMemberId;
    private readonly Guid _sharedShipId;

    public OrgTestHangarRepo(Guid memberId, Guid otherMemberId, Guid sharedShipId)
    {
        _memberId = memberId;
        _otherMemberId = otherMemberId;
        _sharedShipId = sharedShipId;
    }

    public Task<PagedResult<ShipCard>> GetMyHangarAsync(
        Guid userId, string? search, int page, int pageSize, CancellationToken ct)
        => Task.FromResult(new PagedResult<ShipCard>([], page, pageSize, 0, 0));

    public Task<PagedResult<OrgShipCard>> GetOrgHangarAsync(
        Guid currentUserId, string? search, bool mine, Guid? memberId, int page, int pageSize, string sortBy, CancellationToken ct)
    {
        var cards = new List<OrgShipCard>
        {
            new(_sharedShipId, "Gladius", "Aegis", null, null, null, 2,
                [new(_memberId, "Alice"), new(_otherMemberId, "Bob")])
        };

        var filtered = cards.AsEnumerable();
        if (mine) filtered = filtered.Where(c => c.Owners.Any(o => o.UserId == currentUserId));
        else if (memberId.HasValue) filtered = filtered.Where(c => c.Owners.Any(o => o.UserId == memberId.Value));

        var list = filtered.ToList();
        return Task.FromResult(new PagedResult<OrgShipCard>(list, page, pageSize, list.Count, 1));
    }

    public Task<IReadOnlyList<OwningMember>> GetOwningMembersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OwningMember>>([
            new(_memberId, "Alice"),
            new(_otherMemberId, "Bob"),
        ]);

    public Task<PagedResult<CatalogSearchRow>> SearchCatalogAsync(
        Guid userId, string? search, int page, int pageSize, CancellationToken ct)
        => Task.FromResult(new PagedResult<CatalogSearchRow>([], page, pageSize, 0, 0));

    public Task<bool> ExistsAsync(Guid userId, Guid shipId, CancellationToken ct)
        => Task.FromResult(false);

    public Task<ShipCard> AddAsync(Guid userId, Guid shipId, CancellationToken ct)
        => throw new NotImplementedException();

    public Task RemoveAsync(Guid userId, Guid shipId, CancellationToken ct)
        => Task.CompletedTask;

    public Task ReplaceFromImportAsync(Guid userId, IReadOnlyList<Guid> shipIds, CancellationToken ct)
        => Task.CompletedTask;

    public Task<Dictionary<string, Guid>> GetShipIdsByNamesAsync(IReadOnlyList<string> names, CancellationToken ct)
        => Task.FromResult(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));
}

internal sealed class EmptyOrgHangarRepo : IHangarRepository
{
    public Task<PagedResult<ShipCard>> GetMyHangarAsync(
        Guid userId, string? search, int page, int pageSize, CancellationToken ct)
        => Task.FromResult(new PagedResult<ShipCard>([], page, pageSize, 0, 0));

    public Task<PagedResult<OrgShipCard>> GetOrgHangarAsync(
        Guid currentUserId, string? search, bool mine, Guid? memberId, int page, int pageSize, string sortBy, CancellationToken ct)
        => Task.FromResult(new PagedResult<OrgShipCard>([], page, pageSize, 0, 0));

    public Task<IReadOnlyList<OwningMember>> GetOwningMembersAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<OwningMember>>([]);

    public Task<PagedResult<CatalogSearchRow>> SearchCatalogAsync(
        Guid userId, string? search, int page, int pageSize, CancellationToken ct)
        => Task.FromResult(new PagedResult<CatalogSearchRow>([], page, pageSize, 0, 0));

    public Task<bool> ExistsAsync(Guid userId, Guid shipId, CancellationToken ct)
        => Task.FromResult(false);

    public Task<ShipCard> AddAsync(Guid userId, Guid shipId, CancellationToken ct)
        => throw new NotImplementedException();

    public Task RemoveAsync(Guid userId, Guid shipId, CancellationToken ct)
        => Task.CompletedTask;

    public Task ReplaceFromImportAsync(Guid userId, IReadOnlyList<Guid> shipIds, CancellationToken ct)
        => Task.CompletedTask;

    public Task<Dictionary<string, Guid>> GetShipIdsByNamesAsync(IReadOnlyList<string> names, CancellationToken ct)
        => Task.FromResult(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));
}
