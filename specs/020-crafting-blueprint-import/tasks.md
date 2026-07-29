---
description: "Task list for Crafting Blueprint Import"
---

# Tasks: Crafting Blueprint Import

**Input**: Design documents from `/specs/020-crafting-blueprint-import/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, contracts/star-citizen-blueprints.schema.json

**Tests**: INCLUDED — the constitution's Test-First principle applies (plan.md → Constitution Check II) and plan.md pins a per-layer test matrix. Test tasks are written first and MUST fail before their implementation tasks.

**Organization**: Tasks are grouped by user story. All five stories share the same six `sc.*` entities and the `IBlueprintRepository` port, so those are built once in the Foundational phase; each story then adds its own slice and is independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1–US5 (Foundational/Setup/Polish carry no story label)
- File paths are relative to repo root

## Path Conventions

- Backend: `backend/src/NajaEcho.{Domain,Application,Infrastructure,Api}/`, tests in `backend/tests/NajaEcho.{Application,Infrastructure,Api}.Tests/`
- Frontend: `frontend/src/features/admin/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Test fixtures reused across every layer; no project init needed (greenfield inside an established solution).

- [X] T001 [P] Add shared backend dataset fixtures under `backend/tests/Fixtures/Blueprints/`: one valid dataset JSON (small, conforms to `contracts/star-citizen-blueprints.schema.json`, ≥1 blueprint with tiers/slots/resource+item options/modifiers), plus malformed variants (missing `tag`, `guid: "not-a-uuid"`, duplicate `guid`, option matching neither resource nor item, missing top-level `dismantle` section, `meta.totalBlueprints` mismatch).
- [X] T002 [P] Add frontend fixtures under `frontend/src/features/admin/__tests__/fixtures/blueprintDataset.ts`: a sample parsed dataset object, a sample `ImportBlueprintsResponse`, a sample `BlueprintListResponse`, and an oversize-file helper (>50 MB blob) for size-rejection tests.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The six `sc.*` entities, EF configs, additive migration, repository port, endpoint group, and frontend tab shell that ALL user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Domain entities (backend/src/NajaEcho.Domain/Blueprints/)

- [X] T003 [P] Create `CraftingBlueprint` in `backend/src/NajaEcho.Domain/Blueprints/CraftingBlueprint.cs` — `Id` (=guid) PK, descriptive fields (`Tag`, `ProductEntityClass`, `Gear`, `Type?`, `Subtype?`, `ProductName?`, `Manufacturer?`), optional (`IsDefault?`, `SuggestedName?`, `SuggestedProductEntityClass?`, `CigDataError?`), `Tiers` (`JsonDocument`), `ImportedAt`, `UpdatedAt` (per data-model.md).
- [X] T004 [P] Create `CraftingBlueprintTier` in `backend/src/NajaEcho.Domain/Blueprints/CraftingBlueprintTier.cs` — `Id` PK, `BlueprintId` FK, `TierIndex`, `CraftTimeSeconds`.
- [X] T005 [P] Create `CraftingBlueprintSlotOption` in `backend/src/NajaEcho.Domain/Blueprints/CraftingBlueprintSlotOption.cs` — `Id` PK, `TierId` FK, `SlotIndex`, `SlotName`, `OptionIndex`, `Kind`, `MaterialName`, `Quantity`, `MinQuality`, `MatchedUuid?`, `MatchedSource?`.
- [X] T006 [P] Create `CraftingMaterial` in `backend/src/NajaEcho.Domain/Blueprints/CraftingMaterial.cs` and `CraftingMaterialKind` enum in `backend/src/NajaEcho.Domain/Blueprints/CraftingMaterialKind.cs` (`{ Resource, Item }`) — composite identity (`Kind`, `Name`) + `MatchedUuid?`/`MatchedSource?`.
- [X] T007 [P] Create `CatalogSource` enum in `backend/src/NajaEcho.Domain/Blueprints/CatalogSource.cs` (`{ Commodity, Item }`).
- [X] T008 [P] Create `CraftingProperty` in `backend/src/NajaEcho.Domain/Blueprints/CraftingProperty.cs` — `Key`, `Name`, `Unit?`, `Category`, `NameOverrides?` (`JsonDocument`).
- [X] T009 [P] Create `CraftingDataset` in `backend/src/NajaEcho.Domain/Blueprints/CraftingDataset.cs` — `Id`, `Version`, meta totals, dismantle config (`Efficiency`, `DismantleTimeSeconds`), `BlacklistedResources`/`BlacklistedEntityClasses` (`JsonDocument`), `ImportedAt`.

