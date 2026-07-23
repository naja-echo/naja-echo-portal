# Tasks: Blueprint Detail Panel

**Input**: Design documents from `specs/026-blueprint-detail-panel/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/openapi.yaml ✅

**Tests**: Included — constitution mandates TDD (Principle II: NON-NEGOTIABLE). Write tests first, confirm they fail, then implement.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on other in-progress tasks)
- **[Story]**: Which user story this task belongs to (US1–US4)

---

## Phase 1: Setup

**Purpose**: Create the new feature folders for Application layer use cases

- [x] T001 Create Application layer folders `backend/src/NajaEcho.Application/Features/Blueprints/GetBlueprintDetail/` and `backend/src/NajaEcho.Application/Features/Blueprints/RemoveMyBlueprint/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: New DTOs, interface method signatures, and API contract records — shared dependencies for all user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 Add three DTO records to `backend/src/NajaEcho.Application/Features/Blueprints/GetBlueprintDetail/`: `BlueprintDetailDto` (BlueprintId, ProductName?, Type?, CraftTimeSeconds?, IngredientCount, Slots), `BlueprintSlotDto` (SlotIndex, SlotName, Options), `BlueprintSlotOptionDto` (OptionIndex, MaterialName, Kind, Quantity)
- [x] T003 [P] Add `GetBlueprintDetailQuery` record (Guid UserId, Guid BlueprintId) to `backend/src/NajaEcho.Application/Features/Blueprints/GetBlueprintDetail/GetBlueprintDetailQuery.cs`
- [x] T004 [P] Add two new method signatures to `backend/src/NajaEcho.Application/Abstractions/IUserBlueprintRepository.cs`: `GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct)` returning `Task<BlueprintDetailDto?>` and `RemoveAsync(Guid userId, Guid blueprintId, CancellationToken ct)` returning `Task<bool>`
- [x] T005 [P] Add three new contract records to `backend/src/NajaEcho.Api/Features/Blueprints/Contracts/BlueprintContracts.cs`: `BlueprintSlotOptionResponse` (OptionIndex, MaterialName, Kind, Quantity), `BlueprintSlotResponse` (SlotIndex, SlotName, Options), `BlueprintDetailResponse` (BlueprintId, ProductName?, Type?, CraftTimeSeconds?, IngredientCount, Slots)

**Checkpoint**: Foundational types defined — all four user story phases can now begin.

---

## Phase 3: User Story 1 — Route Rename + Clickable Rows + Panel Skeleton (Priority: P1) 🎯 MVP

**Goal**: Rename the frontend route to `/blueprints/personal`, add redirect from `/blueprints/mine`, make blueprint rows clickable, open a Sheet panel from the right, and allow it to close via X or click-outside. No detail content yet — just the panel lifecycle.

**Independent Test**: Navigate to `/blueprints/personal` — page loads. Navigate to `/blueprints/mine` — redirects to `/blueprints/personal`. Click a blueprint row — a Sheet panel slides in from the right with a close button. Click X or outside — panel closes.

### Tests for User Story 1

> **Write these tests FIRST — confirm they FAIL before implementation**

- [x] T006 [US1] Write additions to `frontend/src/features/blueprints/__tests__/MyBlueprintsPage.test.tsx`: (a) navigating to `/blueprints/mine` redirects to `/blueprints/personal`; (b) clicking a blueprint row opens the Sheet panel; (c) clicking the X button closes the panel

### Implementation for User Story 1

- [x] T007 [US1] Update `frontend/src/routes/AppRouter.tsx`: change the existing `/blueprints/mine` route to `/blueprints/personal` and add `<Route path="/blueprints/mine" element={<Navigate to="/blueprints/personal" replace />} />`
- [x] T008 [P] [US1] Update `frontend/src/features/dashboard/navigation/navItems.ts`: change the My Blueprints nav item `path` from `/blueprints/mine` to `/blueprints/personal`
- [x] T009 [US1] Create `frontend/src/features/blueprints/components/BlueprintDetailPanel.tsx` as a Sheet-based skeleton: accepts `blueprintId: string | null` and `onClose: () => void` props; renders shadcn `Sheet` with `side="right"` from `@/components/ui/sheet`; shows `SheetHeader` with `SheetTitle` displaying blueprint name placeholder; includes `SheetClose` X button; calls `onClose` on close/overlay-click; renders nothing when `blueprintId` is null
- [x] T010 [US1] Update `frontend/src/features/blueprints/pages/MyBlueprintsPage.tsx`: add `selectedBlueprintId` state (string | null, default null); make each `<tr>` row have `onClick={() => setSelectedBlueprintId(bp.blueprintId)}` and `className="cursor-pointer hover:bg-muted/50"`; render `<BlueprintDetailPanel blueprintId={selectedBlueprintId} onClose={() => setSelectedBlueprintId(null)} />` at the bottom of the component

