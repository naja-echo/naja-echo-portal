# Implementation Plan: Crafting Blueprint Import

**Branch**: `020-crafting-blueprint-import` | **Date**: 2026-07-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/020-crafting-blueprint-import/spec.md`

## Summary

Add an admin-only **Blueprints** tab to the existing Data Import page that (1) uploads a complete
crafting-blueprint dataset JSON file and (2) lists all known blueprints with a client-side
name search. The browser parses the selected file and POSTs the document as a JSON body (hangar-008
pattern — no multipart) to a new `POST /api/admin/blueprints/import` endpoint with an explicit
50 MB body limit. The Application layer validates each blueprint individually from `JsonElement`s
(malformed entries rejected with reasons, never aborting the import; invalid top-level document →
400 with nothing stored), resolves every distinct material name to a catalog UUID (commodities
first, then items — case-insensitive, unmatched → null + warning), then a repository persists
everything in **one transaction**: reference data (properties catalog, resources/items name lists
with their resolved UUIDs, dismantle config + dataset version) is replaced wholesale, and
blueprints are upserted by `guid` into a new `sc.blueprints` table whose full nested
tier/slot/option/modifier structure lives in a normalized `tiers jsonb` column, with tiers and
flattened slot options additionally written to `sc.blueprint_tiers` and
`sc.blueprint_slot_options` query tables (rebuilt per blueprint on update → no stale records)
carrying quantity, min quality, material name, and the resolved catalog UUID + source — the
storage foundation for future "blueprints using material X" and material-aggregation queries.
The single-flight `IImportCoordinator` serializes uploads (409 when busy). `GET /api/admin/blueprints` returns the full list with a server-computed display
name (`productName` → linked `sc.items` name via read-time join on `items.uuid` → `tag` → `guid`);
the React tab filters it client-side, mirroring `CategorySelector`.

**API contract changes ARE required** — two new `/api/admin/blueprints` endpoints, defined in
[`contracts/openapi.yaml`](./contracts/openapi.yaml) before implementation (the uploaded file's
shape is separately pinned by
[`contracts/star-citizen-blueprints.schema.json`](./contracts/star-citizen-blueprints.schema.json)).
The constitution's UI-only exemption does **not** apply.

## Technical Context

**Language/Version**: C# on .NET (`net10.0` per the solution) backend; TypeScript (strict) frontend.

**Primary Dependencies**: Backend — ASP.NET Core Minimal APIs, EF Core + `Npgsql`
(snake_case convention), `System.Text.Json` (lenient per-blueprint parsing via `JsonElement`),
existing `IImportCoordinator` (singleton `SemaphoreSlim`), Serilog. Frontend — React 19 (Vite),
React Router 7, Tailwind, shadcn/ui (`Tabs`, `Card`, `Button`, `Alert`, `Table`), Lucide, TanStack
Query 5, Zod 4, `apiFetch`. **No new backend or frontend dependency** (YAGNI) — file reading uses
the browser `File` API; parsing uses `System.Text.Json` already in use by `ImportItemsHandler`.

**Storage**: PostgreSQL 16, **`sc` schema** (FR-011), via per-table `ToTable(..., schema: "sc")`
(no default-schema change). One additive, forward-only migration `AddCraftingBlueprints` creating
six tables: `sc.blueprints` (PK = blueprint guid; descriptive columns; `tiers jsonb` full nested
structure incl. modifiers; `imported_at`/`updated_at`), `sc.blueprint_tiers` (FK cascade,
`tier_index`, `craft_time_seconds`), `sc.blueprint_slot_options` (flat one-row-per-option query
table: FK cascade to tier, slot/option indexes + slot name, kind, `material_name`, `quantity`,
`min_quality`, resolved `matched_uuid` + `matched_source`; indexed on `material_name` and
`matched_uuid` — FR-009a/b), `sc.crafting_materials` (composite PK `kind`+`name` + resolved
`matched_uuid`/`matched_source`), `sc.crafting_properties` (PK `property_key`; `name_overrides
jsonb`), `sc.crafting_datasets` (single-row snapshot: version, meta totals, dismantle config with
blacklists as jsonb). Material names resolve at import via case-insensitive exact match —
`sc.commodities` first, then `sc.items`, soft-deleted and empty-uuid rows excluded, deterministic
winner on duplicates, unmatched → null + warning (research Decision 11). No FK to `sc.items` for
the blueprint↔product link — that resolves at read time by joining `items.uuid =
blueprints.id::text` (research Decision 5; `items.uuid` is non-unique varchar and item ids
regenerate on catalog re-import). Full details in [data-model.md](./data-model.md).

**Testing**: Backend — xUnit + FluentAssertions.
- Application unit tests (fakes for repository/coordinator): parser accepts valid blueprints and
  normalizes null modifiers → empty set (FR-007); rejects missing required field / non-UUID guid /
  option matching neither variant, each with an identifying value + reason (FR-017, FR-020);
  duplicate guid → first wins, later occurrences rejected (FR-018, research Decision 8); optional
  fields present/absent preserved (FR-006); meta-total mismatch → warning, not failure (FR-021);
  empty blueprint list still refreshes reference data; coordinator busy →
  `ImportAlreadyInProgressException`; validation completes before any repository write (Decision 7);
  material resolution applied from the lookup map — unmatched names → null link + warning (FR-009).
- Testcontainers (PostgreSQL) integration tests: full import persists all six tables incl. derived
  tier/slot-option rows with correct indexes/quantities; re-import upserts by guid, fully replaces
  `tiers` jsonb + rebuilds tier/option rows (no stale/orphan rows) + reference tables
  (FR-013/014/016, FR-009b, SC-004); material lookup precedence — commodity-first, null/empty-uuid
  commodity falls through to items, case-insensitive, soft-deleted excluded, deterministic winner
  on duplicate names, unmatched stored null (FR-009, research Decision 11); forced mid-transaction
  failure rolls everything back leaving prior data intact (FR-021a, SC-010); listing query computes
  the display-name fallback chain incl. linked-item name, soft-deleted items excluded, unmatched
  guid → unlinked but present (FR-015, FR-022, SC-006).
- API contract tests (`WebApplicationFactory`, `PostAsJsonAsync`): 200 with exact
  `ImportBlueprintsResponse` shape; 400 invalid/missing-section document with nothing stored;
  401 unauthenticated / 403 non-admin on both endpoints (SC-008); 409 when an import is in
  progress; GET returns list ordered by display name.

Frontend — Vitest + RTL + mocked `apiFetch`: file selection → parse → POST flow; invalid-JSON and
oversize (>50 MB) files rejected client-side with a message; upload control disabled while pending
(FR-003, SC-009); result summary renders version, per-collection counts, warnings, and rejection
reasons; list renders display names; search narrows case-insensitively; both empty states
(no blueprints / no matches, FR-024); admin route guarding per existing `adminAccess` test pattern.

**Target Platform**: Linux server (containerized API) + browser SPA.

**Performance Goals**: Dataset scale 500–5,000 blueprints (spec Scale). Import is synchronous
within one request (FR-003a): parse + validate in memory, then one transaction of bulk upserts —
well within interaction-time expectations at this scale (SC-001). Listing returns ≤ ~5,000
name-only rows in one response; search is instant client-side filtering (SC-007).

**Constraints**:
- Upload body limit 50 MB, set explicitly on the import endpoint
  (`RequestSizeLimitAttribute` metadata); the client checks file size first and rejects with a
  clear message (research Decision 2).
- Whole-document atomicity: reference-data refresh + all valid upserts in a single
  `BeginTransactionAsync` scope; per-entry rejection happens during pre-transaction validation and
  never triggers rollback (FR-021a, research Decision 7).
- One import at a time across all catalog types via the shared singleton `IImportCoordinator`
  (FR-003, research Decision 9).
- Blueprints absent from a new file are retained — no delete/soft-delete sweep in v1 (spec
  Assumptions).
- Material catalog resolution is a snapshot at blueprint-import time; catalog re-imports do not
  retro-update stored links (refreshed on the next blueprint upload). Unmatched names warn, never
  reject (FR-009). Material-based query endpoints themselves are out of scope in v1 — only the
  storage supports them (FR-009a/b).
- Structured logging: import outcome line with version + per-collection counts + rejection count;
  no file contents logged.

### Verified existing facts (from codebase inspection)

- **File-upload precedent** (`Api/Features/Hangar/HangarEndpoints.cs:190-219` +
  `frontend/src/features/hangar/components/ImportHangarDialog.tsx`): browser does `file.text()` +
  `JSON.parse` + Zod check, POSTs parsed JSON; endpoint binds a nullable DTO, 400 on null. No
  `IFormFile`/multipart anywhere in the codebase. No request-size configuration exists today
  (Kestrel ~30 MB default) — hence the explicit per-endpoint limit.
- **Admin import endpoint pattern** (`Api/Features/Admin/Items/ItemAdminEndpoints.cs`):
  `MapGroup("/api/admin/items").RequireAuthorization(AuthorizationPolicies.Admin)`;
  `ImportAlreadyInProgressException` → 409; registered via `app.MapItemAdminEndpoints()` in
  `Program.cs` (~lines 261-270). `BlueprintAdminEndpoints` mirrors this.
- **Single-flight lock** (`Infrastructure/Imports/ImportCoordinator.cs`,
  `Application/Abstractions/IImportCoordinator.cs`): singleton `SemaphoreSlim(1,1)`,
  `TryAcquire()`/`Release()` in try/finally — reused as-is.
- **`sc` schema pattern** (`Persistence/Configurations/ItemConfiguration.cs`): default schema is
  `public`; catalog tables opt in via `ToTable("items", schema: "sc")`; snake_case columns; enums
  stored via `HasConversion<string>()`; jsonb columns (`Item.RawData` is `JsonDocument` → `jsonb`).
- **`sc.items` link target** (`Domain/Items/Item.cs`): PK `Guid Id` is app-generated — the game
  UUID is the separate **non-unique** `uuid varchar(128)` column (`ix_items_uuid`, migration
  `AllowDuplicateItemUuid`); `soft_deleted_at` marks catalog removals. This is why the blueprint→
  item link is a read-time join, not a stored FK.
- **Material lookup targets**: `Domain/Commodities/Commodity.cs` — `Uuid` is a **nullable**
  `varchar(128)` (hence the null-uuid fall-through to items) and `Name varchar(512)` required;
  `Item.Uuid` is non-null `varchar(128)`. Both have `SoftDeletedAt` and `UexId` (the deterministic
  tie-breaker for duplicate names). Both catalogs are in the same `AppDbContext`, so the batched
  lookup needs no new port beyond `IBlueprintRepository`.
- **Transaction precedent** (`Infrastructure/Items/ItemRepository.cs`
  `BulkUpsertForCategoryAsync`): explicit `BeginTransactionAsync` … `SaveChangesAsync` …
  `CommitAsync`, duplicate-tolerant dictionaries. Blueprint repository follows this shape.
- **Migrations** (`Infrastructure/Persistence/Migrations/`): `{timestamp}_{PascalName}` naming;
  `migrate.sh` at repo root; DbSets + `ApplyConfiguration` in `AppDbContext.OnModelCreating`
  (lines 41-58).
- **DI** (`Infrastructure/DependencyInjection.cs`): `AddScoped` per repository + handler;
  `IImportCoordinator` already singleton (line 98). No HTTP client needed for this feature.
- **Frontend import area** (`features/admin/pages/DataImportPage.tsx`): shadcn `Tabs` with one
  `<XxxImportTab/>` per catalog — add `<TabsTrigger value="blueprints">` +
  `<BlueprintsImportTab/>`. Route `/dashboard/admin/data-import` already guarded by
  `ProtectedRoute` → `DashboardLayout` → `AdminRoute` (`session.user.roles.includes('Admin')`);
  nav entry exists — **no new route or nav item needed** (satisfies FR-001 by extension).
- **Client-filter precedent** (`features/admin/components/CategorySelector.tsx`): `useState`
  search + `useMemo` case-insensitive `.includes` filter + empty state + "x of y" count — the
  blueprint list copies this.
- **API layer** (`frontend/src/lib/apiClient.ts`): `apiFetch` with `credentials: 'include'`,
  throws `ApiError(status, message)`; feature api modules `schema.parse` responses; mutation hooks
  via TanStack `useMutation` with key invalidation (`features/admin/hooks/useImportItems.ts`).
- **No blueprint code exists anywhere** — greenfield inside an established pattern set.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. API-Contract-First | PASS | Two new endpoints fully specified in `contracts/openapi.yaml` (request body pinned by the JSON Schema) before implementation. Not UI-only — exemption correctly **not** invoked. |
| II. Test-First / TDD | PASS | Failing tests first across all layers: parser/validation unit tests (rejects, duplicates, normalization, warnings), Testcontainers atomic-transaction + upsert-replace + display-name-join tests, API auth/400/409/shape tests, and frontend upload/summary/search/empty-state tests (mapped to FRs/SCs in Technical Context → Testing). |
| III. Frontend/Backend Separation | PASS | Backend owns validation, atomicity, counts, and display-name computation; SPA only reads the file, POSTs JSON, and renders. **Approved deviation — hand-written Zod schemas** reviewed against the contract instead of codegen (established 017/018/019 pattern); any contract change ships with the matching schema in the same PR. |
| IV. Simplicity / YAGNI | PASS | Normalization goes exactly as deep as the stated query needs (blueprints-by-material, material aggregation — both option-grain): tiers table + one flat option table, slots flattened into option rows, modifiers kept only in the `tiers` jsonb (no modifier table — nothing queries them; research Decision 4). One polymorphic `crafting_materials` table; catalog resolution stored as plain columns, not FKs (catalog PKs regenerate — Decision 11); no stored item FK for the product link (read-time join); reuse of `IImportCoordinator`, tabs page, and client-filter pattern; zero new dependencies. |
| V. Observability | PASS | Handler logs a structured outcome line (version, per-collection counts, rejected count, duration) mirroring `ImportItemsHandler`; 400/409 paths log reasons; no file contents or auth data logged; existing Serilog request logging + correlation applies. |
| VI. Modular Monolith + Clean Architecture | PASS | `Blueprints` entities in Domain (no dependencies); parser + `ImportBlueprints`/`GetBlueprints` use cases + `IBlueprintRepository` port in Application; EF repository, configs, migration in Infrastructure; endpoints + response contracts in Api. Frontend logic lives in `features/admin/` hooks/api/schemas; `DataImportPage` route stays thin; no new nav (existing data-driven entry covers it). |

**Migration governance note**: `AddCraftingBlueprints` is **additive only** (six `CreateTable`, no
alters/drops; cascade FKs are internal to the new tables) — the destructive-migration approval
requirement is not triggered.

**Post-design re-check**: PASS — design artifacts introduce no new violations. Complexity Tracking
intentionally empty.

## Project Structure

### Documentation (this feature)

```text
specs/020-crafting-blueprint-import/
├── plan.md                                  # This file (/speckit-plan output)
├── research.md                              # Phase 0 — Decisions 1-10
├── data-model.md                            # Phase 1 — six sc.* tables + jsonb shapes
├── quickstart.md                            # Phase 1 — validation scenarios
├── contracts/
│   ├── openapi.yaml                         # Phase 1 — POST /import + GET list
│   └── star-citizen-blueprints.schema.json  # Authoritative uploaded-file shape (pre-existing)
├── checklists/requirements.md               # Spec quality checklist (complete)
└── tasks.md                                 # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
backend/src/
├── NajaEcho.Domain/
│   └── Blueprints/
│       ├── CraftingBlueprint.cs             # NEW — Id(=guid) PK, descriptive fields, Tiers (JsonDocument), ImportedAt/UpdatedAt
│       ├── CraftingBlueprintTier.cs         # NEW — BlueprintId FK, TierIndex, CraftTimeSeconds
│       ├── CraftingBlueprintSlotOption.cs   # NEW — TierId FK, Slot/OptionIndex, SlotName, Kind, MaterialName, Quantity, MinQuality, MatchedUuid?, MatchedSource?
│       ├── CraftingMaterial.cs              # NEW — Kind + Name (composite identity) + MatchedUuid?/MatchedSource?
│       ├── CraftingMaterialKind.cs          # NEW — enum { Resource, Item }
│       ├── CatalogSource.cs                 # NEW — enum { Commodity, Item }
│       ├── CraftingProperty.cs              # NEW — Key, Name, Unit?, Category, NameOverrides (JsonDocument?)
│       └── CraftingDataset.cs               # NEW — version, meta totals, dismantle config, blacklists (jsonb), ImportedAt
├── NajaEcho.Application/
│   ├── Abstractions/
│   │   └── IBlueprintRepository.cs          # NEW — ResolveMaterialsAsync(names) → uuid/source map (read-only, commodities→items); ImportAsync(parsed snapshot) → persistence counts (single tx); GetListAsync() → list rows
│   └── Features/Blueprints/
│       ├── ImportBlueprints/
│       │   ├── ImportBlueprintsCommand.cs   # NEW — typed top-level sections + List<JsonElement> blueprints
│       │   ├── ImportBlueprintsHandler.cs   # NEW — coordinator lock; parse/validate all → ResolveMaterialsAsync → repo.ImportAsync; builds result + warnings (meta mismatch + unmatched materials)
│       │   ├── BlueprintParser.cs           # NEW — pure per-element validation + normalization (null modifiers→[], variant check, UUID check, first-wins dedup); emits tiers jsonb + derived tier/slot-option row data
│       │   └── ImportBlueprintsResult.cs    # NEW — version, per-collection counts, referenceDataReplaced, warnings, rejections
│       └── GetBlueprints/
│           ├── GetBlueprintsQuery.cs        # NEW — (empty record)
│           ├── GetBlueprintsHandler.cs      # NEW
│           └── BlueprintListItemDto.cs      # NEW — Guid, DisplayName, NameSource, ProductName?, Tag, Manufacturer?
├── NajaEcho.Infrastructure/
│   ├── Blueprints/
│   │   └── BlueprintRepository.cs           # NEW — batched case-insensitive material lookup (commodities→items, lowest uex_id wins); one BeginTransactionAsync: replace materials/properties/dataset + upsert blueprints by PK + rebuild tier/slot-option rows; list query w/ COALESCE display-name join on items.uuid
│   ├── Persistence/
│   │   ├── AppDbContext.cs                  # + 6 DbSets + ApplyConfiguration ×6 (edited)
│   │   ├── Configurations/
│   │   │   ├── CraftingBlueprintConfiguration.cs        # NEW — sc.blueprints, tiers jsonb
│   │   │   ├── CraftingBlueprintTierConfiguration.cs    # NEW — sc.blueprint_tiers, FK cascade, unique (blueprint_id, tier_index)
│   │   │   ├── CraftingBlueprintSlotOptionConfiguration.cs # NEW — sc.blueprint_slot_options, FK cascade, ix on material_name + matched_uuid
│   │   │   ├── CraftingMaterialConfiguration.cs         # NEW — sc.crafting_materials, composite PK, kind→string, matched cols
│   │   │   ├── CraftingPropertyConfiguration.cs         # NEW — sc.crafting_properties
│   │   │   └── CraftingDatasetConfiguration.cs          # NEW — sc.crafting_datasets
│   │   └── Migrations/*_AddCraftingBlueprints.cs        # NEW — additive: 6 CreateTable in sc schema
│   └── DependencyInjection.cs               # + IBlueprintRepository + 2 handlers (edited)
└── NajaEcho.Api/
    ├── Features/Admin/Blueprints/
    │   ├── BlueprintAdminEndpoints.cs       # NEW — MapGroup("/api/admin/blueprints") + Admin policy; POST /import (50 MB RequestSizeLimit, 400/409 mapping); GET /
    │   └── Contracts/                       # NEW — ImportBlueprintsRequest/Response, CollectionCountsResponse, BlueprintRejectionResponse, BlueprintListResponse
    └── Program.cs                           # + app.MapBlueprintAdminEndpoints() (edited)

backend/tests/
├── NajaEcho.Application.Tests/Features/Blueprints/   # NEW — parser + handler unit tests
├── NajaEcho.Infrastructure.Tests/Blueprints/         # NEW — Testcontainers: atomic tx, upsert/replace, display-name join
└── NajaEcho.Api.Tests/Features/Admin/Blueprints/     # NEW — endpoint contract/auth/409 tests

frontend/src/
├── features/admin/
│   ├── pages/DataImportPage.tsx             # + Blueprints TabsTrigger/TabsContent (edited)
│   ├── components/
│   │   ├── BlueprintsImportTab.tsx          # NEW — upload zone (hidden file input, 50 MB cap, parse+shape check, pending/disabled state, result summary) + list below
│   │   ├── BlueprintImportSummary.tsx       # NEW — version, per-collection count cards, warnings, rejection table
│   │   └── BlueprintsList.tsx               # NEW — search input + table + both empty states (CategorySelector pattern)
│   ├── api/blueprintsApi.ts                 # NEW — importBlueprints(document), getBlueprints() via apiFetch
│   ├── hooks/
│   │   ├── blueprintKeys.ts                 # NEW — query-key factory
│   │   ├── useImportBlueprints.ts           # NEW — useMutation + invalidate list
│   │   └── useBlueprints.ts                 # NEW — useQuery list
│   ├── schemas/blueprintSchemas.ts          # NEW — Zod: light top-level dataset check (pre-POST), ImportBlueprintsResponse, BlueprintListResponse
│   └── __tests__/
│       ├── blueprintsImportTab.test.tsx     # NEW — upload flow, size/JSON rejection, pending, summary
│       └── blueprintsList.test.tsx          # NEW — names, search, empty states
```

**Structure Decision**: Backend follows the established four-project Clean Architecture split — a
new `Blueprints` domain folder (six entities, six `sc`-schema tables via one additive migration:
blueprint + normalized tier/flat-option query tables + three reference tables), an
`IBlueprintRepository` port exposing the read-only material lookup and an `ImportAsync` that owns
the single atomic transaction (including the per-blueprint rebuild of derived tier/option rows), a
pure `BlueprintParser` in the Application layer so all validation/rejection/warning logic is
unit-testable without a database, and a `BlueprintAdminEndpoints` minimal-API group registered in
`Program.cs` behind the existing Admin policy. Frontend adds no routes or nav: everything lives in
the existing `features/admin/` Data Import page as a new tab combining the upload surface
(hangar file-input pattern) and the searchable list (CategorySelector client-filter pattern),
with feature-owned hooks/api/schemas per the constitution's frontend conventions.

## Complexity Tracking

> No unjustified constitution violations — table intentionally empty. The storage design carries
> both a `tiers` jsonb column and derived tier/option rows; this is deliberate, not speculative
> (research Decision 4): the jsonb is the full-fidelity record satisfying FR-005/FR-014 (and the
> only home of modifiers, which nothing queries), while the two normalized tables serve the
> explicitly stated material-query needs (FR-009a/b) at exactly the option grain — slots are
> flattened into option rows rather than given their own table, and no modifiers table exists.