### EF Core configurations (backend/src/NajaEcho.Infrastructure/Persistence/Configurations/)

- [X] T010 [P] `CraftingBlueprintConfiguration.cs` — `ToTable("blueprints", schema: "sc")`, PK `id`, `tiers` jsonb, snake_case columns, `imported_at`/`updated_at`.
- [X] T011 [P] `CraftingBlueprintTierConfiguration.cs` — `sc.blueprint_tiers`, FK → `sc.blueprints` `ON DELETE CASCADE`, unique (`blueprint_id`, `tier_index`).
- [X] T012 [P] `CraftingBlueprintSlotOptionConfiguration.cs` — `sc.blueprint_slot_options`, FK → `sc.blueprint_tiers` cascade, `kind`/`matched_source` via `HasConversion<string>()`, indexes on `material_name` and `matched_uuid`, unique (`tier_id`, `slot_index`, `option_index`).
- [X] T013 [P] `CraftingMaterialConfiguration.cs` — `sc.crafting_materials`, composite PK (`kind`, `name`), `kind`/`matched_source` string-converted.
- [X] T014 [P] `CraftingPropertyConfiguration.cs` — `sc.crafting_properties`, PK `property_key`, `name_overrides` jsonb.
- [X] T015 [P] `CraftingDatasetConfiguration.cs` — `sc.crafting_datasets`, `blacklisted_resources`/`blacklisted_entity_classes` jsonb.

### Wiring (single-file edits — not parallel with each other)

- [X] T016 Edit `backend/src/NajaEcho.Infrastructure/Persistence/AppDbContext.cs` — add 6 `DbSet<>` properties and 6 `ApplyConfiguration` calls in `OnModelCreating` (depends on T003–T015).
- [X] T017 Generate the additive migration `AddCraftingBlueprints` (`dotnet ef migrations add AddCraftingBlueprints`) creating all six `sc.*` tables; verify it is additive-only (6 CreateTable, no alters/drops) in `backend/src/NajaEcho.Infrastructure/Persistence/Migrations/` (depends on T016).
- [X] T018 Create the `IBlueprintRepository` port in `backend/src/NajaEcho.Application/Abstractions/IBlueprintRepository.cs` — `ResolveMaterialsAsync(names)` → uuid/source map; `ImportAsync(parsedSnapshot)` → persistence counts (single tx); `GetListAsync()` → list rows.
- [X] T019 Create the endpoint group skeleton `backend/src/NajaEcho.Api/Features/Admin/Blueprints/BlueprintAdminEndpoints.cs` — `MapGroup("/api/admin/blueprints").RequireAuthorization(AuthorizationPolicies.Admin)` (no routes yet) and register `app.MapBlueprintAdminEndpoints()` in `backend/src/NajaEcho.Api/Program.cs`.

### Frontend shell

- [X] T020 Edit `frontend/src/features/admin/pages/DataImportPage.tsx` — add `<TabsTrigger value="blueprints">` + `<TabsContent>` hosting a new empty `frontend/src/features/admin/components/BlueprintsImportTab.tsx` shell (upload region + list region placeholders).
- [X] T021 [P] Create the query-key factory `frontend/src/features/admin/hooks/blueprintKeys.ts`.

**Checkpoint**: Schema migrated, port + endpoint group + tab shell exist. User stories can now begin.

---

## Phase 3: User Story 1 - Upload a Blueprint Dataset File (Priority: P1) 🎯 MVP

**Goal**: Admin selects a valid dataset JSON, uploads it, and the full dataset (blueprints + all reference data + derived query rows) is stored in one transaction with a result summary (version + per-collection counts).

**Independent Test**: Upload the valid sample file; confirm the summary reports version and correct read/inserted counts and that `sc.blueprints` + `sc.crafting_datasets` + derived tier/option rows are populated.