**Checkpoint**: Route rename works, rows are clickable, Sheet opens and closes. All T006–T010 tests pass.

---

## Phase 4: User Story 2 — Blueprint Detail Summary (Priority: P1)

**Goal**: The detail panel fetches blueprint data and renders: the blueprint name at the top, followed by a three-column row showing Type / Craft Time / Ingredient Count with their values beneath each heading.

**Independent Test**: Click a blueprint row — the panel shows the correct product name, type (or "—"), craft time formatted as "Xm Ys" (or "—"), and ingredient count.

### Tests for User Story 2

> **Write these tests FIRST — confirm they FAIL before implementation**

- [x] T011 [P] [US2] Write `backend/tests/NajaEcho.Application.Tests/Features/Blueprints/GetBlueprintDetailHandlerTests.cs`: (a) returns null when user does not own the blueprint; (b) returns correct ProductName, Type, CraftTimeSeconds, IngredientCount for a valid user-owned blueprint; (c) returns empty Slots list when blueprint has no slot_options rows
- [x] T012 [P] [US2] Extend `backend/tests/NajaEcho.Api.Tests/Features/Blueprints/BlueprintEndpointsTests.cs` with GET `/{blueprintId}` cases: (a) 401 when unauthenticated; (b) 404 when blueprint not in user's list; (c) 200 with correct `BlueprintDetailResponse` shape when user owns the blueprint
- [x] T013 [P] [US2] Write `frontend/src/features/blueprints/__tests__/BlueprintDetailPanel.test.tsx` summary section: (a) shows blueprint name in the SheetTitle when API returns data; (b) shows Type/Craft Time/Ingredients row with correct values; (c) shows "—" for null type and null craftTimeSeconds; (d) formats craftTimeSeconds=330 as "5m 30s"

### Implementation for User Story 2

- [x] T014 [US2] Implement `GetBlueprintDetailHandler` in `backend/src/NajaEcho.Application/Features/Blueprints/GetBlueprintDetail/GetBlueprintDetailHandler.cs`: accepts `GetBlueprintDetailQuery`, calls `IUserBlueprintRepository.GetDetailAsync(userId, blueprintId, ct)`, returns the `BlueprintDetailDto` or null; register handler in `backend/src/NajaEcho.Infrastructure/DependencyInjection.cs`
- [x] T015 [US2] Implement `GetDetailAsync` in `backend/src/NajaEcho.Infrastructure/Blueprints/UserBlueprintRepository.cs` using raw SQL: (1) verify user_blueprints row exists for (userId, blueprintId) — return null if not; (2) query `sc.blueprints` for product_name, type; (3) query `sc.blueprint_tiers` for craft_time_seconds where tier_index=0; (4) query `sc.blueprint_slot_options` for all rows at that tier ordered by slot_index, option_index; (5) group by slot_index in application code to build `BlueprintSlotDto` list with nested `BlueprintSlotOptionDto`; (6) compute IngredientCount as count of distinct slot_index values
- [x] T016 [US2] Add `GET /api/blueprints/mine/{blueprintId}` to `backend/src/NajaEcho.Api/Features/Blueprints/BlueprintEndpoints.cs`: extract userId via `TryGetUserId`, require authorization, dispatch `GetBlueprintDetailQuery`, return 200 with `BlueprintDetailResponse` or 404 if handler returns null; map DTO to response including all slots and options
- [x] T017 [P] [US2] Add `getBlueprintDetail(blueprintId: string)` function to `frontend/src/features/blueprints/api/blueprintsApi.ts`: typed fetch to `GET /api/blueprints/mine/{blueprintId}`, returns the detail response object
- [x] T018 [P] [US2] Implement `useGetBlueprintDetail(blueprintId: string | null)` hook in `frontend/src/features/blueprints/hooks/useGetBlueprintDetail.ts`: TanStack Query with key `['blueprints', 'detail', blueprintId]`; `enabled` only when blueprintId is non-null
- [x] T019 [US2] Update `frontend/src/features/blueprints/components/BlueprintDetailPanel.tsx`: call `useGetBlueprintDetail(blueprintId)` inside the component; update `SheetTitle` to show `data.productName ?? '—'`; add a three-column summary row below the title showing "Type" / "Craft Time" / "Ingredients" labels with their values beneath — format `craftTimeSeconds` as `Xm Ys` (e.g., 330 → "5m 30s"), show "—" for null values; show a loading state when `isLoading` is true

**Checkpoint**: Clicking a row loads and displays the full summary. All T011–T019 tests pass.

