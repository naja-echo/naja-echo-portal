# Research: Personal Blueprint Manager

## Schema convention for user-owned data

**Decision**: New `user_blueprints` table lives in the default (public) schema — no `schema:` argument.

**Rationale**: All SC game-data tables use the `sc` schema (`sc.blueprints`, `sc.ships`, `sc.items`, etc.). All user/app-owned tables (`hangar_entries`, `loot_ledger`, `organization_memberships`, `characters`) use the default schema. This convention is already established and must be followed.

**Alternatives considered**: Putting it in `sc` — rejected because `sc` is reserved for Star Citizen reference data, not user state.

---

## Ingredient count definition

**Decision**: The Ingredients column displays the count of distinct ingredient slots in **tier 0** (the first crafting tier, `tier_index = 0`) of the blueprint.

**Rationale**: Blueprints can have multiple tiers (quality levels). Tier 0 is the base tier and represents the canonical ingredient list. A slot can have multiple material options (alternatives within the same slot position), but the _number of slots_ is the ingredient count, not the number of options. The query is: `COUNT(DISTINCT slot_index)` from `blueprint_slot_options` joined through `blueprint_tiers` where `tier_index = 0`.

**Alternatives considered**:
- Summing across all tiers: rejected — confusing and inconsistent with how crafting works in game.
- Counting options rather than slots: rejected — options are alternatives, not additive ingredients.
- Reading from JSONB directly: rejected — the normalized `blueprint_slot_options` table exists specifically for queryable slot data; prefer it over JSONB parsing in SQL.

---

## New API endpoints

**Decision**: Three new endpoints under `/api/blueprints` (not under `/api/admin/blueprints`):

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/blueprints/search` | Member-accessible autosuggest — returns blueprint catalog entries matching a `q` query param against `product_name`. |
| GET | `/api/blueprints/mine` | Returns the authenticated user's personal blueprint list with Blueprint, Type, and Ingredients count. |
| POST | `/api/blueprints/mine` | Adds a catalog blueprint to the authenticated user's personal list. |

**Rationale**: The existing `GET /api/admin/blueprints` is admin-only and returns a different shape (DisplayName computed from fallback logic, no ingredient count). A separate member-facing blueprint search endpoint is required. Placing user endpoints under `/api/blueprints` (separate from `/api/admin/blueprints`) keeps admin and member surfaces cleanly separated — consistent with how `/api/hangar/mine` vs admin routes are split.

**Alternatives considered**: Reusing the admin endpoint with role relaxation — rejected; the admin listing serves import/admin purposes and has a different response shape. Adding `?search=` to the admin endpoint — rejected; mixes concerns and exposes admin data to members.

---

## Autosuggest frontend pattern

**Decision**: Use the same `Command` component pattern from `AddInventoryDialog.tsx`, with a dedicated `useBlueprintSearch(query)` hook backed by TanStack Query and a 300ms debounce.

**Rationale**: `AddInventoryDialog.tsx` is the established pattern for autosuggest-driven add dialogs. It uses shadcn `Command` + `CommandInput` + `CommandGroup` + `CommandItem`, which is consistent with the component system.

**Alternatives considered**: A standard `<select>` with options — rejected; too many blueprints to load upfront. A custom combobox — rejected; `Command` is already the project standard.

---

## Duplicate prevention

**Decision**: Enforce uniqueness at the database level via a unique constraint on `(user_id, blueprint_id)` in `user_blueprints`, and return HTTP 409 Conflict from the POST endpoint when a duplicate is detected.

**Rationale**: Database-level constraint is the safest guard. The 409 response allows the frontend to display a user-friendly message without ambiguity.

**Alternatives considered**: Application-level check only — rejected; subject to race conditions.

---

## No API contract changes required for navigation

Navigation is frontend-only (adding an entry to `navItems.ts`). No backend changes needed for navigation itself.