### Tests for User Story 1 ⚠️ (write first, must fail)

- [X] T022 [P] [US1] Parser unit tests in `backend/tests/NajaEcho.Application.Tests/Features/Blueprints/BlueprintParserTests.cs` — valid blueprint accepted; null slot `modifiers` → empty set (FR-007); optional fields present/absent preserved (FR-006); `tiers` jsonb + derived tier/slot-option row data emitted with correct indexes/quantities; material names collected.
- [X] T023 [P] [US1] Handler unit tests in `backend/tests/NajaEcho.Application.Tests/Features/Blueprints/ImportBlueprintsHandlerTests.cs` (fake repo + coordinator) — validation completes before any repository write (Decision 7); material resolution map applied, unmatched → null link + warning (FR-009); empty blueprint list still refreshes reference data; coordinator busy → `ImportAlreadyInProgressException`.
- [X] T024 [P] [US1] Testcontainers integration test in `backend/tests/NajaEcho.Infrastructure.Tests/Blueprints/BlueprintImportTests.cs` — full import persists all six tables incl. derived tier/slot-option rows with correct indexes; material lookup precedence (commodity-first, null/empty-uuid commodity falls through to items, case-insensitive, soft-deleted excluded, deterministic winner on duplicate names, unmatched stored null) per research Decision 11.
- [X] T025 [P] [US1] API contract test in `backend/tests/NajaEcho.Api.Tests/Features/Admin/Blueprints/ImportBlueprintsEndpointTests.cs` — `PostAsJsonAsync` valid document → 200 with exact `ImportBlueprintsResponse` shape (version, four `CollectionCounts`, `referenceDataReplaced`, `warnings`, `rejections`).
- [X] T026 [P] [US1] Frontend test in `frontend/src/features/admin/__tests__/blueprintsImportTab.test.tsx` — file select → parse → POST flow; upload control disabled while pending (FR-003, SC-009); result summary renders version + per-collection counts + `referenceDataReplaced`.

### Implementation for User Story 1

- [X] T027 [P] [US1] Create `ImportBlueprintsCommand.cs` (typed top-level sections + `List<JsonElement> blueprints`) and `ImportBlueprintsResult.cs` (version, per-collection counts, `referenceDataReplaced`, warnings, rejections) in `backend/src/NajaEcho.Application/Features/Blueprints/ImportBlueprints/`.
- [X] T028 [US1] Implement `BlueprintParser.cs` in `backend/src/NajaEcho.Application/Features/Blueprints/ImportBlueprints/` — pure per-element parse + normalization (null modifiers → `[]`, item options drop modifiers key, unknown fields dropped); emit `tiers` jsonb + derived tier/slot-option row data + collected material names (rejection/dedup logic added in US5).
- [X] T029 [US1] Implement `ImportBlueprintsHandler.cs` — acquire coordinator lock (try/finally); parse via `BlueprintParser`; call `ResolveMaterialsAsync`; call `repo.ImportAsync`; build result + unmatched-material warnings.
- [X] T030 [US1] Implement `BlueprintRepository.cs` in `backend/src/NajaEcho.Infrastructure/Blueprints/` — `ResolveMaterialsAsync` (batched case-insensitive lookup: commodities first excluding soft-deleted/empty-uuid, then items, lowest `uex_id` wins) and `ImportAsync` (one `BeginTransactionAsync`: replace materials/properties/dataset wholesale, upsert blueprints by PK, rebuild derived tier/slot-option rows, commit).
- [X] T031 [US1] Register `IBlueprintRepository` (`AddScoped`) + `ImportBlueprintsHandler` in `backend/src/NajaEcho.Infrastructure/DependencyInjection.cs` (and Application handler registration wherever handlers are registered).
- [X] T032 [P] [US1] Create API contracts in `backend/src/NajaEcho.Api/Features/Admin/Blueprints/Contracts/` — `ImportBlueprintsRequest`, `ImportBlueprintsResponse`, `CollectionCountsResponse`, `BlueprintRejectionResponse` (matching `contracts/openapi.yaml`).
- [X] T033 [US1] Add `POST /import` to `BlueprintAdminEndpoints.cs` — bind nullable request DTO, `RequestSizeLimit` 50 MB metadata, map `ImportAlreadyInProgressException` → 409, structured outcome log line (version + per-collection counts + rejected + duration; no file contents).
- [X] T034 [P] [US1] Create `frontend/src/features/admin/schemas/blueprintSchemas.ts` — light top-level dataset check (pre-POST) + `ImportBlueprintsResponse` Zod schema.
- [X] T035 [US1] Create `frontend/src/features/admin/api/blueprintsApi.ts` `importBlueprints(document)` (via `apiFetch`) and `frontend/src/features/admin/hooks/useImportBlueprints.ts` (`useMutation` + invalidate list key).
- [X] T036 [US1] Build the upload zone in `BlueprintsImportTab.tsx` (hidden file input, `file.text()` + parse + shape check, 50 MB client cap with clear message, pending/disabled state) and `frontend/src/features/admin/components/BlueprintImportSummary.tsx` (version + per-collection count cards + `referenceDataReplaced`).

