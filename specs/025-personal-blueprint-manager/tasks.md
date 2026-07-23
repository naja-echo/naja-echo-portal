# Tasks: Personal Blueprint Manager

**Input**: Design documents from `specs/025-personal-blueprint-manager/`

**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/openapi.yaml ✅

**Tests**: Included — constitution mandates TDD (Principle II: NON-NEGOTIABLE). Write tests first, confirm they fail, then implement.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on other in-progress tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)

---

## Phase 1: Setup

**Purpose**: Create feature folder structure for the new Blueprints feature area

- [x] T001 Create backend feature folder structure: `backend/src/NajaEcho.Application/Features/Blueprints/GetMyBlueprints/`, `SearchBlueprints/`, `AddMyBlueprint/` directories (can create empty placeholder files)
- [x] T002 [P] Create frontend feature folder structure: `frontend/src/features/blueprints/api/`, `hooks/`, `pages/`, `components/`, `__tests__/` directories

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain entity, database migration, repository interfaces, and API contract records — everything that all user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 Create `UserBlueprint` domain entity in `backend/src/NajaEcho.Domain/Blueprints/UserBlueprint.cs` with properties: `Id (Guid)`, `UserId (Guid)`, `BlueprintId (Guid)`, `AddedAt (DateTimeOffset)`
- [x] T004 Create EF Core configuration in `backend/src/NajaEcho.Infrastructure/Persistence/Configurations/UserBlueprintConfiguration.cs`: maps to `user_blueprints` (default schema), unique constraint on `(user_id, blueprint_id)`, index on `user_id`, FK to `characters.id` and `sc.blueprints.id`
- [ ] T005 Register `UserBlueprintConfiguration` in the EF Core `DbContext` and generate EF Core migration `AddUserBlueprints` — confirm migration runs cleanly: `dotnet ef migrations add AddUserBlueprints --project backend/src/NajaEcho.Infrastructure --startup-project backend/src/NajaEcho.Api`
- [x] T006 [P] Define `IUserBlueprintRepository` interface in `backend/src/NajaEcho.Application/Features/Blueprints/` with methods: `GetListAsync(Guid userId, CancellationToken ct)` returning `IReadOnlyList<MyBlueprintListItemDto>`, and `AddAsync(Guid userId, Guid blueprintId, CancellationToken ct)` returning `MyBlueprintListItemDto`
- [x] T007 [P] Define `SearchBlueprintsQuery` and `BlueprintSearchResultDto` in `backend/src/NajaEcho.Application/Features/Blueprints/SearchBlueprints/SearchBlueprintsQuery.cs` and `BlueprintSearchResultDto.cs`; add `SearchAsync(string term, CancellationToken ct)` to the existing `IBlueprintRepository` interface
- [x] T008 [P] Define API contract records in `backend/src/NajaEcho.Api/Features/Blueprints/Contracts/BlueprintContracts.cs`: `BlueprintSearchResultResponse`, `BlueprintSearchResponse`, `MyBlueprintListItemResponse`, `MyBlueprintListResponse`, `AddMyBlueprintRequest` — matching `contracts/openapi.yaml` exactly

**Checkpoint**: Foundation ready — migration exists, interfaces defined, contracts declared. User story implementation can now begin.

---

## Phase 3: User Story 1 — View My Blueprints Page (Priority: P1) 🎯 MVP

**Goal**: Authenticated user navigates to "My Blueprints" and sees their blueprint list (or empty state) in a three-column table: Blueprint, Type, Ingredients.

**Independent Test**: With at least one `user_blueprints` row seeded directly in the DB, `GET /api/blueprints/mine` returns the correct shape and the frontend page renders the three-column table.

### Tests for User Story 1

> **Write these tests FIRST — confirm they FAIL before implementation**

- [x] T009 [P] [US1] Write `GetMyBlueprintsHandlerTests` in `backend/tests/NajaEcho.Application.Tests/Features/Blueprints/GetMyBlueprintsHandlerTests.cs`: (a) returns empty list when user has no blueprints; (b) returns correct `ProductName`, `Type`, `IngredientCount` for a seeded blueprint; (c) only returns blueprints for the requesting user
- [x] T010 [P] [US1] Write `MyBlueprintsPage.test.tsx` in `frontend/src/features/blueprints/__tests__/MyBlueprintsPage.test.tsx` using MSW: (a) empty state renders when API returns empty array; (b) three-column table renders with Blueprint/Type/Ingredients headers and correct row values when API returns data

