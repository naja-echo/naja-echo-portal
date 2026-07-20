# Quickstart & Validation: Organization Foundation & Admin Assignment

**Feature**: 021-org-foundation | **Branch**: `021-org-foundation`

How to run this feature and confirm it does what the spec says. Entity shapes are in
[data-model.md](./data-model.md); the HTTP surface is in [contracts/openapi.yaml](./contracts/openapi.yaml).

---

## Prerequisites

- .NET 8 SDK
- Node 20+ and npm
- Docker running — required by the Testcontainers-based infrastructure tests
- PostgreSQL for local dev (`docker-compose.yml` at the repo root)

---

## Apply the migration

```bash
cd /home/rdurham/source/NajaEchoPortal
./migrate.sh                    # or: dotnet ef database update -p backend/src/NajaEcho.Infrastructure -s backend/src/NajaEcho.Api
```

Confirm the backfill (FR-007, FR-008). All three counts must agree:

```sql
SELECT count(*) FROM organizations;                              -- 1
SELECT count(*) FROM "AspNetUsers";                              -- N
SELECT count(*) FROM organization_memberships WHERE is_current;  -- N
```

**Idempotency check (FR-009)** — re-running must not duplicate anything. Re-execute the seed and
backfill statements from [data-model.md](./data-model.md) directly against the database and confirm
the counts are unchanged.

---

## Run the app

```bash
# Terminal 1
dotnet run --project backend/src/NajaEcho.Api

# Terminal 2
cd frontend && npm run dev
```

Sign in with Discord, then open `/dashboard/admin/users` as a member holding the `Admin` role.

---

## Manual validation

### V1 — Existing members see no change (US1, SC-002)

Sign in as an ordinary member and visit every view: hangar, warehouse, loot ledger, catalogs.
Everything must render exactly as before. Nothing in this feature scopes production data, so any
visible difference is a bug.

### V2 — Organization is visible and assignable (US2, FR-011, FR-012, SC-003)

Start a timer when the Members page finishes loading and stop it when step 2's save completes.
SC-003 targets under 30 seconds to find a member and change their organization; this walkthrough is
the only place that target is checked, so record the number rather than eyeballing it.

On the Members page:

1. Each row shows an Organization column — "Naja Echo" for existing members, an em-dash for members
   with no current membership.
2. Open the assign dialog on a member, choose an organization, save. The row updates.
3. Re-open, clear the organization, save. The row shows the empty state.
4. Assign the same organization twice — the second save is a no-op, not an error (FR-012).

### V3 — Change takes effect without a sign-out (FR-014, SC-004)

1. Sign in as member M in a second browser profile and leave a page open.
2. As admin, change M's organization.
3. Trigger any request as M — a navigation, not a full sign-out.

M's organization claim must reflect the change on that request.

> **Single-instance only.** `InMemoryUserSessionInvalidator` is process-local. Across multiple API
> instances, an instance that did not handle the admin request falls back to the 15-minute refresh
> interval. Pre-existing behaviour for role changes; see research.md D3.

### V4 — Non-admins are refused (FR-013)

As a member without `Admin`:

```bash
curl -i -X PUT http://localhost:5000/api/admin/users/<userId>/organization \
  -H 'Content-Type: application/json' \
  -d '{"organizationId":"<orgId>"}'
```

Expect `403`. Confirm the control is absent from the UI as well — the API check is the real
boundary, but both should hold.

### V5 — Assignment is logged (FR-018, FR-019, SC-009)

Change a member's organization and inspect API stdout. Expect one structured event carrying the
acting admin, the affected member, the previous organization, the new organization, and a timestamp.
Confirm no token, cookie, or authorization header appears anywhere in it.

---

## Automated tests

```bash
# Everything
dotnet test backend/NajaEcho.sln
cd frontend && npm test

# The tests that matter most for this feature
dotnet test backend/tests/NajaEcho.Infrastructure.Tests \
  --filter "FullyQualifiedName~Organizations|FullyQualifiedName~OrganizationScope"
dotnet test backend/tests/NajaEcho.Api.Tests --filter "FullyQualifiedName~Organization"
```

### What must be covered

| Requirement | Test | Project |
|---|---|---|
| FR-004 — at most one current membership | Concurrent assignment; second write violates `ux_organization_memberships_user_current` | Infrastructure.Tests |
| FR-004 — reassignment is atomic | Clear-then-set in one transaction leaves exactly one current row | Infrastructure.Tests |
| FR-007/008/009 — seed and backfill | Migration applied twice; counts unchanged | Infrastructure.Tests |
| FR-014 — live refresh | Invalidated session picks up the new organization claim on next request | Api.Tests |
| FR-013 — admin only | Non-admin `PUT` returns 403 | Api.Tests |
| FR-018 — logging | Handler emits the event with both organization ids | Application.Tests |
| FR-020/021/023 — filter behaviour | See below | Infrastructure.Tests |
| FR-022 — reference data unscoped | An entity without `IOrganizationScoped` returns all rows with a null organization context | Infrastructure.Tests |
| FR-024 — no role bypasses | Same unconditional query as an Admin principal returns only the current organization's rows | Infrastructure.Tests |
| Contract end-to-end | `PUT …/organization` driven through `WebApplicationFactory` against real Postgres, asserting the membership row and the subsequent `GET …/users` response | Api.Tests |
| Members page | Column renders; dialog assigns and clears; list invalidated | frontend (Vitest) |

### The filter test is the important one

No production entity is organization-scoped in this feature, so the mechanism is proven against a
test-only entity and `DbContext` in `NajaEcho.Infrastructure.Tests` (research.md D5), running against
real Postgres via `PostgresFixture`.

It must assert three things:

1. **Scoping** — with organization A current, a LINQ query returns A's rows and not B's, *without the
   query stating any organization condition* (FR-020).
2. **Unassigned** — with no current organization, the same query returns empty and does not throw
   (FR-021).
3. **No model-cache bleed** — a query under organization A followed by one under organization B
   returns each organization's own rows. This catches the failure where the organization is captured
   at model-build time and baked into the cached model. **This is the single most valuable assertion
   in the feature** — it is the bug that would silently defeat the entire epic, and it is invisible
   to any test that only ever uses one organization.

---

## Known limitation to verify you understand

Query filters apply to **LINQ queries only**. `db.Database.SqlQuery<TRow>(...)` returns non-entity
record types and is **never filtered**.

Nothing in this feature is affected — no entity is scoped yet — but #32/#33/#34 inherit the
obligation. For any raw SQL that survives conversion in those features, add an explicit organization
predicate *and* a test proving that query cannot return another organization's rows. See plan.md
"Enforcement boundary" for the per-repository counts and the four queries known to be hard to
convert.

---

## Test fixture note

`AppDbContext` gains an `IOrganizationContext` constructor parameter (data-model.md). Two call sites
construct it directly and need updating:

- `PostgresFixture.CreateContext()` — pass a stub returning a fixed organization id, and expose a way
  for tests to vary it (the model-cache-bleed assertion above depends on being able to switch
  organizations between queries).
- `PostgresFixture.BuildIdentityProvider()` — register `IOrganizationContext` in the service
  collection.

The API test project stubs the database entirely (`StubDatabase()` removes `RoleSeeder` so
`AppDbContext` is never resolved), so it needs no change on this account.
