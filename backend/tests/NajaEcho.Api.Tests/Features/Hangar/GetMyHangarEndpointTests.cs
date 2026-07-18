using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Hangar;
using NajaEcho.Application.Features.Hangar.GetMyHangar;
using NajaEcho.Application.Features.Hangar.GetOrgHangar;
using NajaEcho.Application.Features.Hangar.GetOwningMembers;
using NajaEcho.Application.Features.Hangar.SearchCatalogShips;
using NajaEcho.Domain.Users;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Hangar;

[Collection("ApiTests")]
public sealed class GetMyHangarEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid MemberId = Guid.NewGuid();

    public GetMyHangarEndpointTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<FakeHangarRepository>();
                services.AddSingleton<IHangarRepository>(sp => sp.GetRequiredService<FakeHangarRepository>());

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, HangarTestAuthHandler>(
                        HangarTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = HangarTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = HangarTestAuthHandler.SchemeName;
                });
            });
        });
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient CreateMemberClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", MemberId.ToString());
        return client;
    }

    [Fact]
    public async Task GetMyHangar_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/hangar/mine");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMyHangar_Authenticated_Returns200WithPagedEnvelope()
    {
        var fakeRepo = _factory.Services.GetRequiredService<FakeHangarRepository>();
        fakeRepo.MyHangarCards.Add(new ShipCard(Guid.NewGuid(), "Gladius", "Aegis", null, null, null));

        var response = await CreateMemberClient().GetAsync("/api/hangar/mine");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("items");
        body.Should().Contain("totalCount");
        body.Should().Contain("page");
        body.Should().Contain("Gladius");
    }

    [Fact]
    public async Task GetMyHangar_WithSearchParam_FiltersResults()
    {
        var fakeRepo = _factory.Services.GetRequiredService<FakeHangarRepository>();
        fakeRepo.MyHangarCards.Add(new ShipCard(Guid.NewGuid(), "Gladius", null, null, null, null));
        fakeRepo.MyHangarCards.Add(new ShipCard(Guid.NewGuid(), "Avenger", null, null, null, null));

        var response = await CreateMemberClient().GetAsync("/api/hangar/mine?search=gladius");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Gladius");
        body.Should().NotContain("Avenger");
    }
}

internal sealed class FakeHangarRepository : IHangarRepository
{
    public List<ShipCard> MyHangarCards { get; } = [];

    public Task<PagedResult<ShipCard>> GetMyHangarAsync(
        Guid userId, string? search, int page, int pageSize, CancellationToken ct)
    {
        var filtered = MyHangarCards
            .Where(c => search == null || c.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var total = filtered.Count;
        var items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var totalPages = (int)Math.Ceiling(total / (double)Math.Max(pageSize, 1));
        return Task.FromResult(new PagedResult<ShipCard>(items, page, pageSize, total, totalPages));
    }

    public Task<PagedResult<OrgShipCard>> GetOrgHangarAsync(
        Guid currentUserId, string? search, bool mine, Guid? memberId, int page, int pageSize, string sortBy, CancellationToken ct)
    {
        var result = new PagedResult<OrgShipCard>([], page, pageSize, 0, 0);
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<OwningMember>> GetOwningMembersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OwningMember>>([]);

    public Task<PagedResult<CatalogSearchRow>> SearchCatalogAsync(
        Guid userId, string? search, int page, int pageSize, CancellationToken ct)
    {
        var result = new PagedResult<CatalogSearchRow>([], page, pageSize, 0, 0);
        return Task.FromResult(result);
    }

    public Task<bool> ExistsAsync(Guid userId, Guid shipId, CancellationToken ct) =>
        Task.FromResult(false);

    public Task<ShipCard> AddAsync(Guid userId, Guid shipId, CancellationToken ct) =>
        Task.FromResult(new ShipCard(shipId, "Test", null, null, null, null));

    public Task RemoveAsync(Guid userId, Guid shipId, CancellationToken ct) =>
        Task.CompletedTask;

    public Task ReplaceFromImportAsync(Guid userId, IReadOnlyList<Guid> shipIds, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<Dictionary<string, Guid>> GetShipIdsByNamesAsync(IReadOnlyList<string> names, CancellationToken ct) =>
        Task.FromResult(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase));
}

internal sealed class FakeHangarLoginService : IExternalLoginService
{
    public Task<LocalUser> FindOrCreateAsync(DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<LocalUser?>(new LocalUser(userId, "Test", "test"));
}

internal sealed class HangarTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "HangarTestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "Test Member"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
