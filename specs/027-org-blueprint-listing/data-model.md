# Data Model: Org Blueprint Listing

## No new entities or migrations required

All data for this feature already exists in the database. This feature adds a new read path over existing tables.

---

## Existing Entities Used

### OrgBlueprintListItem (read-only projection)

A deduplicated view of blueprints held by any member of the current user's organization.

| Field | Source | Notes |
|-------|--------|-------|
| `BlueprintId` | `sc.blueprints.id` | Unique blueprint identifier |
| `ProductName` | `sc.blueprints.product_name` | Display name; nullable |
| `Type` | `sc.blueprints.type` | Blueprint category; nullable |
| `IngredientCount` | COUNT(DISTINCT `sc.blueprint_slot_options.slot_index`) at `tier_index = 0` | Number of ingredient slots |

**Derivation**: JOIN `organization_memberships` (is_current = true) → `user_blueprints` → `sc.blueprints`; GROUP BY blueprint_id to deduplicate.

---

### OrgBlueprintDetail (read-only projection)

Full detail for a single blueprint, scoped to the org (no user ownership check required — any org member can view any org blueprint).

| Field | Source | Notes |
|-------|--------|-------|
| `BlueprintId` | `sc.blueprints.id` | |
| `ProductName` | `sc.blueprints.product_name` | Nullable |
| `Type` | `sc.blueprints.type` | Nullable |
| `CraftTimeSeconds` | `sc.blueprint_tiers.craft_time_seconds` at `tier_index = 0` | Nullable |
| `IngredientCount` | COUNT(DISTINCT slot_index) | |
| `Slots` | `sc.blueprint_slot_options` at `tier_index = 0` | Ordered by slot_index, option_index |
| `Owners` | List of `BlueprintOwner` | See below |

**Derivation**: Verifies at least one `user_blueprints` row exists for this blueprint within the org; returns null if not found.

---

### BlueprintOwner (nested in OrgBlueprintDetail)

An org member who has a given blueprint in their personal collection.

| Field | Source | Notes |
|-------|--------|-------|
| `UserId` | `AspNetUsers.Id` | |
| `DisplayName` | `ApplicationUser.DisplayName` or `DiscordUsername` if empty | Human-readable name |

**Derivation**: JOIN `user_blueprints` → `AspNetUsers` → `organization_memberships` (is_current = true, same org); ORDER BY display_name.

---

## Source Table Reference

| Table | Schema | Existing? | Role in this feature |
|-------|--------|-----------|----------------------|
| `user_blueprints` | public | ✅ | Bridge between users and blueprints |
| `organization_memberships` | public | ✅ | Determines which users belong to the current org |
| `AspNetUsers` | public | ✅ | Provides owner display names |
| `sc.blueprints` | sc | ✅ | Blueprint master catalog |
| `sc.blueprint_tiers` | sc | ✅ | Craft time per tier |
| `sc.blueprint_slot_options` | sc | ✅ | Ingredient slots and options |

---

## Relationship Summary

```
organization_memberships (is_current=true, org_id = current user's org)
    └── user_id → user_blueprints.user_id
                       └── blueprint_id → sc.blueprints.id
                                               └── sc.blueprint_tiers (tier_index=0)
                                                       └── sc.blueprint_slot_options
```

Owner lookup reverses the join: given a `blueprint_id`, find all `user_blueprints` rows for that blueprint where the `user_id` belongs to the same org.
