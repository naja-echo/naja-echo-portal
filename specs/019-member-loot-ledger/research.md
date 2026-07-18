# Phase 0 Research: Member Loot Ledger

All spec **Clarifications** were resolved in two clarify sessions (2026-06-22); there are no open
`NEEDS CLARIFICATION` markers. Research below records the **technical** decisions for grounding the
implementation against the existing codebase. The dominant precedent is **017-admin-users-page**
(roles, `AssignRolesDialog`, `UserRepository`, display-name resolution) plus the warehouse
read/endpoint patterns.

## Decision 1 — One polymorphic ledger table with a `kind` discriminator

**Decision**: A single `loot_ledger` table holding both OrgPoints and LootPoints rows,
discriminated by a `kind` column (`OrgPoints` | `LootPoints`). One `LootLedgerEntry` domain entity
carries a `LootLedgerKind` enum. (User decision, 2026-06-22 — overrides the brainstorm's
"resist abstracting into a single polymorphic table" note.)

**Rationale**: The two ledgers have **identical schemas** (member, signed integer amount, reason,
actor, timestamp) and identical read/insert behaviour; only the write-authorization gate differs,
and that gate lives at the **endpoint/handler** layer, not in the table. One table means one entity,
one EF config, one repository, one migration, and one set of indexes — materially less surface than
two parallel copies (Constitution IV / DRY). The Claim Priority read already has to touch both kinds
together, so a single table with a `kind` filter is the natural read shape. A `ck_loot_ledger_kind`
check constraint keeps the discriminator valid.

