# Tasks: Blueprint Filtering

**Input**: Design documents from `specs/028-blueprint-filtering/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/openapi.yaml ✅

**Tests**: Included — Constitution Principle II mandates TDD (Red → Green → Refactor).

**Organization**: Tasks grouped by user story. Foundational phase (backend API change) unblocks all stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: User story label (US1, US2, US3)

---

## Phase 1: Setup

**Purpose**: No new project structure needed — contracts are already written in `specs/028-blueprint-filtering/contracts/openapi.yaml`. Confirm the API contract is committed to the branch before any implementation begins (Constitution Principle I).

- [X] T001 Confirm `specs/028-blueprint-filtering/contracts/openapi.yaml` is committed and reviewed on branch `028-blueprint-filtering`

---

## Phase 2: Foundational — Surface `subtype` in Blueprint List Endpoints

**Purpose**: Both existing list endpoints (`GET /api/blueprints/mine` and `GET /api/blueprints/org`) must return `subtype` in each list item before any frontend filtering can work. These backend changes are a prerequisite for all three user stories.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Tests (write first — must FAIL before implementation)

- [X] T002 [P] Write failing integration test asserting `GET /api/blueprints/mine` response items include `subtype` field in `backend/tests/NajaEcho.Api.IntegrationTests/Blueprints/GetMyBlueprintsTests.cs`
- [X] T003 [P] Write failing integration test asserting `GET /api/blueprints/org` response items include `subtype` field in `backend/tests/NajaEcho.Api.IntegrationTests/Blueprints/GetOrgBlueprintsTests.cs`

### Backend Implementation

- [X] T004 [P] Add `string? Subtype` parameter to `MyBlueprintListItemDto` in `backend/src/NajaEcho.Application/Features/Blueprints/GetMyBlueprints/MyBlueprintListItemDto.cs`
- [X] T005 [P] Add `string? Subtype` parameter to `OrgBlueprintListItemDto` in `backend/src/NajaEcho.Application/Features/Blueprints/GetOrgBlueprints/OrgBlueprintListItemDto.cs`
- [X] T006 Update `ListRow` record and `GetListAsync` SQL query to add `b.subtype AS subtype` to SELECT and `b.subtype` to GROUP BY in `backend/src/NajaEcho.Infrastructure/Blueprints/UserBlueprintRepository.cs` (depends on T004)
- [X] T007 Update `ListRow` record and `GetListAsync` SQL query to add `b.subtype AS subtype` to SELECT and `b.subtype` to GROUP BY in `backend/src/NajaEcho.Infrastructure/Blueprints/OrgBlueprintRepository.cs` (depends on T005)
- [X] T008 Add `string? Subtype` to `MyBlueprintListItemResponse` and `OrgBlueprintListItemResponse` in `backend/src/NajaEcho.Api/Features/Blueprints/Contracts/BlueprintContracts.cs`
- [X] T009 Update endpoint mapping to pass `dto.Subtype` to both response records in `backend/src/NajaEcho.Api/Features/Blueprints/BlueprintEndpoints.cs` (depends on T008)

### Frontend Type Update

- [X] T010 Add `subtype: string | null` to `MyBlueprintListItem` and `OrgBlueprintListItem` interfaces in `frontend/src/features/blueprints/api/blueprintsApi.ts`
- [X] T011 Update MSW mock response handlers in `frontend/src/features/blueprints/__tests__/MyBlueprintsPage.test.tsx` to include `subtype` in mocked list items
- [X] T012 Update MSW mock response handlers in `frontend/src/features/blueprints/__tests__/OrgBlueprintsPage.test.tsx` to include `subtype` in mocked list items

**Checkpoint**: Run backend integration tests (T002, T003) — they should now pass. Frontend types compile without errors.

---

## Phase 3: User Story 1 — Filter by Category & Subcategory (Priority: P1) 🎯 MVP

**Goal**: Users can select a Category and/or Subcategory to narrow the blueprint list on both My Blueprints and Org Blueprints pages. Subcategory is always visible and active; its options narrow when a Category is selected.

**Independent Test**: Navigate to My Blueprints or Org Blueprints. Select a Category — list narrows. Select a Subcategory without a Category — list narrows by subcategory only. Select both — list satisfies both filters. Selecting the same value again clears that filter.

### Tests (write first — must FAIL before implementation)

- [X] T013 [P] [US1] Write failing unit tests for `useBlueprintFilters` hook covering: no filters returns full list; category filter narrows list; subcategory-only filter narrows list; category+subcategory filter narrows list; categoryOptions are distinct sorted non-null type values; subcategoryOptions show all when no category, narrow when category set; clearing category does not auto-clear subcategory selection — in `frontend/src/features/blueprints/__tests__/useBlueprintFilters.test.ts`
- [X] T014 [P] [US1] Write failing component tests for `BlueprintFilters` covering: renders category combobox; renders subcategory combobox (always present); category change calls onCategoryChange; subcategory change calls onSubcategoryChange — in `frontend/src/features/blueprints/__tests__/BlueprintFilters.test.tsx`

### Implementation

- [X] T015 [US1] Implement `useBlueprintFilters` hook with `category` and `subcategory` filter state, `categoryOptions`, `subcategoryOptions` derivation, and `filtered` list output in `frontend/src/features/blueprints/hooks/useBlueprintFilters.ts` (depends on T013, T010)
- [X] T016 [US1] Implement `BlueprintFilters` component with Category `Combobox` and Subcategory `Combobox` in `frontend/src/features/blueprints/components/BlueprintFilters.tsx` (depends on T014)
- [X] T017 [US1] Integrate `BlueprintFilters` and `useBlueprintFilters` into `MyBlueprintsPage` — replace `data.blueprints` table source with `filtered` from hook; render `BlueprintFilters` above the table in `frontend/src/features/blueprints/pages/MyBlueprintsPage.tsx` (depends on T015, T016)
- [X] T018 [US1] Integrate `BlueprintFilters` and `useBlueprintFilters` into `OrgBlueprintsPage` — same pattern as T017 in `frontend/src/features/blueprints/pages/OrgBlueprintsPage.tsx` (depends on T015, T016)
- [X] T019 [P] [US1] Extend `MyBlueprintsPage` tests with category filter scenario: selecting a category shows only matching blueprints in `frontend/src/features/blueprints/__tests__/MyBlueprintsPage.test.tsx` (depends on T017)
- [X] T020 [P] [US1] Extend `OrgBlueprintsPage` tests with category filter scenario: selecting a category shows only matching blueprints in `frontend/src/features/blueprints/__tests__/OrgBlueprintsPage.test.tsx` (depends on T018)

**Checkpoint**: My Blueprints and Org Blueprints pages both show Category and Subcategory filter controls. Filtering by category and/or subcategory narrows the list correctly. All T013–T020 tests pass.

---

## Phase 4: User Story 2 — Search by Name (Priority: P2)

**Goal**: Users can type a partial product name and the list immediately narrows to blueprints whose name contains the search text (case-insensitive, every keystroke, no delay).

**Independent Test**: Type a partial name into the name search input — the list filters on every keystroke. Clear the input — the full (or category-filtered) list is restored. Combined name + category filter works simultaneously.

### Tests (write first — must FAIL before implementation)

- [X] T021 [P] [US2] Write failing unit tests for `useBlueprintFilters` name filtering: name filter matches case-insensitively; name filter applied alongside category filter; clearing name restores list — in `frontend/src/features/blueprints/__tests__/useBlueprintFilters.test.ts` (extends T013 file)
- [X] T022 [P] [US2] Write failing component test for `BlueprintFilters` name input: renders name input; typing calls onNameChange — in `frontend/src/features/blueprints/__tests__/BlueprintFilters.test.tsx` (extends T014 file)

### Implementation

- [X] T023 [US2] Extend `useBlueprintFilters` to add `name` filter state and case-insensitive substring filtering of `productName` in `frontend/src/features/blueprints/hooks/useBlueprintFilters.ts` (depends on T021, T015)
- [X] T024 [US2] Extend `BlueprintFilters` component to render a name search `<input>` with `onChange` wired to `onNameChange` in `frontend/src/features/blueprints/components/BlueprintFilters.tsx` (depends on T022, T016)
- [X] T025 [P] [US2] Extend `MyBlueprintsPage` tests: typing a name narrows list; combined name + category filter shows correct subset in `frontend/src/features/blueprints/__tests__/MyBlueprintsPage.test.tsx` (depends on T023, T024)
- [X] T026 [P] [US2] Extend `OrgBlueprintsPage` tests: same name + combined filter scenarios in `frontend/src/features/blueprints/__tests__/OrgBlueprintsPage.test.tsx` (depends on T023, T024)

**Checkpoint**: Name search input visible on both pages. Filtering by name works on every keystroke. All three filter types (name, category, subcategory) can be combined. T021–T026 tests pass.

---

## Phase 5: User Story 3 — Filter Persistence Within Session (Priority: P3)

**Goal**: Filter selections are preserved when the user opens and closes a blueprint detail panel on the same page. This is naturally satisfied by component-level state (detail panel renders inside the same page component), but must be explicitly verified.

**Independent Test**: Select a Category filter, open a blueprint detail panel, close the panel — the Category filter remains active and the list remains filtered.

### Tests (write first — must FAIL before implementation)

- [X] T027 [US3] Write failing test in `MyBlueprintsPage` that simulates: select category filter → click row to open detail panel → close detail panel → assert category filter still set and filtered list still shown in `frontend/src/features/blueprints/__tests__/MyBlueprintsPage.test.tsx`

### Verification

- [X] T028 [US3] Confirm test T027 passes without additional code changes (filter state in `useState` within page component is inherently preserved across panel open/close). If test fails, adjust page structure so filter state is not reset on panel open/close in `frontend/src/features/blueprints/pages/MyBlueprintsPage.tsx`

**Checkpoint**: Filter state survives detail panel open/close. T027 passes.

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Empty state validation, manual end-to-end verification.

- [X] T029 [P] Verify empty state message is shown (not a crash) when no blueprints match the active filters on both My Blueprints and Org Blueprints pages — test in `frontend/src/features/blueprints/__tests__/MyBlueprintsPage.test.tsx`
- [ ] T030 Run all quickstart.md validation scenarios manually against the running stack to confirm end-to-end behaviour matches the spec

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user stories**
- **Phase 3 (US1)**: Depends on Phase 2 completion
- **Phase 4 (US2)**: Depends on Phase 3 (extends the same hook and component)
- **Phase 5 (US3)**: Depends on Phase 3 (uses same page components)
- **Phase 6 (Polish)**: Depends on Phases 3–5

### User Story Dependencies

- **US1 (P1)**: Blocks US2 and US3 — the hook and component are established here
- **US2 (P2)**: Extends US1 hook and component — depends on US1
- **US3 (P3)**: Verification only — depends on US1 page integration

### Within Each Phase

- Tasks marked [P] within the same phase can run in parallel
- TDD order: write failing test → implement → confirm green
- Backend (T004–T009) and frontend type update (T010–T012) within Phase 2 are independent and can run in parallel

### Parallel Opportunities

```bash
# Phase 2 — backend can proceed in parallel with frontend type update:
T002 + T003  # Backend integration tests (parallel)
T004 + T005  # DTO additions (parallel)
T006 + T007  # Repository SQL updates (after T004/T005)
T010 + T011 + T012  # Frontend type + MSW updates (parallel, independent of backend)

