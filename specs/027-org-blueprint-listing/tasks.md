# Tasks: Org Blueprint Listing

**Input**: Design documents from `specs/027-org-blueprint-listing/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/openapi.yaml ✅

**Tests**: Included — constitution mandates TDD (Principle II: NON-NEGOTIABLE). Write tests first, confirm they fail, then implement.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on other in-progress tasks)
- **[Story]**: Which user story this task belongs to (US1–US3)

---

## Phase 1: Setup

**Purpose**: Create the new Application layer feature folders

- [X] T001 Create Application layer folders `backend/src/NajaEcho.Application/Features/Blueprints/GetOrgBlueprints/` and `backend/src/NajaEcho.Application/Features/Blueprints/GetOrgBlueprintDetail/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: New DTOs, interface, queries, and API contract records — shared by all user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 Add `OrgBlueprintListItemDto` record (BlueprintId, ProductName?, Type?, IngredientCount) to `backend/src/NajaEcho.Application/Features/Blueprints/GetOrgBlueprints/OrgBlueprintListItemDto.cs`
- [X] T003 [P] Add `OrgBlueprintOwnerDto` record (UserId, DisplayName) and `OrgBlueprintDetailDto` record (BlueprintId, ProductName?, Type?, CraftTimeSeconds?, IngredientCount, Slots: IReadOnlyList<BlueprintSlotDto>, Owners: IReadOnlyList<OrgBlueprintOwnerDto>) to `backend/src/NajaEcho.Application/Features/Blueprints/GetOrgBlueprintDetail/OrgBlueprintDetailDto.cs`
- [X] T004 [P] Add `IOrgBlueprintRepository` interface to `backend/src/NajaEcho.Application/Abstractions/IOrgBlueprintRepository.cs` with two methods: `GetListAsync(Guid userId, CancellationToken ct)` returning `Task<IReadOnlyList<OrgBlueprintListItemDto>>` and `GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct)` returning `Task<OrgBlueprintDetailDto?>`
- [X] T005 [P] Add `GetOrgBlueprintsQuery` record (Guid UserId) to `backend/src/NajaEcho.Application/Features/Blueprints/GetOrgBlueprints/GetOrgBlueprintsQuery.cs`
- [X] T006 [P] Add `GetOrgBlueprintDetailQuery` record (Guid UserId, Guid BlueprintId) to `backend/src/NajaEcho.Application/Features/Blueprints/GetOrgBlueprintDetail/GetOrgBlueprintDetailQuery.cs`
- [X] T007 [P] Add three new contract records to `backend/src/NajaEcho.Api/Features/Blueprints/Contracts/BlueprintContracts.cs`: `OrgBlueprintListItemResponse` (BlueprintId, ProductName?, Type?, IngredientCount), `BlueprintOwnerResponse` (UserId, DisplayName), `OrgBlueprintDetailResponse` (BlueprintId, ProductName?, Type?, CraftTimeSeconds?, IngredientCount, Slots: IReadOnlyList<BlueprintSlotResponse>, Owners: IReadOnlyList<BlueprintOwnerResponse>)

**Checkpoint**: All shared types and interfaces defined — user story phases can now begin.

---

## Phase 3: User Story 1 — Org Blueprint Listing Page (Priority: P1) 🎯 MVP

**Goal**: An authenticated user navigates to `/blueprints/org` and sees a deduplicated table of all blueprints held by any org member. The page is accessible from the navigation. No blueprint held by any org member is missing; no blueprint appears more than once.

**Independent Test**: Navigate to `/blueprints/org` — table loads with all org blueprints (Blueprint, Type, Ingredients columns). Navigate there unauthenticated — redirected to login.

### Tests for User Story 1

> **Write these tests FIRST — confirm they FAIL before implementation**

- [X] T008 [P] [US1] Write `backend/tests/NajaEcho.Application.Tests/Features/Blueprints/GetOrgBlueprintsHandlerTests.cs`: (a) returns empty list when org has no blueprints; (b) returns deduplicated list when two users share a blueprint; (c) only returns blueprints from the current user's org, not from other orgs
- [X] T009 [P] [US1] Extend `backend/tests/NajaEcho.Api.Tests/Features/Blueprints/BlueprintEndpointsTests.cs` with `GET /api/blueprints/org` cases: (a) 401 when unauthenticated; (b) 200 with correct `OrgBlueprintListResponse` shape including blueprints array
- [X] T010 [P] [US1] Write `frontend/src/features/blueprints/__tests__/OrgBlueprintsPage.test.tsx`: (a) renders a table of blueprints returned by the API; (b) shows empty-state message when API returns no blueprints; (c) clicking a blueprint row opens a panel (panel can be a stub for now)

