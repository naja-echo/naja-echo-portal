using System.Text.Json;
using FluentAssertions;
using NajaEcho.Application.Features.Blueprints.ImportBlueprints;
using NajaEcho.Domain.Blueprints;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Blueprints;

public class BlueprintParserTests
{
    [Fact]
    public void Parse_ValidDataset_ReadsReferenceDataAndBlueprint()
    {
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(BlueprintFixtures.ValidDataset()));

        result.Version.Should().Be("1.4.0");
        result.Properties.Should().ContainSingle(p => p.Key == "health" && p.Category == "Defense");
        result.ResourceNames.Should().BeEquivalentTo(["Steel", "Titanium"]);
        result.ItemNames.Should().BeEquivalentTo(["Basic Frame"]);
        result.Blueprints.Should().ContainSingle();
        result.Rejections.Should().BeEmpty();
        result.BlueprintsRead.Should().Be(1);
        result.Dataset.Efficiency.Should().Be(0.5);
        result.Dataset.DismantleTimeSeconds.Should().Be(60);
    }

    [Fact]
    public void Parse_BuildsMaterialsForResourcesAndItems()
    {
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(BlueprintFixtures.ValidDataset()));

        result.Materials.Should().Contain(m => m.Kind == CraftingMaterialKind.Resource && m.Name == "Steel");
        result.Materials.Should().Contain(m => m.Kind == CraftingMaterialKind.Resource && m.Name == "Titanium");
        result.Materials.Should().Contain(m => m.Kind == CraftingMaterialKind.Item && m.Name == "Basic Frame");
    }

    [Fact]
    public void Parse_DerivesTierAndSlotOptionRows()
    {
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(BlueprintFixtures.ValidDataset()));

        var bp = result.Blueprints.Single();
        bp.Entity.Id.Should().Be(Guid.Parse(BlueprintFixtures.BlueprintGuid));
        bp.Tiers.Should().ContainSingle();

        var tier = bp.Tiers.Single();
        tier.TierIndex.Should().Be(0);
        tier.CraftTimeSeconds.Should().Be(120);
        tier.Options.Should().HaveCount(2);

        var resourceOption = tier.Options.Single(o => o.Kind == CraftingMaterialKind.Resource);
        resourceOption.MaterialName.Should().Be("Steel");
        resourceOption.Quantity.Should().Be(12.5m);
        resourceOption.MinQuality.Should().Be(100);
        resourceOption.SlotName.Should().Be("Frame");
        resourceOption.SlotIndex.Should().Be(0);
        resourceOption.OptionIndex.Should().Be(0);

        var itemOption = tier.Options.Single(o => o.Kind == CraftingMaterialKind.Item);
        itemOption.MaterialName.Should().Be("Basic Frame");
        itemOption.OptionIndex.Should().Be(1);
    }

    [Fact]
    public void Parse_NullModifiers_StoredAsEmptyArrayInTiersJson()
    {
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(BlueprintFixtures.ValidDataset(modifiers: "null")));

        var tiers = result.Blueprints.Single().Entity.Tiers.RootElement;
        var slot = tiers[0].GetProperty("slots")[0];
        slot.GetProperty("modifiers").ValueKind.Should().Be(JsonValueKind.Array);
        slot.GetProperty("modifiers").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public void Parse_PopulatedModifiers_PreservedInTiersJson()
    {
        var result = BlueprintParser.Parse(
            BlueprintFixtures.Doc(BlueprintFixtures.ValidDataset(modifiers: BlueprintFixtures.PopulatedModifiers)));

        var tiers = result.Blueprints.Single().Entity.Tiers.RootElement;
        var modifiers = tiers[0].GetProperty("slots")[0].GetProperty("modifiers");
        modifiers.GetArrayLength().Should().Be(1);
        modifiers[0].GetProperty("propertyKey").GetString().Should().Be("health");
        modifiers[0].GetProperty("additive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Parse_OptionalFields_PreservedWhenPresentAndUnsetWhenAbsent()
    {
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(BlueprintFixtures.ValidDataset()));

        var entity = result.Blueprints.Single().Entity;
        entity.ProductName.Should().Be("Widget");
        entity.Manufacturer.Should().Be("ACME");
        entity.Type.Should().BeNull();
        entity.IsDefault.Should().BeNull();
        entity.SuggestedName.Should().BeNull();
        entity.CigDataError.Should().BeNull();
    }

    [Fact]
    public void Parse_ItemOption_OmitsModifiersKeyInTiersJson()
    {
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(BlueprintFixtures.ValidDataset()));

        var options = result.Blueprints.Single().Entity.Tiers.RootElement[0].GetProperty("slots")[0].GetProperty("options");
        var itemOption = options.EnumerateArray().Single(o => o.GetProperty("type").GetString() == "item");
        itemOption.TryGetProperty("modifiers", out _).Should().BeFalse();
        itemOption.GetProperty("itemName").GetString().Should().Be("Basic Frame");
    }
}
