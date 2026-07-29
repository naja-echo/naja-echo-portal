# Research: Crafting Blueprint Import

**Feature**: `020-crafting-blueprint-import` | **Date**: 2026-07-17

No NEEDS CLARIFICATION markers remained after `/speckit-clarify`; the decisions below resolve the
technical choices the spec leaves open, grounded in codebase inspection of the existing import
features (006/008/009/010/016/018).

## Decision 1 — Upload transport: JSON request body, not multipart

**Decision**: The browser reads the selected file (`file.text()` + `JSON.parse`) and POSTs the
parsed document as an `application/json` body to `POST /api/admin/blueprints/import`. No
`IFormFile`/multipart.

**Rationale**: This is exactly the hangar JSON import (008) pattern
(`frontend/src/features/hangar/components/ImportHangarDialog.tsx` →
`HangarEndpoints.ImportHangar` binding a JSON DTO). The codebase has **zero** multipart precedent
— no upload helpers, no multipart tests, no `IFormFile` anywhere. Reusing the JSON-body shape keeps
the endpoint testable with `PostAsJsonAsync` like every existing import test.

**Alternatives considered**: `IFormFile` multipart upload — rejected: introduces a new
request-handling, OpenAPI, and test pattern for no functional gain; the client must parse the JSON
anyway to give fast top-level validation feedback.

## Decision 2 — Request size limit raised explicitly for this endpoint

**Decision**: Client-side cap of **50 MB** on the selected file; server-side, the import endpoint
gets explicit `.WithMetadata(new RequestSizeLimitAttribute(52_428_800))` (50 MB) so the limit is a
deliberate, visible number. Oversized uploads are rejected with a clear message client-side before
any network call; the server limit is the backstop.

**Rationale**: No existing endpoint configures a body size, so Kestrel's ~30 MB default applies
everywhere today. A 5,000-blueprint dataset with full tier/slot/option/modifier trees can
plausibly approach that default; the spec requires files above the limit be rejected "with a clear
message" (Edge Cases, Assumptions). 50 MB comfortably covers the stated scale (500–5,000
blueprints) without inviting abuse.

**Alternatives considered**: Global `MaxRequestBodySize` bump — rejected (widens every endpoint);
streaming parse — rejected as YAGNI at ≤5,000 blueprints.

## Decision 3 — Lenient per-blueprint binding via `JsonElement`

**Decision**: The request DTO types the top-level sections strictly (`version`, `meta`,
`dismantle`, `properties`, `resources`, `items`) but binds `blueprints` as
`List<JsonElement>`. The Application-layer parser validates each blueprint element individually
(required fields, UUID-format `guid`, option variant shape) and rejects bad entries with a reason
while the rest import.

**Rationale**: Strictly-typed binding of the whole document would fail the entire request on one
malformed blueprint, violating FR-017/US5 (partial-failure tolerance). Conversely, the top-level
sections MUST be wholesale-valid (FR-004), so strict binding there is correct: a missing/mistyped
section or invalid JSON yields a 400 with nothing stored. The `System.Text.Json` element-walking
style already exists in `ImportItemsHandler`'s extraction helpers.

**Alternatives considered**: Bind everything as `JsonDocument` — rejected: pushes top-level
validation into hand-rolled code that model binding gives us for free. FluentValidation over a
fully-typed DTO — rejected: cannot express "skip this entry, keep the rest" without contorting.

## Decision 4 — Storage shape: `sc.blueprints` (with jsonb `tiers`) + normalized tier/option tables + three reference tables

*(Revised 2026-07-18 to support material-based queries — FR-009a/FR-009b.)*

**Decision**: Six new tables, all in the `sc` schema:

1. `sc.blueprints` — one row per blueprint, PK = the blueprint `guid` itself (`Guid Id`).
   Descriptive fields as typed snake_case columns; the full nested **tiers → slots → options +
   modifiers** structure kept as a validated/normalized `tiers jsonb` column (the full-fidelity
   record, and the only home of modifiers).
2. `sc.blueprint_tiers` — one row per tier: FK to `sc.blueprints` (cascade delete),
   `tier_index`, `craft_time_seconds`; unique (`blueprint_id`, `tier_index`).
3. `sc.blueprint_slot_options` — the **flat query table**: one row per slot option, FK to
   `sc.blueprint_tiers` (cascade delete), carrying `slot_index`, `slot_name`, `option_index`,
   `kind` (`resource`|`item`), `material_name`, `quantity`, `min_quality`, and the resolved
   `matched_uuid` + `matched_source` (Decision 11). Indexed on `material_name` and `matched_uuid`.
4. `sc.crafting_materials` — the dataset's `resources` and `items` name lists in one table with a
   `kind` discriminator (`resource` | `item`), PK (`kind`, `name`), plus the resolved
   `matched_uuid` + `matched_source`.