---

## Phase 5: User Story 3 — Ingredient Listing (Priority: P2)

**Goal**: Below the summary row, the panel shows the ingredient list: each `slot` as a top-level row (slot_name + quantity displayed), with each `option` within that slot indented below it (materialName + kind + quantity).

**Independent Test**: Open the panel for a blueprint with known ingredients — two-level list renders correctly. Open for a blueprint with no ingredients — "No ingredients listed" message appears.

### Tests for User Story 3

> **Write these tests FIRST — confirm they FAIL before implementation**

- [x] T020 [US3] Extend `frontend/src/features/blueprints/__tests__/BlueprintDetailPanel.test.tsx` with ingredient section tests: (a) renders top-level slot rows (slot_name visible); (b) renders sub-option rows indented beneath the parent slot (materialName visible); (c) shows "No ingredients listed" when `slots` array is empty

### Implementation for User Story 3

- [x] T021 [US3] Add the ingredient list section to `frontend/src/features/blueprints/components/BlueprintDetailPanel.tsx`: below the summary row, render a labelled "Ingredients" section; for each slot in `data.slots`, render a top-level row showing `slot.slotName` and `slot.options[0].quantity` (primary option quantity); for each option within the slot, render an indented sub-row showing `option.materialName`, `option.kind`, and `option.quantity`; if `data.slots` is empty, render a `<p>No ingredients listed.</p>` placeholder

**Checkpoint**: Ingredient list renders correctly with two-level hierarchy. All T020–T021 tests pass.

---

## Phase 6: User Story 4 — Remove Blueprint (Priority: P2)

**Goal**: A "Remove" button in the panel footer triggers an inline confirmation. On confirm, the blueprint is deleted from the user's personal list, the panel closes, and the listing updates without a reload.

**Independent Test**: Open a panel, click Remove, confirm — the panel closes and the blueprint is gone from the listing. Click Remove then Cancel — nothing changes.

### Tests for User Story 4

> **Write these tests FIRST — confirm they FAIL before implementation**

- [x] T022 [P] [US4] Write `backend/tests/NajaEcho.Application.Tests/Features/Blueprints/RemoveMyBlueprintHandlerTests.cs`: (a) returns true when user_blueprints row is removed successfully; (b) returns false (or throws) when the row does not exist for the given user+blueprint
- [x] T023 [P] [US4] Extend `backend/tests/NajaEcho.Api.Tests/Features/Blueprints/BlueprintEndpointsTests.cs` with DELETE `/{blueprintId}` cases: (a) 401 when unauthenticated; (b) 204 when blueprint removed successfully; (c) 404 when blueprint not in user's list
- [x] T024 [P] [US4] Extend `frontend/src/features/blueprints/__tests__/BlueprintDetailPanel.test.tsx` with remove flow tests: (a) Remove button is visible; (b) clicking Remove shows inline confirmation ("Remove this blueprint?" with Confirm and Cancel); (c) clicking Cancel hides the confirmation and panel stays open; (d) clicking Confirm fires the delete mutation; (e) on successful delete, onClose is called and the listing query is invalidated

### Implementation for User Story 4

- [x] T025 [US4] Implement `RemoveMyBlueprintCommand` record (Guid UserId, Guid BlueprintId) in `backend/src/NajaEcho.Application/Features/Blueprints/RemoveMyBlueprint/RemoveMyBlueprintCommand.cs` and `RemoveMyBlueprintHandler` in `RemoveMyBlueprintHandler.cs`: calls `IUserBlueprintRepository.RemoveAsync(userId, blueprintId, ct)`, returns bool (true = removed, false = not found); register handler in `DependencyInjection.cs`
- [x] T026 [US4] Implement `RemoveAsync` in `backend/src/NajaEcho.Infrastructure/Blueprints/UserBlueprintRepository.cs`: find the `UserBlueprint` entity by `(user_id, blueprint_id)` using EF Core; if not found return false; call `DbContext.UserBlueprints.Remove(entity)` + `SaveChangesAsync`; return true
- [x] T027 [US4] Add `DELETE /api/blueprints/mine/{blueprintId}` to `backend/src/NajaEcho.Api/Features/Blueprints/BlueprintEndpoints.cs`: extract userId, require authorization, dispatch `RemoveMyBlueprintCommand`, return 204 on true; return 404 on false
- [x] T028 [P] [US4] Add `removeMyBlueprint(blueprintId: string)` to `frontend/src/features/blueprints/api/blueprintsApi.ts`: typed fetch to `DELETE /api/blueprints/mine/{blueprintId}`, returns void; throws on non-204 response
- [x] T029 [P] [US4] Implement `useRemoveMyBlueprint()` hook in `frontend/src/features/blueprints/hooks/useRemoveMyBlueprint.ts`: TanStack `useMutation` calling `removeMyBlueprint(blueprintId)`; on success, invalidates `['blueprints', 'mine']` cache and invalidates `['blueprints', 'detail', blueprintId]`
- [x] T030 [US4] Add Remove button and inline confirmation to `frontend/src/features/blueprints/components/BlueprintDetailPanel.tsx`: add `isConfirming` boolean state (default false); render a panel footer — when `isConfirming` is false: a "Remove" button aligned right; when `isConfirming` is true: "Remove this blueprint?" text with "Confirm" button (calls `useRemoveMyBlueprint` mutate, on success calls `onClose`) and "Cancel" button (sets `isConfirming` to false); show loading state on Confirm while mutation is pending

