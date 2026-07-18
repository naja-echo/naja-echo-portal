using System.Text.Json;

namespace NajaEcho.Application.Tests.Features.Blueprints;

/// <summary>Sample blueprint dataset JSON builders shared across the parser/handler unit tests.</summary>
internal static class BlueprintFixtures
{
    public const string BlueprintGuid = "11111111-1111-1111-1111-111111111111";
    public const string ProductEntityClass = "22222222-2222-2222-2222-222222222222";

    public static JsonElement Doc(string json) => JsonDocument.Parse(json).RootElement;

    /// <summary>A minimal valid single-blueprint dataset.</summary>
    public static string ValidDataset(
        string guid = BlueprintGuid,
        string? productName = "Widget",
        string? modifiers = "null",
        int metaTotalBlueprints = 1,
        int metaTotalResources = 2,
        int metaTotalItems = 1)
    {
        var productNameJson = productName is null ? "null" : $"\"{productName}\"";
        return $$"""
        {
          "version": "1.4.0",
          "meta": { "totalBlueprints": {{metaTotalBlueprints}}, "totalProducts": 1, "totalResources": {{metaTotalResources}}, "totalItems": {{metaTotalItems}} },
          "dismantle": { "efficiency": 0.5, "dismantleTimeSeconds": 60, "blacklistedResources": [], "blacklistedEntityClasses": [] },
          "properties": { "health": { "name": "Health", "unit": null, "category": "Defense", "nameOverrides": { "en": "HP" } } },
          "resources": ["Steel", "Titanium"],
          "items": ["Basic Frame"],
          "blueprints": [
            {
              "guid": "{{guid}}",
              "tag": "BP_Widget",
              "productEntityClass": "{{ProductEntityClass}}",
              "gear": "Weapon",
              "type": null,
              "subtype": null,
              "productName": {{productNameJson}},
              "manufacturer": "ACME",
              "tiers": [
                {
                  "craftTimeSeconds": 120,
                  "slots": [
                    {
                      "name": "Frame",
                      "options": [
                        { "type": "resource", "quantity": 12.5, "minQuality": 100, "resourceName": "Steel" },
                        { "type": "item", "quantity": 1, "minQuality": 0, "modifiers": null, "itemName": "Basic Frame" }
                      ],
                      "modifiers": {{modifiers}}
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;
    }

    public const string PopulatedModifiers =
        """[ { "startQuality": 0, "endQuality": 1000, "modifierAtStart": 0.8, "modifierAtEnd": 1.2, "propertyName": "Health", "propertyKey": "health", "additive": false } ]""";
}