5. `sc.crafting_properties` — properties catalog, PK = `property_key`; `name_overrides` as jsonb.
6. `sc.crafting_datasets` — single-row snapshot table: dataset `version`, meta totals, dismantle
   config (`efficiency`, `dismantle_time_seconds`, blacklists as jsonb), `imported_at`.

**Rationale**: Two concrete near-term query needs justify normalization beyond jsonb: "list all
blueprints that use material X" and "aggregate material requirements to craft items A, B, C". Both
operate at the *option* grain, so slots and options flatten into one row per option (slot identity
preserved as `slot_index`/`slot_name` columns) rather than a four-level table hierarchy; tiers get
their own slim table because craft time and tier membership are per-tier facts the aggregation
queries group by. Modifiers have no query requirement, so they stay only in the `tiers` jsonb —
no modifiers table, no duplication of modifier data across option rows. The jsonb column remains
the full-fidelity structure (FR-005/SC-002: "preserved and retrievable") and makes FR-014 trivial
for the nested content; the tier/option rows are derived data, deleted and re-inserted per
blueprint on upsert inside the import transaction. Reference tables follow the 019
single-table-with-kind precedent; the dataset row is single-row because a file is a complete
snapshot (Assumptions).

**Alternatives considered**: jsonb-only (original design) — rejected once material-based queries
became a stated requirement; querying inside jsonb across ≤5,000 blueprints × tiers × slots is
possible but unindexable-by-default and awkward to aggregate. Full four-table decomposition
including slots and modifiers as rows — rejected: no query touches modifiers, and a separate slots
table adds a join level the option-grain queries never need. Dropping the `tiers` jsonb — rejected:
modifiers would then need their own table to satisfy FR-005 retrievability, adding the machinery
the jsonb avoids.

## Decision 5 — Item link resolved at read time, not stored

**Decision**: No stored FK from `sc.blueprints` to `sc.items`. The listing query LEFT JOINs
`sc.items` on `items.uuid = blueprints.id::text` (excluding soft-deleted items, picking one row
deterministically since `ix_items_uuid` is non-unique) to produce the fallback display name.

**Rationale**: `sc.items.uuid` is a **non-unique varchar** column (migration
`AllowDuplicateItemUuid`), and `Item.Id` is an app-generated Guid regenerated across catalog
re-imports — a stored FK would go stale or dangle whenever the item catalog is re-imported after
blueprints (or blueprints before items). FR-015 requires the association to exist "when a row …
exists" and never to block import; a read-time join is always current in both directions and needs
zero import-order coordination.

**Alternatives considered**: Nullable `item_id` column resolved at import time — rejected: goes
stale on item re-import, and backfilling it would need a second maintenance action the spec
doesn't define.

## Decision 6 — Display name computed server-side; search filtered client-side

**Decision**: `GET /api/admin/blueprints` returns the full list (id/guid + `displayName` + the
fallback source fields), with `displayName` computed in SQL as
`COALESCE(NULLIF(product_name,''), item.name, NULLIF(tag,''), id::text)`. The React tab filters
client-side with case-insensitive `String.includes`, mirroring
`features/admin/components/CategorySelector.tsx`.

