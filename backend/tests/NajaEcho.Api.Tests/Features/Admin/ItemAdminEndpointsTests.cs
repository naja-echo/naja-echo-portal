using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Domain.ItemCategories;
using NajaEcho.Domain.Items;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Api.Tests.Features.Admin;

[Collection("ApiTests")]
public class ItemAdminEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid AdminUserId = Guid.Parse("a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11");
    private static readonly Guid RegularUserId = Guid.Parse("b1ffcd00-0d1c-4ef9-cc7e-7cc0ce491b22");

    public ItemAdminEndpointsTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IExternalLoginService, FakeItemTestLoginService>();

                services.RemoveAll<IUexCategoryClient>();
                services.AddSingleton<FakeApiCategoryClient>();
                services.AddSingleton<IUexCategoryClient>(sp => sp.GetRequiredService<FakeApiCategoryClient>());

                services.RemoveAll<IUexItemClient>();
                services.AddSingleton<FakeApiItemClient>();
                services.AddSingleton<IUexItemClient>(sp => sp.GetRequiredService<FakeApiItemClient>());

                services.RemoveAll<IImportCoordinator>();
                services.AddSingleton<FakeApiItemCoordinator>();
                services.AddSingleton<IImportCoordinator>(sp => sp.GetRequiredService<FakeApiItemCoordinator>());

                services.RemoveAll<IItemCategoryRepository>();
                services.AddSingleton<FakeApiItemCategoryRepository>();
                services.AddSingleton<IItemCategoryRepository>(sp => sp.GetRequiredService<FakeApiItemCategoryRepository>());

                services.RemoveAll<IItemRepository>();
                services.AddSingleton<FakeApiItemRepository>();
                services.AddSingleton<IItemRepository>(sp => sp.GetRequiredService<FakeApiItemRepository>());

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, AdminTestAuthHandler>(
                        AdminTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = AdminTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = AdminTestAuthHandler.SchemeName;
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

    // T022: GET /api/admin/items/categories — 401 unauthenticated
    [Fact]
    public async Task GetCategories_Unauthenticated_Returns401()
    {
        var response = await CreateUnauthenticatedClient().GetAsync("/api/admin/items/categories");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // T022: GET /api/admin/items/categories — 403 non-admin
    [Fact]
    public async Task GetCategories_NonAdmin_Returns403()
    {
        var response = await CreateAuthenticatedClient().GetAsync("/api/admin/items/categories");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T022: GET /api/admin/items/categories — 200 with category list
    [Fact]
    public async Task GetCategories_Admin_Returns200WithCategoryList()
    {
        var response = await CreateAdminClient().GetAsync("/api/admin/items/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("categories");
        body.Should().Contain("lastRefreshedAt");
    }

    // T022: POST /api/admin/items/categories/refresh — 401 unauthenticated
    [Fact]
    public async Task RefreshCategories_Unauthenticated_Returns401()
    {
        var response = await CreateUnauthenticatedClient().PostAsync("/api/admin/items/categories/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // T022: POST /api/admin/items/categories/refresh — 403 non-admin
    [Fact]
    public async Task RefreshCategories_NonAdmin_Returns403()
    {
        var response = await CreateAuthenticatedClient().PostAsync("/api/admin/items/categories/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T022: POST /api/admin/items/categories/refresh — 200 on success
    [Fact]
    public async Task RefreshCategories_Admin_Returns200()
    {
        _factory.Services.GetRequiredService<FakeApiItemCoordinator>().Held = false;
        _factory.Services.GetRequiredService<FakeApiCategoryClient>().ShouldThrow = false;

        var response = await CreateAdminClient().PostAsync("/api/admin/items/categories/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("fetched");
    }

    // T022: POST /api/admin/items/categories/refresh — 409 when already in progress
    [Fact]
    public async Task RefreshCategories_WhenAlreadyInProgress_Returns409()
    {
        _factory.Services.GetRequiredService<FakeApiItemCoordinator>().Held = true;

        var response = await CreateAdminClient().PostAsync("/api/admin/items/categories/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // T022: POST /api/admin/items/categories/refresh — 502 when feed fails
    [Fact]
    public async Task RefreshCategories_WhenFeedFails_Returns502()
    {
        _factory.Services.GetRequiredService<FakeApiItemCoordinator>().Held = false;
        _factory.Services.GetRequiredService<FakeApiCategoryClient>().ShouldThrow = true;

        var response = await CreateAdminClient().PostAsync("/api/admin/items/categories/refresh", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    // T022: POST /api/admin/items/import — 401 unauthenticated
    [Fact]
    public async Task ImportItems_Unauthenticated_Returns401()
    {
        var response = await CreateUnauthenticatedClient().PostAsync("/api/admin/items/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // T022: POST /api/admin/items/import — 403 non-admin
    [Fact]
    public async Task ImportItems_NonAdmin_Returns403()
    {
        var response = await CreateAuthenticatedClient().PostAsync("/api/admin/items/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // T022: POST /api/admin/items/import — 409 when already in progress
    [Fact]
    public async Task ImportItems_WhenAlreadyInProgress_Returns409()
    {
        _factory.Services.GetRequiredService<FakeApiItemCoordinator>().Held = true;

        var response = await CreateAdminClient().PostAsync("/api/admin/items/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // T022: POST /api/admin/items/import — 200 when no eligible categories
    [Fact]
    public async Task ImportItems_NoEligibleCategories_Returns200()
    {
        _factory.Services.GetRequiredService<FakeApiItemCoordinator>().Held = false;
        _factory.Services.GetRequiredService<FakeApiItemCategoryRepository>().EligibleCategories = [];

        var response = await CreateAdminClient().PostAsync("/api/admin/items/import", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("status");
    }
}

internal sealed class FakeApiCategoryClient : IUexCategoryClient
{
    public bool ShouldThrow { get; set; }

    public Task<IReadOnlyList<JsonDocument>> FetchAllCategoriesAsync(CancellationToken ct = default)
    {
        if (ShouldThrow) throw new HttpRequestException("Feed error");
        return Task.FromResult<IReadOnlyList<JsonDocument>>([]);
    }
}

internal sealed class FakeApiItemClient : IUexItemClient
{
    public bool ShouldThrow { get; set; }

    public Task<IReadOnlyList<JsonDocument>> FetchItemsByCategoryAsync(int categoryId, CancellationToken ct = default)
    {
        if (ShouldThrow) throw new HttpRequestException("Feed error");
        return Task.FromResult<IReadOnlyList<JsonDocument>>([]);
    }
}

internal sealed class FakeApiItemCoordinator : IImportCoordinator
{
    public bool Held { get; set; }
    public bool TryAcquire() { if (Held) return false; Held = true; return true; }
    public void Release() => Held = false;
}

internal sealed class FakeApiItemCategoryRepository : IItemCategoryRepository
{
    public IReadOnlyList<ItemCategory> EligibleCategories { get; set; } = [];

    public Task<(int Inserted, int Updated, int Unchanged)> BulkUpsertAsync(
        IReadOnlyList<ItemCategory> incoming, CancellationToken ct) =>
        Task.FromResult((0, 0, 0));

    public Task<IReadOnlyList<ItemCategory>> GetAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ItemCategory>>([]);

    public Task<IReadOnlyList<ItemCategory>> GetEligibleAsync(CancellationToken ct) =>
        Task.FromResult(EligibleCategories);

    public Task<DateTimeOffset?> GetLastRefreshedAtAsync(CancellationToken ct) =>
        Task.FromResult<DateTimeOffset?>(null);

    public Task<int> GetActiveItemCountAsync(int categoryUexId, CancellationToken ct) =>
        Task.FromResult(0);

    public Task<DateTimeOffset?> GetLastImportedAtAsync(int categoryUexId, CancellationToken ct) =>
        Task.FromResult<DateTimeOffset?>(null);
}

internal sealed class FakeApiItemRepository : IItemRepository
{
    public Task<Item?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<Item?>(null);

    public Task<(int Inserted, int Updated, int Unchanged, int SoftDeleted, int Restored)> BulkUpsertForCategoryAsync(
        int idCategory, IReadOnlyList<Item> incoming, CancellationToken ct) =>
        Task.FromResult((incoming.Count, 0, 0, 0, 0));
}

internal sealed class FakeItemTestLoginService : IExternalLoginService
{
    public Task<NajaEcho.Application.Abstractions.LocalUser> FindOrCreateAsync(
        NajaEcho.Domain.Users.DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new NajaEcho.Application.Abstractions.LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<NajaEcho.Application.Abstractions.LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<NajaEcho.Application.Abstractions.LocalUser?>(
            new NajaEcho.Application.Abstractions.LocalUser(userId, "Test", "test"));
}
