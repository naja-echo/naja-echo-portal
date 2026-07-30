# Implementation Plan: Blueprint Filtering

**Branch**: `028-blueprint-filtering` | **Date**: 2026-07-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/028-blueprint-filtering/spec.md`

## Summary

Add Category, Subcategory, and name search filters to both the My Blueprints and Org Blueprints pages. Filtering is performed client-side on the already-fetched list. The `subtype` field (subcategory) is already stored in `sc.blueprints` but is not currently returned by the list endpoints — both list response shapes must be updated. No new endpoints, no migrations.

## Technical Context

**Language/Version**: C# on .NET 10 (backend) / TypeScript with React (frontend)

**Primary Dependencies**: ASP.NET Core Web API, EF Core + Npgsql, TanStack Query, shadcn/ui Combobox (already in repo)

**Storage**: PostgreSQL — existing `sc.blueprints` table; `subtype` column already present and mapped

**Testing**: xUnit + FluentAssertions + WebApplicationFactory (backend); Vitest + React Testing Library + MSW (frontend)

**Target Platform**: Web (browser SPA + .NET API)

**Project Type**: Full-stack web application (modular monolith)

**Performance Goals**: Client-side filtering; no additional API calls on filter change. Standard web performance expectations.

**Constraints**: No new endpoints; no migrations. Additive-only API change (`subtype` added to existing responses).

**Scale/Scope**: Hundreds of blueprints per user/org; client-side filtering is appropriate at this scale.

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. API-Contract-First | ✅ PASS | Updated OpenAPI contract in `contracts/openapi.yaml` defined before implementation; `subtype` added to both list item schemas |
| II. Test-First / TDD | ✅ PASS | Tests written before implementation per tasks.md |
| III. Frontend/Backend Separation | ✅ PASS | SPA consumes API; `subtype` typed from contract, not hand-inferred |
| IV. Simplicity / YAGNI | ✅ PASS | One new component, one new hook; no new endpoints, no new abstractions beyond current need |
| V. Observability | ✅ PASS | No new endpoints; inherits existing structured logging and correlation ID middleware |
| VI. Clean Architecture | ✅ PASS | Changes follow existing feature-folder structure; DTO and repository changes stay in their layers |

**API contract changes required**: Yes — `subtype` (nullable string) added to `MyBlueprintListItem` and `OrgBlueprintListItem` response schemas. This is an additive, non-breaking change.

## Project Structure

### Documentation (this feature)

```text
specs/028-blueprint-filtering/
├── plan.md              # This file
├── research.md          # Phase 0 — research decisions
├── data-model.md        # Phase 1 — data shape changes
├── quickstart.md        # Phase 1 — end-to-end validation guide
├── contracts/
│   └── openapi.yaml     # Updated API contract (two list endpoints)
└── tasks.md             # Phase 2 output (/speckit-tasks — not yet generated)
```

### Backend Source Code

```text
backend/src/NajaEcho.Application/
└── Features/Blueprints/
    ├── GetMyBlueprints/
    │   └── MyBlueprintListItemDto.cs         CHANGE: add Subtype property
    └── GetOrgBlueprints/
        └── OrgBlueprintListItemDto.cs        CHANGE: add Subtype property

backend/src/NajaEcho.Infrastructure/
└── Blueprints/
    ├── UserBlueprintRepository.cs            CHANGE: add b.subtype to ListRow + SELECT + GROUP BY
    └── OrgBlueprintRepository.cs            CHANGE: add b.subtype to ListRow + SELECT + GROUP BY

backend/src/NajaEcho.Api/
└── Features/Blueprints/
    └── Contracts/
        └── BlueprintContracts.cs             CHANGE: add Subtype to MyBlueprintListItemResponse + OrgBlueprintListItemResponse
```

### Frontend Source Code

```text
frontend/src/features/blueprints/
├── api/
│   └── blueprintsApi.ts                     CHANGE: add subtype to MyBlueprintListItem + OrgBlueprintListItem
├── components/
│   └── BlueprintFilters.tsx                 NEW: name input + Category Combobox + Subcategory Combobox
├── hooks/
│   └── useBlueprintFilters.ts               NEW: filter state + derived filtered list + derived options
└── pages/
    ├── MyBlueprintsPage.tsx                 CHANGE: integrate BlueprintFilters + useBlueprintFilters
    └── OrgBlueprintsPage.tsx               CHANGE: integrate BlueprintFilters + useBlueprintFilters

frontend/src/features/blueprints/__tests__/
├── BlueprintFilters.test.tsx               NEW: component rendering + filter interaction tests
├── MyBlueprintsPage.test.tsx              CHANGE: extend with filter scenario tests
└── OrgBlueprintsPage.test.tsx            CHANGE: extend with filter scenario tests
```

## Design Details

### `useBlueprintFilters` hook

Accepts the full list and returns filter state, setters, derived options, and filtered list:

```ts
interface BlueprintFilterState {
  name: string
  category: string
  subcategory: string
}