### Implementation for User Story 1

- [X] T011 [US1] Implement `GetOrgBlueprintsHandler` in `backend/src/NajaEcho.Application/Features/Blueprints/GetOrgBlueprints/GetOrgBlueprintsHandler.cs`: accepts `GetOrgBlueprintsQuery`, calls `IOrgBlueprintRepository.GetListAsync(userId, ct)`, returns the list; register handler in `backend/src/NajaEcho.Infrastructure/DependencyInjection.cs`
- [X] T012 [US1] Create `backend/src/NajaEcho.Infrastructure/Blueprints/OrgBlueprintRepository.cs` implementing `IOrgBlueprintRepository`; implement `GetListAsync` using raw SQL joining `organization_memberships` (is_current=true, orgId resolved via sub-query from userId) → `user_blueprints` → `sc.blueprints` → `sc.blueprint_tiers` (tier_index=0) → `sc.blueprint_slot_options`; GROUP BY b.id to deduplicate; ORDER BY product_name NULLS LAST; register `IOrgBlueprintRepository` → `OrgBlueprintRepository` in DependencyInjection.cs
- [X] T013 [US1] Add `GET /api/blueprints/org` endpoint to `backend/src/NajaEcho.Api/Features/Blueprints/BlueprintEndpoints.cs`: require authorization, extract userId via `TryGetUserId`, dispatch `GetOrgBlueprintsQuery`, return 200 with `{ blueprints: [...OrgBlueprintListItemResponse] }`
- [X] T014 [P] [US1] Add `OrgBlueprintListItem` interface and `getOrgBlueprints()` function to `frontend/src/features/blueprints/api/blueprintsApi.ts`: fetch `GET /api/blueprints/org`, return `{ blueprints: OrgBlueprintListItem[] }`; add `org` key to `blueprintKeys` in `frontend/src/features/blueprints/hooks/useOrgBlueprints.ts` (or a shared keys file)
- [X] T015 [P] [US1] Implement `useOrgBlueprints()` hook in `frontend/src/features/blueprints/hooks/useOrgBlueprints.ts`: TanStack Query with key `['blueprints', 'org']`, calls `getOrgBlueprints()`
- [X] T016 [US1] Create `frontend/src/features/blueprints/pages/OrgBlueprintsPage.tsx`: mirrors `MyBlueprintsPage.tsx`; uses `useOrgBlueprints()`; renders table with columns Blueprint / Type / Ingredients; each row clickable (`onClick` sets `selectedBlueprintId`); renders `<OrgBlueprintDetailPanel blueprintId={selectedBlueprintId} onClose={...} />` (stub panel acceptable here); shows "No blueprints found in your org." empty state
- [X] T017 [P] [US1] Add `/blueprints/org` route to `frontend/src/routes/AppRouter.tsx` pointing to `<OrgBlueprintsPage />`
- [X] T018 [P] [US1] Add "Org Blueprints" nav item to `frontend/src/features/dashboard/navigation/navItems.ts` in the same Blueprints group as Personal Blueprints, path `/blueprints/org`

**Checkpoint**: Navigate to `/blueprints/org` — table of org blueprints loads. All T008–T018 tests pass.

---

## Phase 4: User Story 2 — Blueprint Detail Panel (Priority: P1)

**Goal**: Clicking any row in the Org Blueprint listing opens a slide-in detail panel showing the blueprint name, a summary row (Type / Craft Time / Ingredient Count), and the ingredient listing. No Remove button. Panel closes on X or click-outside.

**Independent Test**: Click a blueprint row — panel slides in showing name, type, craft time, ingredient count, and ingredient list. No Remove button visible. X and click-outside close the panel.

### Tests for User Story 2

> **Write these tests FIRST — confirm they FAIL before implementation**