# Phase 3 — hook test and component test in parallel:
T013 + T014  # Hook tests + Component tests (parallel)
T017 + T018  # Page integrations (parallel, after T015+T016)
T019 + T020  # Page test extensions (parallel)

# Phase 4 — US2 test extensions in parallel:
T021 + T022  # Name filter tests (parallel)
T025 + T026  # Page test extensions (parallel)
```

---

## Implementation Strategy

### MVP (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T012)
3. Complete Phase 3: User Story 1 (T013–T020)
4. **STOP and VALIDATE**: Category and Subcategory filters working on both pages
5. Deploy / demo if ready

### Incremental Delivery

1. Setup + Foundational → `subtype` in API responses
2. US1 → Category + Subcategory filters on both pages (MVP)
3. US2 → Name search added to filter bar
4. US3 → Persistence verified
5. Polish → Empty state + quickstart validation

---

## Notes

- [P] tasks have no file conflicts and can be run in parallel
- All test tasks must produce **failing** tests before implementation begins (Constitution II)
- `useBlueprintFilters` and `BlueprintFilters` are shared between both pages — implement once, use twice
- Backend repositories use raw SQL — add `b.subtype` to SELECT and GROUP BY in both `UserBlueprintRepository` and `OrgBlueprintRepository`
- The `Combobox` component already handles toggle-to-clear (selecting the current value returns `''`)
- No migrations, no new endpoints, no new domain entities
