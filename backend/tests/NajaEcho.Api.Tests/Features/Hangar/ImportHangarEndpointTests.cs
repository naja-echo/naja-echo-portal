using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
public sealed class ImportHangarEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid GladiusId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AvengerTitanId = new("22222222-2222-2222-2222-222222222222");

    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid MemberId = Guid.NewGuid();

    public ImportHangarEndpointTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<ImportFakeHangarRepo>();
                services.AddSingleton<IHangarRepository>(sp => sp.GetRequiredService<ImportFakeHangarRepo>());

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

    [Fact]
    public async Task PostImport_Unauthenticated_Returns401()
    {
        var response = await CreateAnonymousClient().PostAsJsonAsync(
            "/api/hangar/mine/import",
            new { items = Array.Empty<object>() });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostImport_ValidBody_Returns200WithResult()
    {
        var repo = _factory.Services.GetRequiredService<ImportFakeHangarRepo>();
        repo.Catalog["Gladius"] = GladiusId;
        repo.Catalog["Avenger Titan"] = AvengerTitanId;

        var response = await CreateMemberClient().PostAsJsonAsync(
            "/api/hangar/mine/import",
            new
            {
                items = new[]
                {
                    new { name = "Gladius" },
                    new { name = "Avenger Titan" },
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("importedShips");
        body.Should().Contain("totalRecords");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("importedShips").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("totalRecords").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("unmatchedRecords").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task PostImport_EmptyItems_Returns200WithZeroCounts()
    {
        var response = await CreateMemberClient().PostAsJsonAsync(
            "/api/hangar/mine/import",
            new { items = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("importedShips").GetInt32().Should().Be(0);
        doc.RootElement.GetProperty("totalRecords").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task PostImport_MissingItems_Returns400()
    {
        var response = await CreateMemberClient().PostAsJsonAsync(
            "/api/hangar/mine/import",
            new { });  // no "items" field

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostImport_UnidentifiedRecord_SkippedAndCounted()
    {
        var repo = _factory.Services.GetRequiredService<ImportFakeHangarRepo>();
        repo.Catalog["Gladius"] = GladiusId;

        var response = await CreateMemberClient().PostAsJsonAsync(
            "/api/hangar/mine/import",
            new
            {
                items = new object[]
                {
                    new { name = "Gladius" },
                    new { name = "A.T.L.S.", unidentified = "Please report this ship" },
                }
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("importedShips").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("unmatchedRecords").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task PostImport_ReplaceIsCalled_WithMatchedShipIds()
    {
        var repo = _factory.Services.GetRequiredService<ImportFakeHangarRepo>();
        repo.Catalog["Gladius"] = GladiusId;

        await CreateMemberClient().PostAsJsonAsync(
            "/api/hangar/mine/import",
            new { items = new[] { new { name = "Gladius" } } });

        repo.ReplacedWith.Should().ContainSingle().Which.Should().Be(GladiusId);
    }
}

internal sealed class ImportFakeHangarRepo : IHangarRepository
{
    public Dictionary<string, Guid> Catalog { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Guid> ReplacedWith { get; private set; } = [];

    public Task<Dictionary<string, Guid>> GetShipIdsByNamesAsync(IReadOnlyList<string> names, CancellationToken ct)
    {
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var n in names)
            if (Catalog.TryGetValue(n, out var id)) result[n] = id;
        return Task.FromResult(result);
    }

    public Task ReplaceFromImportAsync(Guid userId, IReadOnlyList<Guid> shipIds, CancellationToken ct)
    {
        ReplacedWith = [.. shipIds];
        return Task.CompletedTask;
    }

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
        => Task.FromResult(new ShipCard(shipId, "Test", null, null, null, null));

    public Task RemoveAsync(Guid userId, Guid shipId, CancellationToken ct) => Task.CompletedTask;
}
