# Data Model: Blueprint Detail Panel

## Existing Tables (read-only, no schema changes)

### sc.blueprints
| Column | Type | Notes |
|--------|------|-------|
| id | uuid | Primary key |
| product_name | varchar(512) | Nullable — display name |
| type | varchar(256) | Nullable — blueprint category |
| tiers | jsonb | Full nested tier/slot/option structure (not queried directly) |

### sc.blueprint_tiers
| Column | Type | Notes |
|--------|------|-------|
| id | uuid | Primary key |
| blueprint_id | uuid | FK → sc.blueprints(id) CASCADE |
| tier_index | int | 0 = base crafting tier |
| craft_time_seconds | int | Craft time for this tier |

Unique index: `(blueprint_id, tier_index)`

### sc.blueprint_slot_options
| Column | Type | Notes |
|--------|------|-------|
| id | uuid | Primary key |
| tier_id | uuid | FK → sc.blueprint_tiers(id) CASCADE |
| slot_index | int | Groups rows into ingredient slots; COUNT(DISTINCT slot_index) = ingredient count |
| slot_name | varchar(512) | Top-level slot label (e.g., "Cast Iron") |
| option_index | int | Order within the slot; each row is one material option |
| kind | varchar(16) | Material kind discriminator (e.g., "material", "component") |
| material_name | varchar(512) | Material name for this option (e.g., "Injector Nozzles") |
| quantity | numeric | Amount of this material required |
| min_quality | int | Minimum quality threshold |
| matched_uex_id | int | Nullable — matched UEX material ID |

Unique index: `(tier_id, slot_index, option_index)`

### user_blueprints (existing, no changes)
| Column | Type | Notes |
|--------|------|-------|
| id | uuid | Primary key |
| user_id | uuid | FK → users |
| blueprint_id | uuid | FK → sc.blueprints(id) |
| added_at | timestamptz | When added |

Unique index: `(user_id, blueprint_id)`

---

## New Application-Layer DTOs (no DB changes required)

### BlueprintDetailDto
Returned by `GetBlueprintDetailHandler`.

| Field | Type | Notes |
|-------|------|-------|
| BlueprintId | Guid | |
| ProductName | string? | Nullable |
| Type | string? | Nullable |
| CraftTimeSeconds | int? | Null if no tier 0 record exists |
| IngredientCount | int | COUNT(DISTINCT slot_index) at tier 0 |
| Slots | IReadOnlyList\<BlueprintSlotDto\> | Ordered by slot_index |

### BlueprintSlotDto
One entry per distinct `slot_index` in `sc.blueprint_slot_options`.

| Field | Type | Notes |
|-------|------|-------|
| SlotIndex | int | |
| SlotName | string | Display label for the slot (e.g., "Cast Iron") |
| Options | IReadOnlyList\<BlueprintSlotOptionDto\> | Ordered by option_index |

### BlueprintSlotOptionDto
One entry per row within a slot (one material option).

| Field | Type | Notes |
|-------|------|-------|
| OptionIndex | int | |
| MaterialName | string | e.g., "Injector Nozzles" |
| Kind | string | e.g., "material", "component" |
| Quantity | decimal | |

---

## No EF Core Migrations Required

All tables queried are existing `sc`-schema read-only tables. The `user_blueprints` row delete is a straight EF Core `Remove` on the existing `UserBlueprint` entity — no migration needed.

---

## Relationships (for the detail query)

```
user_blueprints
  └─ blueprint_id → sc.blueprints
                       ├─ id → sc.blueprint_tiers (tier_index = 0)
                       │           └─ id → sc.blueprint_slot_options (ordered by slot_index, option_index)
                       ├─ product_name
                       └─ type
```