- [X] T019 [P] [US2] Write `backend/tests/NajaEcho.Application.Tests/Features/Blueprints/GetOrgBlueprintDetailHandlerTests.cs`: (a) returns null when no org member has the blueprint; (b) returns correct ProductName, Type, CraftTimeSeconds, IngredientCount, and Slots for a blueprint held by an org member; (c) returns empty Slots list when blueprint has no slot_options; (d) returns Owners list with correct DisplayName for each org member who has it
- [X] T020 [P] [US2] Extend `backend/tests/NajaEcho.Api.Tests/Features/Blueprints/BlueprintEndpointsTests.cs` with `GET /api/blueprints/org/{blueprintId}` cases: (a) 401 when unauthenticated; (b) 404 when no org member has the blueprint; (c) 200 with correct `OrgBlueprintDetailResponse` shape (includes slots and owners arrays)
- [X] T021 [P] [US2] Write `frontend/src/features/blueprints/__tests__/OrgBlueprintDetailPanel.test.tsx` summary section: (a) shows blueprint name in SheetTitle; (b) shows Type / Craft Time / Ingredients labels and values; (c) shows "—" for null type and null craftTimeSeconds; (d) formats craftTimeSeconds=330 as "5m 30s"; (e) no Remove button visible

### Implementation for User Story 2

- [X] T022 [US2] Implement `GetOrgBlueprintDetailHandler` in `backend/src/NajaEcho.Application/Features/Blueprints/GetOrgBlueprintDetail/GetOrgBlueprintDetailHandler.cs`: accepts `GetOrgBlueprintDetailQuery`, calls `IOrgBlueprintRepository.GetDetailAsync(userId, blueprintId, ct)`, returns `OrgBlueprintDetailDto?`; register in `backend/src/NajaEcho.Infrastructure/DependencyInjection.cs`
- [X] T023 [US2] Implement `GetDetailAsync` in `backend/src/NajaEcho.Infrastructure/Blueprints/OrgBlueprintRepository.cs` using three raw SQL queries: (1) header query joining `sc.blueprints` → `user_blueprints` → `organization_memberships` (same org sub-query) → `sc.blueprint_tiers` → `sc.blueprint_slot_options` (WHERE b.id = blueprintId), returns null if no rows; (2) slot options query (same as personal detail — reuse pattern from UserBlueprintRepository); (3) owners query joining `user_blueprints` → `AspNetUsers` → `organization_memberships` (same org), returning UserId + CASE WHEN display_name ≠ '' THEN display_name ELSE discord_username END, ORDER BY display_name; group slot options into BlueprintSlotDto hierarchy in application code
- [X] T024 [US2] Add `GET /api/blueprints/org/{blueprintId:guid}` to `backend/src/NajaEcho.Api/Features/Blueprints/BlueprintEndpoints.cs`: require authorization, extract userId, dispatch `GetOrgBlueprintDetailQuery`, return 200 with `OrgBlueprintDetailResponse` (mapping Slots and Owners) or 404 if handler returns null
- [X] T025 [P] [US2] Add `BlueprintOwner`, `OrgBlueprintDetail` interfaces and `getOrgBlueprintDetail(blueprintId: string)` function to `frontend/src/features/blueprints/api/blueprintsApi.ts`: fetch `GET /api/blueprints/org/{blueprintId}`, return `OrgBlueprintDetail` (blueprintId, productName, type, craftTimeSeconds, ingredientCount, slots, owners)
- [X] T026 [P] [US2] Implement `useGetOrgBlueprintDetail(blueprintId: string | null)` hook in `frontend/src/features/blueprints/hooks/useGetOrgBlueprintDetail.ts`: TanStack Query with key `['blueprints', 'org-detail', blueprintId]`; `enabled` only when blueprintId is non-null
- [X] T027 [US2] Create `frontend/src/features/blueprints/components/OrgBlueprintDetailPanel.tsx`: Sheet with `side="right"`; calls `useGetOrgBlueprintDetail(blueprintId)`; SheetTitle shows `data.productName ?? '—'`; three-column summary row (Type / Craft Time formatted as "Xm Ys" / Ingredients count); divider; ingredient listing (same two-level slot/option display as BlueprintDetailPanel); NO Remove button or confirmation state; owners section placeholder (renders nothing — filled in US3)
- [X] T028 [US2] Update `frontend/src/features/blueprints/pages/OrgBlueprintsPage.tsx`: replace stub panel import with real `OrgBlueprintDetailPanel`; ensure `selectedBlueprintId` state and panel props are wired correctly

**Checkpoint**: Clicking a row opens a full detail panel with ingredient list and no Remove button. All T019–T028 tests pass.

---

## Phase 5: User Story 3 — Blueprint Owners List (Priority: P2)

**Goal**: Below the ingredient listing, the detail panel shows a "Who has this blueprint?" section listing the display names of all org members who have it in their personal collection. If no members currently hold it, a placeholder message is shown.

**Independent Test**: Open the detail panel for a blueprint held by two org members — both names appear in the owners section. Open for a blueprint held by no one — placeholder shown.

### Tests for User Story 3

> **Write these tests FIRST — confirm they FAIL before implementation**

