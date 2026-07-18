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
using NajaEcho.Domain.Users;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Api.Tests.Features.Warehouse;

[Collection("ApiTests")]
public sealed class WarehouseLocationEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid MemberId = Guid.NewGuid();

    public WarehouseLocationEndpointsTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IExternalLoginService, LocationEndpointFakeLoginService>();

                services.RemoveAll<ISpaceStationRepository>();
                services.AddSingleton<FakeLocationStationRepo>();
                services.AddSingleton<ISpaceStationRepository>(sp => sp.GetRequiredService<FakeLocationStationRepo>());

                services.RemoveAll<ICityRepository>();
                services.AddSingleton<ICityRepository, FakeLocationCityRepo>();

                services.AddAuthentication()
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, WarehouseTestAuthHandler>(
                        WarehouseTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = WarehouseTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = WarehouseTestAuthHandler.SchemeName;
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
        client.DefaultRequestHeaders.Add("X-Test-Roles", "");
        return client;
    }

    [Fact]
    public async Task GetLocations_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/warehouse/locations");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetLocations_AsAuthenticatedMember_Returns200WithLocationList()
    {
        var repo = _factory.Services.GetRequiredService<FakeLocationStationRepo>();
        repo.Stations = [new StationDto(Guid.NewGuid(), "ARC-L1 Wide Forest Station")];

        var response = await CreateMemberClient().GetAsync("/api/warehouse/locations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("locations");
    }

    [Fact]
    public async Task GetLocations_WithSearchParam_PassesSearchToRepo()
    {
        var repo = _factory.Services.GetRequiredService<FakeLocationStationRepo>();
        repo.Stations = [new StationDto(Guid.NewGuid(), "ARC-L1 Wide Forest Station")];

        var response = await CreateMemberClient().GetAsync("/api/warehouse/locations?search=ARC&limit=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        repo.LastSearch.Should().Be("ARC");
        repo.LastLimit.Should().Be(5);
    }
}

internal sealed class FakeLocationStationRepo : ISpaceStationRepository
{
    public string? LastSearch { get; private set; }
    public int LastLimit { get; private set; }
    public List<StationDto> Stations { get; set; } = [];

    public Task<(int, int, int, int, int)> BulkUpsertAsync(
        IReadOnlyList<JsonDocument> records, IReadOnlyDictionary<int, Guid> starSystemMap, CancellationToken ct = default)
        => Task.FromResult((0, 0, 0, 0, 0));

    public Task<IReadOnlyList<StationDto>> SearchActiveStationsAsync(string? search, int limit, CancellationToken ct = default)
    {
        LastSearch = search;
        LastLimit = limit;
        IReadOnlyList<StationDto> result = [.. Stations];
        return Task.FromResult(result);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(true);
}

internal sealed class FakeLocationCityRepo : ICityRepository
{
    public Task<(int added, int updated, int reactivated, int softDeleted, int skipped)> BulkUpsertAsync(
        IReadOnlyList<JsonDocument> records, IReadOnlyDictionary<int, Guid> starSystemMap, CancellationToken ct = default)
        => Task.FromResult((0, 0, 0, 0, 0));

    public Task<IReadOnlyList<CityDto>> SearchActiveCitiesAsync(string? search, int limit, CancellationToken ct = default)
    {
        IReadOnlyList<CityDto> result = [];
        return Task.FromResult(result);
    }
}

internal sealed class LocationEndpointFakeLoginService : IExternalLoginService
{
    public Task<LocalUser> FindOrCreateAsync(DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<LocalUser?>(new LocalUser(userId, "Test", "test"));
}
