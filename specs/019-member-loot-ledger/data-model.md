# Phase 1 Data Model: Member Loot Ledger

## Overview

One new insert-only polymorphic ledger table in the **default (public) schema** (alongside
`characters` and the ASP.NET Identity tables — these are member-scoped data, not the UEX `sc`
catalog). It references `"AspNetUsers"` and discriminates OrgPoints vs LootPoints rows by a `kind`
column. Claim Priority and running totals are **computed, never stored**.

## Entity: LootLedgerEntry → table `loot_ledger`

Domain: `NajaEcho.Domain.Loot.LootLedgerEntry` (new `Loot` domain folder), with enum
`NajaEcho.Domain.Loot.LootLedgerKind { OrgPoints, LootPoints }`.

| Field         | Type              | Column          | Notes |
|---------------|-------------------|-----------------|-------|
| `Id`          | `Guid`            | `id`            | PK |
| `MemberId`    | `Guid`            | `member_id`     | FK → `"AspNetUsers".id`, the member the entry belongs to. Required. Indexed. |
| `Kind`        | `LootLedgerKind`  | `kind`          | `OrgPoints` or `LootPoints`. Stored as `text` (EF enum-to-string conversion). Required. |
| `Amount`      | `int`             | `amount`        | Signed integer (positive credit / negative debit). Required. |
| `Reason`      | `string`          | `reason`        | Required, non-empty, `max length 500`. |
| `ActorId`     | `Guid`            | `actor_id`      | FK → `"AspNetUsers".id`, who posted the entry. Required. |
| `CreatedAt`   | `DateTimeOffset`  | `created_at`    | Set server-side at insert. Required. |

Constraints / indexes:
- `pk_loot_ledger` (id)
- `fk_loot_ledger_member_id` → `AspNetUsers(id)` `ON DELETE Cascade`
- `fk_loot_ledger_actor_id` → `AspNetUsers(id)` `ON DELETE Restrict`
- `ix_loot_ledger_member_kind_created` on `(member_id, kind, created_at DESC)` — supports both the
  per-member newest-first reads (FR-002, FR-008) and the per-kind aggregate sums
- `ck_loot_ledger_kind` — `kind IN ('OrgPoints', 'LootPoints')`
- `ck_loot_ledger_reason` — `length(btrim(reason)) >= 1` (DB guard for FR-011)

The auth gate (CRO writes OrgPoints, Quartermaster writes LootPoints) is enforced at the
endpoint/handler layer, **not** by the table — a single table is auth-agnostic (research Decision 1).

## Computed: Claim Priority (not persisted)

```
orgTotal  = SUM(amount) WHERE kind = 'OrgPoints'
lootTotal = SUM(amount) WHERE kind = 'LootPoints'
ClaimPriority(member) = orgTotal / COALESCE(NULLIF(lootTotal, 0), 100)
```

- Both totals zero (no entries) → `0 / 100 = 0.00`.
- LootPoints total `0`, OrgPoints positive → `Org / 100`.
- Displayed to exactly 2 decimal places (FR-003) — formatting is frontend-only; the API returns the
  raw computed number plus the two integer totals.
- **Lower ratio = higher priority** → Distribution table default sort is Claim Priority **ascending**
  (FR-006). Sort is applied in SQL for the all-member query and re-applied client-side (no
  pagination, FR-006).

## Database view: `loot_member_standing` (per-user rollup)

A read-only Postgres view, **grouped by member**, that materializes the distribution shape so the
Claim Priority math lives in one place in the database (not duplicated across ad-hoc queries). One
row per `AspNetUsers` member (every account, even with zero entries — `LEFT JOIN` to the ledger),
exposing the member, the two per-kind sums, and the computed Claim Priority:

```sql
CREATE VIEW loot_member_standing AS
SELECT
    u.id                                                                   AS member_id,
    COALESCE(c.name, u.display_name)                                       AS display_name,
    COALESCE(SUM(l.amount) FILTER (WHERE l.kind = 'OrgPoints'), 0)         AS org_points_total,
    COALESCE(SUM(l.amount) FILTER (WHERE l.kind = 'LootPoints'), 0)        AS loot_points_total,
    COALESCE(SUM(l.amount) FILTER (WHERE l.kind = 'OrgPoints'), 0)::double precision
        / COALESCE(NULLIF(SUM(l.amount) FILTER (WHERE l.kind = 'LootPoints'), 0), 100)
                                                                           AS claim_priority
FROM "AspNetUsers" u
LEFT JOIN loot_ledger l ON l.member_id = u.id
LEFT JOIN LATERAL (
    SELECT name FROM characters
    WHERE owner_user_id = u.id
    ORDER BY created_at
    LIMIT 1
) c ON TRUE
GROUP BY u.id, u.display_name, c.name;
```

- `FILTER (WHERE kind = …)` gives the two per-kind sums in a single grouped pass.
- The `NULLIF(…, 0)` + `COALESCE(…, 100)` encodes the divide-by-zero rule (LootPoints total 0 → use
  100). A member with no entries yields `0 / 100 = 0.00`.
- The `LATERAL` subquery resolves the single registered character name deterministically (v1 single
  character assumption); `COALESCE` falls back to the Discord display name (FR-013).
- The view is created in the `AddLootLedger` migration via `migrationBuilder.Sql(...)` (and dropped
  in `Down`).

## Read shapes

**Distribution (all members)** — `SELECT * FROM loot_member_standing ORDER BY claim_priority ASC`.
Mapped via a **keyless** EF entity `LootMemberStanding` (`HasNoKey().ToView("loot_member_standing")`)
or read with `db.Database.SqlQuery<…>`. One read, no N+1; the rollup/math is owned by the view.

**Per-member ledger** (`/me` and `/{userId}`) — the entries for one member, split into two
newest-first lists by `kind` in the handler:
```
each entry: id, kind, amount, reason, created_at,
            posted_by (COALESCE(actor_character.name, actor_user.display_name))
WHERE member_id = @memberId
ORDER BY created_at DESC   -- newest first (FR-002, FR-008)
```

## Display-name resolution (FR-013)

`COALESCE(c.name, u.display_name)` where `characters c` joins `AspNetUsers u` on
`c.owner_user_id = u.id`. v1 assumes a single registered character per member (spec Assumptions);
if multiple rows exist the query takes the first by `created_at` to stay deterministic. Applied both
to the member identity (distribution rows, `/me` subject) and to the **poster** identity on each
ledger entry (FR-005) via a second join on `actor_id`.

## Migration: `AddLootLedger` (forward-only, non-destructive)

Single EF Core migration creating `loot_ledger` (its two FKs, the composite index, and both check
constraints) **and** the `loot_member_standing` view (`migrationBuilder.Sql(CREATE VIEW …)` in
`Up`, `DROP VIEW` in `Down`). **Not destructive** — additive only (one `CreateTable` + one
`CREATE VIEW`, no column drops/type changes), so no special PR approval note is required under the
constitution's Development Workflow. Snake_case naming via the existing Npgsql convention; table and
view in the default schema (no `.ToTable(name, "sc")`).

The new `CrewResourceOfficer` role requires **no migration** — it is seeded idempotently at startup
by `RoleSeeder` (research Decision 4); `AspNetRoles`/`AspNetUserRoles` already exist.

## Validation rules (consolidated)

| Rule | Source | Enforced at |
|------|--------|-------------|
| Reason required / non-empty | FR-011 | Zod (frontend), API binding/validator, `ck_loot_ledger_reason` |
| Amount is a whole integer | FR-016 | Zod (`z.number().int()`), `integer` column type |
| Amount may be negative | FR-009/010, US3#5 | no positivity constraint anywhere |
| `kind` is valid | Decision 1 | enum, `ck_loot_ledger_kind` |
| Entries immutable | FR-004 | no update/delete code path |
| OrgPoints write = CRO/Admin | FR-009/014 | `CrewResourceOfficer` auth policy (endpoint) |
| LootPoints write = QM/Admin | FR-010/015 | `Quartermaster` auth policy (endpoint) |
| Reads = any authenticated member | FR-007 | group `RequireAuthorization()` |