**Checkpoint**: A valid dataset uploads end-to-end and stores everything atomically. MVP is demoable.

---

## Phase 4: User Story 2 - Browse and Search Blueprints by Name (Priority: P1)

**Goal**: Admin sees all known blueprints by display name (fallback chain) and filters client-side by substring.

**Independent Test**: Seed blueprints (some with null product names, some guid-matching `sc.items.uuid`); open the tab; verify fallback names render and typing a partial name narrows the list.

### Tests for User Story 2 ⚠️ (write first, must fail)

- [X] T037 [P] [US2] Testcontainers test in `backend/tests/NajaEcho.Infrastructure.Tests/Blueprints/BlueprintListQueryTests.cs` — `GetListAsync` computes display-name fallback (productName → linked `sc.items` name via `items.uuid = id::text` → tag → guid), soft-deleted items excluded, unmatched guid → unlinked but present, ordered by display name (FR-015, FR-022, SC-006).
- [X] T038 [P] [US2] API contract test in `backend/tests/NajaEcho.Api.Tests/Features/Admin/Blueprints/GetBlueprintsEndpointTests.cs` — `GET /api/admin/blueprints` → 200 `BlueprintListResponse` ordered by display name, each item carrying `guid`/`displayName`/`nameSource`.
- [X] T039 [P] [US2] Frontend test in `frontend/src/features/admin/__tests__/blueprintsList.test.tsx` — list renders display names; search narrows case-insensitively; both empty states (no blueprints / no matches) per FR-024.

### Implementation for User Story 2

- [X] T040 [P] [US2] Create `GetBlueprintsQuery.cs` (empty record) and `BlueprintListItemDto.cs` (Guid, DisplayName, NameSource, ProductName?, Tag, Manufacturer?) in `backend/src/NajaEcho.Application/Features/Blueprints/GetBlueprints/`.
- [X] T041 [US2] Implement `BlueprintRepository.GetListAsync` — LEFT JOIN `sc.items` on `items.uuid = blueprints.id::text` with `soft_deleted_at IS NULL`, deterministic winner on duplicate uuids, `COALESCE(NULLIF(product_name,''), item.name, NULLIF(tag,''), id::text)` display name + `nameSource`, ordered case-insensitively.
- [X] T042 [US2] Implement `GetBlueprintsHandler.cs` and register it in DI.
- [X] T043 [US2] Create `BlueprintListResponse` contract and add `GET /` to `BlueprintAdminEndpoints.cs`.
- [X] T044 [P] [US2] Add `getBlueprints()` to `blueprintsApi.ts`, `useBlueprints.ts` query hook, and `BlueprintListResponse` schema to `blueprintSchemas.ts`.
- [X] T045 [US2] Build `frontend/src/features/admin/components/BlueprintsList.tsx` (search input + `useMemo` case-insensitive filter + table + both empty states, CategorySelector pattern) and wire it into the list region of `BlueprintsImportTab.tsx`.

**Checkpoint**: Both P1 stories work; import + list/search are independently functional.

---

## Phase 5: User Story 3 - Access Control (Priority: P1)

**Goal**: Both endpoints and the tab are admin-only, matching existing import pages.

**Independent Test**: As non-admin, hitting either endpoint returns 401/403 and the route redirects away.

