# Data Model: Personal Blueprint Manager

## New table: `user_blueprints` (default schema)

Stores the many-to-many association between an authenticated user and catalog blueprints they have personally saved.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK | Surrogate key |
| `user_id` | `uuid` | NOT NULL, FK → `characters.id` | The member who owns this entry |
| `blueprint_id` | `uuid` | NOT NULL, FK → `sc.blueprints.id` | The catalog blueprint |
| `added_at` | `timestamptz` | NOT NULL | UTC timestamp when the association was created |

**Unique constraint**: `(user_id, blueprint_id)` — prevents duplicates.

**Index**: On `user_id` for efficient listing queries.

---

## Read-only catalog tables (existing — not modified)

| Table | Schema | Used for |
|-------|--------|---------|
| `blueprints` | `sc` | product_name, type, id |
| `blueprint_tiers` | `sc` | tier_index — filtered to tier 0 for ingredient count |
| `blueprint_slot_options` | `sc` | slot_index — distinct count gives ingredient count |

---

## New domain entity: `UserBlueprint`

```
UserBlueprint
  Id          : Guid         — surrogate PK
  UserId      : Guid         — FK to Character/User
  BlueprintId : Guid         — FK to CraftingBlueprint
  AddedAt     : DateTimeOffset
```

---

## Query shape: `MyBlueprintListItem`

Returned by the `GET /api/blueprints/mine` handler. Joined across `user_blueprints`, `sc.blueprints`, `sc.blueprint_tiers`, and `sc.blueprint_slot_options`.

| Field | Source | Notes |
|-------|--------|-------|
| `BlueprintId` | `sc.blueprints.id` | Catalog blueprint identifier |
| `ProductName` | `sc.blueprints.product_name` | Blueprint column |
| `Type` | `sc.blueprints.type` | Type column (nullable) |
| `IngredientCount` | `COUNT(DISTINCT bso.slot_index)` | Slots in tier 0 of the blueprint |

**Ingredient count query logic**:
```sql
SELECT COUNT(DISTINCT bso.slot_index)
FROM sc.blueprint_slot_options bso
JOIN sc.blueprint_tiers bt ON bt.id = bso.tier_id
WHERE bt.blueprint_id = <blueprintId>
  AND bt.tier_index = 0
```

---

## Search query shape: `BlueprintSearchResult`

Returned by `GET /api/blueprints/search?q=`. Used to populate the autosuggest dropdown.

| Field | Source | Notes |
|-------|--------|-------|
| `BlueprintId` | `sc.blueprints.id` | Used as the value when the user selects |
| `ProductName` | `sc.blueprints.product_name` | Display label in dropdown |
| `Type` | `sc.blueprints.type` | Optional secondary label |

Search performs a case-insensitive `ILIKE '%q%'` on `product_name`. Only blueprints with a non-null `product_name` are returned. Results are limited to 20 entries.

---

## Entity relationships

```
characters (user_id)
    │
    └──< user_blueprints >──── sc.blueprints
                                    │
                               sc.blueprint_tiers (tier_index = 0)
                                    │
                               sc.blueprint_slot_options (slot_index)
```
