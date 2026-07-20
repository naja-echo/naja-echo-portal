# Phase 0 Research: Organization Foundation & Admin Assignment

**Feature**: 021-org-foundation | **Date**: 2026-07-19

All unknowns from Technical Context are resolved below. Each decision records what was chosen, why,
and what was rejected.

---

## D1 — Where the "current organization" fact lives

**Decision**: On the `organization_memberships` row, as an `is_current` boolean. `ApplicationUser`
gains no organization column.

**Rationale**: Issue #31 originally specified a denormalized `CurrentOrganizationId` on
`ApplicationUser` *alongside* the join table. That stores the same fact twice, and the two can
disagree — a class of bug with no upside while every user has exactly one membership. Putting
currency on the relationship keeps one source of truth and still supports multi-membership later
(flip which row is current). Confirmed with the user during `/speckit-clarify`; issue #31 has been
updated.

**Alternatives considered**:
- *`CurrentOrganizationId` on the user, join table as the authoritative record* — rejected: two
  sources of truth requiring reconciliation logic and a consistency test that can only ever catch
  drift after the fact.
- *`CurrentOrganizationId` only, no join table* — rejected: makes multi-membership a data migration,
  reversing Epic #30's deliberate decision to pay for the join table up front.

---

## D2 — Enforcing at most one current membership per user

**Decision**: A partial unique index in Postgres:

```sql
CREATE UNIQUE INDEX ux_organization_memberships_user_current
  ON organization_memberships (user_id)
  WHERE is_current;
```

EF Core equivalent in `OrganizationMembershipConfiguration`:

```csharp
builder.HasIndex(m => m.UserId)
       .IsUnique()
       .HasFilter("is_current")
       .HasDatabaseName("ux_organization_memberships_user_current");
```

**Rationale**: FR-004 requires that no interleaving of concurrent admin writes can leave a user with
two current memberships. A read-then-write check in the handler cannot guarantee that without
explicit locking. A partial unique index makes the state physically unrepresentable — the database
rejects the second write regardless of timing. Postgres supports partial indexes natively and EF
Core 8 models them via `HasFilter`.

Assignment therefore runs inside a transaction: clear any existing current membership for the user,
then set the new one. A concurrent duplicate surfaces as a `DbUpdateException` on the index name,
translated to a domain exception — the pattern already used for
`ux_hangar_entries_user_ship` in `HangarRepository`.

**Alternatives considered**:
- *Application-level check (`SELECT` then `UPDATE`)* — rejected: racy without `SELECT … FOR UPDATE`,
  and the index is simpler than the locking it would replace.
- *Unique index on `(user_id, is_current)`* — rejected: does not work. It permits many rows with
  `is_current = false` (correct) but also fails to constrain to one `true` per user only if `false`
  values collide. The partial index expresses the actual invariant.
- *Serializable isolation* — rejected: heavyweight for one admin action, and would require retry
  handling everywhere.

A second unique index `ux_organization_memberships_user_org` on `(user_id, organization_id)`
prevents duplicate memberships in the same organization (edge case in spec: re-assigning to an
organization the user already belongs to marks the existing row current rather than creating a
second).

---

## D3 — How the organization reaches the request

**Decision**: As a session claim (`najaecho:org`), issued at sign-in and kept fresh by the existing
`RoleClaimsRefresher`, generalized to refresh roles *and* organization together.

**Rationale**: The mechanism already exists and already satisfies the requirement. `RoleClaimsRefresher`
stamps `roles.refreshed_at` into `AuthenticationProperties`, and on each request decides staleness
via `IUserSessionInvalidator.IsStaleSince` or a 15-minute fallback, then calls
`ctx.ReplacePrincipal(...)` without signing the user out. `AssignRolesHandler` already calls
`sessionInvalidator.Invalidate(userId)` after a change, which is exactly the FR-014 pattern
("effective on the member's next request, no sign-out"). Reusing it means the organization claim
inherits behaviour that is already tested, rather than inventing a parallel path.

