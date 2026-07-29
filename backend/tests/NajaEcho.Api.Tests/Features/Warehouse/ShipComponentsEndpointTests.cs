using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
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
using NajaEcho.Application.Features.Warehouse.ShipComponents.GetShipComponentFilters;
using NajaEcho.Application.Features.Warehouse.ShipComponents.GetShipComponents;
using NajaEcho.Application.Features.Warehouse.ShipComponents.SearchSystemsCatalog;
using NajaEcho.Domain.Warehouse;
using NajaEcho.Infrastructure.Persistence;
using NajaEcho.Domain.Organizations;

namespace NajaEcho.Api.Tests.Features.Warehouse;

[Collection("ApiTests")]
public sealed class ShipComponentsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid QuartermasterId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    public ShipComponentsEndpointTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IExternalLoginService, ScFakeLoginService>();

                services.RemoveAll<IShipComponentRepository>();
                services.AddSingleton<FakeScRepo>();
                services.AddSingleton<IShipComponentRepository>(sp => sp.GetRequiredService<FakeScRepo>());

                services.RemoveAll<IWarehouseInventoryRepository>();
                services.AddSingleton<FakeWarehouseRepo>();
                services.AddSingleton<IWarehouseInventoryRepository>(sp => sp.GetRequiredService<FakeWarehouseRepo>());

                services.RemoveAll<IItemRepository>();
                services.AddSingleton<IItemRepository, ScFakeItemRepo>();

                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository, ScFakeUserRepo>();

                services.RemoveAll<IUexItemAttributeClient>();
                services.AddSingleton<IUexItemAttributeClient, ScFakeUexAttributeClient>();

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, ScTestAuthHandler>(ScTestAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = ScTestAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = ScTestAuthHandler.SchemeName;
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

    private HttpClient CreateQuartermasterClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", QuartermasterId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Quartermaster");
        return client;
    }

    private HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", AdminId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Admin");
        return client;
    }

    // ── GET /api/warehouse/ship-components ──────────────────────────────

    [Fact]
    public async Task GetShipComponents_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/warehouse/ship-components");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetShipComponents_AuthenticatedNonQM_Returns200()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/ship-components");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetShipComponents_Returns200WithItemsEnvelope()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/ship-components");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("items");
    }

    [Fact]
    public async Task GetShipComponents_NullClassSizeGrade_SerializedAsNull()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/ship-components");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"class\":null");
    }

    // ── GET /api/warehouse/ship-components/filters ───────────────────────

    [Fact]
    public async Task GetShipComponentFilters_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/warehouse/ship-components/filters");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetShipComponentFilters_AuthenticatedNonQM_Returns200()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/ship-components/filters");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetShipComponentFilters_ReturnsExpectedFields()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/ship-components/filters");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("types");
        body.Should().Contain("unknownClass");
        body.Should().Contain("unknownSize");
        body.Should().Contain("unknownGrade");
    }

    // ── GET /api/warehouse/ship-components/catalog/search ────────────────

    [Fact]
    public async Task GetSystemsCatalogSearch_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/api/warehouse/ship-components/catalog/search");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSystemsCatalogSearch_NonQM_Returns403()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/ship-components/catalog/search");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSystemsCatalogSearch_Quartermaster_Returns200()
    {
        var response = await CreateQuartermasterClient().GetAsync("/api/warehouse/ship-components/catalog/search?search=shield");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSystemsCatalogSearch_Admin_Returns200()
    {
        var response = await CreateAdminClient().GetAsync("/api/warehouse/ship-components/catalog/search");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostShipComponents_Quartermaster_Returns201()
    {
        var fakeRepo = _factory.Services.GetRequiredService<FakeWarehouseRepo>();
        fakeRepo.NextAddIsNew = true;

        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/ship-components",
            new { itemId = Guid.NewGuid(), location = "Bay 1", quantity = 1, quality = 700 });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"quality\":700");
    }

    [Fact]
    public async Task PostShipComponents_InvalidQuality_Returns400()
    {
        var response = await CreateQuartermasterClient().PostAsJsonAsync("/api/warehouse/ship-components",
            new { itemId = Guid.NewGuid(), location = "Bay 1", quantity = 1, quality = 0 });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Filter params on GET /api/warehouse/ship-components ──────────────

    [Fact]
    public async Task GetShipComponents_WithUnknownClassParam_Returns200()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/ship-components?unknownClass=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetShipComponents_WithMultipleTypeParams_Returns200()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/ship-components?type=Shield&type=Gun");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GET /api/warehouse/ship-components/filters ─────────────────────────

    [Fact]
    public async Task GetShipComponentFilters_ReturnsUnknownFlagFields()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/ship-components/filters");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"unknownClass\"");
        body.Should().Contain("\"unknownSize\"");
        body.Should().Contain("\"unknownGrade\"");
    }

    [Fact]
    public async Task GetShipComponentFilters_ReturnsOwnersList()
    {
        var response = await CreateMemberClient().GetAsync("/api/warehouse/ship-components/filters");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"owners\"");
    }
}