**Alternatives considered**: Two separate `org_points_ledger` / `loot_points_ledger` tables (the
brainstorm's suggestion) — rejected: the duplicated schema/config/repository/migration outweighs the
"different auth gates" argument, since auth is enforced per-endpoint regardless of table layout.

## Decision 2 — Schema placement: default schema, not `sc`

**Decision**: Both tables live in the default (public) schema, like `characters` and the Identity
tables — not the UEX `sc` catalog schema.

**Rationale**: These are member-scoped transactional records keyed on `AspNetUsers`, mirroring
`characters` (which is in the default schema and FK's `AspNetUsers`). The `sc` schema is reserved
for imported UEX reference data (stations, cities, ships, items). Keeping ledgers next to
`characters` keeps the member domain cohesive.

## Decision 3 — Amounts are integers (spec overrides brainstorm)

**Decision**: `amount` is a signed **`int`** / `integer` column.

**Rationale**: Spec FR-016 and the 2026-06-22 clarification mandate whole-integer amounts and reject
non-integer input. The brainstorm's earlier "decimal" note is superseded by the clarified spec
(spec is the authority). Integer storage also makes the divide-by-zero rule and 2-dp display
unambiguous: totals are integers, the ratio is computed as floating-point at read time.

## Decision 4 — New role seeded, not migrated

**Decision**: Add `"CrewResourceOfficer"` to `RoleSeeder.Roles` (idempotent startup seeding) and to
the `Quartermaster`-style policy set. No EF migration touches `AspNetRoles`.

**Rationale**: `RoleSeeder` already seeds `["Admin", "Quartermaster"]` idempotently at startup
(`RoleExistsAsync` guard). Adding one string is the established, lowest-risk path; Identity tables
already exist. `AssignRolesHandler.ValidRoles` and the frontend `roleDisplayNames`/`availableRoles`
must be extended in lockstep so the existing 017 role-assignment UI can grant it (spec Assumption,
FR-012).

## Decision 5 — Authorization policies

**Decision**: Add a `CrewResourceOfficer` policy to `AuthorizationPolicies.AddPolicies()` granting
`CrewResourceOfficer` **or** `Admin` (mirroring the existing `Quartermaster` policy which grants
`Quartermaster` or `Admin`). OrgPoints writes require the CRO policy; LootPoints writes require the
existing `Quartermaster` policy; all reads require only authentication.

**Rationale**: Admin is the combined safety-valve role (US5) — the "or Admin" disjunction in each
policy delivers FR-014/FR-015 and US5 without extra branching. Pattern already proven for
Quartermaster.

## Decision 6 — Endpoints under `/api/loot`, minimal-API group (Quartermaster precedent)

**Decision**: New `LootEndpoints.MapLootEndpoints()` static class registering a
`/api/loot` `MapGroup` with `RequireAuthorization()`, individual write routes adding
`.RequireAuthorization(<policy>)` — exactly like `WarehouseEndpoints`. Registered in `Program.cs`
beside `MapWarehouseEndpoints()`.

Routes: `GET /distribution`, `GET /me`, `GET /{userId:guid}`, `POST /{userId:guid}/org-points`
(CRO policy), `POST /{userId:guid}/loot-points` (Quartermaster policy).

**Rationale**: Matches the existing minimal-API + feature-group convention; per-route policy
overrides are already used in `WarehouseEndpoints`.

## Decision 7 — Distribution via a Postgres view; per-member read via raw SQL

**Decision**: The all-member distribution rollup is a database **view** `loot_member_standing`
(grouped by member: member id, display name, `SUM` OrgPoints, `SUM` LootPoints, computed Claim
Priority — see data-model.md), created in the `AddLootLedger` migration and queried as
`SELECT * FROM loot_member_standing ORDER BY claim_priority ASC` through a **keyless** EF entity
(`HasNoKey().ToView(...)`). The per-member ledger read (`/me`, `/{userId}`) stays as raw
parameterized SQL (`db.Database.SqlQuery<Row>`) joining `loot_ledger` to the character/user tables
for the poster display name, newest-first. Writes use plain EF `Add` + `SaveChanges`.

**Rationale**: A view puts the Claim-Priority formula and the per-kind `FILTER` sums in **one
authoritative place** in the database, so the rollup can't drift between callers and is reusable by
later features (events/claims). It matches the established raw-SQL/`SqlQuery` read-model convention
(`UserRepository.GetUsersWithRolesAndCharactersAsync`, warehouse repos) while avoiding N+1. The
per-member detail read is left as a direct query — it returns row-level entries, not an aggregate,
so it gains nothing from the view.

**Alternatives considered**: Ad-hoc aggregate query in the repository (no view) — rejected: would
duplicate the divide-by-zero/`FILTER` logic at each call site. A materialized view — rejected as
premature (org-scale data, always-current requirement; refresh adds complexity for no current
benefit, Constitution IV).

## Decision 8 — Immutability by omission

**Decision**: Provide **no** update or delete code path for the ledger — the repository exposes only
insert and read. Corrections are compensating entries (Edge Cases, FR-004).

**Rationale**: The simplest enforcement of an append-only ledger is to never write the mutation
code. The `ck_loot_ledger_reason` constraint and FK restrict on `actor_id` further protect audit
integrity.

## Decision 9 — No new dependencies; Zod-from-contract convention retained

**Decision**: No new backend or frontend package. Frontend request/response types are hand-written
Zod schemas reviewed against `contracts/openapi.yaml` (the established project convention, same as
017/018 — codegen is not used here).

**Rationale**: YAGNI (Constitution IV); consistency with the existing frontend data layer
(`apiFetch`, TanStack Query, feature-owned schemas/keys/hooks).

## Decision 10 — Frontend: new `crew-resources` feature folder + new top-level nav group

**Decision**: New `frontend/src/features/crew-resources/` owning both pages, hooks, query-key
factory, api client, and Zod schemas. Two `navItems` entries with `group: 'Crew Resources'` and
**no** `access` rule (visible to all authenticated members, spec Assumption / FR-001). Routes
`/crew-resources/my-loot` and `/crew-resources/loot-distribution` mounted inside the existing
`ProtectedRoute` + dashboard shell. The drill-down uses the existing shadcn `Sheet` primitive.

**Rationale**: Constitution VI (feature folders, data-driven nav single source of truth, thin
routes). `access` is omitted because both pages are intentionally transparent; role gating applies
only to the in-sheet write actions, driven by `session.user.roles` from `useCurrentUser`.