Concretely:
- Add the organization claim to the identity built in `Program.cs` `OnTicketReceived`.
- Extend the refresher to fetch the current organization alongside roles and preserve/replace that
  claim the same way role claims are handled.
- `AssignOrganizationHandler` calls `sessionInvalidator.Invalidate(targetUserId)`.

**Known limitation, inherited not introduced**: `InMemoryUserSessionInvalidator` is process-local
(`ConcurrentDictionary`, registered as a singleton). On a multi-instance deployment, an instance
that did not handle the admin request never sees the flag and falls back to the 15-minute interval.
The interface's own doc comment names itself "the seam for a distributed implementation." This
already applies to role changes today; organization changes acquire the same characteristic. FR-014
is therefore satisfied on single-instance deployments and degrades to ≤15 minutes on multi-instance
ones. **Flagged rather than fixed** — making it distributed is its own feature.

**Alternatives considered**:
- *Per-request database lookup of the current membership* — rejected: adds a query to every
  authenticated request to serve a value that changes a few times a year, and duplicates a caching
  mechanism that already exists.
- *A second, organization-specific invalidation path* — rejected: two mechanisms doing the same job,
  with two chances to be wrong.

---

## D4 — Applying the query filter by convention

**Decision**: A marker interface in Domain:

```csharp
public interface IOrganizationScoped { Guid OrganizationId { get; set; } }
```

plus a `ModelBuilder` extension that walks the model and applies a filter to every entity type
implementing it. `AppDbContext` exposes the current organization as an instance property and calls
the extension at the end of `OnModelCreating`.

**Rationale**: #32/#33/#34 should opt an entity in by implementing an interface and adding a column,
not by hand-writing filter wiring three times. The filter expression must reference a *DbContext
instance member* rather than a captured value — EF Core parameterizes instance-member access in
query filters, so the compiled model stays cacheable while the value varies per request. Capturing
`orgContext.CurrentOrganizationId` directly at model-build time would bake the first request's
organization into the cached model, which is the classic multi-tenancy bug.

The filter must also let unassigned users through as *empty*, not error (FR-021), which a
`Guid?`-valued property handles naturally: `x.OrganizationId == CurrentOrganizationId` is never true
when the right side is null.

**Note on Domain layer purity**: the marker interface lives in `NajaEcho.Domain` and references only
`System.Guid`. No EF Core dependency crosses into Domain; the filter application lives in
Infrastructure. Principle VI is preserved.

**Alternatives considered**:
- *`HasQueryFilter` written per entity in each configuration class* — rejected: three copies of the
  same expression, and nothing stops #34 from forgetting it.
- *A base entity class* — rejected: no domain entity uses inheritance today (verified across all 30
  Domain files), and a marker interface achieves the same without imposing a hierarchy.

---

## D5 — Proving the filter works with no scoped entity

**Decision**: A test-only `DbContext` and entity in `NajaEcho.Infrastructure.Tests`, exercised
against the real Testcontainers Postgres instance via `EnsureCreated`, asserting that a LINQ query
issued under organization A cannot see organization B's rows and that an unset context returns
empty.

**Rationale**: FR-023 requires the mechanism to be demonstrably working *in this feature*, but no
production entity is scoped until #32. `AppDbContext` is `sealed`, so it cannot be subclassed for
testing. Extracting the wiring into a `ModelBuilder` extension makes it independently testable: the
test context applies the same extension to a purpose-built entity. This tests the actual shipped
code path rather than a re-implementation.

**Alternatives considered**:
- *Scope one real entity now as a pilot* — rejected: contradicts the epic's decomposition (#33 owns
  hangar) and the spec's explicit non-goal.
- *Unseal `AppDbContext`* — rejected: weakens a production type for a test-only reason.
- *Unit-test the expression tree without a database* — rejected: would not catch the model-caching
  bug described in D4, which is the failure mode most worth testing.