// ── Fakes ─────────────────────────────────────────────────────────────────────

internal sealed class FakeScRepo : IShipComponentRepository
{
    public static readonly Guid KnownRowId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid KnownItemId = new("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid KnownOwnerId = new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static ShipComponentRowDto MakeRow(Guid id) =>
        new(id, KnownItemId, "Shield Mk1", "Shield", null, null, null, 1, 500, KnownOwnerId, "Alice", "Bay 1");

    public Task<IReadOnlyList<ShipComponentRowDto>> GetShipComponentsAsync(GetShipComponentsQuery query, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ShipComponentRowDto>>([MakeRow(KnownRowId)]);

    public Task<ShipComponentFiltersDto> GetShipComponentFiltersAsync(CancellationToken ct) =>
        Task.FromResult(new ShipComponentFiltersDto(["Shield"], [], [], [], [], [], false, false, false));

    public Task<IReadOnlyList<SystemsCatalogItemDto>> SearchSystemsCatalogAsync(string? search, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SystemsCatalogItemDto>>([new SystemsCatalogItemDto(KnownItemId, "Shield Mk1", "Shield")]);

    public Task<bool> HasCachedAttributesAsync(Guid itemId, CancellationToken ct) => Task.FromResult(false);
    public Task SaveItemAttributesAsync(IReadOnlyList<NajaEcho.Domain.Warehouse.ItemAttribute> attributes, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ScFakeItemRepo : IItemRepository
{
    public Task<NajaEcho.Domain.Items.Item?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<NajaEcho.Domain.Items.Item?>(new NajaEcho.Domain.Items.Item
        {
            Id = id,
            Name = "Shield Mk1",
            Section = "Systems",
            Status = NajaEcho.Domain.Items.ItemStatus.Active,
            Uuid = id.ToString(),
            UexId = 1,
            IdCategory = 1,
            RawData = System.Text.Json.JsonDocument.Parse("{}"),
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

    public Task<(int Inserted, int Updated, int Unchanged, int SoftDeleted, int Restored)> BulkUpsertForCategoryAsync(
        int idCategory, IReadOnlyList<NajaEcho.Domain.Items.Item> incoming, CancellationToken ct) =>
        throw new NotImplementedException();
}

internal sealed class ScFakeUserRepo : IUserRepository
{
    public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(true);
    public Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
    public Task<IReadOnlyList<NajaEcho.Application.Features.Admin.Users.GetUsers.AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NajaEcho.Application.Features.Admin.Users.GetUsers.AdminUserDto>>([]);
    public Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct) => Task.CompletedTask;


    public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}

internal sealed class ScFakeUexAttributeClient : IUexItemAttributeClient
{
    public Task<IReadOnlyList<System.Text.Json.JsonDocument>> FetchItemAttributesAsync(int uexItemId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<System.Text.Json.JsonDocument>>([]);
}

internal sealed class ScFakeLoginService : IExternalLoginService
{
    public Task<LocalUser> FindOrCreateAsync(NajaEcho.Domain.Users.DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<LocalUser?>(new LocalUser(userId, "Test", "test"));
}

internal sealed class ScTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ScTestScheme";

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

        if (Request.Headers.TryGetValue("X-Test-Roles", out var rolesHeader))
        {
            foreach (var role in rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries))
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