- [X] T029 [US3] Extend `frontend/src/features/blueprints/__tests__/OrgBlueprintDetailPanel.test.tsx` with owners section tests: (a) renders "Who has this blueprint?" heading and each owner's displayName; (b) shows placeholder text when owners array is empty

### Implementation for User Story 3

- [X] T030 [US3] Add owners section to `frontend/src/features/blueprints/components/OrgBlueprintDetailPanel.tsx`: below the ingredient listing, add a divider and a section with heading "Who has this blueprint?"; map `data.owners` to a list of `<p>{owner.displayName}</p>` entries styled as `text-sm text-muted-foreground`; if `data.owners` is empty, render `<p className="text-sm text-muted-foreground">No members currently have this blueprint.</p>`

**Checkpoint**: Owners section renders correctly. All T029–T030 tests pass.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T031 [P] Run `quickstart.md` validation end-to-end: verify org listing loads, deduplication works, panel opens with correct data, ingredient list renders, owners list shows correct members, unauthenticated 401 cases
- [X] T032 [P] Code review pass: verify Clean Architecture boundaries in new handlers; confirm `TryGetUserId` used in both new endpoints; confirm org sub-query correctly scoped; confirm `OrgBlueprintRepository` registered in DI; confirm no Remove button present in OrgBlueprintDetailPanel

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (US1)**: Depends on Phase 2 — backend + frontend listing
- **Phase 4 (US2)**: Depends on Phase 2 and Phase 3 (panel must be wired into the page) — full stack detail
- **Phase 5 (US3)**: Depends on Phase 4 (owners data comes from the same detail endpoint) — frontend only
- **Phase 6 (Polish)**: Depends on all story phases complete

### User Story Dependencies

- **US1 (Listing page)**: Depends only on Foundational — can start after Phase 2
- **US2 (Detail panel)**: Backend independent after Foundational; frontend wiring requires US1 page to exist
- **US3 (Owners list)**: Frontend-only change; depends on US2 backend returning owners in response

### Parallel Opportunities

- T003, T004, T005, T006, T007 (Foundational types) — parallel after T002
- T008, T009, T010 (US1 tests) — parallel
- T014, T015, T017, T018 (US1 frontend hooks/route/nav) — parallel after T013
- T019, T020, T021 (US2 tests) — parallel
- T025, T026 (US2 frontend hook + API) — parallel after T024
- T031, T032 (polish) — parallel

---

## Parallel Example: User Story 2

```bash
# Write all US2 tests together (Red phase):
T019: GetOrgBlueprintDetailHandlerTests
T020: BlueprintEndpointsTests GET /org/{blueprintId} cases
T021: OrgBlueprintDetailPanel.test.tsx summary section

# After backend handler + endpoint green, wire frontend:
T025: getOrgBlueprintDetail() in blueprintsApi.ts
T026: useGetOrgBlueprintDetail hook
```

---

## Implementation Strategy

### MVP First (US1 only = browsable org listing)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (all shared types)
3. Complete Phase 3: US1 — listing page with route and nav
4. **STOP and VALIDATE**: Navigate to `/blueprints/org`, see all org blueprints in table
5. Add Phase 4 (US2): Detail panel with ingredient list
6. Add Phase 5 (US3): Owners section in panel

### Incremental Delivery

1. Setup + Foundational → shared types ready
2. US1 → org listing visible to all authenticated users
3. US2 → clickable rows open a read-only detail panel
4. US3 → owners section reveals who can craft each item
5. Polish → quickstart validated

---

## Notes

- No EF Core migration required — all queries use existing tables
- `craftTimeSeconds` formatting: `${Math.floor(s / 60)}m ${s % 60}s`; null → "—"
- Org scoping: both repository methods accept `userId` and resolve `organizationId` internally via `SELECT organization_id FROM organization_memberships WHERE user_id = {userId} AND is_current = true LIMIT 1`
- Owner display name: `CASE WHEN display_name <> '' THEN display_name ELSE discord_username END`
- `BlueprintSlotResponse` and `BlueprintSlotOptionResponse` already exist in `BlueprintContracts.cs` — reuse them in `OrgBlueprintDetailResponse`
- `BlueprintSlotDto` and `BlueprintSlotOptionDto` already exist from feature 026 — reuse in `OrgBlueprintDetailDto`
- Sheet component already in repo at `frontend/src/components/ui/sheet.tsx` — no new dependencies
- Constitution Principle II is NON-NEGOTIABLE: all tests must be written and confirmed FAILING before implementation begins
