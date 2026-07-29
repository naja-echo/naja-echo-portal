# Implementation Plan: Organization Foundation & Admin Assignment

**Branch**: `021-org-foundation` | **Date**: 2026-07-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/021-org-foundation/spec.md`

## Summary

Install the tenant boundary: an `Organization` entity, an `OrganizationMembership` join table
that carries its own currency marker, a per-request organization context resolved from a
session claim, and EF Core global query filter infrastructure applied automatically to any
entity marked organization-scoped. Ship a migration creating the default "Naja Echo"
organization and a current membership for every existing user, plus admin UI on the existing
Members page to set or clear a user's organization.

No production entity becomes organization-scoped in this feature. The filter infrastructure is
proven against a purpose-built test entity in a real Postgres database, so #32/#33/#34 inherit a
mechanism that is already known to work rather than one that is merely written.

## Technical Context

**Language/Version**: C# 12 on .NET 8; TypeScript 5.x (strict) on the frontend

**Primary Dependencies**: ASP.NET Core Web API, EF Core 8 + Npgsql, `EFCore.NamingConventions`,
ASP.NET Core Identity (cookie auth), Serilog; React + Vite, TanStack Query, React Hook Form + Zod,
shadcn/ui

**Storage**: PostgreSQL 16. Default schema `public` (user-owned data); `sc` schema holds
reference/catalog data. Both new tables live in `public`.

**Testing**: xUnit + FluentAssertions. Infrastructure tests use Testcontainers Postgres with
Respawn (`PostgresFixture`); API tests use `WebApplicationFactory<Program>` with all repositories
faked and the database stubbed out (`StubDatabase()`). Frontend uses Vitest + React Testing
Library + MSW.

**Target Platform**: Linux container (backend), browser SPA (frontend)

**Project Type**: Web application — separate backend API and frontend SPA

**Performance Goals**: No new performance surface. The organization context resolves from an
existing session claim, adding no per-request query. Assignment is a low-frequency admin action.

**Constraints**: The organization change must be visible on the member's next request without a
sign-out (FR-014), reusing the existing claims-refresh mechanism. The at-most-one-current-membership
invariant (FR-004) must hold under concurrent writes, enforced in the database rather than by
application checks.

**Scale/Scope**: One organization, tens of members. Two new tables, one migration, three
endpoints, one new admin dialog and table column.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. API-Contract-First | **PASS** (with pre-existing debt, see below) | This feature adds backend HTTP behaviour, so `contracts/openapi.yaml` is authored before implementation. |
| II. Test-First / TDD | **PASS** | Every task in `tasks.md` is ordered test-then-code. The query-filter mechanism in particular is specified as a failing integration test against real Postgres before the infrastructure exists. The Development Workflow rule requiring at least one Testcontainers-backed end-to-end API test is met by T026a, which drives `PUT …/organization` through `WebApplicationFactory` against real Postgres rather than the usual `StubDatabase()` fakes. |
| III. Frontend/Backend Separation | **PARTIAL — see below** | No server-rendered HTML and no direct DB access from the SPA. However, the Frontend Conventions rule that API types be generated from the contract rather than hand-written is not satisfied by the Zod schemas this feature adds. Recorded as deviation 2 below and in Complexity Tracking. |
| IV. Simplicity / YAGNI | **DEVIATION — documented** | See Complexity Tracking. The entire epic is a knowingly-accepted YAGNI exception; two specific sub-decisions are recorded there. |
| V. Observability | **PASS** | FR-018 requires a structured log event per assignment change; FR-019 forbids secrets in it. Follows the existing `"<Operation> key={Prop} outcome=<slug>"` house convention. |
| VI. Modular Monolith + Clean Architecture | **PASS** | `IOrganizationContext` is an Application-layer port. Its HTTP-reading implementation lives in the API layer (which already owns `HttpContext` concerns and performs DI composition). `AppDbContext` in Infrastructure depends only on the Application port. Dependency direction is preserved. |

### Pre-existing constitution deviations found during research

Neither is introduced by this feature, and neither blocks it. Recording them because this feature
touches both files, and silently inheriting a violation is how it becomes permanent.

1. **Contract drift (Principle I).** `PUT /api/admin/users/{userId}/roles` ships in
   `UserAdminEndpoints.cs` but appears in no `specs/*/contracts/openapi.yaml`. This plan's contract
   documents that endpoint alongside the new ones, since it is in the same route group and the
   marginal cost is a few lines. **Fixed here.**

2. **Hand-written DTO types (Principle I/III, constitution §"API client and type generation").**
   `openapi-typescript` is wired up with nine per-feature scripts, but *nothing in `frontend/src`
   imports the generated types* — every feature hand-writes Zod schemas instead, which the
   constitution explicitly forbids. There is also no generation script for features 015/017/018/019/020.
   **Not fixed project-wide here**, because correcting it properly means changing how every feature
   consumes the API, far outside this feature's scope.

   It is, however, **not silently repeated here either.** Left alone, this feature would add *new*
   hand-written API DTO types, turning inherited debt into fresh debt. Instead the Zod schemas in
   T043 are typed against the generated `organizations.d.ts` (`z.ZodType<components['schemas'][…]>`),
   so the contract still governs the shape: a schema that drifts from the OpenAPI document fails to
   compile. Runtime parsing stays Zod, matching the surrounding code. This is the smallest change
   that stops the violation growing without a project-wide migration, and it makes T001/T002's
   generated output load-bearing rather than decorative. Full adoption remains logged as debt.

## Key design decisions

Full reasoning in [research.md](./research.md). Summary of what the implementation must honour:

1. **Currency lives on the membership, not the user.** Per spec FR-003, there is no organization
   column on `ApplicationUser`. This supersedes issue #31's original `CurrentOrganizationId`
   approach.

2. **The invariant is a database constraint, not a code check.** A partial unique index
   (`... ON organization_memberships (user_id) WHERE is_current`) makes two current memberships
   physically unrepresentable, satisfying FR-004 under concurrency without application-level locking.

3. **Organization context travels as a session claim**, refreshed by the existing
   `RoleClaimsRefresher` mechanism generalized to cover both roles and organization. This satisfies
   FR-014 without a per-request database lookup and without inventing a second invalidation path.

4. **Query filters are applied by convention, not per entity.** A marker interface
   (`IOrganizationScoped`) plus a `ModelBuilder` extension applies the filter to every entity that
   carries it. #32/#33/#34 then opt an entity in by implementing the interface and adding a column —
   they do not each hand-write filter wiring.

5. **Enforcement is LINQ-scoped, and the spec says so.** EF Core query filters apply only to LINQ
   queries rooted on an entity type. This codebase's dominant read path in the areas to be scoped is
   `db.Database.SqlQuery<TRow>(...)`, which projects to non-entity records and is therefore
   *not* filtered. See "Enforcement boundary" below — this is the single most important constraint
   in this plan.

## Enforcement boundary (read this before implementing #32–#34)

The spec's fail-closed guarantee (FR-020) is **partial**, and deliberately so. Research counted 35
raw SQL calls across the repositories that #32–#34 will scope:

| Verdict | Count | Meaning |
|---|---|---|
| Trivial to convert to LINQ | ~12 | `DISTINCT`, `EXISTS` probes, plain filters |
| Moderate | ~14 | Null-guard `::text IS NULL OR …` casts, `ILIKE`, conditional `LEFT JOIN`s |
| Hard reads | 4 | `json_agg`+`GROUP BY` (org hangar ×2), array `ANY(unnest(...))` with tri-state filters (ship components), `LEFT JOIN LATERAL` (loot ledger) |
| Hard writes | 2 | `ON CONFLICT … RETURNING (xmax = 0)` upserts — need `organization_id` in the conflict target, not a filter |

The obligation this places on each scoping feature:

- Convert tractable queries to LINQ so the global filter covers them.
- For the 4 hard reads that stay raw SQL: add an explicit organization predicate **and** an
  integration test asserting that query cannot return another organization's rows.
- Never assume the filter covers a `Database.SqlQuery` call. It does not.

**Alternative considered and deferred: Postgres Row-Level Security.** RLS enforces at the storage
layer and would cover raw SQL, LINQ, and any future access path identically, with an unset context
failing closed by default. It was deferred because it requires a transaction-per-request seam that
does not exist today, and a non-superuser test role (the Testcontainers `test` user is a superuser
and silently bypasses RLS). With one organization there is nothing to leak to yet; RLS remains
available later as `ALTER TABLE` + `CREATE POLICY` on three tables. Recorded in research.md.

## Project Structure

### Documentation (this feature)

```text
specs/021-org-foundation/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output — decisions and rejected alternatives
├── data-model.md        # Phase 1 output — entities, constraints, migration
├── quickstart.md        # Phase 1 output — how to run and validate
├── contracts/
│   └── openapi.yaml     # Phase 1 output — admin organization endpoints
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
backend/src/
├── NajaEcho.Domain/
│   └── Organizations/
│       ├── Organization.cs                     # NEW entity
│       ├── OrganizationMembership.cs           # NEW entity
│       ├── IOrganizationScoped.cs              # NEW marker interface
│       └── DefaultOrganization.cs              # NEW — fixed id + name constants
├── NajaEcho.Application/
│   ├── Abstractions/
│   │   ├── IOrganizationContext.cs             # NEW port — current org for the request
│   │   └── IOrganizationRepository.cs          # NEW port
│   └── Features/Admin/Organizations/
│       ├── GetOrganizations/                   # Query + Handler + DTO
│       └── AssignOrganization/                 # Command + Handler + exceptions
├── NajaEcho.Infrastructure/
│   ├── Organizations/
│   │   └── OrganizationRepository.cs           # NEW
│   └── Persistence/
│       ├── AppDbContext.cs                     # MODIFIED — 2 DbSets, filter wiring
│       ├── OrganizationScopeExtensions.cs      # NEW — ModelBuilder filter application
│       ├── Configurations/
│       │   ├── OrganizationConfiguration.cs           # NEW
│       │   └── OrganizationMembershipConfiguration.cs # NEW
│       └── Migrations/
│           └── *_AddOrganizations.cs           # NEW — tables, indexes, seed, backfill
└── NajaEcho.Api/
    ├── Authorization/
    │   ├── OrganizationClaims.cs               # NEW — claim type constant
    │   └── RoleClaimsRefresher.cs              # MODIFIED — also refreshes org claim
    ├── Organizations/
    │   └── HttpOrganizationContext.cs          # NEW — IOrganizationContext impl
    ├── Features/Admin/Users/
    │   ├── UserAdminEndpoints.cs               # MODIFIED — PUT organization
    │   └── Contracts/                          # MODIFIED — org on user response
    ├── Features/Admin/Organizations/
    │   └── OrganizationAdminEndpoints.cs       # NEW — GET /api/admin/organizations
    └── Program.cs                              # MODIFIED — org claim at sign-in, DI

backend/tests/
├── NajaEcho.Infrastructure.Tests/
│   ├── Organizations/
│   │   ├── OrganizationRepositoryTests.cs      # NEW
│   │   └── MembershipInvariantTests.cs         # NEW — partial unique index behaviour
│   └── Persistence/
│       ├── OrganizationScopeTests.cs           # NEW — filter proven vs test entity
│       └── ScopeTestDbContext.cs               # NEW — test-only context + entity
├── NajaEcho.Api.Tests/
│   ├── Authorization/
│   │   └── OrganizationClaimsRefreshTests.cs   # NEW
│   └── Features/Admin/Organizations/
│       └── OrganizationAdminEndpointTests.cs   # NEW
└── NajaEcho.Application.Tests/Features/Admin/Organizations/
    └── AssignOrganizationHandlerTests.cs       # NEW

frontend/src/features/admin/
├── api/organizationsApi.ts                     # NEW
├── schemas/organizationSchemas.ts              # NEW
├── hooks/
│   ├── useOrganizations.ts                     # NEW
│   ├── useAssignOrganizationForUser.ts         # NEW
│   └── organizationKeys.ts                     # NEW
├── components/
│   ├── AssignOrganizationDialog.tsx            # NEW
│   └── UsersTable.tsx                          # MODIFIED — Organization column
├── pages/AdminUsersPage.tsx                    # MODIFIED — dialog state + filter
└── __tests__/adminOrganizations.test.tsx       # NEW
```

**Structure Decision**: Web application — the existing four-project backend (Domain →
Application → Infrastructure → API) plus the React SPA. This feature adds no new project and no
new layer; it extends each existing layer along its established seams. Backend files follow the
repo's feature-folder convention (`Features/<Area>/<Operation>/`); frontend files follow the
feature-folder convention under `features/admin/`.

## Complexity Tracking

> Filled because Principle IV (Simplicity / YAGNI) is knowingly deviated from.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|---|---|---|
| The entire tenant boundary, with no second tenant | Epic #30 records this as a deliberate, accepted YAGNI exception: installing the boundary while the data model is small is far cheaper than retrofitting it. Confirmed at epic level and re-confirmed during `/speckit-clarify`. | "Do nothing until a second organization exists" was the status quo and was explicitly rejected when Epic #30 was approved. The cost of reversing single-org assumptions across warehouse, hangar, and loot later was judged higher than the cost of the seam now. |
| A join table when every user holds exactly one membership | Spec FR-002. Multi-membership later becomes a UI change rather than a data migration. | A plain `organization_id` column on the user was the original issue #31 approach. Rejected during `/speckit-clarify` because it makes multi-membership a migration, and because pairing it with a join table created two sources of truth for the same fact. |
| Query filter infrastructure with zero scoped entities | Spec FR-023 requires the mechanism to be demonstrably working in this feature, so that #32/#33/#34 inherit something proven. It is exercised by a real integration test, not left dormant. | "Add the filter wiring in #32" was rejected because it makes #32 responsible for both inventing the mechanism and migrating data, and leaves this feature unable to prove the property it exists to establish. |

## Spec amendment applied

Research invalidated one spec statement. FR-020 claimed that a retrieval omitting an organization
condition returns nothing outside that organization — unconditionally. That is true for LINQ paths
and false for `Database.SqlQuery` paths. FR-020 was therefore split during this planning step: it
now scopes the guarantee to the standard data-access path, and the new FR-020a carries the
explicit-predicate-plus-test obligation for hand-authored queries. See the spec's Clarifications
section (Session 2026-07-19, planning) and FR-020/FR-020a. No further spec change is outstanding.

## Phase status

- [x] Phase 0 — research complete → [research.md](./research.md)
- [x] Phase 1 — design complete → [data-model.md](./data-model.md), [contracts/openapi.yaml](./contracts/openapi.yaml), [quickstart.md](./quickstart.md)
- [x] Phase 2 — tasks complete → [tasks.md](./tasks.md)