**Checkpoint**: Full remove flow works end-to-end. All T022–T030 tests pass.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [x] T031 [P] Run `quickstart.md` validation end-to-end: verify redirect from `/blueprints/mine`, panel open/close, detail load with correct data, ingredient two-level list, remove flow, and unauthenticated 401 cases
- [x] T032 [P] Code review pass: verify Clean Architecture boundaries respected in new handlers (no EF Core in Application layer); confirm `TryGetUserId` pattern used in both new endpoints; confirm craft time formatting handles edge case of 0 seconds and exactly 60 seconds; confirm Sheet accessible dismiss (keyboard Escape) works

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (US1)**: Depends on Phase 2 — frontend-only, no backend needed
- **Phase 4 (US2)**: Depends on Phase 2 and Phase 3 (panel skeleton must exist) — full stack
- **Phase 5 (US3)**: Depends on Phase 4 (panel must show detail data to add ingredients section)
- **Phase 6 (US4)**: Depends on Phase 2 (new interface methods needed) — can be developed in parallel with Phase 4/5 on backend; requires Phase 3 panel for frontend wiring
- **Phase 7 (Polish)**: Depends on all story phases complete

### User Story Dependencies

- **US1 (Route + panel skeleton)**: Frontend only, independent after Foundational
- **US2 (Detail summary)**: Depends on US1 panel skeleton existing; full-stack (new endpoint + frontend integration)
- **US3 (Ingredient list)**: Depends on US2 (slot data comes from same endpoint)
- **US4 (Remove)**: Backend independent after Foundational; frontend wiring requires US1 panel

### Parallel Opportunities

- T003, T004, T005 (Foundational types) — parallel after T002
- T007, T008 (route + nav rename) — parallel
- T011, T012, T013 (US2 tests) — parallel
- T017, T018 (US2 API client + hook) — parallel after T016
- T022, T023, T024 (US4 tests) — parallel
- T028, T029 (US4 API client + hook) — parallel after T027
- T031, T032 (polish) — parallel

---

## Parallel Example: User Story 4

```bash
# Write all US4 tests together (Red phase):
T022: RemoveMyBlueprintHandlerTests
T023: BlueprintEndpointsTests DELETE cases
T024: BlueprintDetailPanel remove flow tests

# After backend handler + endpoint green, wire frontend:
T028: removeMyBlueprint() in blueprintsApi.ts
T029: useRemoveMyBlueprint hook
```

---

## Implementation Strategy

### MVP First (US1 + US2 = usable panel)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (interface + contract records)
3. Complete Phase 3: US1 — route rename, clickable rows, panel open/close
4. Complete Phase 4: US2 — backend endpoint + panel summary
5. **STOP and VALIDATE**: Panel opens, shows name + Type/Craft Time/Ingredients count
6. Add Phase 5 (US3): Ingredient list
7. Add Phase 6 (US4): Remove flow

### Incremental Delivery

1. Setup + Foundational → shared types ready
2. US1 → route works, panel opens/closes
3. US2 → panel shows real data
4. US3 → ingredient hierarchy visible
5. US4 → remove flow complete
6. Polish → quickstart validated

---

## Notes

- No EF Core migration required — all reads from existing `sc`-schema tables; remove is a plain EF `Remove` on existing `UserBlueprint` entity
- `craftTimeSeconds` formatting: `${Math.floor(s / 60)}m ${s % 60}s`; null → "—"
- Sheet component already exists at `frontend/src/components/ui/sheet.tsx` — no new dependencies
- Backend API URLs (`/api/blueprints/mine/...`) are unchanged; only the frontend UI route is renamed
- The `slot_name` column in `sc.blueprint_slot_options` is the top-level label; `material_name` + `kind` + `quantity` are the indented option details
- Constitution Principle II is NON-NEGOTIABLE: all tests must be written and confirmed FAILING before implementation begins