### Implementation for User Story 1

- [x] T011 [US1] Implement `GetMyBlueprintsQuery` and `GetMyBlueprintsHandler` in `backend/src/NajaEcho.Application/Features/Blueprints/GetMyBlueprints/GetMyBlueprintsQuery.cs` and `GetMyBlueprintsHandler.cs`: handler calls `IUserBlueprintRepository.GetListAsync(userId, ct)` and returns the DTOs
- [x] T012 [US1] Implement `UserBlueprintRepository.GetListAsync` in `backend/src/NajaEcho.Infrastructure/Blueprints/UserBlueprintRepository.cs`: joins `user_blueprints` → `sc.blueprints` → `sc.blueprint_tiers` (tier_index = 0) → `sc.blueprint_slot_options`, returns `COUNT(DISTINCT slot_index)` as `IngredientCount`; register the repository via DI
- [x] T013 [US1] Implement `GET /api/blueprints/mine` in `backend/src/NajaEcho.Api/Features/Blueprints/BlueprintEndpoints.cs`: extract userId via `TryGetUserId`, require authorization, dispatch `GetMyBlueprintsQuery`, map DTO to `MyBlueprintListResponse`; register endpoint group in the API
- [x] T014 [US1] Write Testcontainers integration test for `GET /api/blueprints/mine` in `backend/tests/NajaEcho.Api.Tests/Features/Blueprints/BlueprintEndpointsTests.cs`: (a) 401 when unauthenticated; (b) 200 with empty list for new user; (c) 200 with correct data after seeding `user_blueprints` row
- [x] T015 [P] [US1] Implement `blueprintsApi.ts` `getMyBlueprints()` function in `frontend/src/features/blueprints/api/blueprintsApi.ts` — typed fetch to `GET /api/blueprints/mine`
- [x] T016 [P] [US1] Implement `useMyBlueprints` hook in `frontend/src/features/blueprints/hooks/useMyBlueprints.ts` using TanStack Query with key `['blueprints', 'mine']`
- [x] T017 [US1] Implement `MyBlueprintsPage` in `frontend/src/features/blueprints/pages/MyBlueprintsPage.tsx`: renders three-column table (Blueprint, Type, Ingredients) when data exists; renders empty state message when list is empty; includes placeholder "Add Blueprint" button (wired up in US2)
- [x] T018 [US1] Add `{ label: 'My Blueprints', path: '/blueprints/mine', icon: ScrollText, group: 'Blueprints' }` to `frontend/src/features/dashboard/navigation/navItems.ts` and register the `/blueprints/mine` route pointing to `MyBlueprintsPage` in the app router

**Checkpoint**: Navigate to "My Blueprints" in the sidebar — page loads, shows empty state or table. `GET /api/blueprints/mine` returns 200. All T009–T018 tests pass.

---

## Phase 4: User Story 2 — Add a Blueprint (Priority: P1)

**Goal**: User clicks "Add Blueprint", types a partial name, sees autosuggest results, selects one, and confirms — the blueprint appears in the listing without a page reload.

**Independent Test**: Open the Add Blueprint modal, search for a known `product_name`, select a result, click submit — POST returns 201 and the new entry appears in the listing.

### Tests for User Story 2

> **Write these tests FIRST — confirm they FAIL before implementation**

- [x] T019 [P] [US2] Write `SearchBlueprintsHandlerTests` in `backend/tests/NajaEcho.Application.Tests/Features/Blueprints/SearchBlueprintsHandlerTests.cs`: (a) returns matching entries for partial term (case-insensitive); (b) excludes blueprints with null `product_name`; (c) returns at most 20 results; (d) returns empty list when no match
- [x] T020 [P] [US2] Write `AddMyBlueprintHandlerTests` in `backend/tests/NajaEcho.Application.Tests/Features/Blueprints/AddMyBlueprintHandlerTests.cs`: (a) returns correct DTO on success; (b) throws `DuplicateBlueprintException` when already added; (c) throws `NotFoundException` when blueprint catalog id does not exist
- [x] T021 [P] [US2] Write `AddBlueprintDialog.test.tsx` in `frontend/src/features/blueprints/__tests__/AddBlueprintDialog.test.tsx` using MSW: (a) Add Blueprint button is disabled before selection; (b) typing triggers search API call; (c) selecting a result enables submit button; (d) successful submit closes modal and invalidates `['blueprints', 'mine']` cache

