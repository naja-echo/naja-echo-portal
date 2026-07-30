using FluentAssertions;
using NajaEcho.Application.Features.Blueprints.EnrichBlueprints;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Blueprints;

public class CraftingItemsParserTests
{
    private static System.Text.Json.JsonElement Doc(string json) =>
        System.Text.Json.JsonDocument.Parse(json).RootElement;

    // ── Root validation ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_RootIsNotObject_Throws()
    {
        var act = () => CraftingItemsParser.Parse(Doc("[1,2,3]"));
        act.Should().Throw<InvalidCraftingItemsDocumentException>();
    }

    [Fact]
    public void Parse_MissingItemsKey_Throws()
    {
        var act = () => CraftingItemsParser.Parse(Doc("""{ "version": "1.0" }"""));
        act.Should().Throw<InvalidCraftingItemsDocumentException>();
    }

    [Fact]
    public void Parse_ItemsIsNotArray_Throws()
    {
        var act = () => CraftingItemsParser.Parse(Doc("""{ "items": {} }"""));
        act.Should().Throw<InvalidCraftingItemsDocumentException>();
    }

    // ── Empty / skip cases ───────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyItemsArray_ReturnsEmptyList()
    {
        var result = CraftingItemsParser.Parse(Doc("""{ "items": [] }"""));
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ItemMissingEntityClass_IsSkipped()
    {
        var result = CraftingItemsParser.Parse(Doc("""
            { "items": [ { "size": 1, "grade": 1, "componentClass": "Military" } ] }
            """));
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ItemEntityClassNotString_IsSkipped()
    {
        var result = CraftingItemsParser.Parse(Doc("""
            { "items": [ { "entityClass": 42 } ] }
            """));
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ItemEntityClassInvalidGuid_IsSkipped()
    {
        var result = CraftingItemsParser.Parse(Doc("""
            { "items": [ { "entityClass": "not-a-guid" } ] }
            """));
        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NonObjectElement_IsSkipped()
    {
        var result = CraftingItemsParser.Parse(Doc("""
            { "items": [ "just-a-string" ] }
            """));
        result.Should().BeEmpty();
    }

    // ── Valid parsing ────────────────────────────────────────────────────────

    private const string ValidEntityClass = "39424912-de99-46d1-87a6-0f59696ca60b";

    [Fact]
    public void Parse_FullyPopulatedItem_ReturnsCorrectAttributes()
    {
        var result = CraftingItemsParser.Parse(Doc($$"""
            { "items": [ {
                "entityClass": "{{ValidEntityClass}}",
                "componentClass": "Civilian",
                "size": 2,
                "grade": 2
            } ] }
            """));

        result.Should().HaveCount(1);
        result[0].EntityClass.Should().Be(Guid.Parse(ValidEntityClass));
        result[0].ComponentClass.Should().Be("Civilian");
        result[0].ComponentSize.Should().Be(2);
        result[0].ComponentGrade.Should().Be("B");
    }

    [Fact]
    public void Parse_ItemWithoutComponentClass_NullClass()
    {
        var result = CraftingItemsParser.Parse(Doc($$"""
            { "items": [ { "entityClass": "{{ValidEntityClass}}", "size": 1, "grade": 1 } ] }
            """));

        result[0].ComponentClass.Should().BeNull();
    }

    [Fact]
    public void Parse_ItemWithoutSize_NullSize()
    {
        var result = CraftingItemsParser.Parse(Doc($$"""
            { "items": [ { "entityClass": "{{ValidEntityClass}}", "grade": 1 } ] }
            """));

        result[0].ComponentSize.Should().BeNull();
    }

    [Fact]
    public void Parse_ItemWithoutGrade_NullGrade()
    {
        var result = CraftingItemsParser.Parse(Doc($$"""
            { "items": [ { "entityClass": "{{ValidEntityClass}}", "size": 3 } ] }
            """));

        result[0].ComponentGrade.Should().BeNull();
    }

    [Fact]
    public void Parse_OnlyEntityClass_AllOptionalFieldsNull()
    {
        var result = CraftingItemsParser.Parse(Doc($$"""
            { "items": [ { "entityClass": "{{ValidEntityClass}}" } ] }
            """));

        result.Should().HaveCount(1);
        result[0].ComponentClass.Should().BeNull();
        result[0].ComponentSize.Should().BeNull();
        result[0].ComponentGrade.Should().BeNull();
    }

    // ── Grade mapping ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, "A")]
    [InlineData(2, "B")]
    [InlineData(3, "C")]
    [InlineData(4, "D")]
    public void Parse_GradeInt_MapsToExpectedLetter(int gradeInt, string expectedLetter)
    {
        var result = CraftingItemsParser.Parse(Doc($$"""
            { "items": [ { "entityClass": "{{ValidEntityClass}}", "grade": {{gradeInt}} } ] }
            """));

        result[0].ComponentGrade.Should().Be(expectedLetter);
    }

    [Fact]
    public void Parse_UnknownGrade_FallsBackToIntString()
    {
        var result = CraftingItemsParser.Parse(Doc($$"""
            { "items": [ { "entityClass": "{{ValidEntityClass}}", "grade": 99 } ] }
            """));

        result[0].ComponentGrade.Should().Be("99");
    }

    // ── Mixed valid + skipped items ──────────────────────────────────────────

    [Fact]
    public void Parse_MixedValidAndSkippedItems_ReturnsOnlyValid()
    {
        const string validId2 = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        var result = CraftingItemsParser.Parse(Doc($$"""
            { "items": [
                { "entityClass": "{{ValidEntityClass}}", "size": 1, "grade": 1 },
                { "size": 2 },
                "string-item",
                { "entityClass": "not-a-guid" },
                { "entityClass": "{{validId2}}", "size": 2, "grade": 3 }
            ] }
            """));

        result.Should().HaveCount(2);
        result[0].EntityClass.Should().Be(Guid.Parse(ValidEntityClass));
        result[1].EntityClass.Should().Be(Guid.Parse(validId2));
        result[1].ComponentGrade.Should().Be("C");
    }
}
