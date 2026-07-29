# Data Model: Crafting Blueprint Import

**Feature**: `020-crafting-blueprint-import` | **Date**: 2026-07-17

All tables live in the **`sc` schema** (per-table `ToTable(name, schema: "sc")`, snake_case
columns — same pattern as `ItemConfiguration`). One additive, forward-only migration
`AddCraftingBlueprints` creates all six tables. No existing table is modified.

> **Revised 2026-07-18**: tiers and slot options are additionally normalized into query tables
> (`sc.blueprint_tiers`, `sc.blueprint_slot_options`), and material names are resolved to catalog
> UUIDs at import (commodities first, then items — research Decision 11).

## Entity: CraftingBlueprint → `sc.blueprints`

Domain: `NajaEcho.Domain/Blueprints/CraftingBlueprint.cs`

| Property | Column | Type | Notes |
|---|---|---|---|
| `Id` | `id` | `uuid` PK | The blueprint `guid` from the file — stable identity (FR-012); NOT app-generated |
| `Tag` | `tag` | `varchar(512)` not null | |
| `ProductEntityClass` | `product_entity_class` | `uuid` not null | |
| `Gear` | `gear` | `varchar(256)` not null | |
| `Type` | `type` | `varchar(256)` null | nullable in file |
| `Subtype` | `subtype` | `varchar(256)` null | nullable in file |
| `ProductName` | `product_name` | `varchar(512)` null | nullable in file; first choice for display name |
| `Manufacturer` | `manufacturer` | `varchar(512)` null | nullable in file |
| `IsDefault` | `is_default` | `boolean` null | absent → null (FR-006) |
| `SuggestedName` | `suggested_name` | `varchar(512)` null | absent → null |
| `SuggestedProductEntityClass` | `suggested_product_entity_class` | `uuid` null | absent → null |
| `CigDataError` | `cig_data_error` | `boolean` null | absent → null |
| `Tiers` | `tiers` | `jsonb` not null | Normalized nested structure (see below) |
| `ImportedAt` | `imported_at` | `timestamptz` not null | set on insert |
| `UpdatedAt` | `updated_at` | `timestamptz` not null | set on insert and every update |

Indexes: PK only (listing reads the whole table; name search is client-side).

### `tiers` jsonb shape (normalized parsed output, not raw file bytes)

```json
[
  {
    "craftTimeSeconds": 120,
    "slots": [
      {
        "name": "Frame",
        "options": [
          { "type": "resource", "quantity": 12.5, "minQuality": 100, "resourceName": "Steel" },
          { "type": "item", "quantity": 1, "minQuality": 0, "itemName": "Basic Frame" }
        ],
        "modifiers": [
          {
            "startQuality": 0, "endQuality": 1000,
            "modifierAtStart": 0.8, "modifierAtEnd": 1.2,
            "propertyName": "Health", "propertyKey": "health",
            "additive": false
          }
        ]
      }
    ]
  }
]
```

Normalization rules applied by the parser before storage:
- Slot `modifiers: null` → stored as `[]` (FR-007).
- Item options always carry `modifiers: null` in the file; the option object stores no modifiers key.
- Modifier `additive` absent → key omitted (read as "not additive").
- Unknown/extra fields are dropped.

**Update semantics (FR-013/FR-014)**: upsert by `Id`. On update, every column including `tiers` is
overwritten from the latest file, and the blueprint's derived `blueprint_tiers` /
`blueprint_slot_options` rows are deleted and re-inserted — together guaranteeing no stale nested
records. Blueprints absent from a new file are retained untouched (Assumptions; no soft delete in v1).

## Entity: CraftingBlueprintTier → `sc.blueprint_tiers`

Domain: `NajaEcho.Domain/Blueprints/CraftingBlueprintTier.cs`

Normalized per-tier record for query support (FR-009b). Derived from the same parsed data as the
`tiers` jsonb; rebuilt on every blueprint insert/update.

| Property | Column | Type | Notes |
|---|---|---|---|
| `Id` | `id` | `uuid` PK | app-generated |
| `BlueprintId` | `blueprint_id` | `uuid` not null | FK → `sc.blueprints.id`, `ON DELETE CASCADE` |
| `TierIndex` | `tier_index` | `integer` not null | 0-based position in the file's `tiers` array |
| `CraftTimeSeconds` | `craft_time_seconds` | `integer` not null | |

Indexes: unique (`blueprint_id`, `tier_index`).

## Entity: CraftingBlueprintSlotOption → `sc.blueprint_slot_options`

Domain: `NajaEcho.Domain/Blueprints/CraftingBlueprintSlotOption.cs`

The **flat query table** — one row per slot option (slots are flattened into their options; slot
identity preserved as columns). Serves "all blueprints using material X" and material-aggregation
queries. Modifiers are NOT stored here (jsonb only).

| Property | Column | Type | Notes |
|---|---|---|---|
| `Id` | `id` | `uuid` PK | app-generated |
| `TierId` | `tier_id` | `uuid` not null | FK → `sc.blueprint_tiers.id`, `ON DELETE CASCADE` |
| `SlotIndex` | `slot_index` | `integer` not null | 0-based position of the slot within the tier |
| `SlotName` | `slot_name` | `varchar(512)` not null | |
| `OptionIndex` | `option_index` | `integer` not null | 0-based position of the option within the slot |
| `Kind` | `kind` | `varchar(16)` not null | `CraftingMaterialKind` stored as string (`resource` \| `item`) |
| `MaterialName` | `material_name` | `varchar(512)` not null | the option's `resourceName`/`itemName` as provided |
| `Quantity` | `quantity` | `numeric` not null | |
| `MinQuality` | `min_quality` | `integer` not null | 0–1000 |
| `MatchedUuid` | `matched_uuid` | `varchar(128)` null | catalog UUID resolved per research Decision 11; null when unmatched |
| `MatchedSource` | `matched_source` | `varchar(16)` null | `CatalogSource` stored as string (`commodity` \| `item`); null when unmatched |

