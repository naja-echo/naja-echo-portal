using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NajaEcho.Application.Features.Blueprints.ImportBlueprints;
using NajaEcho.Domain.Blueprints;
using NajaEcho.Domain.Items;
using NajaEcho.Infrastructure.Blueprints;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Blueprints;

[Collection(PostgresCollection.Name)]
public sealed class BlueprintRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private AppDbContext _db = null!;

    public BlueprintRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _db = _fixture.CreateContext();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private BlueprintRepository MakeRepo() => new(_db);

    private static AppDbContext NewContext(string connectionString)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(opts, new StubOrganizationContext());
    }

    // Each import runs in its own context, mirroring the per-request scoped DbContext in production.
    private async Task<Application.Abstractions.BlueprintImportCounts> ImportFresh(ParsedBlueprintDataset dataset)
    {
        await using var ctx = NewContext(_fixture.ConnectionString);
        return await new BlueprintRepository(ctx).ImportAsync(dataset);
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────

    private const string Guid1 = "11111111-1111-1111-1111-111111111111";
    private const string Guid2 = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string Pec = "22222222-2222-2222-2222-222222222222";

    private static ParsedBlueprintDataset Dataset(string json) =>
        BlueprintParser.Parse(JsonDocument.Parse(json).RootElement);

    private static string DatasetJson(
        string guid = Guid1,
        int craftTime = 120,
        string resources = "\"Steel\", \"Titanium\"",
        string version = "1.4.0")
        => $$"""
        {
          "version": "{{version}}",
          "meta": { "totalBlueprints": 1, "totalProducts": 1, "totalResources": 2, "totalItems": 1 },
          "dismantle": { "efficiency": 0.5, "dismantleTimeSeconds": 60, "blacklistedResources": [], "blacklistedEntityClasses": [] },
          "properties": { "health": { "name": "Health", "unit": null, "category": "Defense" } },
          "resources": [{{resources}}],
          "items": ["Basic Frame"],
          "blueprints": [
            {
              "guid": "{{guid}}", "tag": "BP", "productEntityClass": "{{Pec}}", "gear": "Weapon",
              "type": null, "subtype": null, "productName": "Widget", "manufacturer": "ACME",
              "tiers": [ { "craftTimeSeconds": {{craftTime}}, "slots": [
                { "name": "Frame", "options": [
                    { "type": "resource", "quantity": 12.5, "minQuality": 100, "resourceName": "Steel" },
                    { "type": "item", "quantity": 1, "minQuality": 0, "modifiers": null, "itemName": "Basic Frame" }
                  ], "modifiers": null } ] } ]
            }
          ]
        }
        """;

    private Item AddItem(string name, string uuid, int uexId, bool softDeleted = false)
    {
        var i = new Item
        {
            Id = Guid.NewGuid(),
            Uuid = uuid,
            UexId = uexId,
            IdCategory = 1,
            Name = name,
            Status = softDeleted ? ItemStatus.SoftDeleted : ItemStatus.Active,
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            SoftDeletedAt = softDeleted ? DateTimeOffset.UtcNow : null,
        };
        _db.Items.Add(i);
        return i;
    }

    // ── Material resolution (FR-009, Decision 11) — sc.items only, by uex_id ──

    [Fact]
    public async Task ResolveMaterials_IsCaseInsensitive()
    {
        AddItem("Titanium", "i-ti", 10);
        await _db.SaveChangesAsync();

        var map = await MakeRepo().ResolveMaterialsAsync(["TITANIUM"]);

        map.Should().ContainKey("titanium");
        map["titanium"].UexId.Should().Be(10);
    }

    [Fact]
    public async Task ResolveMaterials_ExcludesSoftDeletedAndPicksLowestUexId()
    {
        AddItem("Gold", "i-gold-soft", 1, softDeleted: true);
        AddItem("Gold", "i-gold-5", 5);
        AddItem("Gold", "i-gold-2", 2);
        await _db.SaveChangesAsync();

        var map = await MakeRepo().ResolveMaterialsAsync(["Gold"]);

        map["gold"].UexId.Should().Be(2);
    }

    [Fact]
    public async Task ResolveMaterials_UnmatchedName_Absent()
    {
        var map = await MakeRepo().ResolveMaterialsAsync(["Nonexistium"]);
        map.Should().NotContainKey("nonexistium");
    }

    // ── Import persistence (FR-005/008/009a/009b) ────────────────────────────

    [Fact]
    public async Task Import_PersistsAllSixTablesAndDerivedRows()
    {
        AddItem("Steel", "i-steel", 10);
        await _db.SaveChangesAsync();

        var repo = MakeRepo();
        var dataset = Dataset(DatasetJson());
        var map = await repo.ResolveMaterialsAsync(["Steel", "Titanium", "Basic Frame"]);
        ApplyMap(dataset, map);

        var counts = await repo.ImportAsync(dataset);
        counts.BlueprintsInserted.Should().Be(1);

        using var verify = NewContext(_fixture.ConnectionString);
        (await verify.Blueprints.CountAsync()).Should().Be(1);
        (await verify.BlueprintTiers.CountAsync()).Should().Be(1);
        (await verify.BlueprintSlotOptions.CountAsync()).Should().Be(2);
        (await verify.CraftingMaterials.CountAsync()).Should().Be(3);
        (await verify.CraftingProperties.CountAsync()).Should().Be(1);
        (await verify.CraftingDatasets.CountAsync()).Should().Be(1);

        var steelOption = await verify.BlueprintSlotOptions.SingleAsync(o => o.MaterialName == "Steel");
        steelOption.MatchedUexId.Should().Be(10);
        steelOption.Quantity.Should().Be(12.5m);
        steelOption.MinQuality.Should().Be(100);
    }

    // ── Re-import upsert (FR-013/014/016, SC-004) ────────────────────────────

    [Fact]
    public async Task Reimport_UpsertsByGuidAndRebuildsDerivedRowsWithoutOrphans()
    {
        var first = Dataset(DatasetJson(craftTime: 120));
        await ImportFresh(first);

        // Second file: same guid with changed craft time + one new blueprint + extra resource.
        var second = Dataset($$"""
        {
          "version": "1.5.0",
          "meta": { "totalBlueprints": 2, "totalProducts": 2, "totalResources": 3, "totalItems": 1 },
          "dismantle": { "efficiency": 0.9, "dismantleTimeSeconds": 90, "blacklistedResources": [], "blacklistedEntityClasses": [] },
          "properties": {},
          "resources": ["Steel", "Titanium", "Gold"],
          "items": ["Basic Frame"],
          "blueprints": [
            {
              "guid": "{{Guid1}}", "tag": "BP", "productEntityClass": "{{Pec}}", "gear": "Weapon",
              "type": null, "subtype": null, "productName": "Widget", "manufacturer": "ACME",
              "tiers": [ { "craftTimeSeconds": 500, "slots": [
                { "name": "Frame", "options": [ { "type": "resource", "quantity": 1, "minQuality": 0, "resourceName": "Gold" } ], "modifiers": null } ] } ]
            },
            {
              "guid": "{{Guid2}}", "tag": "BP2", "productEntityClass": "{{Pec}}", "gear": "Armor",
              "type": null, "subtype": null, "productName": "Gadget", "manufacturer": "ACME",
              "tiers": [ { "craftTimeSeconds": 10, "slots": [ { "name": "Core", "options": [ { "type": "resource", "quantity": 2, "minQuality": 0, "resourceName": "Steel" } ], "modifiers": null } ] } ]
            }
          ]
        }
        """);

        var counts = await ImportFresh(second);
        counts.BlueprintsInserted.Should().Be(1);
        counts.BlueprintsUpdated.Should().Be(1);

        using var verify = NewContext(_fixture.ConnectionString);
        (await verify.Blueprints.CountAsync()).Should().Be(2);

        // The updated blueprint reflects only file B (500s craft time, single Gold option) — no stale rows.
        var updatedTiers = await verify.BlueprintTiers.Where(t => t.BlueprintId == Guid.Parse(Guid1)).ToListAsync();
        updatedTiers.Should().ContainSingle().Which.CraftTimeSeconds.Should().Be(500);

        var updatedOptions = await verify.BlueprintSlotOptions
            .Where(o => o.TierId == updatedTiers[0].Id).ToListAsync();
        updatedOptions.Should().ContainSingle().Which.MaterialName.Should().Be("Gold");

        // No orphaned slot options remain from the first import (which had 2 options).
        (await verify.BlueprintSlotOptions.CountAsync()).Should().Be(2); // 1 (updated BP) + 1 (new BP)

        // Reference data refreshed to file B.
        (await verify.CraftingMaterials.CountAsync(m => m.Kind == CraftingMaterialKind.Resource)).Should().Be(3);
        (await verify.CraftingDatasets.SingleAsync()).Version.Should().Be("1.5.0");
    }

    // ── Atomic rollback (FR-021a, SC-010) ────────────────────────────────────

    [Fact]
    public async Task Import_FailureMidTransaction_RollsBackLeavingPriorDataIntact()
    {
        await ImportFresh(Dataset(DatasetJson()));

        int priorBlueprintCount;
        int priorMaterialCount;
        using (var pre = NewContext(_fixture.ConnectionString))
        {
            priorBlueprintCount = await pre.Blueprints.CountAsync();
            priorMaterialCount = await pre.CraftingMaterials.CountAsync();
        }

        // A version longer than varchar(64) forces the dataset insert to fail after reference deletes.
        var oversizedVersion = new string('9', 100);
        var failing = Dataset(DatasetJson(guid: Guid2, version: oversizedVersion));

        var act = () => ImportFresh(failing);
        await act.Should().ThrowAsync<Exception>();

        using var verify = NewContext(_fixture.ConnectionString);
        (await verify.Blueprints.CountAsync()).Should().Be(priorBlueprintCount);
        (await verify.CraftingMaterials.CountAsync()).Should().Be(priorMaterialCount);
        (await verify.Blueprints.AnyAsync(b => b.Id == Guid.Parse(Guid2))).Should().BeFalse();
    }

    // ── Listing / display-name join (FR-015/022, SC-006) ─────────────────────

    [Fact]
    public async Task GetList_ComputesDisplayNameFallbackChain()
    {
        // Linked item for a blueprint whose product name is null.
        var linkedGuid = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
        AddItem("Linked Item Name", linkedGuid, 99);
        // Soft-deleted item with the SAME uuid must be ignored.
        AddItem("Deleted Name", linkedGuid, 100, softDeleted: true);
        await _db.SaveChangesAsync();

        await SeedBlueprint(Guid1, productName: "Explicit Product", tag: "TAG1");
        await SeedBlueprint(linkedGuid, productName: null, tag: "TAG2");
        await SeedBlueprint(Guid2, productName: null, tag: "OnlyTag");

        var list = await MakeRepo().GetListAsync();

        list.Single(x => x.Guid == Guid.Parse(Guid1)).NameSource.Should().Be("productName");
        var linked = list.Single(x => x.Guid == Guid.Parse(linkedGuid));
        linked.NameSource.Should().Be("linkedItem");
        linked.DisplayName.Should().Be("Linked Item Name");
        list.Single(x => x.Guid == Guid.Parse(Guid2)).NameSource.Should().Be("tag");

        // Ordered by display name (case-insensitive).
        list.Select(x => x.DisplayName).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    private async Task SeedBlueprint(string guid, string? productName, string tag)
    {
        _db.Blueprints.Add(new CraftingBlueprint
        {
            Id = Guid.Parse(guid),
            Tag = tag,
            ProductEntityClass = Guid.Parse(Pec),
            Gear = "Gear",
            ProductName = productName,
            Manufacturer = "ACME",
            Tiers = JsonDocument.Parse("[]"),
            ImportedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    private static void ApplyMap(ParsedBlueprintDataset dataset, IReadOnlyDictionary<string, NajaEcho.Application.Abstractions.MaterialMatch> map)
    {
        foreach (var m in dataset.Materials)
        {
            if (map.TryGetValue(m.Name.ToLowerInvariant(), out var match))
            {
                m.MatchedUexId = match.UexId;
            }
        }

        foreach (var option in dataset.Blueprints.SelectMany(b => b.Tiers).SelectMany(t => t.Options))
        {
            if (map.TryGetValue(option.MaterialName.ToLowerInvariant(), out var match))
            {
                option.MatchedUexId = match.UexId;
            }
        }
    }
}
