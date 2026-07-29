---

description: "Task list for 021-org-foundation"
---

# Tasks: Organization Foundation & Admin Assignment

**Input**: Design documents from `/specs/021-org-foundation/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/openapi.yaml](./contracts/openapi.yaml), [quickstart.md](./quickstart.md)

**Tests**: Included and mandatory. Constitution Principle II (Test-First / TDD) is NON-NEGOTIABLE — every test task must be written and confirmed **failing** before its implementation task begins.

**Organization**: Grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel — no dependency on an incomplete task, and no conflict with another
  `[P]` task's work. Several `[P]` groups here are independent *test cases that land in the same
  file* (e.g. T018–T021 in `OrganizationRepositoryTests.cs`, T013–T015 in
  `MigrationBackfillTests.cs`). They parallelize as thinking and as authoring, but two people
  writing them simultaneously will conflict on the file — split by file, not by task ID, when
  dividing work across a team.
- **[Story]**: US1 / US2 / US3, mapping to spec.md user stories
- Exact file paths included in every task

## Path Conventions

Web application. Backend is a four-project Clean Architecture solution under `backend/src/`
(Domain → Application → Infrastructure → API) with tests under `backend/tests/`. Frontend is a React
SPA under `frontend/src/` organized by feature folder.

---

## Phase 1: Setup

**Purpose**: Contract plumbing before any implementation (Constitution Principle I).

- [X] T001 [P] Add a `gen:api:organizations` script to `frontend/package.json` pointing at `../specs/021-org-foundation/contracts/openapi.yaml` with output `src/lib/api/organizations.d.ts`, matching the nine existing `gen:api:*` scripts
- [X] T002 [P] Run `npm run gen:api:organizations` in `frontend/` and commit the generated `frontend/src/lib/api/organizations.d.ts`

> **Note**: Per plan.md Constitution Check, no frontend feature currently imports these generated types — every feature hand-writes Zod instead. T001/T002 keep this feature consistent with the repo's existing (if unused) convention. Actually adopting generated types is logged as separate debt and is **not** in scope here.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entities, persistence configuration, and the organization-context port. Every user story depends on these.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Domain

- [X] T003 [P] Create `Organization` entity (`Id`, `Name`, `CreatedAt`) in `backend/src/NajaEcho.Domain/Organizations/Organization.cs` per data-model.md
- [X] T004 [P] Create `OrganizationMembership` entity (`Id`, `UserId`, `OrganizationId`, `IsCurrent`, `JoinedAt`) in `backend/src/NajaEcho.Domain/Organizations/OrganizationMembership.cs` per data-model.md
- [X] T005 [P] Create `DefaultOrganization` constants in `backend/src/NajaEcho.Domain/Organizations/DefaultOrganization.cs` with `Id = 9b8ac811-3cec-421c-8cfb-cc56f775ad5a` and `Name = "Naja Echo"`

### Application port

- [X] T006 [P] Create `IOrganizationContext` port exposing `Guid? CurrentOrganizationId` in `backend/src/NajaEcho.Application/Abstractions/IOrganizationContext.cs`

### Persistence configuration

- [X] T007 [P] Create `OrganizationConfiguration` in `backend/src/NajaEcho.Infrastructure/Persistence/Configurations/OrganizationConfiguration.cs` — table `organizations`, `name` max length 100, explicit `HasColumnName` on every property per repo convention
- [X] T008 [P] Create `OrganizationMembershipConfiguration` in `backend/src/NajaEcho.Infrastructure/Persistence/Configurations/OrganizationMembershipConfiguration.cs` — both unique indexes (`ux_organization_memberships_user_current` with `.HasFilter("is_current")`, and `ux_organization_memberships_user_org`), FK to `Organization` with `OnDelete(Restrict)` named `fk_organization_memberships_organization_id`, FK to `ApplicationUser` with cascade delete
- [X] T009 Add `Organizations` and `OrganizationMemberships` DbSets, the two `ApplyConfiguration` calls, an `IOrganizationContext` constructor parameter, and a `public Guid? CurrentOrganizationId => organizationContext.CurrentOrganizationId;` property to `backend/src/NajaEcho.Infrastructure/Persistence/AppDbContext.cs` (depends on T003–T008)
- [X] T010 Create the organization claim type constant in `backend/src/NajaEcho.Api/Authorization/OrganizationClaims.cs`, implement `HttpOrganizationContext : IOrganizationContext` in `backend/src/NajaEcho.Api/Organizations/HttpOrganizationContext.cs` reading that claim from `IHttpContextAccessor` and returning null when absent or unparseable, then register `IHttpContextAccessor` and `IOrganizationContext` in `backend/src/NajaEcho.Api/Program.cs` before `AddInfrastructure` so `AppDbContext` can be resolved (depends on T006)

> **Why this is foundational, not US2**: T009 gives `AppDbContext` an `IOrganizationContext` constructor parameter. Without a registered implementation the container cannot construct the context, and every DB-touching test fails. The claim is simply empty until US2 starts issuing it.

### Test infrastructure

- [X] T011 Add a mutable stub `IOrganizationContext` and update `PostgresFixture.CreateContext()` and `PostgresFixture.BuildIdentityProvider()` in `backend/tests/NajaEcho.Infrastructure.Tests/PostgresFixture.cs` to supply it — the stub MUST allow a test to change the current organization between queries (T049 depends on this)
- [X] T012 Run `dotnet build backend/NajaEcho.slnx` and confirm the `AppDbContext` constructor change has not broken any other direct construction site

**Checkpoint**: Entities and persistence config compile; test fixture can vary the organization context.

---

## Phase 3: User Story 1 — Existing members keep working, unchanged (Priority: P1) 🎯 MVP

**Goal**: A migration creates the default "Naja Echo" organization and gives every existing user a current membership, changing nothing a member can see.

**Independent Test**: Apply the migration to a copy of production data; every existing user holds a current membership in "Naja Echo" and every portal view renders identically to the pre-migration build.

### Tests for User Story 1 ⚠️ WRITE FIRST, CONFIRM FAILING

- [X] T013 [P] [US1] Integration test in `backend/tests/NajaEcho.Infrastructure.Tests/Organizations/MigrationBackfillTests.cs` asserting that after migration exactly one organization exists, its id equals `DefaultOrganization.Id`, and its name is "Naja Echo" (FR-007)
- [X] T014 [P] [US1] Integration test in `backend/tests/NajaEcho.Infrastructure.Tests/Organizations/MigrationBackfillTests.cs` asserting every row in `"AspNetUsers"` has exactly one membership with `is_current = true` pointing at the default organization (FR-008, SC-001)
- [X] T015 [P] [US1] Integration test in `backend/tests/NajaEcho.Infrastructure.Tests/Organizations/MigrationBackfillTests.cs` asserting idempotency — executing the seed and backfill statements a second time changes no row counts and creates no duplicate organization or membership (FR-009)

### Implementation for User Story 1

- [X] T016 [US1] Create migration `AddOrganizations` in `backend/src/NajaEcho.Infrastructure/Persistence/Migrations/` via `dotnet ef migrations add AddOrganizations -p backend/src/NajaEcho.Infrastructure -s backend/src/NajaEcho.Api`, then hand-edit `Up` to add the guarded seed (`ON CONFLICT (id) DO NOTHING`) and the guarded backfill (`INSERT … SELECT … WHERE NOT EXISTS`) exactly as written in data-model.md — remember to quote `"AspNetUsers"` (depends on T009, T013–T015)
- [X] T017 [US1] Verify the `Down` method in the new `backend/src/NajaEcho.Infrastructure/Persistence/Migrations/*_AddOrganizations.cs` drops both tables and touches no pre-existing data, confirming this is not a destructive migration under the constitution's Development Workflow rule

**Checkpoint**: US1 complete and independently shippable. Existing members are all in the default organization; nothing in the UI has changed. This alone is a valid release.

---

## Phase 4: User Story 2 — Admin assigns a member to an organization (Priority: P2)

**Goal**: A global admin sees each member's organization on the Members page and can set or clear it, taking effect on the member's next request without a sign-out.

**Independent Test**: Register a new member; as admin assign them to "Naja Echo"; confirm as that member the change is reflected on their very next request.

**Deliverable split**: this phase is half the feature, so treat it as two shippable slices.

| Slice | Tasks | Independently testable? |
|---|---|---|
| **US2a — backend** | T018–T040, incl. T022a and T026a (25) | Yes — API tests cover every endpoint, status code, and the claims refresh, and T026a proves the whole stack against a real database. No UI required. |
| **US2b — frontend** | T041–T048 (8) | Yes — MSW mocks the endpoints, so it needs the contract, not a running backend. |

US2a is the substantial half and carries all the invariant risk. US2b cannot ship alone (an admin
would have no working API behind the dialog), but it can be *built* in parallel once T037's response
shape and the contract are settled.

### Backend tests ⚠️ WRITE FIRST, CONFIRM FAILING

- [X] T018 [P] [US2] Repository integration test in `backend/tests/NajaEcho.Infrastructure.Tests/Organizations/OrganizationRepositoryTests.cs` — assigning an unassigned user creates one current membership
- [X] T019 [P] [US2] Repository integration test in the same file — reassigning from org A to org B leaves exactly one current membership, in B, with A's row retained but `is_current = false` (data-model.md state transitions)
- [X] T020 [P] [US2] Repository integration test in `backend/tests/NajaEcho.Infrastructure.Tests/Organizations/OrganizationRepositoryTests.cs` — assigning a user to an organization they already hold a non-current membership in reactivates that row rather than inserting a duplicate (satisfies `ux_organization_memberships_user_org`)
- [X] T021 [P] [US2] Repository integration test in `backend/tests/NajaEcho.Infrastructure.Tests/Organizations/OrganizationRepositoryTests.cs` — clearing leaves the membership row present with `is_current = false` and the user with no current membership
- [X] T022 [P] [US2] Invariant test in `backend/tests/NajaEcho.Infrastructure.Tests/Organizations/MembershipInvariantTests.cs` — a direct insert of a second `is_current = true` row for the same user is rejected by `ux_organization_memberships_user_current` (FR-004)
- [X] T022a [P] [US2] Concurrency test in `backend/tests/NajaEcho.Infrastructure.Tests/Organizations/MembershipInvariantTests.cs` — two overlapping `SetCurrentAsync` calls for the same user, on separate connections, with the first transaction held open past the second's write. Assert exactly one current membership survives and the loser fails on `ux_organization_memberships_user_current` rather than succeeding. This is the arm of SC-008 that the single-connection duplicate-insert test (T022) does not reach, and it is the interleaving the spec's Edge Cases and plan.md's Constraints both single out (SC-008, FR-004)
- [X] T023 [P] [US2] Handler unit tests in `backend/tests/NajaEcho.Application.Tests/Features/Admin/Organizations/AssignOrganizationHandlerTests.cs` covering: unknown user throws `UserNotFoundException`; unknown organization throws `OrganizationNotFoundException`; success calls `IUserSessionInvalidator.Invalidate`; assigning the current organization again is a no-op
- [X] T024 [P] [US2] Handler unit test in `backend/tests/NajaEcho.Application.Tests/Features/Admin/Organizations/AssignOrganizationHandlerTests.cs` asserting the structured log event carries acting admin, target member, previous organization, and new organization, and contains no token, cookie, or authorization header (FR-018, FR-019)
- [X] T025 [P] [US2] API endpoint tests in `backend/tests/NajaEcho.Api.Tests/Features/Admin/Organizations/OrganizationAdminEndpointTests.cs` following the existing `UserAdminEndpointTests` shape (own `AuthenticationHandler`, `StubDatabase()`, faked repositories): `GET /api/admin/organizations` returns 200 for admin, 403 for non-admin, 401 unauthenticated
- [X] T026 [P] [US2] API endpoint tests in `backend/tests/NajaEcho.Api.Tests/Features/Admin/Organizations/OrganizationAdminEndpointTests.cs` for `PUT /api/admin/users/{userId}/organization`: 204 on success, 204 on clear with null body value, 403 for non-admin (FR-013), 404 with `urn:najaecho:error:user-not-found`, 404 with `urn:najaecho:error:organization-not-found`
- [X] T026a [US2] **End-to-end API test against real Postgres** in `backend/tests/NajaEcho.Api.Tests/Features/Admin/Organizations/OrganizationAssignmentEndToEndTests.cs` — a `WebApplicationFactory<Program>` wired to `PostgresFixture`'s real database instead of `StubDatabase()` and instead of faked repositories, driving `PUT /api/admin/users/{userId}/organization` as an admin and asserting the `organization_memberships` row actually changed, then `GET /api/admin/users` reflects the new organization. Every other API test in this feature fakes the repositories, so this is the only task that proves endpoint, handler, repository, and database agree. Required by the constitution's Development Workflow rule (*"At least one integration test covering the API contract end-to-end through the real database"*) — the rest of the suite does not satisfy it (depends on T026, T040)
- [X] T027 [P] [US2] Claims refresh test in `backend/tests/NajaEcho.Api.Tests/Authorization/OrganizationClaimsRefreshTests.cs` — an invalidated session picks up the new organization claim on the next request without `RejectPrincipal` being called (FR-014), mirroring `RoleClaimsRefresherTests`

### Backend implementation

- [X] T028 [P] [US2] Create `IOrganizationRepository` port in `backend/src/NajaEcho.Application/Abstractions/IOrganizationRepository.cs` with `GetAllAsync`, `ExistsAsync`, `GetCurrentForUserAsync`, and `SetCurrentAsync(Guid userId, Guid? organizationId, CancellationToken)`
- [X] T029 [US2] Implement `OrganizationRepository` in `backend/src/NajaEcho.Infrastructure/Organizations/OrganizationRepository.cs` using LINQ (not raw SQL). `SetCurrentAsync` MUST run in a transaction and clear the existing current membership **before** setting the new one, or the partial unique index rejects the write. Translate `DbUpdateException` on `ux_organization_memberships_user_current` to a domain exception, following the `ux_hangar_entries_user_ship` pattern in `HangarRepository` (depends on T018–T022, T028)
- [X] T030 [P] [US2] Create `GetOrganizationsQuery`, `GetOrganizationsHandler`, and `OrganizationDto` in `backend/src/NajaEcho.Application/Features/Admin/Organizations/GetOrganizations/`
- [X] T031 [P] [US2] Create `AssignOrganizationCommand` (carrying `TargetUserId`, `OrganizationId`, and `CallerId` for logging), `UserNotFoundException` reuse, and `OrganizationNotFoundException` in `backend/src/NajaEcho.Application/Features/Admin/Organizations/AssignOrganization/`
- [X] T032 [US2] Implement `AssignOrganizationHandler` in `backend/src/NajaEcho.Application/Features/Admin/Organizations/AssignOrganization/AssignOrganizationHandler.cs` — validate user and organization exist, read the previous organization for logging, call `SetCurrentAsync`, call `IUserSessionInvalidator.Invalidate(targetUserId)`, then emit the FR-018 log event using the house convention `"AssignOrganization caller={CallerId} targetUserId={TargetUserId} previousOrganizationId={PreviousOrganizationId} newOrganizationId={NewOrganizationId} outcome=success"` (depends on T023, T024, T029, T031)
- [X] T033 [P] [US2] Add `GetCurrentOrganizationAsync(Guid userId, CancellationToken)` to `IUserRepository` in `backend/src/NajaEcho.Application/Abstractions/IUserRepository.cs` and implement it in `backend/src/NajaEcho.Infrastructure/Identity/UserRepository.cs` using LINQ over the current membership — this is what the claims refresher reads on each refresh
- [X] T034 [US2] Add the organization claim to the `ClaimsIdentity` built in `OnTicketReceived` in `backend/src/NajaEcho.Api/Program.cs`, so a freshly signed-in member carries their organization without waiting for a refresh (depends on T010, T033)
- [X] T035 [US2] Generalize `backend/src/NajaEcho.Api/Authorization/RoleClaimsRefresher.cs` to refresh the organization claim alongside role claims — fetch the current organization in the same pass via T033, and extend `WithRoles` to preserve non-role, non-organization claims while replacing both. Keep the existing staleness logic, `roles.refreshed_at` stamp, and 15-minute fallback untouched (depends on T027, T033)
- [X] T036 [US2] Add an end-to-end test in `backend/tests/NajaEcho.Api.Tests/Authorization/OrganizationClaimsRefreshTests.cs` proving `AppDbContext.CurrentOrganizationId` resolves from the signed-in principal on a real request — this is the seam where the claim, `HttpOrganizationContext`, and the DbContext meet, and none of T010/T034/T035 proves it alone (depends on T034, T035)
- [X] T037 [P] [US2] Add `OrganizationSummaryResponse` and extend `AdminUserResponse` with a nullable `Organization` property in `backend/src/NajaEcho.Api/Features/Admin/Users/Contracts/AdminUserListResponse.cs`, matching `contracts/openapi.yaml`
- [X] T038 [US2] Extend `GetUsersHandler` and `IUserRepository.GetUsersWithRolesAndCharactersAsync` to include each user's current organization in `backend/src/NajaEcho.Application/Features/Admin/Users/GetUsers/` and `backend/src/NajaEcho.Infrastructure/Identity/UserRepository.cs` (FR-011)
- [X] T039 [US2] Create `OrganizationAdminEndpoints` mapping `GET /api/admin/organizations` under `RequireAuthorization(AuthorizationPolicies.Admin)` in `backend/src/NajaEcho.Api/Features/Admin/Organizations/OrganizationAdminEndpoints.cs`, and map it in `Program.cs` (depends on T025, T030)
- [X] T040 [US2] Add `PUT /{userId:guid}/organization` to the existing group in `backend/src/NajaEcho.Api/Features/Admin/Users/UserAdminEndpoints.cs` with `AssignOrganizationRequest`, caller-id extraction, and `Results.Problem` mapping for both 404 `type` URNs, following the existing try/catch-per-exception style (depends on T026, T032)

### Frontend tests ⚠️ WRITE FIRST, CONFIRM FAILING

- [X] T041 [P] [US2] Component tests in `frontend/src/features/admin/__tests__/adminOrganizations.test.tsx` using MSW: the Organization column renders the organization name, and an em-dash for a member with none
- [X] T042 [P] [US2] Component tests in `frontend/src/features/admin/__tests__/adminOrganizations.test.tsx`: opening the dialog, selecting an organization, and saving issues the `PUT` and invalidates the admin users list; clearing sends a null `organizationId`; a 404 response surfaces a readable message via the existing `mapError` pattern

### Frontend implementation

- [X] T043 [P] [US2] Create Zod schemas (`organizationSummarySchema`, `organizationListResponseSchema`) in `frontend/src/features/admin/schemas/organizationSchemas.ts`, and extend `adminUserSchema` in `frontend/src/features/admin/schemas/userSchemas.ts` with a nullable `organization` field. Annotate each new schema as `z.ZodType<components['schemas']['…']>` against the `organizations.d.ts` generated by T002, so a schema that drifts from `contracts/openapi.yaml` fails to compile. Runtime parsing stays Zod, matching the surrounding code — see plan.md Constitution Check deviation 2 for why this half-step, and not full generated-type adoption, is the right scope here
- [X] T044 [P] [US2] Create `getOrganizations` and `assignOrganizationForUser` in `frontend/src/features/admin/api/organizationsApi.ts`, wrapping `apiFetch` and parsing through Zod at the boundary per the existing `usersApi.ts` pattern
- [X] T045 [P] [US2] Create `organizationKeys` in `frontend/src/features/admin/hooks/organizationKeys.ts` following the nested-object style of the existing `userKeys`
- [X] T046 [US2] Create `useOrganizations` and `useAssignOrganizationForUser` hooks in `frontend/src/features/admin/hooks/`, with the mutation invalidating `userKeys.adminUsers.list()` on success, mirroring `useAssignRolesForUser` (depends on T043–T045)
- [X] T047 [US2] Create `AssignOrganizationDialog` in `frontend/src/features/admin/components/AssignOrganizationDialog.tsx` — shadcn `Dialog`, single-select over the organizations list plus an explicit "No organization" option, `mapError` switching on `ApiError.status`, disabled/pending submit state, accessible labels (depends on T041, T042, T046)
- [X] T048 [US2] Add the Organization column to `frontend/src/features/admin/components/UsersTable.tsx` with an `onAssignOrganization` callback, and wire dialog state plus organization-name matching into the existing client-side filter in `frontend/src/features/admin/pages/AdminUsersPage.tsx` (depends on T047)

**Checkpoint**: US1 and US2 both work independently. Admins can manage organization membership end to end.

---

## Phase 5: User Story 3 — Scoping fails closed, not open (Priority: P3)

**Goal**: Query filter infrastructure that restricts organization-scoped entities by default, proven working against a real database even though no production entity is scoped yet.

**Independent Test**: An integration test retrieves organization-scoped rows while acting as a member of one organization, with the query written **without** any organization condition, and cannot see another organization's rows.

### Tests for User Story 3 ⚠️ WRITE FIRST, CONFIRM FAILING

- [X] T049 [P] [US3] Create a test-only `ScopeTestDbContext` and a test entity implementing `IOrganizationScoped` in `backend/tests/NajaEcho.Infrastructure.Tests/Persistence/ScopeTestDbContext.cs`, applying the same `ApplyOrganizationFilters` extension the production context uses, created via `EnsureCreated` against `PostgresFixture` (depends on T011)
- [X] T050 [P] [US3] Test in `backend/tests/NajaEcho.Infrastructure.Tests/Persistence/OrganizationScopeTests.cs` — with organization A current, a LINQ query stating no organization condition returns only A's rows (FR-020)
- [X] T051 [P] [US3] Test in `backend/tests/NajaEcho.Infrastructure.Tests/Persistence/OrganizationScopeTests.cs` — with no current organization, the same query returns empty and does not throw (FR-021)
- [X] T052a [P] [US3] Test in `backend/tests/NajaEcho.Infrastructure.Tests/Persistence/OrganizationScopeTests.cs` — **no role bypasses the filter**. With organization A current and the acting principal holding the Admin role, the same unconditional LINQ query returns only A's rows, identical to a plain member. This feature exists to prove the enforcement seam before #32–#34 inherit it, and "admins can see everything" is the most likely way a later feature would try to break it (FR-024)
- [X] T052b [P] [US3] Test in `backend/tests/NajaEcho.Infrastructure.Tests/Persistence/OrganizationScopeTests.cs` — an entity that does **not** implement `IOrganizationScoped` returns all rows with the organization context set to null, confirming the filter is genuinely opt-in and reference data stays unscoped. True by construction today; asserted so that a future change to `ApplyOrganizationFilters` cannot silently start scoping the catalog (FR-022, SC-006)
- [X] T052 [US3] **Model-cache bleed test** in `backend/tests/NajaEcho.Infrastructure.Tests/Persistence/OrganizationScopeTests.cs` — query under organization A, then under organization B, asserting each returns only its own rows. This is the highest-value assertion in the feature: it catches the organization being captured at model-build time and baked into EF's cached model, which would silently serve one tenant's data to every tenant and is invisible to any single-organization test.
  **The two contexts MUST share one model cache**, or the test passes without proving anything. Build a single `ServiceProvider` (or a single `DbContextOptions` with one `IModelCacheKeyFactory`), resolve two contexts from it, and vary only the stub's organization value between them. Constructing a fresh provider per organization gives each its own compiled model and hides the exact bug being tested — write the test so that reverting T054 to capture the value at model-build time makes it fail

### Implementation for User Story 3

- [X] T053 [P] [US3] Create the `IOrganizationScoped` marker interface with a `Guid OrganizationId { get; set; }` member in `backend/src/NajaEcho.Domain/Organizations/IOrganizationScoped.cs`, referencing only `System.Guid` so Domain acquires no framework dependency
- [X] T054 [US3] Create `ApplyOrganizationFilters` extension in `backend/src/NajaEcho.Infrastructure/Persistence/OrganizationScopeExtensions.cs` — walk `modelBuilder.Model.GetEntityTypes()`, select types implementing `IOrganizationScoped`, and apply `HasQueryFilter` with an expression built against a **DbContext instance member** so EF parameterizes it per request (depends on T049–T053)
- [X] T055 [US3] Call `modelBuilder.ApplyOrganizationFilters(() => CurrentOrganizationId)` at the end of `OnModelCreating` in `backend/src/NajaEcho.Infrastructure/Persistence/AppDbContext.cs` (depends on T054; modifies the same file as T009, so sequence after it)

**Checkpoint**: All three user stories independently functional. The enforcement seam is proven and ready for #32–#34 to opt entities into.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T056 [P] Run the full suite — `dotnet test backend/NajaEcho.slnx` and `cd frontend && npm test` — and confirm green
- [ ] T057 [P] Walk every manual validation V1–V5 in [quickstart.md](./quickstart.md), especially V1 (no visible change for existing members, SC-002) and V5 (log event contains no secrets, FR-019). Time the V2 walkthrough from landing on the Members page to a saved assignment and record it — this is the only check on SC-003's 30-second target — **outstanding**: requires running the app against a real database and a Discord sign-in, which cannot be done from an automated run. V2–V4 are covered in substance by `OrganizationAssignmentEndToEndTests` (assign, clear, 403, both 404s, against real Postgres), but V1 (no visible change for an existing member, SC-002), V5 (log output carries no secrets, FR-019) and the SC-003 timing still need a human at the browser
- [X] T058 [P] Run `grep -rn "Database.SqlQuery\|Database.ExecuteSql" backend/src/NajaEcho.Infrastructure/Organizations/` and confirm it returns nothing — `backend/src/NajaEcho.Infrastructure/Organizations/OrganizationRepository.cs` must be LINQ-only so the query filter will cover it when entities are scoped in #32–#34
- [ ] T059 ~~Update `specs/ROADMAP.md` to mark #31 complete and note that #32–#34 are unblocked~~ — **not done, deliberately.** `specs/ROADMAP.md` defines the planning *model* and states plainly that "the board is the source of truth for live state", that it holds "present + future work only", and that completed features are "not backfilled — the `specs/NNN-` folders are their archive". Writing per-feature status into it would contradict its own stated policy. The status change belongs on the [Naja Echo Planning board](https://github.com/orgs/naja-echo/projects/3): move #31 to **In Review** when the PR opens and **Done** on merge, then mark #32–#34 unblocked. Left for a human because it is an outward-facing change to a shared board, and the work is not merged yet
- [X] T060 Verify `contracts/openapi.yaml` still matches the shipped endpoints — including the `PUT …/roles` entry this feature documented to close pre-existing drift (plan.md Constitution Check). Also confirm the *absence* of any organization create, rename, or delete route in both the contract and `OrganizationAdminEndpoints.cs`: FR-015 makes "no such path exists" a requirement, and only a negative check can verify it

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies
- **Foundational (Phase 2)**: depends on Setup — **blocks all user stories**
- **US1 (Phase 3)**: depends on Foundational
- **US2 (Phase 4)**: depends on Foundational. Does not depend on US1, but shipping US2 without US1 leaves existing members unassigned, so deliver in priority order
- **US3 (Phase 5)**: depends on Foundational. Fully independent of US1 and US2 — different files except the one-line `AppDbContext` change in T055
- **Polish (Phase 6)**: depends on all desired stories

### Cross-story file contention

Only two files are touched by more than one phase:

| File | Phases | Handling |
|---|---|---|
| `AppDbContext.cs` | T009 (Foundational), T055 (US3) | T055 appends one line; sequence after T009 |
| `Program.cs` | T010 (Foundational), T034 and T039 (US2) | Three-way, and the only file crossing a phase boundary with more than one edit on the far side. T010 registers DI, T034 adds the sign-in claim, T039 maps the endpoint group. Strictly sequential in that order |

### Within each story

Tests are written and confirmed failing before implementation. Domain → configuration → repository →
handler → endpoint → UI.

---

## Parallel Opportunities

**Phase 2 foundational** — T003, T004, T005, T006, T007, T008 all touch different new files:

```bash
Task: "Create Organization entity in backend/src/NajaEcho.Domain/Organizations/Organization.cs"
Task: "Create OrganizationMembership entity in backend/src/NajaEcho.Domain/Organizations/OrganizationMembership.cs"
Task: "Create DefaultOrganization constants in backend/src/NajaEcho.Domain/Organizations/DefaultOrganization.cs"
Task: "Create IOrganizationContext port in backend/src/NajaEcho.Application/Abstractions/IOrganizationContext.cs"
Task: "Create OrganizationConfiguration in backend/src/NajaEcho.Infrastructure/Persistence/Configurations/OrganizationConfiguration.cs"
Task: "Create OrganizationMembershipConfiguration in backend/src/NajaEcho.Infrastructure/Persistence/Configurations/OrganizationMembershipConfiguration.cs"
```

**US2 backend tests** — T018–T027 plus T022a are twelve independent test cases. They span five
files, so divide by file: `OrganizationRepositoryTests.cs` (T018–T021), `MembershipInvariantTests.cs`
(T022, T022a), `AssignOrganizationHandlerTests.cs` (T023, T024), `OrganizationAdminEndpointTests.cs`
(T025, T026), `OrganizationClaimsRefreshTests.cs` (T027). T026a is sequential — it depends on T040.

**US2 frontend scaffolding** — T043, T044, T045 are three different new files.

**US3** — T049, T050, T051, T052a, T052b, T053 parallelize (the four `OrganizationScopeTests.cs`
cases share a file — see the `[P]` note above); T052, T054, T055 are sequential.

**Across stories** — once Phase 2 completes, US1 (migration) and US3 (filter infrastructure) share no
files and can be built simultaneously by different people.

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational → Phase 3 US1
2. **STOP and validate**: every existing member is in "Naja Echo"; no view changed
3. Shippable on its own — it is a pure data-model addition with zero user-visible surface

### Incremental delivery — build US3 before US2

Spec priority (P1 → P2 → P3) reflects **user value**. Build order should follow **risk**, and the two
differ here.

1. Setup + Foundational → foundation ready
2. **+ US1** (5 tasks) → default organization exists, everyone backfilled → deploy (MVP)
3. **+ US3** (9 tasks) → enforcement seam proven → deploy
4. **+ US2** (33 tasks) → admins can manage membership → deploy

US3 is the smallest phase and the only one that can invalidate the design. If the query filter
cannot be made to work correctly — particularly the model-cache behaviour in T052 — that changes the
approach for the whole epic. Discovering it after building US2's 33 tasks costs far more than
discovering it after US1's five. US3 depends on nothing in US2, so nothing is lost by moving it up.

Ship order remains free: US1 alone is a valid release, and US3 adds no user-visible surface.

### Parallel team strategy

After Phase 2, three tracks run concurrently:

- Developer A: US1 (migration + backfill tests)
- Developer B: US2 backend (repository, handlers, endpoints, claims)
- Developer C: US3 (filter infrastructure) — then US2 frontend once B's contract lands

---

## Notes

- **US3's T052 is the task most worth reviewing carefully.** The model-cache bug it guards against is
  the one failure mode that would silently defeat the entire epic while every other test passes.
- `OrganizationRepository` must be LINQ-only (T029, T058). Introducing raw SQL there would make the
  repository invisible to the very filter this feature exists to install.
- `SetCurrentAsync` ordering is load-bearing: clear before set, inside a transaction, or the partial
  unique index rejects the write (research.md D2).
- `InMemoryUserSessionInvalidator` is process-local, so FR-014's "next request" guarantee holds on
  single-instance deployments and degrades to ≤15 minutes on multi-instance ones. Pre-existing for
  role changes; not fixed here (research.md D3).
- Commit after each task or logical group; stop at any checkpoint to validate a story independently.
