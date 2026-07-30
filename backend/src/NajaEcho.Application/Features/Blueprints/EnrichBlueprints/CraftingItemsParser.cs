using System.Text.Json;

namespace NajaEcho.Application.Features.Blueprints.EnrichBlueprints;

public static class CraftingItemsParser
{
    private static readonly IReadOnlyDictionary<int, string> GradeMap = new Dictionary<int, string>
    {
        [1] = "A",
        [2] = "B",
        [3] = "C",
        [4] = "D",
    };

    public static IReadOnlyList<ParsedItemAttributes> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidCraftingItemsDocumentException("The uploaded document must be a JSON object.");

        if (!root.TryGetProperty("items", out var itemsEl) || itemsEl.ValueKind != JsonValueKind.Array)
            throw new InvalidCraftingItemsDocumentException("Missing or invalid required section 'items'.");

        var results = new List<ParsedItemAttributes>();

        foreach (var el in itemsEl.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
                continue;

            if (!el.TryGetProperty("entityClass", out var ecEl) ||
                ecEl.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(ecEl.GetString(), out var entityClass))
            {
                continue;
            }

            string? componentClass = null;
            if (el.TryGetProperty("componentClass", out var ccEl) && ccEl.ValueKind == JsonValueKind.String)
                componentClass = ccEl.GetString();

            short? componentSize = null;
            if (el.TryGetProperty("size", out var sizeEl) && sizeEl.ValueKind == JsonValueKind.Number &&
                sizeEl.TryGetInt16(out var size))
            {
                componentSize = size;
            }

            string? componentGrade = null;
            if (el.TryGetProperty("grade", out var gradeEl) && gradeEl.ValueKind == JsonValueKind.Number &&
                gradeEl.TryGetInt32(out var gradeInt))
            {
                componentGrade = GradeMap.TryGetValue(gradeInt, out var letter) ? letter : gradeInt.ToString();
            }

            results.Add(new ParsedItemAttributes(entityClass, componentClass, componentSize, componentGrade));
        }

        return results;
    }
}
