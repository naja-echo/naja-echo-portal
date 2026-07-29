# Data Model: Blueprint Filtering (028)

No new tables, entities, or migrations are introduced. This feature surfaces an existing column (`subtype`) that is already stored in `sc.blueprints` but not yet projected into the list response shapes.

## Affected Shapes

### `MyBlueprintListItemDto` (Application layer)

**Change**: Add `Subtype` field.

| Field | Type | Notes |
|-------|------|-------|
| BlueprintId | `Guid` | Unchanged |
| ProductName | `string?` | Unchanged |
| Type | `string?` | Category — unchanged |
| **Subtype** | `string?` | **NEW** — maps to `b.subtype` in SQL |
| IngredientCount | `int` | Unchanged |

### `OrgBlueprintListItemDto` (Application layer)

**Change**: Add `Subtype` field (mirrors My Blueprint change).

| Field | Type | Notes |
|-------|------|-------|
| BlueprintId | `Guid` | Unchanged |
| ProductName | `string?` | Unchanged |
| Type | `string?` | Category — unchanged |
| **Subtype** | `string?` | **NEW** — maps to `b.subtype` in SQL |
| IngredientCount | `int` | Unchanged |

### `MyBlueprintListItemResponse` / `OrgBlueprintListItemResponse` (API contracts)

**Change**: Add `Subtype` property (nullable string, JSON key `subtype`).

## SQL Queries — Changes

Both repositories query `sc.blueprints` using raw SQL. The `ListRow` projection record and the `SELECT` clause in `GetListAsync` must be updated in both `UserBlueprintRepository` and `OrgBlueprintRepository`:

```sql
-- Add to SELECT list:
b.subtype AS subtype

-- Add to GROUP BY:
b.subtype
```

## Frontend Types — Changes

In `frontend/src/features/blueprints/api/blueprintsApi.ts`:

- `MyBlueprintListItem` — add `subtype: string | null`
- `OrgBlueprintListItem` — add `subtype: string | null`

## New Frontend Artifacts

### `BlueprintFilterValues` (interface in `BlueprintFilters.tsx`)

| Field | Type | Notes |
|-------|------|-------|
| name | `string` | Substring match against productName |
| category | `string` | Exact match against `type` |
| subcategory | `string` | Exact match against `subtype` |

### Filter State Derivation Rules

- **Category options**: distinct non-null `type` values from the full list, sorted alphabetically.
- **Subcategory options**: when category is empty → distinct non-null `subtype` values from the full list; when category is set → distinct non-null `subtype` values from blueprints matching the selected category. Sorted alphabetically.
- **Filtered list**: blueprints that satisfy all three predicates simultaneously:
  1. `name` is empty OR `productName` contains `name` (case-insensitive)
  2. `category` is empty OR `type === category`
  3. `subcategory` is empty OR `subtype === subcategory`

## Existing Tables (reference, no changes)

| Table | Schema | Role |
|-------|--------|------|
| `sc.blueprints` | `sc` | Source of type, subtype, product_name |
| `user_blueprints` | public | Links users to blueprints (My Blueprints) |
| `organization_memberships` | public | Org scoping (Org Blueprints) |
