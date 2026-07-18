using System.Text.Json;
using System.Text.Json.Serialization;
using NajaEcho.Domain.Blueprints;

namespace NajaEcho.Application.Features.Blueprints.ImportBlueprints;

/// <summary>
/// Pure, database-free validation + normalization of an uploaded blueprint dataset. Top-level
/// invalidity throws <see cref="InvalidBlueprintDocumentException"/> (→ 400); malformed individual
/// blueprints are collected as rejections and never abort the import (FR-017, FR-021a).
/// </summary>
public static class BlueprintParser
{
    private static readonly JsonSerializerOptions TiersJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ParsedBlueprintDataset Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidBlueprintDocumentException("The uploaded document must be a JSON object.");
        }

        var version = RequireString(root, "version");
        var meta = RequireObject(root, "meta");
        var dismantle = RequireObject(root, "dismantle");
        var propertiesEl = RequireObject(root, "properties");
        var resourcesEl = RequireArray(root, "resources");
        var itemsEl = RequireArray(root, "items");
        var blueprintsEl = RequireArray(root, "blueprints");

        var dataset = new CraftingDataset
        {
            Id = Guid.NewGuid(),
            Version = version,
            TotalBlueprints = RequireInt(meta, "totalBlueprints"),
            TotalProducts = RequireInt(meta, "totalProducts"),
            TotalResources = RequireInt(meta, "totalResources"),
            TotalItems = RequireInt(meta, "totalItems"),
            Efficiency = RequireNumber(dismantle, "efficiency"),
            DismantleTimeSeconds = RequireInt(dismantle, "dismantleTimeSeconds"),
            BlacklistedResources = CloneArray(dismantle, "blacklistedResources"),
            BlacklistedEntityClasses = CloneArray(dismantle, "blacklistedEntityClasses"),
        };

        var properties = ParseProperties(propertiesEl);
        var resourceNames = ParseStringList(resourcesEl);
        var itemNames = ParseStringList(itemsEl);

        var materials = new List<CraftingMaterial>();
        AddMaterials(materials, resourceNames, CraftingMaterialKind.Resource);
        AddMaterials(materials, itemNames, CraftingMaterialKind.Item);

        var blueprints = new List<ParsedBlueprint>();
        var rejections = new List<BlueprintRejection>();
        var seenGuids = new HashSet<Guid>();
        int read = 0;

        foreach (var el in blueprintsEl.EnumerateArray())
        {
            read++;
            if (TryParseBlueprint(el, seenGuids, out var parsed, out var rejection))
            {
                blueprints.Add(parsed!);
            }
            else
            {
                rejections.Add(rejection!);
            }
        }

        return new ParsedBlueprintDataset
        {
            Version = version,
            MetaTotalBlueprints = dataset.TotalBlueprints,
            MetaTotalProducts = dataset.TotalProducts,
            MetaTotalResources = dataset.TotalResources,
            MetaTotalItems = dataset.TotalItems,
            Dataset = dataset,
            Properties = properties,
            ResourceNames = resourceNames,
            ItemNames = itemNames,
            Materials = materials,
            Blueprints = blueprints,
            Rejections = rejections,
            BlueprintsRead = read,
            RawResourceCount = resourcesEl.GetArrayLength(),
            RawItemCount = itemsEl.GetArrayLength(),
        };
    }

    private static bool TryParseBlueprint(
        JsonElement el, HashSet<Guid> seenGuids, out ParsedBlueprint? parsed, out BlueprintRejection? rejection)
    {
        parsed = null;
        rejection = null;

        var rawGuid = OptionalString(el, "guid");
        var productName = OptionalString(el, "productName");

        if (rawGuid is null || !Guid.TryParse(rawGuid, out var guid))
        {
            rejection = new BlueprintRejection(rawGuid, productName, "guid is missing or not a valid UUID.");
            return false;
        }

        // Only a successfully-parsed entry reserves the guid (see the seenGuids.Add before the
        // successful return), so a malformed first occurrence does not block a later valid duplicate.
        if (seenGuids.Contains(guid))
        {
            rejection = new BlueprintRejection(rawGuid, productName, "Duplicate guid within the file; first occurrence wins.");
            return false;
        }

        var tag = OptionalString(el, "tag");
        if (string.IsNullOrWhiteSpace(tag))
        {
            rejection = new BlueprintRejection(rawGuid, productName, "Missing required field 'tag'.");
            return false;
        }

        var rawProductEntityClass = OptionalString(el, "productEntityClass");
        if (rawProductEntityClass is null || !Guid.TryParse(rawProductEntityClass, out var productEntityClass))
        {
            rejection = new BlueprintRejection(rawGuid, productName, "Missing or invalid 'productEntityClass' UUID.");
            return false;
        }

        var gear = OptionalString(el, "gear");
        if (string.IsNullOrWhiteSpace(gear))
        {
            rejection = new BlueprintRejection(rawGuid, productName, "Missing required field 'gear'.");
            return false;
        }

        if (!el.TryGetProperty("tiers", out var tiersEl) || tiersEl.ValueKind != JsonValueKind.Array
            || tiersEl.GetArrayLength() == 0)
        {
            rejection = new BlueprintRejection(rawGuid, productName, "Missing or empty 'tiers'.");
            return false;
        }

        var normTiers = new List<NormTier>();
        var parsedTiers = new List<ParsedTier>();

        int tierIndex = 0;
        foreach (var tierEl in tiersEl.EnumerateArray())
        {
            if (tierEl.ValueKind != JsonValueKind.Object
                || !TryGetInt(tierEl, "craftTimeSeconds", out var craftTime)
                || !tierEl.TryGetProperty("slots", out var slotsEl)
                || slotsEl.ValueKind != JsonValueKind.Array
                || slotsEl.GetArrayLength() == 0)
            {
                rejection = new BlueprintRejection(rawGuid, productName, $"Tier {tierIndex} is missing 'craftTimeSeconds' or 'slots'.");
                return false;
            }

            var normSlots = new List<NormSlot>();
            var tierOptions = new List<ParsedSlotOption>();

            int slotIndex = 0;
            foreach (var slotEl in slotsEl.EnumerateArray())
            {
                var slotName = OptionalString(slotEl, "name");
                if (slotEl.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(slotName)
                    || !slotEl.TryGetProperty("options", out var optionsEl)
                    || optionsEl.ValueKind != JsonValueKind.Array || optionsEl.GetArrayLength() == 0)
                {
                    rejection = new BlueprintRejection(rawGuid, productName, $"Tier {tierIndex} slot {slotIndex} is missing 'name' or 'options'.");
                    return false;
                }

                var normOptions = new List<NormOption>();
                int optionIndex = 0;
                foreach (var optionEl in optionsEl.EnumerateArray())
                {
                    if (!TryParseOption(optionEl, out var kind, out var materialName, out var quantity, out var minQuality))
                    {
                        rejection = new BlueprintRejection(rawGuid, productName,
                            $"Tier {tierIndex} slot {slotIndex} option {optionIndex} matches neither the resource nor the item variant.");
                        return false;
                    }

                    normOptions.Add(kind == CraftingMaterialKind.Resource
                        ? new NormOption("resource", quantity, minQuality, materialName, null)
                        : new NormOption("item", quantity, minQuality, null, materialName));

                    tierOptions.Add(new ParsedSlotOption
                    {
                        SlotIndex = slotIndex,
                        SlotName = slotName!,
                        OptionIndex = optionIndex,
                        Kind = kind,
                        MaterialName = materialName,
                        Quantity = quantity,
                        MinQuality = minQuality,
                    });
                    optionIndex++;
                }

                normSlots.Add(new NormSlot(slotName!, normOptions, ParseModifiers(slotEl)));
                slotIndex++;
            }

            normTiers.Add(new NormTier(craftTime, normSlots));
            parsedTiers.Add(new ParsedTier
            {
                TierIndex = tierIndex,
                CraftTimeSeconds = craftTime,
                Options = tierOptions,
            });
            tierIndex++;
        }

        var entity = new CraftingBlueprint
        {
            Id = guid,
            Tag = tag!,
            ProductEntityClass = productEntityClass,
            Gear = gear!,
            Type = OptionalString(el, "type"),
            Subtype = OptionalString(el, "subtype"),
            ProductName = productName,
            Manufacturer = OptionalString(el, "manufacturer"),
            IsDefault = OptionalBool(el, "isDefault"),
            SuggestedName = OptionalString(el, "suggestedName"),
            SuggestedProductEntityClass = OptionalGuid(el, "suggestedProductEntityClass"),
            CigDataError = OptionalBool(el, "cigDataError"),
            Tiers = JsonSerializer.SerializeToDocument(normTiers, TiersJsonOptions),
        };

        seenGuids.Add(guid);
        parsed = new ParsedBlueprint { Entity = entity, Tiers = parsedTiers };
        return true;
    }

    private static bool TryParseOption(
        JsonElement optionEl, out CraftingMaterialKind kind, out string materialName, out decimal quantity, out int minQuality)
    {
        kind = default;
        materialName = string.Empty;
        quantity = 0;
        minQuality = 0;

        if (optionEl.ValueKind != JsonValueKind.Object
            || !optionEl.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String
            || !TryGetDecimal(optionEl, "quantity", out quantity)
            || !TryGetInt(optionEl, "minQuality", out minQuality))
        {
            return false;
        }

        var type = typeEl.GetString();
        if (type == "resource")
        {
            var name = OptionalString(optionEl, "resourceName");
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            kind = CraftingMaterialKind.Resource;
            materialName = name!;
            return true;
        }

        if (type == "item")
        {
            var name = OptionalString(optionEl, "itemName");
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            kind = CraftingMaterialKind.Item;
            materialName = name!;
            return true;
        }

        return false;
    }

    private static List<NormModifier> ParseModifiers(JsonElement slotEl)
    {
        // FR-007: a null (or absent) modifiers value is stored as an empty set.
        if (!slotEl.TryGetProperty("modifiers", out var modsEl) || modsEl.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<NormModifier>();
        foreach (var m in modsEl.EnumerateArray())
        {
            if (m.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            list.Add(new NormModifier(
                GetIntOrZero(m, "startQuality"),
                GetIntOrZero(m, "endQuality"),
                GetDoubleOrZero(m, "modifierAtStart"),
                GetDoubleOrZero(m, "modifierAtEnd"),
                OptionalString(m, "propertyName") ?? string.Empty,
                OptionalString(m, "propertyKey") ?? string.Empty,
                OptionalBool(m, "additive")));
        }

        return list;
    }

    private static List<CraftingProperty> ParseProperties(JsonElement propertiesEl)
    {
        var list = new List<CraftingProperty>();
        foreach (var prop in propertiesEl.EnumerateObject())
        {
            var value = prop.Value;
            if (value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            list.Add(new CraftingProperty
            {
                Key = prop.Name,
                Name = OptionalString(value, "name") ?? string.Empty,
                Unit = OptionalString(value, "unit"),
                Category = OptionalString(value, "category") ?? string.Empty,
                NameOverrides = value.TryGetProperty("nameOverrides", out var no) && no.ValueKind == JsonValueKind.Object
                    ? JsonSerializer.SerializeToDocument(no)
                    : null,
            });
        }

        return list;
    }

    private static List<string> ParseStringList(JsonElement arrayEl)
    {
        var list = new List<string>();
        foreach (var el in arrayEl.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    list.Add(s);
                }
            }
        }

        return list;
    }

    private static void AddMaterials(List<CraftingMaterial> materials, IReadOnlyList<string> names, CraftingMaterialKind kind)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (seen.Add(name))
            {
                materials.Add(new CraftingMaterial { Kind = kind, Name = name });
            }
        }
    }

    // ── Top-level requirements (throw on failure → 400) ──────────────────────

    private static string RequireString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()!
            : throw new InvalidBlueprintDocumentException($"Missing or invalid required field '{prop}'.");

    private static JsonElement RequireObject(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Object
            ? v
            : throw new InvalidBlueprintDocumentException($"Missing or invalid required section '{prop}'.");

    private static JsonElement RequireArray(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array
            ? v
            : throw new InvalidBlueprintDocumentException($"Missing or invalid required section '{prop}'.");

    private static int RequireInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
            ? i
            : throw new InvalidBlueprintDocumentException($"Missing or invalid required numeric field '{prop}'.");

    private static double RequireNumber(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : throw new InvalidBlueprintDocumentException($"Missing or invalid required numeric field '{prop}'.");

    private static JsonDocument CloneArray(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array
            ? JsonSerializer.SerializeToDocument(v)
            : throw new InvalidBlueprintDocumentException($"Missing or invalid required section '{prop}'.");

    // ── Optional / lenient readers ───────────────────────────────────────────

    private static string? OptionalString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool? OptionalBool(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var v))
        {
            return null;
        }

        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static Guid? OptionalGuid(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out var g)
            ? g
            : null;

    private static bool TryGetInt(JsonElement el, string prop, out int value)
    {
        value = 0;
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out value);
    }

    private static bool TryGetDecimal(JsonElement el, string prop, out decimal value)
    {
        value = 0;
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out value);
    }

    private static int GetIntOrZero(JsonElement el, string prop) => TryGetInt(el, prop, out var v) ? v : 0;

    private static double GetDoubleOrZero(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0d;

    // ── Normalized jsonb shapes (camelCase, nulls omitted) ───────────────────

    private sealed record NormTier(int craftTimeSeconds, List<NormSlot> slots);

    private sealed record NormSlot(string name, List<NormOption> options, List<NormModifier> modifiers);

    private sealed record NormOption(string type, decimal quantity, int minQuality, string? resourceName, string? itemName);

    private sealed record NormModifier(
        int startQuality, int endQuality, double modifierAtStart, double modifierAtEnd,
        string propertyName, string propertyKey, bool? additive);
}