---

## D6 — Enforcement scope: what the filter does and does not cover

**Decision**: Query filters are the enforcement mechanism, and the guarantee is explicitly scoped to
LINQ queries. Raw SQL paths carry an explicit organization predicate plus a dedicated test.

**Rationale**: EF Core global query filters attach to entity types and apply to LINQ queries rooted
on a `DbSet<T>`. `db.Database.SqlQuery<TRow>(...)` returns arbitrary record types with no entity
type, so **no filter is applied**. This codebase uses that pattern heavily in exactly the areas
#32–#34 will scope. Measured across the five repositories:

| Repository | Raw SQL calls |
|---|---|
| `Hangar/HangarRepository.cs` | 11 |
| `Warehouse/ShipComponentRepository.cs` | 11 |
| `Warehouse/WarehouseInventoryRepository.cs` | 7 |
| `Warehouse/MaterialInventoryRepository.cs` | 6 |
| `Loot/LootLedgerRepository.cs` | 1 |

Translatability analysis of those 35 calls: ~12 trivial, ~14 moderate, 4 hard reads, 2 hard writes.
The hard reads are `json_agg` + `GROUP BY` (org hangar, ×2), array `ANY(unnest(...))` with tri-state
unknown filters (ship components), and `LEFT JOIN LATERAL` (loot ledger). The 2 hard writes are
`ON CONFLICT … RETURNING (xmax = 0)` upserts, which need `organization_id` in the conflict target —
a write concern, not a filter concern.

Mitigating factor: the genuinely awkward SQL operates on *reference data* (`sc.ships` JSONB
extraction, regex-guarded numeric casts, catalog search), which is never organization-scoped. The
org-scoped tables are joined in with plain equality predicates, so a filter on the scoped entity
does not require the whole query to be LINQ-friendly.

**This makes FR-020's guarantee partial, and the spec has been amended to say so.** Claiming
unconditional fail-closed behaviour while ~10% of read paths bypass the mechanism would be a false
assurance in the document future implementers trust most.

**Alternative considered and deferred: Postgres Row-Level Security.**

RLS would enforce at the storage layer, covering raw SQL, LINQ, `ExecuteSql`, and any future access
path identically. `current_setting('app.current_org_id', true)::uuid` returns NULL when unset, and
`organization_id = NULL` is never true — so an unset context yields zero rows, making fail-closed the
default state rather than an implemented behaviour.

Deferred because:
1. It requires a transaction-per-request seam (`SET LOCAL` is transaction-scoped). Repositories do
   not open transactions today except the hangar import path. Session-scoped `SET` without a
   transaction risks an organization id leaking across pooled connections — precisely the bug being
   prevented.
2. The Testcontainers fixture connects as `test`, a **superuser**, and superusers bypass RLS
   silently. Adopting RLS requires a non-privileged application role in the fixture, or isolation
   tests pass while proving nothing.
3. Policies live in raw SQL migrations, outside the EF model snapshot.
4. With one organization there is nothing to leak to. The cost of adding RLS later is
   `ALTER TABLE` + `CREATE POLICY` on three tables — not a rewrite.

Decision made by the user on 2026-07-19 after reviewing the translatability analysis. Revisit if a
second organization becomes real.

---

## D7 — Default organization identity and idempotent backfill

**Decision**: A fixed, hardcoded `Guid` constant for the default organization
(`DefaultOrganization.Id` in Domain), inserted by the migration with a guard, and memberships
backfilled by an `INSERT … SELECT … WHERE NOT EXISTS`.

**Rationale**: FR-009 requires the upgrade to be safe to apply more than once. A fixed id makes the
guard trivial and makes the default organization referenceable from tests and seed logic without a
lookup. EF migrations run once per database by version, but re-runs against a partially-migrated or
restored database are a real operational scenario, and `Down`/`Up` cycles happen in development.

