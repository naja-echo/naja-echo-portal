# Quickstart: Crafting Blueprint Import

**Feature**: `020-crafting-blueprint-import`

Validation scenarios proving the feature works end-to-end. Contract details:
[contracts/openapi.yaml](./contracts/openapi.yaml); file shape:
[contracts/star-citizen-blueprints.schema.json](./contracts/star-citizen-blueprints.schema.json);
storage: [data-model.md](./data-model.md).

## Prerequisites

- Docker running (PostgreSQL via `docker-compose.yml`, Testcontainers for integration tests).
- Backend: `.NET 10 SDK`; Frontend: Node + npm (`frontend/`).
- An admin user (role `Admin`) and a non-admin user to verify access control.
- A sample dataset file conforming to the JSON Schema (500+ blueprints for scale checks; a small
  hand-made file is fine for functional checks).

## Setup

```bash
# from repo root
docker compose up -d db
./migrate.sh                      # applies AddCraftingBlueprints (creates the six sc.* tables)
dotnet run --project backend/src/NajaEcho.Api
# separate shell
cd frontend && npm install && npm run dev
```

## Automated test suites

```bash
# Backend — all layers (unit + Testcontainers integration + API contract)
dotnet test backend

# Frontend
cd frontend && npm test
```

Expected: all green. Key suites to look for:
- `NajaEcho.Application.Tests/Features/Blueprints/` — parser/handler unit tests (per-entry
  rejection reasons, duplicate-guid first-wins, null modifiers → empty set, meta-mismatch and
  unmatched-material warnings, coordinator 409 path).
- `NajaEcho.Infrastructure.Tests/Blueprints/` — Testcontainers: atomic import transaction
  (forced mid-import failure leaves prior data intact — SC-010), upsert-by-guid, tiers jsonb +
  derived tier/slot-option rows rebuilt on re-import (no orphans), material lookup precedence
  (commodity first, null-uuid fall-through to items, case-insensitive, soft-deleted excluded),
  reference tables replaced, display-name join incl. fallback chain.
- `NajaEcho.Api.Tests/Features/Admin/Blueprints/` — 200 with counts, 400 invalid document,
  401/403 auth matrix, 409 concurrent import.
- `frontend/src/features/admin/__tests__/` — blueprints tab: file select + upload flow, disabled
  while pending, result summary rendering, list + search filter + empty states.

## Scenario 1 — Clean import (US1)

1. Sign in as admin → Dashboard → Admin → Data Import → **Blueprints** tab.
2. Select a valid dataset JSON file; click upload.
3. **Expected**: loading state while pending (upload disabled); then a result summary showing the
   dataset `version` and per-collection read/inserted/updated/rejected counts, with
   `referenceDataReplaced` noted; blueprint list below now shows all imported blueprints.
4. Verify storage directly (optional): `SELECT count(*) FROM sc.blueprints;` matches the inserted
   count; `SELECT version FROM sc.crafting_datasets;` matches the file; tier/option rows exist —
   e.g. `SELECT count(*) FROM sc.blueprint_slot_options;`.
5. Verify material resolution: for a material name known to exist in `sc.commodities`,
   `SELECT matched_uuid, matched_source FROM sc.crafting_materials WHERE name = '<name>';` shows
   the commodity uuid with source `commodity`; a name only in `sc.items` shows source `item`; a
   name in neither is null/null and appears in the upload summary's warnings.
6. Verify the query shapes the storage exists for (no v1 endpoint — run in SQL):
   `SELECT DISTINCT b.id FROM sc.blueprints b JOIN sc.blueprint_tiers t ON t.blueprint_id = b.id
   JOIN sc.blueprint_slot_options o ON o.tier_id = t.id WHERE o.material_name = 'Savrilium';`
   returns the expected blueprints.

## Scenario 2 — List & search (US2)

1. On the Blueprints tab, confirm every blueprint appears with a name.
2. Seed/verify fallbacks: a blueprint with `productName: null` whose guid matches a `sc.items.uuid`
   shows the item's name; one matching nothing shows its `tag`; with a null-ish tag, the guid.
3. Type a partial name in the search box → list narrows (case-insensitive substring).
4. Search gibberish → "no blueprints match" empty state. With an empty database → empty state
   directing to upload a file.

## Scenario 3 — Access control (US3)

1. As a non-admin, navigate to `/dashboard/admin/data-import` → redirected away (AdminRoute).
2. `curl -X POST …/api/admin/blueprints/import` unauthenticated → 401 problem+json; as
   authenticated non-admin → 403. Same for `GET /api/admin/blueprints`.

## Scenario 4 — Re-upload / upsert (US4)

1. Import file A. Note counts.
2. Import file B = file A with one blueprint's values changed (e.g. different `craftTimeSeconds`,
   fewer tiers) plus one new blueprint and an extra entry in `resources`.
3. **Expected**: summary shows 1 updated, 1 inserted; the changed blueprint's stored `tiers` jsonb
   AND its `sc.blueprint_tiers`/`sc.blueprint_slot_options` rows reflect only file B (no stale or
   orphaned rows); `sc.crafting_materials` matches file B's lists with refreshed catalog
   resolution; dataset `version` updated. Blueprints only in file A remain present.

## Scenario 5 — Validation & partial failure (US5)

1. Upload a file mixing valid entries with: one missing `tag`, one with `guid: "not-a-uuid"`, one
   duplicate guid, one option that is neither resource- nor item-shaped.
2. **Expected**: valid entries stored; summary lists each rejection with guid/productName and a
   reason; duplicate guid → first occurrence imported, second rejected.
3. Upload a file missing the top-level `dismantle` section (or invalid JSON) → 400, clear error in
   the UI, nothing stored (verify `sc.blueprints` unchanged).
4. Upload a file whose `meta.totalBlueprints` disagrees with the actual array length →
   import succeeds with a warning in the summary.

## Scenario 6 — Concurrency & limits

1. Trigger an upload and immediately trigger another (second browser tab or curl) → second gets
   409 "import already in progress"; UI keeps the upload control disabled while pending.
2. Select a file > 50 MB → rejected client-side with a clear message before any network call.