**Rationale**: ≤5,000 name-only rows fit comfortably in one response; the spec scopes search to
"case-insensitive substring over the displayed name" with no pagination or extra filters
(Assumptions). The fallback chain involves the item join, so it must be computed server-side to be
searchable consistently. Client filtering gives instant narrowing (SC-007 "single page
interaction") and matches the existing import-area precedent exactly.

**Alternatives considered**: Server-side `?search=` + pagination (hangar/warehouse pattern) —
rejected as YAGNI at this scale for an admin verification list; revisit if a member-facing browser
ships later.

## Decision 7 — Atomicity: one repository transaction; rejects are pre-transaction

**Decision**: The Application handler first parses/validates every blueprint (producing the
valid set + the rejection list + duplicate-guid dispositions + meta-mismatch warnings) with **no
database writes** — the only pre-transaction database work is the read-only batched material
lookup (Decision 11). It then calls a single repository method that opens one
`BeginTransactionAsync`, refreshes the reference tables + the dataset row (delete-and-insert),
upserts all valid blueprints by PK and rebuilds each upserted blueprint's tier and slot-option
rows (delete children + re-insert), and commits. Any exception inside rolls the whole transaction
back (FR-021a, SC-010).

**Rationale**: Separating validation (pure, in-memory) from persistence (one transaction) makes
"rejected entries are skipped, not rollback triggers" fall out naturally, and matches
`ItemRepository.BulkUpsertForCategoryAsync`'s explicit-transaction precedent. Blueprints absent
from the new file are retained (Assumptions — no soft-delete sweep in v1).

**Alternatives considered**: Per-blueprint savepoints — rejected: validation-before-write already
guarantees per-entry isolation without nested transaction machinery.

## Decision 8 — Duplicate `guid` within a file: first occurrence wins

**Decision**: The first occurrence of a `guid` in the file's `blueprints` array is imported;
each subsequent occurrence is rejected with reason `"Duplicate guid within file (first occurrence
imported)"` and counted in `rejected`.

**Rationale**: FR-018 demands a deterministic, reported outcome. First-wins is order-stable,
trivially explainable in the result summary, and never stores conflicting records.

**Alternatives considered**: Last-wins — equally deterministic but silently discards an entry the
admin already scrolled past; rejected for explainability.

## Decision 9 — Concurrency: reuse the singleton `IImportCoordinator`

**Decision**: The import handler wraps its work in the existing
`IImportCoordinator.TryAcquire()/Release()` (singleton `SemaphoreSlim(1,1)`); a busy coordinator
throws `ImportAlreadyInProgressException` → 409 Conflict at the endpoint, and the UI disables the
upload control while the mutation `isPending` (FR-003, SC-009).

**Rationale**: Identical to every existing import (items, commodities, ships, locations). Sharing
the coordinator also serializes a blueprint upload against other catalog imports, which is safe
and simpler than a second lock.

**Alternatives considered**: A blueprint-specific lock — rejected: no requirement for concurrent
heterogeneous imports.

## Decision 10 — Frontend: hand-written Zod schemas; upload lives in a new Data Import tab

**Decision**: A new `BlueprintsImportTab` inside the existing `DataImportPage` tabs hosts both the
upload zone (file-input pattern from `ImportHangarDialog`, 50 MB cap, client top-level shape check
via a light Zod schema before POST) and the searchable blueprint list beneath it. Response and
request types use hand-written Zod schemas in `features/admin/schemas/blueprintSchemas.ts`,
reviewed against `contracts/openapi.yaml`.

**Rationale**: The spec puts the listing "with the import pages under the admin section" — one tab
holding upload + list keeps it a single page interaction (SC-001/SC-007) and reuses the
`AdminRoute`-guarded `/dashboard/admin/data-import` route with no new nav entry. Hand-written Zod
against the contract is the established approved deviation from codegen (017/018/019 plans); any
contract change ships with the matching schema in the same PR. Client-side validation stays
shallow (valid JSON + required top-level keys) — the server is authoritative for everything else,
so validation logic isn't duplicated.

**Alternatives considered**: Separate listing page + nav item — rejected: extra route/nav for an
admin verification list the spec anchors to the import area. Full client-side dataset validation
with Zod — rejected: duplicates server rules and would drift.

## Decision 11 — Material name → catalog UUID resolution (added 2026-07-18)

**Decision**: During import (before the transaction, alongside validation), every distinct
material name — the union of the dataset's `resources` list, `items` list, and any option
`resourceName`/`itemName` values — is resolved to a catalog UUID:

1. Look up `sc.commodities` by **case-insensitive exact** name match, excluding soft-deleted rows
   and rows whose `uuid` is null/empty.
2. If no commodity yields a UUID, look up `sc.items` the same way (excluding soft-deleted; its
   `uuid` is non-null but may be empty — empty disqualifies).
3. If neither matches, the material is unlinked: `matched_uuid = null`, `matched_source = null`,
   and the name is reported in the result `warnings` — never a rejection (spec FR-009).

When multiple catalog rows share a name, the winner is chosen deterministically (lowest `uex_id`).
The result (`matched_uuid` `varchar(128)`, `matched_source` enum `commodity`|`item`) is stored on
`sc.crafting_materials` and denormalized onto every `sc.blueprint_slot_options` row referencing
that name. One batched lookup per import (two `WHERE lower(name) = ANY(...)` queries), keyed by
lower-cased name.

**Rationale**: The user requirement is explicit: commodities first, then items, capturing the UUID
and its source table. Case-insensitive matching tolerates casing drift between the crafting
dataset and the UEX catalogs; a commodity row without a UUID cannot satisfy the goal (a UUID), so
it falls through to items rather than producing a matched-but-useless link. Resolution is a
snapshot at blueprint-import time — re-importing commodities/items does not retro-update stored
links; the next blueprint upload refreshes them (documented in spec Assumptions). Storing
`uuid` as `varchar(128)` matches the catalogs' own column type rather than parsing to `uuid`,
since catalog uuids are strings of unverified format.

**Alternatives considered**: FK to commodity/item PK rows — rejected: catalog PKs are
app-regenerated on catalog re-import (same staleness problem as Decision 5), and the game UUID is
the durable identifier. Lookup at read time — rejected: the user explicitly wants the resolved
UUID stored on the imported rows, and query features will join on `matched_uuid` directly.
Case-sensitive match — rejected per clarification. Failing the blueprint on unmatched material —
rejected: catalog completeness must never block a blueprint import (consistent with FR-015).