The migration uses `migrationBuilder.Sql(...)` for the seed and backfill rather than EF's
`InsertData`, because the backfill is a set-based `INSERT … SELECT` over `"AspNetUsers"` whose row
count is unknown at authoring time.

**Note on Identity table quoting**: Identity tables remain PascalCase and quoted
(`"AspNetUsers"`) because they predate the snake_case convention. The backfill SQL must quote them,
matching the existing convention in `WarehouseInventoryRepository`.

**Alternatives considered**:
- *Generate the id at migration time* — rejected: not idempotent, and gives tests nothing stable to
  assert against.
- *Seed at application startup like `RoleSeeder`* — rejected: `RoleSeeder` is wrapped in a
  non-fatal try/catch and there is no `Database.Migrate()` at startup, so startup seeding would not
  be guaranteed to have run before the first request touches organization data.

---

## D8 — API surface

**Decision**: Three endpoints.

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/admin/organizations` | List assignable organizations (populates the dialog) |
| `PUT` | `/api/admin/users/{userId}/organization` | Set or clear a user's current organization |
| `GET` | `/api/admin/users` *(modified)* | Existing list; each user gains an `organization` field |

**Rationale**: `PUT` with a nullable body field expresses set-and-clear as one idempotent operation,
mirroring the existing `PUT …/roles` full-replace semantics rather than introducing a `DELETE` for
the clear case. `GET /api/admin/organizations` is needed even though only one organization exists
(FR-015), because the dialog must render a choice rather than hardcode a name.

All three sit under the existing `/api/admin` group with `RequireAuthorization(AuthorizationPolicies.Admin)`,
satisfying FR-013.

**Alternatives considered**:
- *`POST`/`DELETE` pair for assign and clear* — rejected: two endpoints for one field, and not
  idempotent.
- *Fold organization assignment into the existing `PUT …/roles`* — rejected: unrelated concerns, and
  would make a roles-only change silently rewrite organization.

---

## D9 — Frontend approach

**Decision**: Follow the existing `AssignRolesDialog` pattern exactly — a separate
`AssignOrganizationDialog`, a `useAssignOrganizationForUser` mutation invalidating
`userKeys.adminUsers.list()`, hand-written Zod schemas at the API boundary, and a new Organization
column in `UsersTable`.

**Rationale**: The Members page has a settled pattern (conditionally-mounted dialog so state resets
on unmount, `mapError` switching on `ApiError.status`, broad list invalidation on success). Matching
it keeps the page coherent.

**Deviation acknowledged**: hand-written Zod duplicates the API contract, which the constitution
forbids in favour of generated types. As recorded in the plan's Constitution Check, *no* frontend
feature currently imports the generated types, so following the constitution here would make this
one feature inconsistent with every neighbour while leaving the underlying problem unsolved. Logged
as debt for a dedicated cleanup.

**Alternatives considered**:
- *Extend `AssignRolesDialog` into a combined "edit member" dialog* — rejected: enlarges an existing
  tested component and couples two independent admin actions.
- *Introduce generated-type consumption for this feature only* — rejected: a project-wide concern
  that deserves its own decision, not a side effect of this feature.

---

## Resolved unknowns summary

| Unknown | Resolution |
|---|---|
| Current-organization storage | Membership row with `is_current` (D1) |
| Single-current enforcement | Partial unique index (D2) |
| Per-request org resolution | Session claim + generalized refresher (D3) |
| Filter application | Marker interface + `ModelBuilder` extension (D4) |
| Proving the filter with nothing scoped | Test-only context and entity (D5) |
| Raw SQL coverage | Out of filter scope; explicit predicate + test (D6) |
| Idempotent seed/backfill | Fixed Guid + guarded SQL (D7) |
| API shape | Three admin endpoints (D8) |
| Frontend pattern | Mirror `AssignRolesDialog` (D9) |

No NEEDS CLARIFICATION markers remain.
