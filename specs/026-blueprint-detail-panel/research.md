# Research: Blueprint Detail Panel

## Decision 1 — Craft Time Source

**Decision**: Craft time is sourced from `sc.blueprint_tiers.craft_time_seconds` where `tier_index = 0`.

**Rationale**: The `sc.blueprints` table has no direct `craft_time` column. The normalized `blueprint_tiers` table holds `craft_time_seconds` per tier. Tier 0 is the base crafting tier, consistent with how ingredient count is already calculated.

**Alternatives considered**: Reading from the JSONB `tiers` column on `sc.blueprints` — rejected because the normalized table is already indexed and used by existing queries; raw JSONB parsing adds complexity with no benefit.

**Display format**: `craft_time_seconds` is an integer. The frontend will format it as human-readable time (e.g., "5m 30s") — no additional backend transformation required.

---

## Decision 2 — Ingredient Hierarchy Model

**Decision**: The ingredient list is modelled as a two-level structure: **slots** (distinct `slot_index`) containing **options** (rows within a slot identified by `option_index`).

**Rationale**: The `sc.blueprint_slot_options` table stores:
- `slot_index` — groups rows into logical ingredient slots
- `option_index` — within a slot, each row is a specific material option
- `slot_name` — the name/label for the slot (top-level, e.g., "Cast Iron")
- `material_name` — the raw material name for the option (indented level, e.g., "Injector Nozzles")
- `quantity` — amount required for this option
- `kind` — string enum discriminator (e.g., "material", "component")

The detail API returns slots grouped by `slot_index`, each with their ordered options. The frontend renders slot_name + quantity at the top level and material_name + kind + quantity indented beneath it — matching the user's described layout.

**Alternatives considered**: Reading from the JSONB `tiers` column for the full nested tree — rejected because the normalized tables are already queryable and indexed; JSONB traversal is not needed for two-level display.

---

## Decision 3 — New API Endpoints

**Decision**: Two new endpoints added to the existing `/api/blueprints` group:

- `GET /api/blueprints/mine/{blueprintId}` — returns full blueprint detail (summary + ingredient list) for a blueprint in the user's personal list.
- `DELETE /api/blueprints/mine/{blueprintId}` — removes a blueprint from the user's personal list.

**Rationale**: Both endpoints are user-scoped (require the requesting user to own the user_blueprint row) and slot naturally into the existing `/api/blueprints/mine` resource group pattern.

**Alternatives considered**: Using a generic `GET /api/blueprints/{blueprintId}` not scoped to the user — rejected because ownership verification is required (only blueprints in the user's list can be detailed/removed), and the user-scoped URL makes the access boundary explicit.

---

## Decision 4 — Frontend Route Rename

**Decision**: The frontend route `/blueprints/mine` is renamed to `/blueprints/personal`. A redirect from the old path is added so any existing deep links or bookmarks continue to work.

**Rationale**: User-driven naming change. The redirect prevents broken navigation.

**Scope**: Frontend router only. Backend API URLs (`/api/blueprints/mine/...`) are unchanged — they describe a resource pattern, not a UI path.

---

## Decision 5 — Slide-Over Panel Component

**Decision**: The `Sheet` component from `frontend/src/components/ui/sheet.tsx` (already present in the repository) is used for the slide-over panel, opened with `side="right"`.

**Rationale**: The component is already in the codebase, built on Radix UI `DialogPrimitive`, and provides the overlay, close-on-click-outside, X button, and animation out of the box — matching all requirements with no additional dependencies.

**Alternatives considered**: Custom CSS slide-over panel — rejected; the existing component covers all requirements and avoids re-implementing accessible dismiss behaviour.

---

## Decision 6 — Remove Confirmation UX

**Decision**: Inline confirmation within the Sheet panel: clicking "Remove" toggles to a confirmation state showing "Remove this blueprint?" with "Confirm" and "Cancel" buttons within the panel footer. No second modal is opened.

**Rationale**: Keeps the interaction self-contained within the slide-over panel. A second modal over the panel would create stacking complexity. Inline state toggle is lightweight and consistent with the principle of minimal UI chrome.

**Alternatives considered**: Browser `confirm()` dialog — rejected (non-accessible, non-styled). Separate confirmation dialog — rejected (modal-over-modal complexity).

---

## Decision 7 — Blueprint Detail Query Strategy

**Decision**: The `GET /api/blueprints/mine/{blueprintId}` endpoint delegates to a new `GetBlueprintDetailHandler` that:
1. Verifies the `user_blueprints` row exists (user owns this blueprint).
2. Queries `sc.blueprints` for name, type.
3. Queries `sc.blueprint_tiers` for `craft_time_seconds` at `tier_index = 0`.
4. Queries `sc.blueprint_slot_options` for all rows at tier 0, ordered by `slot_index`, `option_index`.
5. Groups into slot → options hierarchy in application code.

**Rationale**: Raw SQL projection is already the established pattern in this codebase (UserBlueprintRepository uses `SqlQuery<T>`). Keeping the grouping in application code avoids complex SQL `json_agg` aggregation.
