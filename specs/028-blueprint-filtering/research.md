# Research: Blueprint Filtering (028)

## Decision 1: Is `subtype` in the database?

**Decision**: Yes — `subtype` is already a mapped column in `sc.blueprints` (column name `subtype`, max 256 chars, nullable). The EF Core configuration in `CraftingBlueprintConfiguration.cs` confirms it. No migration is required.

**Rationale**: The `CraftingBlueprint` domain entity has `Subtype { get; set; }` mapped via `HasColumnName("subtype")`. The data is already persisted by the blueprint importer; it just isn't projected into the list DTOs.

**Alternatives considered**: Adding a new lookup endpoint for filter options — rejected as unnecessary since the list responses already return all data needed to derive options client-side.

---

## Decision 2: Does this feature require API contract changes?

**Decision**: Yes — `subtype` must be added to both `MyBlueprintListItemResponse` and `OrgBlueprintListItemResponse`. This is an additive, non-breaking change (new nullable field). The OpenAPI contract must be updated before backend implementation (Constitution Principle I).

**Rationale**: Client-side subcategory filtering requires `subtype` on each list item. The field already exists in the DB; surfacing it is a small backend addition to each list endpoint's response shape. No new endpoints are introduced.

**Alternatives considered**:
- Server-side filtering via query params — rejected; dataset is small (hundreds of blueprints), client-side is simpler and avoids additional query parameter surface.
- Separate filter-options endpoint — rejected per YAGNI; deriving options from the already-fetched list is sufficient.

---

## Decision 3: Filter state location

**Decision**: Component-level React state (`useState`) in each page component, passed down via props to the filter bar. No URL params, no global store.

**Rationale**: Spec explicitly states filter state is held in component state. Persisting across panel open/close (User Story 3) is automatic since the panel is rendered inside the same page component and does not unmount the filter state. The dataset is small enough that no memoization complexity is needed beyond a simple `useMemo` on the filtered list.

**Alternatives considered**: URL search params — rejected (spec out of scope); Zustand/Context — rejected (YAGNI, single-page concern).

---

## Decision 4: Reuse of `Combobox` component for Category and Subcategory

**Decision**: Use the existing `Combobox` component from `components/ui/combobox.tsx` for both Category and Subcategory filter controls. Selecting the currently-selected value clears it (toggle behaviour already built into the component via `handleSelect`).

**Rationale**: The `Combobox` already implements the toggle-to-clear pattern (`selectedValue === value ? '' : selectedValue`), supports search within options, and is the same control used in the Warehouse Ship Components filter. No new primitive is needed.

---

## Decision 5: Shared `BlueprintFilters` component vs. duplicated per-page

**Decision**: Extract a single shared `BlueprintFilters` component (in `features/blueprints/components/`) and a single `useBlueprintFilters` hook used by both pages.

**Rationale**: Both pages are identical in filtering behaviour (FR-009). Constitution Principle VI requires shared components when two features need the same behaviour. A single component reduces duplication and ensures consistent behaviour across My Blueprints and Org Blueprints.

---

## Decision 6: Frontend type source

**Decision**: Update the hand-written types in `blueprintsApi.ts` (adding `subtype: string | null` to `MyBlueprintListItem` and `OrgBlueprintListItem`). OpenAPI code generation is not yet set up for this project; types are maintained manually.

**Rationale**: The project currently hand-writes frontend API types. Adding one field to two existing interfaces is trivial and consistent with the existing approach.
