using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Domain.Ships;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Hangar;

[Collection("ApiTests")]
public sealed class AddRemoveShipEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Active ship that is NOT pre-owned → POST returns 201
    internal static readonly Guid KnownFreeShipId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    // Active ship that IS pre-owned → POST returns 409
    internal static readonly Guid KnownOwnedShipId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid MemberId = Guid.NewGuid();

    public AddRemoveShipEndpointTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IHangarRepository, AddRemoveFakeHangarRepo>();

                services.RemoveAll<IShipRepository>();
                services.AddSingleton<IShipRepository, AddRemoveFakeShipRepo>();

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

    // ── POST /api/hangar/mine ────────────────────────────────────────────

    [Fact]
    public async Task PostMine_Unauthenticated_Returns401()
    {
        var response = await CreateAnonymousClient()
            .PostAsJsonAsync("/api/hangar/mine", new { shipId = KnownFreeShipId });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostMine_ActiveShipNotOwned_Returns201()
    {
        var response = await CreateMemberClient()
            .PostAsJsonAsync("/api/hangar/mine", new { shipId = KnownFreeShipId });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PostMine_InactiveShip_Returns404()
    {
        var response = await CreateMemberClient()
            .PostAsJsonAsync("/api/hangar/mine", new { shipId = Guid.NewGuid() });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostMine_AlreadyOwnedShip_Returns409()
    {
        var response = await CreateMemberClient()
            .PostAsJsonAsync("/api/hangar/mine", new { shipId = KnownOwnedShipId });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── DELETE /api/hangar/mine/{shipId} ────────────────────────────────

    [Fact]
    public async Task DeleteMine_Unauthenticated_Returns401()
    {
        var response = await CreateAnonymousClient()
            .DeleteAsync($"/api/hangar/mine/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteMine_Authenticated_Returns204()
    {
        var response = await CreateMemberClient()
            .DeleteAsync($"/api/hangar/mine/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── GET /api/hangar/catalog/search ──────────────────────────────────

    [Fact]
    public async Task GetCatalogSearch_Unauthenticated_Returns401()
    {
        var response = await CreateAnonymousClient().GetAsync("/api/hangar/catalog/search");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCatalogSearch_Authenticated_Returns200WithPagedEnvelope()
    {
        var response = await CreateMemberClient().GetAsync("/api/hangar/catalog/search?search=glad");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("items");
        body.Should().Contain("totalCount");
    }
}

// ── Fakes ──────────────────────────────────────────────────────────────────

/// <summary>
/// Knows two ships: KnownFreeShipId (not owned) and KnownOwnedShipId (owned).
/// </summary>
internal sealed class AddRemoveFakeHangarRepo : NajaEcho.Application.Abstractions.IHangarRepository
{
    private static readonly HashSet<Guid> _preOwned = [AddRemoveShipEndpointTests.KnownOwnedShipId];

    public Task<NajaEcho.Application.Features.Hangar.PagedResult<NajaEcho.Application.Features.Hangar.GetMyHangar.ShipCard>> GetMyHangarAsync(
        Guid userId, string? search, int page, int pageSize, CancellationToken ct)
        => Task.FromResult(new NajaEcho.Application.Features.Hangar.PagedResult<NajaEcho.Application.Features.Hangar.GetMyHangar.ShipCard>([], page, pageSize, 0, 0));

    public Task<NajaEcho.Application.Features.Hangar.PagedResult<NajaEcho.Application.Features.Hangar.GetOrgHangar.OrgShipCard>> GetOrgHangarAsync(
        Guid currentUserId, string? search, bool mine, Guid? memberId, int page, int pageSize, string sortBy, CancellationToken ct)
        => Task.FromResult(new NajaEcho.Application.Features.Hangar.PagedResult<NajaEcho.Application.Features.Hangar.GetOrgHangar.OrgShipCard>([], page, pageSize, 0, 0));

    public Task<IReadOnlyList<NajaEcho.Application.Features.Hangar.GetOwningMembers.OwningMember>> GetOwningMembersAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<NajaEcho.Application.Features.Hangar.GetOwningMembers.OwningMember>>([]);

    public Task<NajaEcho.Application.Features.Hangar.PagedResult<NajaEcho.Application.Features.Hangar.SearchCatalogShips.CatalogSearchRow>> SearchCatalogAsync(
        Guid userId, string? search, int page, int pageSize, CancellationToken ct)
    {
        var items = _preOwned
            .Select(id => new NajaEcho.Application.Features.Hangar.SearchCatalogShips.CatalogSearchRow(
                id, "OwnedShip", null, null, null, null, true))
            .ToList();
        return Task.FromResult(new NajaEcho.Application.Features.Hangar.PagedResult<NajaEcho.Application.Features.Hangar.SearchCatalogShips.CatalogSearchRow>(
            items, page, pageSize, items.Count, 1));
    }

    public Task<bool> ExistsAsync(Guid userId, Guid shipId, CancellationToken ct)
        => Task.FromResult(_preOwned.Contains(shipId));

    public Task<NajaEcho.Application.Features.Hangar.GetMyHangar.ShipCard> AddAsync(Guid userId, Guid shipId, CancellationToken ct)
        => Task.FromResult(new NajaEcho.Application.Features.Hangar.GetMyHangar.ShipCard(shipId, "Test", null, null, null, null));

    public Task RemoveAsync(Guid userId, Guid shipId, CancellationToken ct)
        => Task.CompletedTask;

    public Task ReplaceFromImportAsync(Guid userId, IReadOnlyList<Guid> shipIds, CancellationToken ct)
        => Task.CompletedTask;

    public Task<Dictionary<string, Guid>> GetShipIdsByNamesAsync(IReadOnlyList<string> names, CancellationToken ct)
        => Task.FromResult(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// Returns Active ships for KnownFreeShipId and KnownOwnedShipId; null for all others.
/// </summary>
internal sealed class AddRemoveFakeShipRepo : IShipRepository
{
    private static readonly HashSet<Guid> _activeIds =
    [
        AddRemoveShipEndpointTests.KnownFreeShipId,
        AddRemoveShipEndpointTests.KnownOwnedShipId,
    ];

    public Task<Ship?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (_activeIds.Contains(id))
        {
            return Task.FromResult<Ship?>(new Ship
            {
                Id = id,
                Name = "TestShip",
                Status = ShipStatus.Active,
                UexId = 1,
                RawData = System.Text.Json.JsonDocument.Parse("{}"),
                ImportedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        return Task.FromResult<Ship?>(null);
    }

    public Task<Ship?> GetByUexIdAsync(int uexId, CancellationToken ct = default) => Task.FromResult<Ship?>(null);
    public Task<(IReadOnlyList<Ship> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<(int Added, int Updated, int Reactivated, int SoftDeleted)> BulkUpsertAsync(IReadOnlyList<Ship> incoming, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<int>> GetAllActiveUexIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
}