Indexes: (`tier_id`), (`material_name`), (`matched_uuid`).

Unique (`tier_id`, `slot_index`, `option_index`).

## Entity: CraftingMaterial → `sc.crafting_materials`

Domain: `NajaEcho.Domain/Blueprints/CraftingMaterial.cs` + `CraftingMaterialKind.cs`

| Property | Column | Type | Notes |
|---|---|---|---|
| `Kind` | `kind` | `varchar(16)` PK part | enum `CraftingMaterialKind { Resource, Item }`, stored as string (`HasConversion<string>`) |
| `Name` | `name` | `varchar(512)` PK part | from the dataset's `resources` / `items` string lists |
| `MatchedUuid` | `matched_uuid` | `varchar(128)` null | catalog UUID resolved per research Decision 11 (commodities first, then items); null when unmatched |
| `MatchedSource` | `matched_source` | `varchar(16)` null | `commodity` \| `item` (string-stored enum); null when unmatched |

Composite PK (`kind`, `name`). One polymorphic table (019 `loot_ledger` precedent). Holds the
crafting-material name lists with their catalog resolution snapshot (FR-009); the same resolution
is denormalized onto `sc.blueprint_slot_options` rows. Refresh on import = delete all + insert
from the new file (FR-016), inside the import transaction. Resolution is a snapshot at blueprint
import; catalog re-imports do not retro-update it.

## Entity: CraftingProperty → `sc.crafting_properties`

Domain: `NajaEcho.Domain/Blueprints/CraftingProperty.cs`

| Property | Column | Type | Notes |
|---|---|---|---|
| `Key` | `property_key` | `varchar(256)` PK | the `properties` map key |
| `Name` | `name` | `varchar(512)` not null | |
| `Unit` | `unit` | `varchar(128)` null | nullable in file |
| `Category` | `category` | `varchar(256)` not null | |
| `NameOverrides` | `name_overrides` | `jsonb` null | optional string→string map, stored as-is |

Refresh on import = delete all + insert. Modifier `propertyKey` values inside blueprint `tiers`
reference these keys informally — never enforced (Assumptions; missing key is at most a warning).

## Entity: CraftingDataset → `sc.crafting_datasets`

Domain: `NajaEcho.Domain/Blueprints/CraftingDataset.cs`

Single-row snapshot of the most recent upload (FR-010; history out of scope).

| Property | Column | Type | Notes |
|---|---|---|---|
| `Id` | `id` | `uuid` PK | app-generated per import |
| `Version` | `version` | `varchar(64)` not null | dataset semver string |
| `TotalBlueprints` | `total_blueprints` | `integer` not null | file `meta` totals, stored as reported |
| `TotalProducts` | `total_products` | `integer` not null | |
| `TotalResources` | `total_resources` | `integer` not null | |
| `TotalItems` | `total_items` | `integer` not null | |
| `Efficiency` | `efficiency` | `numeric` not null | dismantle config |
| `DismantleTimeSeconds` | `dismantle_time_seconds` | `integer` not null | |
| `BlacklistedResources` | `blacklisted_resources` | `jsonb` not null | `[{guid, name}]` as-is |
| `BlacklistedEntityClasses` | `blacklisted_entity_classes` | `jsonb` not null | `[{guid, name, nameKey}]` as-is |
| `ImportedAt` | `imported_at` | `timestamptz` not null | |

Refresh on import = delete existing row(s) + insert the new snapshot.

## Relationship: Blueprint ↔ `sc.items` (read-time, not stored)

No FK. The listing query LEFT JOINs `sc.items` on `items.uuid = blueprints.id::text` with
`items.soft_deleted_at IS NULL`, selecting one item deterministically when duplicates exist
(`ix_items_uuid` is non-unique — pick e.g. lowest `items.id`). Produces the display-name fallback
chain (FR-022):

```
display_name = COALESCE(NULLIF(product_name, ''), matched_item.name, NULLIF(tag, ''), id::text)
```

A blueprint with no matching item row is simply unlinked — never an import error (FR-015).

## Transient (not persisted)

**ImportBlueprintsResult** (Application layer → API response): dataset `version`; per-collection
counts (`blueprints`, `resources`, `items`, `properties`) each with `read` / `inserted` /
`updated` / `rejected`; `referenceDataReplaced` flag; `warnings[]` (meta-total mismatches per
FR-021, plus material names unmatched by the catalog lookup per FR-009); `rejections[]` of
`{ guid?, productName?, reason }` (FR-020). Reference-data collections always report `updated: 0`
and `rejected` only for top-level-invalid cases (they are replaced wholesale, counted as
`read`/`inserted`).

**Material resolution map** (in-memory, research Decision 11): distinct lower-cased material name
→ `{ matchedUuid, matchedSource }`, built from two batched read-only catalog queries (commodities,
then items) before the import transaction; applied to `crafting_materials` rows and denormalized
onto `blueprint_slot_options` rows.

**Parsed dataset** (parser output, in-memory): valid `CraftingBlueprint` list + normalized tiers
json + derived tier/slot-option row data, materials, properties, dataset snapshot, rejection
list, warning list. Validation is fully completed before any database write (research Decision 7).

## State transitions

Blueprints have no status field in v1. Lifecycle: inserted → updated in place on re-import →
retained when absent from later files. `blueprint_tiers`/`blueprint_slot_options` rows are derived
data, rebuilt whenever their blueprint is inserted or updated. Reference tables and the dataset
row are replace-on-import.
