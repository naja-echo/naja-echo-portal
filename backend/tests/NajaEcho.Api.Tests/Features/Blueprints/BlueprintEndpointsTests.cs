using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetMyBlueprints;
using NajaEcho.Application.Features.Blueprints.GetOrgBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetOrgBlueprints;
using NajaEcho.Application.Features.Blueprints.SearchBlueprints;
using NajaEcho.Domain.Blueprints;
using NajaEcho.Domain.Users;

namespace NajaEcho.Api.Tests.Features.Blueprints;

[Collection("ApiTests")]
public class BlueprintEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly Guid UserId = Guid.Parse("c0eebc99-9c0b-4ef8-bb6d-6bb9bd380a33");

    public BlueprintEndpointsTests(WebApplicationFactory<Program> factory)
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
                services.AddSingleton<IExternalLoginService, FakeBlueprintTestLoginService>();

                services.RemoveAll<IBlueprintRepository>();
                services.AddSingleton<FakeSearchableBlueprintRepository>();
                services.AddSingleton<IBlueprintRepository>(sp => sp.GetRequiredService<FakeSearchableBlueprintRepository>());

                services.RemoveAll<IUserBlueprintRepository>();
                services.AddSingleton<FakeUserBlueprintTestRepository>();
                services.AddSingleton<IUserBlueprintRepository>(sp => sp.GetRequiredService<FakeUserBlueprintTestRepository>());

                services.RemoveAll<IOrgBlueprintRepository>();
                services.AddSingleton<FakeOrgBlueprintTestRepository>();
                services.AddSingleton<IOrgBlueprintRepository>(sp => sp.GetRequiredService<FakeOrgBlueprintTestRepository>());

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, BlueprintTestUserAuthHandler>(
                        BlueprintTestUserAuthHandler.SchemeName, _ => { });

                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = BlueprintTestUserAuthHandler.SchemeName;
                    opts.DefaultChallengeScheme = BlueprintTestUserAuthHandler.SchemeName;
                });
            });
        });
    }

    private HttpClient AuthenticatedClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-UserId", UserId.ToString());
        return client;
    }

    private HttpClient AnonymousClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    // ── GET /api/blueprints/mine ───────────────────────────────────

    [Fact]
    public async Task GetMine_Unauthenticated_Returns401()
    {
        var response = await AnonymousClient().GetAsync("/api/blueprints/mine");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMine_Authenticated_EmptyList_Returns200WithEmptyArray()
    {
        var response = await AuthenticatedClient().GetAsync("/api/blueprints/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListResponseShape>();
        body!.Blueprints.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMine_Authenticated_WithSeededData_ReturnsBlueprints()
    {
        var blueprintId = Guid.NewGuid();
        _factory.Services.GetRequiredService<FakeUserBlueprintTestRepository>()
            .Seed(UserId, [new MyBlueprintListItemDto(blueprintId, "Widgeteer", "Weapon", null, null, 4)]);

        var response = await AuthenticatedClient().GetAsync("/api/blueprints/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListResponseShape>();
        body!.Blueprints.Should().HaveCount(1);
        body.Blueprints[0].BlueprintId.Should().Be(blueprintId);
        body.Blueprints[0].ProductName.Should().Be("Widgeteer");
        body.Blueprints[0].IngredientCount.Should().Be(4);
    }

    [Fact]
    public async Task GetMine_ResponseIncludesSubtype()
    {
        var blueprintId = Guid.NewGuid();
        _factory.Services.GetRequiredService<FakeUserBlueprintTestRepository>()
            .Seed(UserId, [new MyBlueprintListItemDto(blueprintId, "Widget Mk1", "Weapon", "Pistol", null, 2)]);

        var response = await AuthenticatedClient().GetAsync("/api/blueprints/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListResponseShape>();
        body!.Blueprints.Should().HaveCount(1);
        body.Blueprints[0].Subtype.Should().Be("Pistol");
    }

    // ── GET /api/blueprints/search ─────────────────────────────────

    [Fact]
    public async Task Search_Unauthenticated_Returns401()
    {
        var response = await AnonymousClient().GetAsync("/api/blueprints/search?q=widget");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_EmptyQuery_Returns200WithEmptyResults()
    {
        var response = await AuthenticatedClient().GetAsync("/api/blueprints/search");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchResponseShape>();
        body!.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_WithResults_Returns200WithMatches()
    {
        var blueprintId = Guid.NewGuid();
        _factory.Services.GetRequiredService<FakeSearchableBlueprintRepository>()
            .SearchResults = [new BlueprintSearchResultDto(blueprintId, "Widget Mk1", "Weapon")];

        var response = await AuthenticatedClient().GetAsync("/api/blueprints/search?q=widget");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SearchResponseShape>();
        body!.Results.Should().HaveCount(1);
        body.Results[0].ProductName.Should().Be("Widget Mk1");
    }

    // ── POST /api/blueprints/mine ──────────────────────────────────

    [Fact]
    public async Task AddMine_Unauthenticated_Returns401()
    {
        var response = await AnonymousClient().PostAsync("/api/blueprints/mine",
            Json("""{"blueprintId":"11111111-1111-1111-1111-111111111111"}"""));
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddMine_ValidBlueprint_Returns201()
    {
        var blueprintId = Guid.NewGuid();
        _factory.Services.GetRequiredService<FakeUserBlueprintTestRepository>()
            .AddResult = new MyBlueprintListItemDto(blueprintId, "Widget", "Weapon", null, null, 2);

        var response = await AuthenticatedClient().PostAsync("/api/blueprints/mine",
            Json($$"""{"blueprintId":"{{blueprintId}}"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ItemResponseShape>();
        body!.BlueprintId.Should().Be(blueprintId);
    }

    [Fact]
    public async Task AddMine_DuplicateBlueprint_Returns409()
    {
        var blueprintId = Guid.NewGuid();
        _factory.Services.GetRequiredService<FakeUserBlueprintTestRepository>()
            .ThrowDuplicate = true;

        var response = await AuthenticatedClient().PostAsync("/api/blueprints/mine",
            Json($$"""{"blueprintId":"{{blueprintId}}"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddMine_NotFoundBlueprint_Returns404()
    {
        var blueprintId = Guid.NewGuid();
        _factory.Services.GetRequiredService<FakeUserBlueprintTestRepository>()
            .ThrowNotFound = true;

        var response = await AuthenticatedClient().PostAsync("/api/blueprints/mine",
            Json($$"""{"blueprintId":"{{blueprintId}}"}"""));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/blueprints/mine/{blueprintId} ─────────────────────

    [Fact]
    public async Task GetDetail_Unauthenticated_Returns401()
    {
        var blueprintId = Guid.NewGuid();
        var response = await AnonymousClient().GetAsync($"/api/blueprints/mine/{blueprintId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetDetail_NotInUserList_Returns404()
    {
        var blueprintId = Guid.NewGuid();
        var response = await AuthenticatedClient().GetAsync($"/api/blueprints/mine/{blueprintId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDetail_InUserList_Returns200WithDetailShape()
    {
        var blueprintId = Guid.NewGuid();
        _factory.Services.GetRequiredService<FakeUserBlueprintTestRepository>()
            .DetailResult = new BlueprintDetailDto(blueprintId, "Widget Mk1", "Weapon", 330, 2,
                [new BlueprintSlotDto(0, "Cast Iron", [new BlueprintSlotOptionDto(0, "Iron Ore", "material", 1.5m)])]);

        var response = await AuthenticatedClient().GetAsync($"/api/blueprints/mine/{blueprintId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DetailResponseShape>();
        body!.BlueprintId.Should().Be(blueprintId);
        body.ProductName.Should().Be("Widget Mk1");
        body.CraftTimeSeconds.Should().Be(330);
        body.Slots.Should().HaveCount(1);
    }

    // ── DELETE /api/blueprints/mine/{blueprintId} ──────────────────

    [Fact]
    public async Task RemoveMine_Unauthenticated_Returns401()
    {
        var blueprintId = Guid.NewGuid();
        var response = await AnonymousClient().DeleteAsync($"/api/blueprints/mine/{blueprintId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveMine_NotInUserList_Returns404()
    {
        var blueprintId = Guid.NewGuid();
        var response = await AuthenticatedClient().DeleteAsync($"/api/blueprints/mine/{blueprintId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveMine_InUserList_Returns204()
    {
        var blueprintId = Guid.NewGuid();
        _factory.Services.GetRequiredService<FakeUserBlueprintTestRepository>()
            .RemoveResult = true;

        var response = await AuthenticatedClient().DeleteAsync($"/api/blueprints/mine/{blueprintId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── GET /api/blueprints/org ────────────────────────────────────

    [Fact]
    public async Task GetOrg_Unauthenticated_Returns401()
    {
        var response = await AnonymousClient().GetAsync("/api/blueprints/org");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrg_Authenticated_Returns200WithBlueprintsArray()
    {
        var blueprintId = Guid.NewGuid();
        _factory.Services.GetRequiredService<FakeOrgBlueprintTestRepository>()
            .ListResult = [new OrgBlueprintListItemDto(blueprintId, "Hull Panel", "Component", null, null, 2)];

        var response = await AuthenticatedClient().GetAsync("/api/blueprints/org");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OrgListResponseShape>();
        body!.Blueprints.Should().HaveCount(1);
        body.Blueprints[0].BlueprintId.Should().Be(blueprintId);
        body.Blueprints[0].ProductName.Should().Be("Hull Panel");
    }

    [Fact]
    public async Task GetOrg_ResponseIncludesSubtype()
    {
        var blueprintId = Guid.NewGuid();
        _factory.Services.GetRequiredService<FakeOrgBlueprintTestRepository>()
            .ListResult = [new OrgBlueprintListItemDto(blueprintId, "Hull Panel", "Component", "Armor", null, 2)];

        var response = await AuthenticatedClient().GetAsync("/api/blueprints/org");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OrgListResponseShape>();
        body!.Blueprints.Should().HaveCount(1);
        body.Blueprints[0].Subtype.Should().Be("Armor");
    }

    // ── GET /api/blueprints/org/{blueprintId} ─────────────────────

    [Fact]
    public async Task GetOrgDetail_Unauthenticated_Returns401()
    {
        var blueprintId = Guid.NewGuid();
        var response = await AnonymousClient().GetAsync($"/api/blueprints/org/{blueprintId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOrgDetail_NotInOrgList_Returns404()
    {
        var blueprintId = Guid.NewGuid();
        var response = await AuthenticatedClient().GetAsync($"/api/blueprints/org/{blueprintId}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrgDetail_InOrgList_Returns200WithDetailShape()
    {
        var blueprintId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _factory.Services.GetRequiredService<FakeOrgBlueprintTestRepository>()
            .DetailResult = new OrgBlueprintDetailDto(
                blueprintId, "Widget Mk1", "Weapon", 330, 2,
                [new BlueprintSlotDto(0, "Cast Iron", [new BlueprintSlotOptionDto(0, "Iron Ore", "material", 1.5m)])],
                [new OrgBlueprintOwnerDto(ownerId, "Nashtok")]);

        var response = await AuthenticatedClient().GetAsync($"/api/blueprints/org/{blueprintId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OrgDetailResponseShape>();
        body!.BlueprintId.Should().Be(blueprintId);
        body.ProductName.Should().Be("Widget Mk1");
        body.CraftTimeSeconds.Should().Be(330);
        body.Slots.Should().HaveCount(1);
        body.Owners.Should().HaveCount(1);
        body.Owners[0].DisplayName.Should().Be("Nashtok");
    }

    // ── Response shapes ───────────────────────────────────────────

    private sealed record ItemResponseShape(Guid BlueprintId, string? ProductName, string? Type, string? Subtype, string? Gear, int IngredientCount);
    private sealed record ListResponseShape(List<ItemResponseShape> Blueprints);
    private sealed record SearchItemShape(Guid BlueprintId, string ProductName, string? Type);
    private sealed record SearchResponseShape(List<SearchItemShape> Results);
    private sealed record SlotOptionShape(int OptionIndex, string MaterialName, string Kind, decimal Quantity);
    private sealed record SlotShape(int SlotIndex, string SlotName, List<SlotOptionShape> Options);
    private sealed record DetailResponseShape(Guid BlueprintId, string? ProductName, string? Type, int? CraftTimeSeconds, int IngredientCount, List<SlotShape> Slots);
    private sealed record OwnerShape(Guid UserId, string DisplayName);
    private sealed record OrgItemShape(Guid BlueprintId, string? ProductName, string? Type, string? Subtype, string? Gear, int IngredientCount);
    private sealed record OrgListResponseShape(List<OrgItemShape> Blueprints);
    private sealed record OrgDetailResponseShape(Guid BlueprintId, string? ProductName, string? Type, int? CraftTimeSeconds, int IngredientCount, List<SlotShape> Slots, List<OwnerShape> Owners);
}

// ── Fakes ────────────────────────────────────────────────────────

internal sealed class FakeUserBlueprintTestRepository : IUserBlueprintRepository
{
    private readonly Dictionary<Guid, List<MyBlueprintListItemDto>> _data = new();
    public MyBlueprintListItemDto? AddResult { get; set; }
    public bool ThrowDuplicate { get; set; }
    public bool ThrowNotFound { get; set; }
    public BlueprintDetailDto? DetailResult { get; set; }
    public bool RemoveResult { get; set; }

    public void Seed(Guid userId, IEnumerable<MyBlueprintListItemDto> items)
    {
        _data[userId] = items.ToList();
    }

    public Task<IReadOnlyList<MyBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default)
    {
        var result = _data.TryGetValue(userId, out var list)
            ? (IReadOnlyList<MyBlueprintListItemDto>)list
            : [];
        return Task.FromResult(result);
    }

    public Task<MyBlueprintListItemDto> AddAsync(Guid userId, Guid blueprintId, CancellationToken ct = default)
    {
        if (ThrowNotFound) throw new BlueprintNotFoundException(blueprintId);
        if (ThrowDuplicate) throw new DuplicateBlueprintException(blueprintId);
        return Task.FromResult(AddResult ?? new MyBlueprintListItemDto(blueprintId, null, null, null, 0));
    }

    public Task<BlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
        Task.FromResult(DetailResult);

    public Task<bool> RemoveAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
        Task.FromResult(RemoveResult);
}

internal sealed class FakeSearchableBlueprintRepository : IBlueprintRepository
{
    public IReadOnlyList<BlueprintSearchResultDto> SearchResults { get; set; } = [];

    public Task<IReadOnlyDictionary<string, NajaEcho.Application.Abstractions.MaterialMatch>> ResolveMaterialsAsync(
        IReadOnlyCollection<string> names, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, NajaEcho.Application.Abstractions.MaterialMatch>>(
            new Dictionary<string, NajaEcho.Application.Abstractions.MaterialMatch>());

    public Task<NajaEcho.Application.Abstractions.BlueprintImportCounts> ImportAsync(
        NajaEcho.Application.Features.Blueprints.ImportBlueprints.ParsedBlueprintDataset dataset,
        CancellationToken ct = default) =>
        Task.FromResult(new NajaEcho.Application.Abstractions.BlueprintImportCounts(0, 0));

    public Task<IReadOnlyList<NajaEcho.Application.Features.Blueprints.GetBlueprints.BlueprintListItemDto>> GetListAsync(
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<NajaEcho.Application.Features.Blueprints.GetBlueprints.BlueprintListItemDto>>([]);

    public Task<IReadOnlyList<BlueprintSearchResultDto>> SearchAsync(string term, int limit = 20, CancellationToken ct = default) =>
        Task.FromResult(SearchResults);
}

internal sealed class FakeOrgBlueprintTestRepository : IOrgBlueprintRepository
{
    public IReadOnlyList<OrgBlueprintListItemDto> ListResult { get; set; } = [];
    public OrgBlueprintDetailDto? DetailResult { get; set; }

    public Task<IReadOnlyList<OrgBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(ListResult);

    public Task<OrgBlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
        Task.FromResult(DetailResult);
}

internal sealed class FakeBlueprintTestLoginService : IExternalLoginService
{
    public Task<LocalUser> FindOrCreateAsync(NajaEcho.Domain.Users.DiscordProfile profile, CancellationToken ct = default) =>
        Task.FromResult(new LocalUser(Guid.NewGuid(), profile.DisplayName, profile.Username));

    public Task<LocalUser?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult<LocalUser?>(new LocalUser(userId, "Test", "test"));
}

internal sealed class BlueprintTestUserAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "BlueprintUserTestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-UserId", out var userIdValue) ||
            !Guid.TryParse(userIdValue, out var userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, "Test User"),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