### Tests for User Story 3 ⚠️ (write first, must fail)

- [X] T046 [P] [US3] API auth-matrix tests in `backend/tests/NajaEcho.Api.Tests/Features/Admin/Blueprints/BlueprintAuthTests.cs` — unauthenticated → 401 and authenticated non-admin → 403 on both `POST /import` and `GET /` (SC-008).
- [X] T047 [P] [US3] Frontend admin-guard test following the existing `adminAccess` test pattern — non-admin cannot reach the Blueprints tab under `/dashboard/admin/data-import`.

### Implementation for User Story 3

- [X] T048 [US3] Confirm `RequireAuthorization(AuthorizationPolicies.Admin)` on the group (added in T019) enforces both endpoints and that 401/403 map to problem+json; add no new route/nav (existing `AdminRoute` guard covers the tab).

**Checkpoint**: Authorization verified on the full surface.

---

## Phase 6: User Story 4 - Re-Upload to Update the Dataset (Priority: P2)

**Goal**: Re-import upserts blueprints by guid, rebuilds derived rows with no stale records, refreshes reference data, retains absent blueprints.

**Independent Test**: Import file A, then file B (one changed blueprint, one new, extra resource); confirm 1 updated / 1 inserted, derived rows reflect only B, reference data refreshed, A-only blueprints retained.

### Tests for User Story 4 ⚠️ (write first, must fail)

- [X] T049 [P] [US4] Testcontainers test in `backend/tests/NajaEcho.Infrastructure.Tests/Blueprints/BlueprintReimportTests.cs` — re-import upserts by guid (correct updated/inserted counts), fully replaces `tiers` jsonb + rebuilds `blueprint_tiers`/`blueprint_slot_options` (no stale/orphan rows), replaces reference tables with refreshed material resolution, retains blueprints absent from the new file (FR-013/014/016, FR-009b, SC-004).

### Implementation for User Story 4

- [X] T050 [US4] Ensure `BlueprintRepository.ImportAsync` (T030) performs upsert-by-`Id` with delete+reinsert of derived rows and wholesale reference replacement, and that the handler reports `updated` vs `inserted` counts correctly; extend as needed to pass T049.
- [X] T051 [US4] Verify `BlueprintImportSummary.tsx` renders updated-vs-inserted counts per collection (adjust if the summary only showed inserts).

**Checkpoint**: Re-upload maintenance flow works without duplicates or orphans.

---

## Phase 7: User Story 5 - Validation and Partial-Failure Handling (Priority: P2)

**Goal**: Malformed blueprint entries are rejected with reasons without aborting the import; an invalid top-level document is rejected wholesale (400, nothing stored); meta mismatches warn.

**Independent Test**: Upload a mixed valid/invalid file → valid stored, each rejection reported; upload a document missing `dismantle` → 400, `sc.blueprints` unchanged.

### Tests for User Story 5 ⚠️ (write first, must fail)

- [X] T052 [P] [US5] Parser/handler unit tests in `backend/tests/NajaEcho.Application.Tests/Features/Blueprints/BlueprintRejectionTests.cs` — reject missing required field / non-UUID guid / option matching neither variant, each with identifying value + reason (FR-017, FR-020); duplicate guid → first wins, later rejected (FR-018, Decision 8); meta-total mismatch → warning not failure (FR-021).
- [X] T053 [P] [US5] Integration/API tests — invalid top-level document (missing `dismantle` / invalid JSON) → 400 with nothing stored (`sc.blueprints` unchanged); forced mid-transaction failure rolls everything back leaving prior data intact (FR-021a, SC-010). Add to `backend/tests/NajaEcho.Infrastructure.Tests/Blueprints/BlueprintImportTests.cs` and the API endpoint test class.

### Implementation for User Story 5

- [X] T054 [US5] Add rejection + dedup logic to `BlueprintParser.cs` — missing required field, non-UUID guid, option matching neither resource nor item variant → `rejections` (with guid/productName + reason); duplicate guid first-wins.
- [X] T055 [US5] Harden `ImportBlueprintsHandler`/`POST /import` — top-level invalid/missing-section document → 400 (nothing stored, no coordinator write); add meta-total-mismatch warnings (FR-021) alongside unmatched-material warnings.
- [X] T056 [US5] Extend `BlueprintImportSummary.tsx` to render the rejection table + warnings list, and confirm `BlueprintsImportTab.tsx` shows client-side invalid-JSON and oversize (>50 MB) rejection messages before any network call.

