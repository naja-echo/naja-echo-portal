# Implementation Plan: Personal Blueprint Manager

**Branch**: `025-personal-blueprint-manager` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/025-personal-blueprint-manager/spec.md`

## Summary

Add a personal blueprint manager accessible from the sidebar navigation. Members can search the catalog by product name using an autosuggest modal and save blueprints to a personal list. The list is displayed in a three-column table showing Blueprint (product_name), Type, and Ingredients (distinct slot count at tier 0). Duplicate additions are prevented at the database level. Backend adds a new `user_blueprints` join table and three new member-accessible endpoints; frontend adds a new page, route, nav item, and add-blueprint dialog following the established `AddInventoryDialog` pattern.

## Technical Context

**Language/Version**: C# / .NET 8 (backend); TypeScript / React (frontend)

**Primary Dependencies**: ASP.NET Core Web API, EF Core 8 + Npgsql, TanStack Query, shadcn/ui (`Command` component), React Hook Form + Zod

**Storage**: PostgreSQL — new `user_blueprints` table (default schema); reads from `sc.blueprints`, `sc.blueprint_tiers`, `sc.blueprint_slot_options`

**Testing**: xUnit + Testcontainers (backend); Vitest + React Testing Library + MSW (frontend)

**Target Platform**: Web — authenticated dashboard shell

**Project Type**: Web application (ASP.NET Core API + React SPA)

**Performance Goals**: Search autosuggest returns within 500ms; blueprint list loads within 2s

**Constraints**: Max 20 autosuggest results per query; no pagination required for personal list (scope is personal, not org-wide)

**Scale/Scope**: Single-user personal list; org-wide features are out of scope

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. API-Contract-First | PASS | OpenAPI contract defined at `contracts/openapi.yaml` before implementation |
| II. Test-First / TDD | PASS | Unit + integration tests required per tasks |
| III. Frontend/Backend Separation | PASS | Frontend consumes generated API types only; no direct DB access |
| IV. Simplicity / YAGNI | PASS | No pagination, no delete, no sharing — only what the spec requires |
| V. Observability | PASS | New endpoints emit structured logs and carry correlation IDs via existing middleware |
| VI. Modular Monolith + Clean Architecture | PASS | New feature folders follow Domain → Application → Infrastructure → API dependency direction; frontend uses feature folder `features/blueprints/` |

**Post-design re-check**: All principles hold. No violations.

## Project Structure

### Documentation (this feature)

```text
specs/025-personal-blueprint-manager/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── openapi.yaml     # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code

**Backend**

```text
backend/src/NajaEcho.Domain/Blueprints/
└── UserBlueprint.cs                         # New domain entity

backend/src/NajaEcho.Application/Features/Blueprints/
├── SearchBlueprints/
│   ├── SearchBlueprintsQuery.cs
│   ├── SearchBlueprintsHandler.cs
│   └── BlueprintSearchResultDto.cs
├── GetMyBlueprints/
│   ├── GetMyBlueprintsQuery.cs
│   ├── GetMyBlueprintsHandler.cs
│   └── MyBlueprintListItemDto.cs
└── AddMyBlueprint/
    ├── AddMyBlueprintCommand.cs
    ├── AddMyBlueprintHandler.cs
    └── AddMyBlueprintValidator.cs

backend/src/NajaEcho.Infrastructure/
├── Blueprints/
│   └── UserBlueprintRepository.cs           # Implements IUserBlueprintRepository
└── Persistence/
    ├── Configurations/
    │   └── UserBlueprintConfiguration.cs    # EF Core table mapping
    └── Migrations/
        └── <timestamp>_AddUserBlueprints.cs

backend/src/NajaEcho.Api/Features/Blueprints/
├── BlueprintEndpoints.cs                    # /api/blueprints/search + /api/blueprints/mine
└── Contracts/
    └── BlueprintContracts.cs                # Request/response records

backend/tests/NajaEcho.Application.Tests/Features/Blueprints/
├── SearchBlueprintsHandlerTests.cs
├── GetMyBlueprintsHandlerTests.cs
└── AddMyBlueprintHandlerTests.cs

backend/tests/NajaEcho.Api.Tests/Features/Blueprints/
└── BlueprintEndpointsTests.cs               # Integration tests (Testcontainers)
```

**Frontend**

```text
frontend/src/features/blueprints/
├── api/
│   └── blueprintsApi.ts                     # searchBlueprints, getMyBlueprints, addMyBlueprint
├── hooks/
│   ├── useBlueprintSearch.ts                # Debounced autosuggest query hook
│   ├── useMyBlueprints.ts                   # TanStack Query — GET /api/blueprints/mine
│   └── useAddMyBlueprint.ts                 # TanStack mutation — POST /api/blueprints/mine
├── components/
│   └── AddBlueprintDialog.tsx               # Modal with Command autosuggest (follows AddInventoryDialog pattern)
├── pages/
│   └── MyBlueprintsPage.tsx                 # Route component: listing table + Add Blueprint button
└── __tests__/
    ├── AddBlueprintDialog.test.tsx
    └── MyBlueprintsPage.test.tsx

frontend/src/features/dashboard/navigation/
└── navItems.ts                              # Add "My Blueprints" entry (Blueprints group)

frontend/src/app/
└── router.tsx (or routes file)              # Add /blueprints/mine route
```

