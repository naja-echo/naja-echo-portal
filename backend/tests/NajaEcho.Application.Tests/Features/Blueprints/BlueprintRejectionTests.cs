using FluentAssertions;
using NajaEcho.Application.Features.Blueprints.ImportBlueprints;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Blueprints;

public class BlueprintRejectionTests
{
    private const string Guid1 = "11111111-1111-1111-1111-111111111111";
    private const string Pec = "22222222-2222-2222-2222-222222222222";

    private static string DatasetWith(string blueprintsJson, int metaTotal = 1) => $$"""
    {
      "version": "1.0.0",
      "meta": { "totalBlueprints": {{metaTotal}}, "totalProducts": 1, "totalResources": 0, "totalItems": 0 },
      "dismantle": { "efficiency": 1, "dismantleTimeSeconds": 1, "blacklistedResources": [], "blacklistedEntityClasses": [] },
      "properties": {},
      "resources": [],
      "items": [],
      "blueprints": [ {{blueprintsJson}} ]
    }
    """;

    private static string Blueprint(
        string guid = Guid1, string tag = "\"BP\"", string pec = Pec, string gear = "\"Gear\"",
        string tiers = """[ { "craftTimeSeconds": 1, "slots": [ { "name": "S", "options": [ { "type": "resource", "quantity": 1, "minQuality": 0, "resourceName": "Steel" } ], "modifiers": null } ] } ]""")
        => $$"""
        { "guid": "{{guid}}", "tag": {{tag}}, "productEntityClass": "{{pec}}", "gear": {{gear}},
          "type": null, "subtype": null, "productName": "P", "manufacturer": null, "tiers": {{tiers}} }
        """;

    [Fact]
    public void Parse_InvalidGuid_RejectsWithReason()
    {
        var json = DatasetWith(Blueprint(guid: "not-a-uuid"));
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(json));

        result.Blueprints.Should().BeEmpty();
        result.Rejections.Should().ContainSingle();
        result.Rejections[0].Reason.Should().Contain("guid");
    }

    [Fact]
    public void Parse_MissingTag_RejectsWithIdentifyingValue()
    {
        var json = DatasetWith(Blueprint(tag: "\"\""));
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(json));

        result.Rejections.Should().ContainSingle();
        result.Rejections[0].Reason.Should().Contain("tag");
        result.Rejections[0].Guid.Should().Be(Guid1);
        result.Rejections[0].ProductName.Should().Be("P");
    }

    [Fact]
    public void Parse_OptionMatchingNeitherVariant_Rejects()
    {
        var tiers = """[ { "craftTimeSeconds": 1, "slots": [ { "name": "S", "options": [ { "type": "bogus", "quantity": 1, "minQuality": 0 } ], "modifiers": null } ] } ]""";
        var json = DatasetWith(Blueprint(tiers: tiers));
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(json));

        result.Blueprints.Should().BeEmpty();
        result.Rejections.Should().ContainSingle();
        result.Rejections[0].Reason.Should().Contain("neither");
    }

    [Fact]
    public void Parse_DuplicateGuid_FirstWinsRestRejected()
    {
        var json = DatasetWith($"{Blueprint()}, {Blueprint()}", metaTotal: 2);
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(json));

        result.Blueprints.Should().ContainSingle();
        result.Rejections.Should().ContainSingle();
        result.Rejections[0].Reason.Should().Contain("Duplicate");
    }

    [Fact]
    public void Parse_MalformedFirstOccurrence_DoesNotBlockValidDuplicateWithSameGuid()
    {
        var bad = Blueprint(tag: "\"\""); // same guid, missing tag → rejected
        var good = Blueprint();           // same guid, valid → should still be stored
        var json = DatasetWith($"{bad}, {good}", metaTotal: 2);
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(json));

        result.Blueprints.Should().ContainSingle(b => b.Entity.Id == System.Guid.Parse(Guid1));
        result.Rejections.Should().ContainSingle();
        result.Rejections[0].Reason.Should().Contain("tag");
    }

    [Fact]
    public void Parse_MixedValidAndInvalid_KeepsValidAndReportsInvalid()
    {
        var good = Blueprint(guid: "33333333-3333-3333-3333-333333333333");
        var bad = Blueprint(guid: "bad");
        var json = DatasetWith($"{good}, {bad}", metaTotal: 2);
        var result = BlueprintParser.Parse(BlueprintFixtures.Doc(json));

        result.Blueprints.Should().ContainSingle();
        result.Rejections.Should().ContainSingle();
        result.BlueprintsRead.Should().Be(2);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("meta")]
    [InlineData("dismantle")]
    [InlineData("properties")]
    [InlineData("resources")]
    [InlineData("items")]
    [InlineData("blueprints")]
    public void Parse_MissingTopLevelSection_Throws(string section)
    {
        var full = DatasetWith(Blueprint());
        var root = System.Text.Json.JsonDocument.Parse(full).RootElement;

        // Rebuild the document without the target section.
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name == section)
                {
                    continue;
                }

                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        var trimmed = System.Text.Json.JsonDocument.Parse(stream.ToArray()).RootElement;

        var act = () => BlueprintParser.Parse(trimmed);
        act.Should().Throw<InvalidBlueprintDocumentException>();
    }
}