**Checkpoint**: All five stories functional; robustness and atomicity verified.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T057 [P] Verify the structured import outcome log line (version + per-collection counts + rejected count + duration; no file contents / auth data) matches `ImportItemsHandler`'s observability shape.
- [X] T058 Run `dotnet test backend` and `cd frontend && npm test` — all suites green; then `cd frontend && npm run lint` and `dotnet build backend` clean.
- [ ] T059 Execute the `quickstart.md` scenarios 1–6 manually against a running stack to confirm end-to-end behavior (clean import, list/search, access control, re-upload, validation, concurrency/limits).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup. BLOCKS all user stories. Within it: domain (T003–T009) → EF configs (T010–T015) → DbContext (T016) → migration (T017); port (T018), endpoint group (T019), and frontend shell (T020–T021) can proceed once their prerequisites exist.
- **US1 (Phase 3)**: Depends on Foundational. Delivers the MVP.
- **US2 (Phase 4)**: Depends on Foundational only (seeds DB directly for its independent test) — does not require US1's handler. Shares the endpoint group (T019) and tab shell (T020).
- **US3 (Phase 5)**: Depends on Foundational (endpoint group). Verifies auth on endpoints created by US1/US2.
- **US4 (Phase 6)**: Builds on US1's `ImportAsync` (T030) — hardens upsert/rebuild semantics.
- **US5 (Phase 7)**: Builds on US1's parser/handler/endpoint — adds rejection/wholesale-400/warnings.
- **Polish (Phase 8)**: After all targeted stories.

### Story Independence

- US1, US2, US3 (all P1) are independently testable once Foundational is done. US2 can be built and tested in parallel with US1 (different files: `GetBlueprints/*`, `GetListAsync`, `BlueprintsList.tsx`).
- US4 and US5 (P2) extend US1's shared `ImportAsync`/parser and are best sequenced after US1 to avoid same-file churn.

### Within Each Story

- Tests written first and failing → implementation → checkpoint.
- Models before configs before repository; repository before handler before endpoint; backend before its frontend hook.

---

## Parallel Opportunities

- **Setup**: T001, T002 in parallel.
- **Foundational domain**: T003–T009 all [P] (distinct files). Then EF configs T010–T015 all [P]. T020/T021 [P] with backend work.
- **US1 tests**: T022–T026 all [P] (distinct test files/layers).
- **US2 tests**: T037–T039 all [P].
- **Cross-story (P1)**: With capacity, US2 (Phase 4) proceeds alongside US1 (Phase 3) after Foundational.

### Parallel Example: Foundational domain entities

```bash
Task: "Create CraftingBlueprint in .../Blueprints/CraftingBlueprint.cs"
Task: "Create CraftingBlueprintTier in .../Blueprints/CraftingBlueprintTier.cs"
Task: "Create CraftingBlueprintSlotOption in .../Blueprints/CraftingBlueprintSlotOption.cs"
Task: "Create CraftingMaterial + CraftingMaterialKind"
Task: "Create CatalogSource enum"
Task: "Create CraftingProperty"
Task: "Create CraftingDataset"
```

---

## Implementation Strategy

### MVP First

1. Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1).
2. **STOP and VALIDATE**: upload the sample file, confirm atomic storage + summary. Demoable MVP.

### Incremental Delivery

1. Foundation ready → US1 (import MVP) → US2 (list/search) → US3 (auth verified) — all P1, ship as the first complete increment.
2. Add US4 (re-upload) → US5 (validation/partial-failure) — P2 hardening.
3. Polish (Phase 8) → run quickstart + full test suites.

---

## Notes

- [P] = different files, no incomplete-task dependency.
- Tests precede implementation and must fail first (constitution II / plan TDD gate).
- `AddCraftingBlueprints` is additive-only — no destructive-migration approval needed.
- Zero new backend/frontend dependencies (YAGNI).
- Commit after each task or logical group.
