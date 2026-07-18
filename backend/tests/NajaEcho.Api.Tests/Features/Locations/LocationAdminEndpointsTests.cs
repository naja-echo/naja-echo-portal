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
using NajaEcho.Domain.Users;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Locations;

[Collection("ApiTests")]
public sealed class LocationAdminEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid AdminUserId = Guid.Parse("c2eebc99-9c0b-4ef8-bb6d-6bb9bd380a33");
    private static readonly Guid RegularUserId = Guid.Parse("d3ffcd00-0d1c-4ef9-cc7e-7cc0ce491b44");

    public LocationAdminEndpointsTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IExternalLoginService, LocationFakeLoginService>();

                services.RemoveAll<IUexLocationClient>();
                services.AddSingleton<FakeLocationUexClient>();
                services.AddSingleton<IUexLocationClient>(sp => sp.GetRequiredService<FakeLocationUexClient>());

                services.RemoveAll<IImportCoordinator>();
                services.AddSingleton<FakeLocationCoordinator>();
                services.AddSingleton<IImportCoordinator>(sp => sp.GetRequiredService<FakeLocationCoordinator>());

                services.RemoveAll<IStarSystemRepository>();
                services.AddSingleton<IStarSystemRepository, FakeStarSystemRepo>();

                services.RemoveAll<ISpaceStationRepository>();
                services.AddSingleton<ISpaceStationRepository, FakeStationRepoForAdmin>();

                services.RemoveAll<ICityRepository>();
                services.AddSingleton<ICityRepository, FakeCityRepoForAdmin>();

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, LocationTestAuthHandler>(
                        LocationTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = LocationTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = LocationTestAuthHandler.SchemeName;
                });
            });
        });
    }

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", AdminUserId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        return client;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", RegularUserId.ToString());
        return client;
    }

    [Fact]
    public async Task Import_Unauthenticated_Returns401()
    {
        var response = await CreateClient().PostAsync("/api/admin/locations/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Import_AuthenticatedNonAdmin_Returns403()
    {
        var response = await CreateAuthenticatedClient().PostAsync("/api/admin/locations/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Import_AsAdmin_Returns200WithSummary()
    {
        _factory.Services.GetRequiredService<FakeLocationUexClient>().StarSystems = [MakeDoc("{}")];
        _factory.Services.GetRequiredService<FakeLocationUexClient>().SpaceStations = [MakeDoc("{}")];
        _factory.Services.GetRequiredService<FakeLocationCoordinator>().Held = false;

        var response = await CreateAdminClient().PostAsync("/api/admin/locations/import", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("starSystems");
        body.Should().Contain("spaceStations");
        body.Should().Contain("cities");
    }

    [Fact]
    public async Task Import_WhenAlreadyInProgress_Returns409()
    {
        _factory.Services.GetRequiredService<FakeLocationCoordinator>().Held = true;

        var response = await CreateAdminClient().PostAsync("/api/admin/locations/import", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Import_WhenSourceUnreachable_Returns502()
    {
        _factory.Services.GetRequiredService<FakeLocationUexClient>().ShouldThrow = true;
        _factory.Services.GetRequiredService<FakeLocationCoordinator>().Held = false;

        var response = await CreateAdminClient().PostAsync("/api/admin/locations/import", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Import_WhenSourceEmpty_Returns502()
    {
        _factory.Services.GetRequiredService<FakeLocationUexClient>().StarSystems = [];
        _factory.Services.GetRequiredService<FakeLocationUexClient>().SpaceStations = [MakeDoc("{}")];
        _factory.Services.GetRequiredService<FakeLocationCoordinator>().Held = false;

        var response = await CreateAdminClient().PostAsync("/api/admin/locations/import", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    private static JsonDocument MakeDoc(string json) => JsonDocument.Parse(json);
}

internal sealed class FakeLocationUexClient : IUexLocationClient
{
    public IReadOnlyList<JsonDocument> StarSystems { get; set; } = [JsonDocument.Parse("{}")];
    public IReadOnlyList<JsonDocument> SpaceStations { get; set; } = [JsonDocument.Parse("{}")];
    public IReadOnlyList<JsonDocument> Cities { get; set; } = [JsonDocument.Parse("{}")];
    public bool ShouldThrow { get; set; }

    public Task<IReadOnlyList<JsonDocument>> FetchAllStarSystemsAsync(CancellationToken ct = default)
    {
        if (ShouldThrow) throw new HttpRequestException("Unreachable");
        return Task.FromResult(StarSystems);
    }

    public Task<IReadOnlyList<JsonDocument>> FetchAllSpaceStationsAsync(CancellationToken ct = default)
    {
        if (ShouldThrow) throw new HttpRequestException("Unreachable");
        return Task.FromResult(SpaceStations);
    }

    public Task<IReadOnlyList<JsonDocument>> FetchAllCitiesAsync(CancellationToken ct = default)
    {
        if (ShouldThrow) throw new HttpRequestException("Unreachable");
        return Task.FromResult(Cities);
    }
}

internal sealed class FakeLocationCoordinator : IImportCoordinator
{
    public bool Held { get; set; }
    public bool TryAcquire() { if (Held) return false; Held = true; return true; }
    public void Release() => Held = false;
}

internal sealed class FakeStarSystemRepo : IStarSystemRepository
{
    public Task<(int added, int updated, int reactivated, int softDeleted)> BulkUpsertAsync(
        IReadOnlyList<JsonDocument> records, CancellationToken ct = default)
        => Task.FromResult((records.Count, 0, 0, 0));

    public Task<IReadOnlyDictionary<int, Guid>> GetActiveUexIdToIdMapAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());
}

internal sealed class FakeCityRepoForAdmin : ICityRepository
{
    public Task<(int added, int updated, int reactivated, int softDeleted, int skipped)> BulkUpsertAsync(
        IReadOnlyList<JsonDocument> records, IReadOnlyDictionary<int, Guid> starSystemMap, CancellationToken ct = default)
        => Task.FromResult((0, 0, 0, 0, 0));

    public Task<IReadOnlyList<NajaEcho.Application.Abstractions.CityDto>> SearchActiveCitiesAsync(string? search, int limit, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<NajaEcho.Application.Abstractions.CityDto>>([]);
}

internal sealed class FakeStationRepoForAdmin : ISpaceStationRepository
{
    public Task<(int, int, int, int, int)> BulkUpsertAsync(
        IReadOnlyList<JsonDocument> records, IReadOnlyDictionary<int, Guid> starSystemMap, CancellationToken ct = default)
        => Task.FromResult((records.Count, 0, 0, 0, 0));

    public Task<IReadOnlyList<StationDto>> SearchActiveStationsAsync(string? search, int limit, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<StationDto>>([]);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(true);
}

internal sealed class LocationFakeLoginService : IExternalLoginService
{
    public Task<LocalUser> FindOrCreateAsync(DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<LocalUser?>(new LocalUser(userId, "Test", "test"));
}

internal sealed class LocationTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "LocationTestScheme";

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
