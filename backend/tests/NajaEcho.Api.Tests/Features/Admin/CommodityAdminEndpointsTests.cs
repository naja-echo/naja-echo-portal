using System.Net;
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
using NajaEcho.Application.Features.Commodities.GetCommodities;
using NajaEcho.Domain.Commodities;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Api.Tests.Features.Admin;

[Collection("ApiTests")]
public class CommodityAdminEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid AdminUserId = Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
    private static readonly Guid RegularUserId = Guid.Parse("b1ffcd00-0d1c-4ef9-cc7e-7cc0ce491b22");

    public CommodityAdminEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Discord:ClientId"] = "test-client-id",
                    ["Discord:ClientSecret"] = "test-client-secret",
                    ["ConnectionStrings:Default"] = "Host=localhost;Database=test;Username=test;Password=test",
                });
            });

            b.ConfigureTestServices(services =>
            {
                services.StubDatabase();

                services.RemoveAll<IExternalLoginService>();
                services.AddSingleton<IExternalLoginService, FakeCommodityTestLoginService>();

                services.RemoveAll<IUexCommodityClient>();
                services.AddSingleton<FakeApiCommodityClient>();
                services.AddSingleton<IUexCommodityClient>(sp => sp.GetRequiredService<FakeApiCommodityClient>());

                services.RemoveAll<IImportCoordinator>();
                services.AddSingleton<FakeApiCommodityCoordinator>();
                services.AddSingleton<IImportCoordinator>(sp => sp.GetRequiredService<FakeApiCommodityCoordinator>());

                services.RemoveAll<ICommodityRepository>();
                services.AddSingleton<FakeApiCommodityRepository>();
                services.AddSingleton<ICommodityRepository>(sp => sp.GetRequiredService<FakeApiCommodityRepository>());

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, CommodityTestAuthHandler>(
                        CommodityTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = CommodityTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = CommodityTestAuthHandler.SchemeName;
                });
            });
        });
    }

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", AdminUserId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        return client;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", RegularUserId.ToString());
        return client;
    }

    private HttpClient CreateUnauthenticatedClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task ImportCommodities_Unauthenticated_Returns401()
    {
        var response = await CreateUnauthenticatedClient().PostAsync("/api/admin/commodities/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ImportCommodities_NonAdmin_Returns403()
    {
        var response = await CreateAuthenticatedClient().PostAsync("/api/admin/commodities/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ImportCommodities_Admin_EmptyFeed_Returns202WithWarning()
    {
        _factory.Services.GetRequiredService<FakeApiCommodityCoordinator>().Held = false;
        _factory.Services.GetRequiredService<FakeApiCommodityClient>().Records = [];

        var response = await CreateAdminClient().PostAsync("/api/admin/commodities/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("warning");
    }

    [Fact]
    public async Task ImportCommodities_Admin_WithRecords_Returns200()
    {
        _factory.Services.GetRequiredService<FakeApiCommodityCoordinator>().Held = false;
        _factory.Services.GetRequiredService<FakeApiCommodityClient>().Records =
        [
            JsonDocument.Parse("""{"id":1,"name":"Agricium","code":"AGR","slug":"agricium","is_available":1,"is_available_live":0,"is_visible":1,"is_extractable":0,"is_mineral":0,"is_raw":0,"is_pure":0,"is_refined":0,"is_refinable":0,"is_harvestable":0,"is_buyable":1,"is_sellable":1,"is_temporary":0,"is_illegal":0,"is_volatile_qt":0,"is_volatile_time":0,"is_inert":0,"is_explosive":0,"is_buggy":0,"is_fuel":0,"date_added":0,"date_modified":0}""")
        ];

        var response = await CreateAdminClient().PostAsync("/api/admin/commodities/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("fetched");
        body.Should().Contain("inserted");
    }

    [Fact]
    public async Task ImportCommodities_WhenAlreadyInProgress_Returns409()
    {
        _factory.Services.GetRequiredService<FakeApiCommodityCoordinator>().Held = true;

        var response = await CreateAdminClient().PostAsync("/api/admin/commodities/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ImportCommodities_WhenFeedFails_Returns502()
    {
        _factory.Services.GetRequiredService<FakeApiCommodityCoordinator>().Held = false;
        _factory.Services.GetRequiredService<FakeApiCommodityClient>().ShouldThrow = true;

        var response = await CreateAdminClient().PostAsync("/api/admin/commodities/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }
}

internal sealed class FakeApiCommodityClient : IUexCommodityClient
{
    public bool ShouldThrow { get; set; }
    public IReadOnlyList<JsonDocument> Records { get; set; } = [];

    public Task<IReadOnlyList<JsonDocument>> FetchAllCommoditiesAsync(CancellationToken ct = default)
    {
        if (ShouldThrow) throw new HttpRequestException("Feed error");
        return Task.FromResult(Records);
    }
}

internal sealed class FakeApiCommodityCoordinator : IImportCoordinator
{
    public bool Held { get; set; }
    public bool TryAcquire() { if (Held) return false; Held = true; return true; }
    public void Release() => Held = false;
}

internal sealed class FakeApiCommodityRepository : ICommodityRepository
{
    public Task<(int Inserted, int Updated, int Unchanged, int Restored, int SoftDeleted)> BulkUpsertAsync(
        IReadOnlyList<Commodity> incoming, CancellationToken ct) =>
        Task.FromResult((incoming.Count, 0, 0, 0, 0));

    public Task<(IReadOnlyList<CommodityListItem> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default) =>
        Task.FromResult(((IReadOnlyList<CommodityListItem>)[], 0));

    public Task<Commodity?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<Commodity?>(null);
}

internal sealed class FakeCommodityTestLoginService : IExternalLoginService
{
    public Task<NajaEcho.Application.Abstractions.LocalUser> FindOrCreateAsync(
        NajaEcho.Domain.Users.DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new NajaEcho.Application.Abstractions.LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<NajaEcho.Application.Abstractions.LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<NajaEcho.Application.Abstractions.LocalUser?>(
            new NajaEcho.Application.Abstractions.LocalUser(userId, "Test", "test"));
}

internal sealed class CommodityTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "CommodityTestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "Test User"),
        };

        if (Request.Headers.TryGetValue("X-Test-Role", out var role))
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