**Structure Decision**: Web application (backend + frontend). Backend follows the four-layer Clean Architecture with feature folders. Frontend follows the established `features/<name>/` folder structure.

## Implementation Sequence

### Step 1 — OpenAPI contract (already done)

`contracts/openapi.yaml` defines all three endpoints before any implementation code is written. ✅

### Step 2 — Backend: Domain entity + migration

1. `UserBlueprint.cs` — domain entity with `Id`, `UserId`, `BlueprintId`, `AddedAt`
2. `UserBlueprintConfiguration.cs` — maps to `user_blueprints` table (default schema), unique constraint on `(user_id, blueprint_id)`, index on `user_id`, FK to `characters` and `sc.blueprints`
3. Migration: `AddUserBlueprints`

### Step 3 — Backend: Application layer

For each use case:

**SearchBlueprints**
- Query: `SearchBlueprintsQuery(string Term)`
- Handler: calls `IBlueprintRepository.SearchAsync(term, ct)` — ILIKE on `product_name`, limit 20, non-null `product_name` only
- DTO: `BlueprintSearchResultDto(Guid BlueprintId, string ProductName, string? Type)`

**GetMyBlueprints**
- Query: `GetMyBlueprintsQuery(Guid UserId)`
- Handler: calls `IUserBlueprintRepository.GetListAsync(userId, ct)` — joins `user_blueprints` → `sc.blueprints` → `sc.blueprint_tiers` (tier 0) → `sc.blueprint_slot_options`, `COUNT(DISTINCT slot_index)` for ingredient count
- DTO: `MyBlueprintListItemDto(Guid BlueprintId, string? ProductName, string? Type, int IngredientCount)`

**AddMyBlueprint**
- Command: `AddMyBlueprintCommand(Guid UserId, Guid BlueprintId)`
- Validator: `BlueprintId` required, non-empty GUID
- Handler: verifies blueprint exists in catalog, calls `IUserBlueprintRepository.AddAsync(...)`, catches unique constraint violation → throws `DuplicateBlueprintException` (→ 409)

### Step 4 — Backend: Infrastructure + API

- `UserBlueprintRepository.cs` — implements `IUserBlueprintRepository` using EF Core
- `BlueprintEndpoints.cs` — maps routes, extracts `userId` via `TryGetUserId`, requires `RequireAuthorization()`
- `BlueprintContracts.cs` — request/response records matching the OpenAPI contract

### Step 5 — Frontend: API client + hooks

- `blueprintsApi.ts` — three typed API functions using fetch/generated types
- `useBlueprintSearch.ts` — `useQuery` with 300ms debounce on the search term; disabled when term is empty
- `useMyBlueprints.ts` — `useQuery(['blueprints', 'mine'])`
- `useAddMyBlueprint.ts` — `useMutation` with cache invalidation of `['blueprints', 'mine']` on success

### Step 6 — Frontend: Components + page

- `AddBlueprintDialog.tsx` — shadcn `Dialog` wrapping a shadcn `Command` autosuggest input. Submit button disabled until a blueprint is selected. On 409 response, displays inline "Already in your list" message. On success, closes modal.
- `MyBlueprintsPage.tsx` — renders three-column table (Blueprint, Type, Ingredients). Empty state when list is empty. "Add Blueprint" button opens `AddBlueprintDialog`.

### Step 7 — Frontend: Navigation + routing

- Add `{ label: 'My Blueprints', path: '/blueprints/mine', icon: ScrollText, group: 'Blueprints' }` to `navItems.ts`
- Wire `/blueprints/mine` route to `MyBlueprintsPage` inside the authenticated shell

### Step 8 — Tests

**Backend unit tests** (per handler):
- `SearchBlueprintsHandlerTests` — returns matching results, empty term returns empty list
- `GetMyBlueprintsHandlerTests` — returns user's blueprints with correct ingredient counts
- `AddMyBlueprintHandlerTests` — adds successfully, throws on duplicate, throws on unknown blueprint

**Backend integration test** (Testcontainers):
- `BlueprintEndpointsTests` — exercises all three endpoints against a real database; verifies 201, 409, 404, 401 responses

**Frontend tests**:
- `AddBlueprintDialog.test.tsx` — MSW mocks for search and add; verifies button disabled until selection; verifies duplicate message on 409
- `MyBlueprintsPage.test.tsx` — empty state renders; populated state renders table with correct columns

## Key Design Decisions

| Decision | Choice | Reason |
|----------|--------|--------|
| Ingredient count basis | Distinct slot_index at tier 0 | Tier 0 is the base crafting tier; options are alternatives, not additive |
| Duplicate prevention | DB unique constraint + 409 | Race-condition-safe; clear HTTP semantics |
| Autosuggest search field | `product_name` ILIKE only | Spec scope; other fields out of scope |
| Result limit | 20 | Practical UX ceiling for an autosuggest list |
| Schema for user_blueprints | Default (public) | Consistent with all other user-owned tables |
| No delete endpoint | Omitted | Not in spec; YAGNI |