### Implementation for User Story 2

- [x] T022 [US2] Implement `SearchBlueprintsHandler` in `backend/src/NajaEcho.Application/Features/Blueprints/SearchBlueprints/SearchBlueprintsHandler.cs` and `IBlueprintRepository.SearchAsync` in `backend/src/NajaEcho.Infrastructure/Blueprints/BlueprintRepository.cs`: ILIKE `%term%` on `product_name`, non-null only, limit 20
- [x] T023 [US2] Implement `GET /api/blueprints/search` endpoint in `backend/src/NajaEcho.Api/Features/Blueprints/BlueprintEndpoints.cs`: require authorization, validate `q` query param is non-empty, dispatch `SearchBlueprintsQuery`, map to `BlueprintSearchResponse`
- [x] T024 [US2] Implement `AddMyBlueprintCommand`, `AddMyBlueprintValidator`, and `AddMyBlueprintHandler` in `backend/src/NajaEcho.Application/Features/Blueprints/AddMyBlueprint/`: validator requires non-empty `BlueprintId`; handler verifies blueprint exists in catalog, calls `IUserBlueprintRepository.AddAsync`, catches unique constraint violation and throws `DuplicateBlueprintException`
- [x] T025 [US2] Implement `UserBlueprintRepository.AddAsync` in `backend/src/NajaEcho.Infrastructure/Blueprints/UserBlueprintRepository.cs`: inserts new `user_blueprints` row and returns the joined `MyBlueprintListItemDto` (including ingredient count) for the response
- [x] T026 [US2] Implement `POST /api/blueprints/mine` in `backend/src/NajaEcho.Api/Features/Blueprints/BlueprintEndpoints.cs`: extract userId, dispatch `AddMyBlueprintCommand`, return 201 with `MyBlueprintListItemResponse`; map `DuplicateBlueprintException` → 409, `NotFoundException` → 404; add integration tests for 201/409/404/401 in `BlueprintEndpointsTests.cs`
- [x] T027 [P] [US2] Implement `searchBlueprints()` and `addMyBlueprint()` in `frontend/src/features/blueprints/api/blueprintsApi.ts`
- [x] T028 [P] [US2] Implement `useBlueprintSearch(query)` hook in `frontend/src/features/blueprints/hooks/useBlueprintSearch.ts`: TanStack Query with 300ms debounce; disabled when query is empty; key `['blueprints', 'search', query]`
- [x] T029 [P] [US2] Implement `useAddMyBlueprint()` hook in `frontend/src/features/blueprints/hooks/useAddMyBlueprint.ts`: TanStack mutation; on success, invalidates `['blueprints', 'mine']` cache
- [x] T030 [US2] Implement `AddBlueprintDialog` in `frontend/src/features/blueprints/components/AddBlueprintDialog.tsx`: shadcn `Dialog` with shadcn `Command` autosuggest input; submit button disabled until selection made; calls `useAddMyBlueprint` on submit; closes on success
- [x] T031 [US2] Wire `AddBlueprintDialog` into `MyBlueprintsPage` — "Add Blueprint" button opens the dialog; new entry appears in table after successful add without a full page reload

**Checkpoint**: Full add-a-blueprint flow works end-to-end. Search returns suggestions, selection enabled submit, 201 response, listing updates. All T019–T031 tests pass.

---

## Phase 5: User Story 3 — Prevent Duplicate Blueprints (Priority: P2)

**Goal**: System rejects a second attempt to add the same blueprint; frontend displays a clear inline message instead of a generic error.

**Independent Test**: Add a blueprint successfully, then attempt to add the same blueprint — backend returns 409, frontend shows an inline "Already in your list" message and does not close the modal.

### Tests for User Story 3

> **Write these tests FIRST — confirm they FAIL before implementation**

- [x] T032 [US3] Extend `AddBlueprintDialog.test.tsx` in `frontend/src/features/blueprints/__tests__/AddBlueprintDialog.test.tsx`: (a) when MSW returns 409, inline "Already in your list" message is shown; (b) modal stays open on 409; (c) modal closes on 201

### Implementation for User Story 3