interface UseBlueprintFiltersResult<T extends { productName: string | null; type: string | null; subtype: string | null }> {
  filters: BlueprintFilterState
  setName: (v: string) => void
  setCategory: (v: string) => void
  setSubcategory: (v: string) => void
  categoryOptions: ComboboxOption[]
  subcategoryOptions: ComboboxOption[]
  filtered: T[]
}
```

- `categoryOptions`: distinct non-null `type` values from the full list, sorted alphabetically.
- `subcategoryOptions`: when `category` is empty → distinct non-null `subtype` values from the full list; when `category` is set → distinct non-null `subtype` values from blueprints with matching `type`. Sorted alphabetically.
- `filtered`: blueprints satisfying all three predicates:
  1. `name` is empty OR `productName?.toLowerCase().includes(name.toLowerCase())`
  2. `category` is empty OR `type === category`
  3. `subcategory` is empty OR `subtype === subcategory`

### `BlueprintFilters` component

```ts
interface BlueprintFiltersProps {
  name: string
  category: string
  subcategory: string
  categoryOptions: ComboboxOption[]
  subcategoryOptions: ComboboxOption[]
  onNameChange: (v: string) => void
  onCategoryChange: (v: string) => void
  onSubcategoryChange: (v: string) => void
}
```

Renders:
- A plain `<input>` for name search (same pattern as `ShipComponentsFilters`)
- A `Combobox` for Category (placeholder: "All categories")
- A `Combobox` for Subcategory (placeholder: "All subcategories"; always rendered and active)

### MSW handlers for tests

Tests for the pages need MSW handlers that return `subtype` in the mocked list responses. Existing handlers in the test files must be updated to include `subtype: string | null`.

## Backend Change Details

### `MyBlueprintListItemDto.cs`

```csharp
// Before:
public sealed record MyBlueprintListItemDto(
    Guid BlueprintId, string? ProductName, string? Type, int IngredientCount);

// After:
public sealed record MyBlueprintListItemDto(
    Guid BlueprintId, string? ProductName, string? Type, string? Subtype, int IngredientCount);
```

### `OrgBlueprintListItemDto.cs`

Same change — add `string? Subtype` parameter.

### `UserBlueprintRepository.cs` — `GetListAsync`

```csharp
// ListRow: add Subtype
private sealed record ListRow(Guid BlueprintId, string? ProductName, string? Type, string? Subtype, int IngredientCount);

// SQL: add to SELECT and GROUP BY
b.subtype AS subtype
// GROUP BY: add b.subtype

// Projection:
new MyBlueprintListItemDto(r.BlueprintId, r.ProductName, r.Type, r.Subtype, r.IngredientCount)
```

### `OrgBlueprintRepository.cs` — `GetListAsync`

Same changes as `UserBlueprintRepository`.

### `BlueprintContracts.cs`

```csharp
// Before:
public sealed record MyBlueprintListItemResponse(
    Guid BlueprintId, string? ProductName, string? Type, int IngredientCount);

// After:
public sealed record MyBlueprintListItemResponse(
    Guid BlueprintId, string? ProductName, string? Type, string? Subtype, int IngredientCount);

// Same for OrgBlueprintListItemResponse
```

### Endpoint mapping

`BlueprintEndpoints.cs` maps DTOs to response records. After the record parameters are updated, the mapping call must pass `Subtype`:
```csharp
new MyBlueprintListItemResponse(dto.BlueprintId, dto.ProductName, dto.Type, dto.Subtype, dto.IngredientCount)
```

(Same for org list endpoint.)

## Testing Strategy

### Backend

- **Unit tests**: None required — the DTO and contract changes are pure data shape; logic is in the repository SQL.
- **Integration tests** (extend existing or add new):
  - `GET /api/blueprints/mine` response includes `subtype` field for each item.
  - `GET /api/blueprints/org` response includes `subtype` field for each item.
  - Both tests verify that `subtype` value matches what's in the seeded `sc.blueprints` row.

### Frontend

- **`useBlueprintFilters` tests** (Vitest):
  - Returns all items when no filters active.
  - Filters by name (case-insensitive substring).
  - Filters by category.
  - Filters by subcategory without category.
  - Filters by category + subcategory together.
  - `categoryOptions` derived correctly (distinct, sorted, no nulls).
  - `subcategoryOptions` shows all when no category; narrows when category selected.
  - Clearing category does NOT auto-clear subcategory; subcategory options expand to full set.

- **`BlueprintFilters` component tests** (RTL):
  - Renders name input, Category combobox, Subcategory combobox.
  - Name input calls `onNameChange` on change.
  - Category selection calls `onCategoryChange`.
  - Subcategory selection calls `onSubcategoryChange`.
  - Subcategory combobox is rendered and interactive when category is empty.

- **Page tests** (extend existing):
  - MSW handlers updated to return `subtype` in mocked responses.
  - `MyBlueprintsPage`: filter bar visible; filtering by category shows correct subset.
  - `OrgBlueprintsPage`: same scenarios.