- [x] T033 [US3] Verify `DuplicateBlueprintException` → 409 mapping is registered in the global exception handler or endpoint error mapping (already partially wired in T026 — confirm the 409 body includes a user-readable `detail` field per the `ProblemDetails` contract)
- [x] T034 [US3] Update `AddBlueprintDialog` in `frontend/src/features/blueprints/components/AddBlueprintDialog.tsx`: on 409 response from `useAddMyBlueprint`, display inline "This blueprint is already in your list." message below the search input; modal remains open; message clears when user changes selection

**Checkpoint**: Duplicate add is blocked at the DB level, returns 409, and the frontend shows a clear non-dismissing inline message. All T032–T034 tests pass.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validation completeness, empty state polish, and quickstart walkthrough

- [ ] T035 Run `quickstart.md` validation end-to-end: apply migration, verify search endpoint, add a blueprint, list blueprints, spot-check ingredient count against raw SQL — document any discrepancies
- [ ] T036 [P] Verify `ScrollText` (or equivalent) Lucide icon is available; confirm the "My Blueprints" nav item renders correctly in both desktop sidebar and mobile navigation; confirm unauthenticated access to `/blueprints/mine` redirects to login
- [ ] T037 [P] Code review pass: confirm no implementation detail leaks across layer boundaries (no EF Core types in Application layer, no direct DB access from API layer); confirm `TryGetUserId` pattern used consistently in `BlueprintEndpoints.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (US1)**: Depends on Phase 2 — can start as soon as foundational complete
- **Phase 4 (US2)**: Depends on Phase 2 — can start as soon as foundational complete (parallel with US1 if desired)
- **Phase 5 (US3)**: Depends on Phase 4 (duplicate logic builds on the add flow)
- **Phase 6 (Polish)**: Depends on all story phases complete

### User Story Dependencies

- **US1 (View listing)**: Independent after Foundational — empty state is a valid deliverable
- **US2 (Add blueprint)**: Independent after Foundational — can be tested via API even before UI
- **US3 (Prevent duplicates)**: Depends on US2 (requires add flow to exist)

### Within Each User Story

1. Write tests first — confirm they FAIL (Red)
2. Implement to make tests pass (Green)
3. Refactor if needed
4. Checkpoint validation before moving to next story

### Parallel Opportunities

- T001 and T002 (folder setup) — parallel
- T006, T007, T008 (interfaces + contracts) — parallel after T003–T005
- T009 and T010 (US1 tests) — parallel
- T015 and T016 (US1 API client + hook) — parallel
- T019, T020, T021 (US2 tests) — parallel
- T027, T028, T029 (US2 API client + hooks) — parallel
- T035, T036, T037 (polish) — parallel

---

## Parallel Example: User Story 2

```bash
# Write all US2 tests together (Red phase):
Task T019: SearchBlueprintsHandlerTests
Task T020: AddMyBlueprintHandlerTests
Task T021: AddBlueprintDialog.test.tsx

# After foundational + backend handlers are green, wire frontend together:
Task T027: blueprintsApi.ts (search + add functions)
Task T028: useBlueprintSearch hook
Task T029: useAddMyBlueprint hook
```

---

## Implementation Strategy

### MVP First (US1 + US2 together = usable feature)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (migration, interfaces, contracts)
3. Complete Phase 3: US1 — navigate to page, see empty state/listing
4. Complete Phase 4: US2 — add blueprints via modal
5. **STOP and VALIDATE**: Full listing + add flow works end-to-end
6. Add Phase 5 (US3): Duplicate prevention

### Incremental Delivery

1. Setup + Foundational → DB ready, interfaces defined
2. US1 → Page renders, GET endpoint works (empty state is valid)
3. US2 → Add flow works, listing populates
4. US3 → Duplicate protection
5. Polish → Validation complete

---

## Notes

- [P] tasks involve different files with no dependency on other in-progress tasks in the same phase
- Tests marked with "Write FIRST — confirm FAIL" follow the TDD Red-Green-Refactor cycle (constitution Principle II)
- The `user_blueprints` table uses the default schema (no `sc.` prefix) — see research.md
- Ingredient count is `COUNT(DISTINCT slot_index)` at `tier_index = 0` — see data-model.md
- 409 Conflict on duplicate add is enforced at both DB constraint and application exception handler levels
- `ScrollText` is the suggested Lucide icon for blueprints — verify availability or substitute
