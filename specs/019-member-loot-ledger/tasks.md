# Tasks: Member Loot Ledger

**Input**: Design documents from `/specs/019-member-loot-ledger/`

**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/openapi.yaml ✅, quickstart.md ✅

**Tests**: Included — Test-First / TDD is mandated by Constitution Principle II and explicitly required in plan.md for all three test tiers (Application/Domain unit, Testcontainers integration, API contract, and Vitest/RTL/MSW frontend).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Exact file paths are included in all descriptions

## Path Conventions

- **Backend source**: `backend/src/`
- **Backend tests**: `backend/tests/`
- **Frontend source**: `frontend/src/`

---

## Phase 1: Setup

**Purpose**: Verify development environment and establish new folder structure.

- [X] T001 Verify backend builds (`dotnet build backend/NajaEcho.slnx`), frontend builds (`cd frontend && npm run build`), and PostgreSQL is accessible (`docker-compose up -d db`) per quickstart.md prerequisites
- [X] T002 [P] Create frontend feature folder skeleton: `frontend/src/features/crew-resources/{pages,components,hooks,api,schemas,__tests__}/` directories

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain model, EF configuration, migration, role/auth plumbing, repository, and DI wiring that MUST be complete before any user story implementation.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 [P] Create `LootLedgerKind` enum in `backend/src/NajaEcho.Domain/Loot/LootLedgerKind.cs` with values `OrgPoints` and `LootPoints`
- [X] T004 [P] Create `LootLedgerEntry` entity in `backend/src/NajaEcho.Domain/Loot/LootLedgerEntry.cs` with fields: `Id` (Guid), `MemberId` (Guid), `Kind` (LootLedgerKind), `Amount` (int), `Reason` (string max 500), `ActorId` (Guid), `CreatedAt` (DateTimeOffset)
- [X] T005 [P] Create `LootMemberStanding` keyless read model in `backend/src/NajaEcho.Domain/Loot/LootMemberStanding.cs` with fields: `MemberId` (Guid), `DisplayName` (string), `OrgPointsTotal` (int), `LootPointsTotal` (int), `ClaimPriority` (double)
- [X] T006 [P] Create `ClaimPriority` pure-calc helper in `backend/src/NajaEcho.Application/Features/Loot/ClaimPriority.cs` — `orgTotal / (lootTotal == 0 ? 100 : lootTotal)`; mirrors the `loot_member_standing` view formula; used by handlers to compute priority for `/me` and `/{userId}` responses
- [X] T007 [P] Create `ILootLedgerRepository` interface in `backend/src/NajaEcho.Application/Abstractions/ILootLedgerRepository.cs` with methods: `AddEntryAsync(LootLedgerEntry, CancellationToken)`, `GetDistributionAsync(CancellationToken)` (reads `loot_member_standing` view), `GetMemberLedgerAsync(Guid memberId, CancellationToken)` (newest-first entries with poster display name) — no update or delete methods (research Decision 8)
- [X] T008 Create `LootLedgerEntryConfiguration` in `backend/src/NajaEcho.Infrastructure/Persistence/Configurations/LootLedgerEntryConfiguration.cs` — maps to `loot_ledger` table (snake_case via Npgsql convention); `kind` column via enum-to-string value conversion; FK `member_id → AspNetUsers(id)` ON DELETE Cascade; FK `actor_id → AspNetUsers(id)` ON DELETE Restrict; composite index `ix_loot_ledger_member_kind_created` on `(member_id, kind, created_at DESC)`; check constraints `ck_loot_ledger_kind` (`kind IN ('OrgPoints','LootPoints')`) and `ck_loot_ledger_reason` (`length(btrim(reason)) >= 1`)
- [X] T009 [P] Create `LootMemberStandingConfiguration` in `backend/src/NajaEcho.Infrastructure/Persistence/Configurations/LootMemberStandingConfiguration.cs` — `HasNoKey().ToView("loot_member_standing")`; maps columns to `LootMemberStanding` properties
- [X] T010 Update `AppDbContext` in `backend/src/NajaEcho.Infrastructure/Persistence/AppDbContext.cs` — add `DbSet<LootLedgerEntry> LootLedger` and keyless `DbSet<LootMemberStanding> LootMemberStandings`
- [X] T011 Generate `AddLootLedger` EF Core migration via `dotnet ef migrations add AddLootLedger` in `backend/src/NajaEcho.Infrastructure/`; then edit the generated `Migrations/*_AddLootLedger.cs` to add `migrationBuilder.Sql(CREATE VIEW loot_member_standing AS ...)` in `Up` (full view SQL from data-model.md with LATERAL character join and `NULLIF`/`COALESCE` Claim Priority formula) and `migrationBuilder.Sql("DROP VIEW IF EXISTS loot_member_standing")` before `DropTable` in `Down`
- [X] T012 Add `"CrewResourceOfficer"` to the `Roles` string array in `backend/src/NajaEcho.Infrastructure/Identity/RoleSeeder.cs`
- [X] T013 Add `public const string CrewResourceOfficer = "CrewResourceOfficer"` and a corresponding policy (`options.AddPolicy(CrewResourceOfficer, p => p.RequireRole(CrewResourceOfficer, Admin))`) to `backend/src/NajaEcho.Api/Authorization/AuthorizationPolicies.cs`, mirroring the existing `Quartermaster` pattern
- [X] T014 Add `"CrewResourceOfficer"` to the `ValidRoles` hashset in `backend/src/NajaEcho.Application/Features/Admin/Users/AssignRoles/AssignRolesHandler.cs`
- [X] T015 [P] Add `CrewResourceOfficer: "Crew Resource Officer"` entry to `roleDisplayNames` in `frontend/src/features/admin/lib/roleDisplayNames.ts` so the existing 017 `AssignRolesDialog` can grant the new role (no structural change per spec Assumption)
- [X] T016 Create `LootLedgerRepository` in `backend/src/NajaEcho.Infrastructure/Loot/LootLedgerRepository.cs` implementing `ILootLedgerRepository`: EF `db.LootLedger.Add(entry)` + `SaveChangesAsync` for inserts; `db.LootMemberStandings.OrderBy(x => x.ClaimPriority).ToListAsync()` for distribution; raw parameterized `db.Database.SqlQuery<LedgerEntryRow>` with `COALESCE(c.name, actor.display_name)` for per-member newest-first reads (mirroring `UserRepository.GetUsersWithRolesAndCharactersAsync` pattern); sets `CreatedAt = DateTimeOffset.UtcNow` server-side on insert
- [X] T017 Register `ILootLedgerRepository → LootLedgerRepository` as `AddScoped` in `backend/src/NajaEcho.Infrastructure/DependencyInjection.cs` (individual handler registrations are added per user story phase)

**Checkpoint**: Foundation ready — domain model, EF configuration, migration, role seeding, auth policies, repository, and DI wiring are all in place. User story implementation can now begin.

---

## Phase 3: User Story 1 — Member Views Their Own Loot Ledger (Priority: P1) 🎯 MVP

**Goal**: Any authenticated member can navigate to `/crew-resources/my-loot` and see their full OrgPoints ledger, LootPoints ledger, and current Claim Priority. Entries are newest-first showing amount, reason, poster name, and date. No edit or delete controls exist.

**Independent Test**: Sign in as any member and navigate to `/crew-resources/my-loot` — two ledger tables and a Claim Priority value appear. A new member with no entries sees empty tables and Claim Priority `0.00`.

### Tests for User Story 1 — write first, must FAIL before implementation

- [X] T018 [P] [US1] Write `ClaimPriorityTests` in `backend/tests/NajaEcho.Application.Tests/Features/Loot/ClaimPriorityTests.cs` covering: both totals zero → `0.00`; loot total zero, org positive (150/0 → `1.50`); normal ratio (150/300 → `0.50`); negative OrgPoints (-20/100 → `-0.20`) — must be RED before T022
- [X] T019 [P] [US1] Write Testcontainers integration test in `backend/tests/NajaEcho.Infrastructure.Tests/Loot/LootLedgerRepositoryTests.cs` for per-member ledger read: insert OrgPoints + LootPoints rows for a member, verify newest-first order and poster display name resolves character name over Discord display name; member with zero entries returns empty lists — must be RED before T023
- [X] T020 [P] [US1] Write API contract test for `GET /api/loot/me` in `backend/tests/NajaEcho.Api.Tests/Features/Loot/LootEndpointsTests.cs`: unauthenticated → 401; authenticated → 200 with `MemberLedgerResponse` schema (memberId, displayName, orgPoints[], lootPoints[], orgPointsTotal, lootPointsTotal, claimPriority) — must be RED before T025
- [X] T021 [P] [US1] Write Vitest + RTL + MSW frontend tests for `MyLootPage` in `frontend/src/features/crew-resources/__tests__/MyLootPage.test.tsx`: renders two `LedgerTable` sections + Claim Priority value; empty state shows `0.00` and empty-table messages; entry rows display amount, reason, poster name, date — must be RED before T033

### Implementation for User Story 1

- [X] T022 [P] [US1] Create `GetMemberLedgerQuery`, `GetMemberLedgerHandler`, `MemberLedgerDto`, and `LedgerEntryDto` in `backend/src/NajaEcho.Application/Features/Loot/GetMemberLedger/` — handler calls `ILootLedgerRepository.GetMemberLedgerAsync`, splits entries by kind into OrgPoints/LootPoints lists, computes `ClaimPriority` using the helper in `ClaimPriority.cs`; throws `MemberNotFoundException` if member not found
- [X] T023 [US1] Implement per-member raw SQL read in `backend/src/NajaEcho.Infrastructure/Loot/LootLedgerRepository.cs` — parameterized query joining `loot_ledger` to `AspNetUsers` (actor) and `characters` (actor's character) with `COALESCE(c.name, actor.display_name) AS posted_by`, `WHERE l.member_id = @memberId ORDER BY l.created_at DESC`; returns 404-able empty for unknown member — makes T019 GREEN
- [X] T024 [P] [US1] Create API contract DTOs in `backend/src/NajaEcho.Api/Features/Loot/Contracts/`: `LedgerEntryResponse` (id, amount, reason, postedBy, createdAt) and `MemberLedgerResponse` (memberId, displayName, orgPoints `LedgerEntryResponse[]`, lootPoints `LedgerEntryResponse[]`, orgPointsTotal, lootPointsTotal, claimPriority `double`)
- [X] T025 [US1] Create `LootEndpoints.cs` in `backend/src/NajaEcho.Api/Features/Loot/LootEndpoints.cs` with static `MapLootEndpoints` extension method — create `/api/loot` group with `.RequireAuthorization()`; add `GET /me` route resolving caller's own ID from `HttpContext` and dispatching to `GetMemberLedgerHandler` — makes T020 GREEN
- [X] T026 [US1] Register `GetMemberLedgerHandler` (AddScoped) in `backend/src/NajaEcho.Infrastructure/DependencyInjection.cs` and call `app.MapLootEndpoints()` in `backend/src/NajaEcho.Api/Program.cs` beside `app.MapWarehouseEndpoints()`
- [X] T027 [P] [US1] Create Zod schemas in `frontend/src/features/crew-resources/schemas/lootSchemas.ts`: `ledgerEntrySchema` (id, amount `z.number().int()`, reason, postedBy, createdAt) and `memberLedgerResponseSchema` (memberId, displayName, orgPoints, lootPoints, orgPointsTotal, lootPointsTotal, claimPriority)
- [X] T028 [P] [US1] Create query key factory in `frontend/src/features/crew-resources/hooks/lootKeys.ts`: `lootKeys.myLoot()`, `lootKeys.distribution()`, `lootKeys.memberLedger(userId: string)` — stable typed key factories for TanStack Query cache
- [X] T029 [P] [US1] Create `lootApi.ts` in `frontend/src/features/crew-resources/api/lootApi.ts` with `apiFetch` wrappers for all five contract endpoints: `getMyLoot()`, `getDistribution()`, `getMemberLedger(userId)`, `addOrgPoints(userId, req)`, `awardLootPoints(userId, req)`
- [X] T030 [US1] Create `useMyLoot` hook in `frontend/src/features/crew-resources/hooks/useMyLoot.ts` — `useQuery` calling `lootApi.getMyLoot()` with key `lootKeys.myLoot()`; validates response against `memberLedgerResponseSchema`
- [X] T031 [P] [US1] Create `LedgerTable` component in `frontend/src/features/crew-resources/components/LedgerTable.tsx` — renders a shadcn `Table` of `LedgerEntry` rows (amount, reason, postedBy, createdAt) newest-first; renders an empty-state message when entries array is empty
- [X] T032 [P] [US1] Create `ClaimPriorityBadge` component in `frontend/src/features/crew-resources/components/ClaimPriorityBadge.tsx` — formats `claimPriority: number` to exactly 2 decimal places (e.g., `1.50`, `0.00`)
- [X] T033 [US1] Create `MyLootPage` in `frontend/src/features/crew-resources/pages/MyLootPage.tsx` — thin route component using `useMyLoot`; renders `ClaimPriorityBadge` and two `LedgerTable` instances (OrgPoints, LootPoints); loading and error states — makes T021 GREEN
- [X] T034 [US1] Add "Crew Resources" nav group entries to `frontend/src/features/dashboard/navigation/navItems.ts` (`{ label: 'My Loot', path: '/crew-resources/my-loot', group: 'Crew Resources' }` and `{ label: 'Loot Distribution', path: '/crew-resources/loot-distribution', group: 'Crew Resources' }`, no `access` rule per FR-001); add two `ProtectedRoute`-wrapped routes in `frontend/src/routes/AppRouter.tsx` for `/crew-resources/my-loot` and `/crew-resources/loot-distribution`

**Checkpoint**: My Loot is fully functional and independently testable. Any authenticated member can view their own ledger and Claim Priority.

---

## Phase 4: User Story 2 — Member Views the Loot Distribution Table (Priority: P1)

**Goal**: Any authenticated member can navigate to `/crew-resources/loot-distribution` and see all org members sorted by Claim Priority ascending (lowest ratio first). Clicking View on a row opens a shadcn `Sheet` with that member's full OrgPoints and LootPoints history.

**Independent Test**: Sign in as any authenticated member, navigate to Loot Distribution — all members appear with correct totals and display names (character name if registered, Discord display name otherwise); click View on any row — side sheet opens with that member's ledger entries newest-first.

### Tests for User Story 2 — write first, must FAIL before implementation

- [X] T035 [P] [US2] Extend Testcontainers integration tests in `backend/tests/NajaEcho.Infrastructure.Tests/Loot/LootLedgerRepositoryTests.cs` for `GetDistributionAsync`: all members returned including those with zero ledger entries (LEFT JOIN behavior); sorted by `claim_priority ASC`; COALESCE resolves character name over Discord display name for member subject; zero-entry member shows `0.00` Claim Priority — must be RED before T039
- [X] T036 [P] [US2] Extend API contract tests in `backend/tests/NajaEcho.Api.Tests/Features/Loot/LootEndpointsTests.cs` for `GET /api/loot/distribution` (401 unauthenticated → 200 with `LootDistributionResponse.members` array) and `GET /api/loot/{userId}` (401 unauthenticated; 404 unknown userId; 200 with `MemberLedgerResponse`) — must be RED before T041
- [X] T037 [P] [US2] Write Vitest + RTL + MSW frontend tests for `LootDistributionPage` in `frontend/src/features/crew-resources/__tests__/LootDistributionPage.test.tsx`: all members rendered sorted by Claim Priority ascending; clicking View opens `MemberLedgerSheet` with the correct member's data; display names resolve character name vs Discord name — must be RED before T045

### Implementation for User Story 2

- [X] T038 [P] [US2] Create `GetDistributionQuery`, `GetDistributionHandler`, and `DistributionRowDto` in `backend/src/NajaEcho.Application/Features/Loot/GetDistribution/` — handler calls `ILootLedgerRepository.GetDistributionAsync()` and returns the pre-sorted list (view already orders by `claim_priority ASC`)
- [X] T039 [US2] Implement `GetDistributionAsync` in `backend/src/NajaEcho.Infrastructure/Loot/LootLedgerRepository.cs` — `db.LootMemberStandings.OrderBy(x => x.ClaimPriority).ToListAsync()` reading the `loot_member_standing` keyless entity — makes T035 GREEN
- [X] T040 [P] [US2] Add `LootDistributionResponse` and `DistributionRowResponse` contract DTOs to `backend/src/NajaEcho.Api/Features/Loot/Contracts/` (memberId, displayName, orgPointsTotal `int`, lootPointsTotal `int`, claimPriority `double`)
- [X] T041 [US2] Add `GET /distribution` and `GET /{userId:guid}` routes to `backend/src/NajaEcho.Api/Features/Loot/LootEndpoints.cs`; register `GetDistributionHandler` in `backend/src/NajaEcho.Infrastructure/DependencyInjection.cs` — makes T036 GREEN
- [X] T042 [P] [US2] Add `distributionRowSchema` and `lootDistributionResponseSchema` Zod schemas to `frontend/src/features/crew-resources/schemas/lootSchemas.ts`
- [X] T043 [US2] Create `useDistribution` hook in `frontend/src/features/crew-resources/hooks/useDistribution.ts` and `useMemberLedger(userId)` hook in `frontend/src/features/crew-resources/hooks/useMemberLedger.ts` — each a TanStack Query `useQuery` with the corresponding `lootKeys` key factory
- [X] T044 [US2] Create `MemberLedgerSheet` component in `frontend/src/features/crew-resources/components/MemberLedgerSheet.tsx` — shadcn `Sheet`; receives `memberId`; uses `useMemberLedger(memberId)`; renders two `LedgerTable` instances (OrgPoints, LootPoints) and `ClaimPriorityBadge`; includes role-gated action slots (stubs for Add Points and Award Loot — wired in US3/US4)
- [X] T045 [US2] Create `LootDistributionPage` in `frontend/src/features/crew-resources/pages/LootDistributionPage.tsx` — thin route using `useDistribution`; renders a shadcn `Table` of all members sorted by Claim Priority ascending with a View button per row that opens `MemberLedgerSheet` — makes T037 GREEN

**Checkpoint**: Loot Distribution and the member side sheet are fully functional for read-only access. Both P1 stories complete.

---

## Phase 5: User Story 3 — Crew Resource Officer Posts OrgPoints (Priority: P2)

**Goal**: A CRO can open a member's side sheet in Loot Distribution, click Add Points, enter a whole-integer amount (positive or negative) and a required non-empty reason, and submit. The entry appears in the member's OrgPoints ledger immediately and Claim Priority updates without a page reload. The Add Points action is not visible to non-CRO/non-Admin members.

**Independent Test**: Sign in as CRO, open any member's sheet, submit an OrgPoints entry — entry appears in ledger immediately via TanStack Query invalidation and Claim Priority updates. Sign in as a plain member and confirm Add Points action is absent. Directly call `POST /api/loot/{userId}/org-points` as non-CRO and receive 403.

### Tests for User Story 3 — write first, must FAIL before implementation

- [X] T046 [P] [US3] Write `AddOrgPointsHandlerTests` in `backend/tests/NajaEcho.Application.Tests/Features/Loot/AddOrgPointsHandlerTests.cs`: empty/whitespace reason → rejected (validation exception); `MemberNotFoundException` thrown for unknown memberId; negative amount accepted; valid entry has correct `Kind = OrgPoints`, actor, amount, reason, and `CreatedAt` set — must be RED before T050
- [X] T047 [P] [US3] Extend API contract tests in `backend/tests/NajaEcho.Api.Tests/Features/Loot/LootEndpointsTests.cs` for `POST /api/loot/{userId}/org-points`: 401 unauthenticated; 403 authenticated non-CRO (Quartermaster or plain member); 201 returning `LedgerEntry` with all audit fields (amount, reason, postedBy, createdAt); 404 unknown userId; 422 empty reason and 422 non-integer amount — must be RED before T053
- [X] T048 [P] [US3] Write Vitest + RTL + MSW frontend tests for `AddPointsDialog` in `frontend/src/features/crew-resources/__tests__/AddPointsDialog.test.tsx`: visible only to CRO and Admin (hidden for Quartermaster-only and plain-member sessions); empty reason blocked with validation message; non-integer amount blocked; valid submit calls mutation and triggers `lootKeys.memberLedger` and `lootKeys.distribution` invalidations — must be RED before T056

### Implementation for User Story 3

- [X] T049 [P] [US3] Create `MemberNotFoundException` in `backend/src/NajaEcho.Application/Features/Loot/AddOrgPoints/MemberNotFoundException.cs` — domain exception thrown when target member does not exist; mapped to 404 at the endpoint layer
- [X] T050 [US3] Create `AddOrgPointsCommand` and `AddOrgPointsHandler` in `backend/src/NajaEcho.Application/Features/Loot/AddOrgPoints/` — command: `{ MemberId (Guid), Amount (int), Reason (string), ActorId (Guid) }`; handler: validates reason non-empty, throws `MemberNotFoundException` if member not found, calls `ILootLedgerRepository.AddEntryAsync` with `Kind.OrgPoints`, logs structured `outcome=succeeded` line with caller/target/amount (Principle V) — makes T046 GREEN
- [X] T051 [US3] Confirm `AddEntryAsync` insert in `backend/src/NajaEcho.Infrastructure/Loot/LootLedgerRepository.cs` correctly sets `CreatedAt = DateTimeOffset.UtcNow` and persists all fields (implemented as part of T016; extend if needed to handle `MemberNotFoundException` lookup)
- [X] T052 [P] [US3] Add `AddLedgerEntryRequest` contract DTO to `backend/src/NajaEcho.Api/Features/Loot/Contracts/` — `{ Amount: int, Reason: string }` with `[Required]` and `[MaxLength(500)]` on Reason; reflects `contracts/openapi.yaml` `AddLedgerEntryRequest` schema
- [X] T053 [US3] Add `POST /{userId:guid}/org-points` route to `backend/src/NajaEcho.Api/Features/Loot/LootEndpoints.cs` with `.RequireAuthorization(AuthorizationPolicies.CrewResourceOfficer)`; map `MemberNotFoundException` to 404; map validation failure to 422; register `AddOrgPointsHandler` in `backend/src/NajaEcho.Infrastructure/DependencyInjection.cs` — makes T047 GREEN
- [X] T054 [US3] Create `useAddOrgPoints` mutation hook in `frontend/src/features/crew-resources/hooks/useAddOrgPoints.ts` — `useMutation` calling `lootApi.addOrgPoints(userId, req)`; on success invalidates `lootKeys.memberLedger(userId)` and `lootKeys.distribution()` so the sheet and table update live (SC-003)
- [X] T055 [P] [US3] Add `addLedgerEntryRequestSchema` Zod form schema to `frontend/src/features/crew-resources/schemas/lootSchemas.ts` — `z.object({ amount: z.number().int('Must be a whole number'), reason: z.string().min(1, 'Reason is required').max(500) })` (FR-011, FR-016)
- [X] T056 [US3] Create `AddPointsDialog` in `frontend/src/features/crew-resources/components/AddPointsDialog.tsx` — shadcn `Dialog` with RHF + `addLedgerEntryRequestSchema`; integer validation and required reason with accessible error messages; calls `useAddOrgPoints`; no confirmation step per spec; closes and resets form on success — makes T048 GREEN
- [X] T057 [US3] Wire `AddPointsDialog` into `MemberLedgerSheet` in `frontend/src/features/crew-resources/components/MemberLedgerSheet.tsx` — rendered only when `useCurrentUser().roles` includes `'CrewResourceOfficer'` or `'Admin'` (FR-014)

**Checkpoint**: OrgPoints write flow complete. CRO and Admin can post OrgPoints entries with live refresh.

---

## Phase 6: User Story 4 — Quartermaster Awards LootPoints (Priority: P2)

**Goal**: A Quartermaster can open a member's side sheet in Loot Distribution, click Award Loot, enter a whole-integer amount and required reason, and submit. The entry appears in the member's LootPoints ledger immediately and Claim Priority updates without a page reload. The Award Loot action is not visible to non-QM/non-Admin members.

**Independent Test**: Sign in as Quartermaster, open any member's sheet, submit a LootPoints entry — entry appears immediately and Claim Priority updates. Confirm Award Loot action is absent for plain member. Direct `POST /api/loot/{userId}/loot-points` as non-QM → 403.

### Tests for User Story 4 — write first, must FAIL before implementation

- [X] T058 [P] [US4] Write `AddLootPointsHandlerTests` in `backend/tests/NajaEcho.Application.Tests/Features/Loot/AddLootPointsHandlerTests.cs`: empty/whitespace reason rejected; `MemberNotFoundException` for unknown member; negative amount accepted; valid entry has `Kind = LootPoints` — must be RED before T061
- [X] T059 [P] [US4] Extend API contract tests in `backend/tests/NajaEcho.Api.Tests/Features/Loot/LootEndpointsTests.cs` for `POST /api/loot/{userId}/loot-points`: 401 unauthenticated; 403 authenticated non-QM (CRO or plain member); 201 with full audit data; 404 unknown userId; 422 empty reason — must be RED before T062
- [X] T060 [P] [US4] Write Vitest + RTL + MSW frontend tests for `AwardLootDialog` in `frontend/src/features/crew-resources/__tests__/AwardLootDialog.test.tsx`: visible only to Quartermaster and Admin; empty reason blocked; non-integer amount blocked; valid submit calls mutation and triggers cache invalidations — must be RED before T063

### Implementation for User Story 4

- [X] T061 [US4] Create `AddLootPointsCommand` and `AddLootPointsHandler` in `backend/src/NajaEcho.Application/Features/Loot/AddLootPoints/` — identical pattern to `AddOrgPointsHandler` but with `Kind.LootPoints`; logs structured `outcome=` line; register handler in `backend/src/NajaEcho.Infrastructure/DependencyInjection.cs` — makes T058 GREEN
- [X] T062 [US4] Add `POST /{userId:guid}/loot-points` route to `backend/src/NajaEcho.Api/Features/Loot/LootEndpoints.cs` with `.RequireAuthorization(AuthorizationPolicies.Quartermaster)`; same 404/422 mapping as org-points route — makes T059 GREEN
- [X] T063 [US4] Create `AwardLootDialog` in `frontend/src/features/crew-resources/components/AwardLootDialog.tsx` — shadcn `Dialog` with RHF + `addLedgerEntryRequestSchema` (reused from T055); calls `useAwardLootPoints`; no confirmation step; closes and resets on success — makes T060 GREEN
- [X] T064 [US4] Create `useAwardLootPoints` mutation hook in `frontend/src/features/crew-resources/hooks/useAwardLootPoints.ts` — `useMutation` calling `lootApi.awardLootPoints(userId, req)`; on success invalidates `lootKeys.memberLedger(userId)` and `lootKeys.distribution()` (SC-004)
- [X] T065 [US4] Wire `AwardLootDialog` into `MemberLedgerSheet` in `frontend/src/features/crew-resources/components/MemberLedgerSheet.tsx` — rendered only when `useCurrentUser().roles` includes `'Quartermaster'` or `'Admin'` (FR-015)

**Checkpoint**: LootPoints write flow complete. QM and Admin can post LootPoints entries with live refresh. Both P2 stories complete.

---

## Phase 7: User Story 5 — Admin Manages Both Ledgers (Priority: P3)

**Goal**: An Admin sees both Add Points and Award Loot actions in any member's sheet (combined CRO + QM capability). The CrewResourceOfficer role is visible and assignable in the existing 017 role assignment UI so access can be granted in-app.

**Independent Test**: Sign in as Admin, open any member's sheet in Loot Distribution — both Add Points and Award Loot are visible and functional. Assign CrewResourceOfficer to a member via the Users admin page — that member immediately gains OrgPoints posting ability.

### Tests for User Story 5 — write first, must FAIL before implementation

- [X] T066 [P] [US5] Write Vitest + RTL + MSW frontend tests for `MemberLedgerSheet` combined role display in `frontend/src/features/crew-resources/__tests__/MemberLedgerSheet.test.tsx`: Admin session → both Add Points and Award Loot visible; CRO-only session → only Add Points visible; QM-only session → only Award Loot visible; plain-member session → neither action visible — must be RED before T068
- [X] T067 [P] [US5] Write frontend test for Crew Resources nav group visibility in `frontend/src/features/crew-resources/__tests__/navigation.test.tsx`: nav group "Crew Resources" (My Loot, Loot Distribution) is visible for all authenticated member sessions regardless of role (no `access` guard per FR-001) — must be RED before T068

### Implementation for User Story 5

- [X] T068 [US5] Verify `MemberLedgerSheet` role-gate logic from T057 and T065 covers Admin correctly — the `CRO || Admin` check (T057) shows Add Points for Admin and the `QM || Admin` check (T065) shows Award Loot for Admin; no new production code if implemented correctly; run T066/T067 test assertions to confirm and fix any gaps — makes T066, T067 GREEN

**Checkpoint**: All five user stories complete. Admin has full combined ledger access. CRO role is assignable via the existing admin UI. P3 complete.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Observability, immutability verification, full test run, and end-to-end quickstart walkthrough.

- [X] T069 [P] Verify structured logging in `AddOrgPointsHandler` and `AddLootPointsHandler` — each write emits `outcome=succeeded` or `outcome=failed` with caller userId, target memberId, and amount (mirroring `AssignRolesHandler` pattern); no sensitive data in log output (Constitution Principle V)
- [X] T070 [P] Verify auth redirect: unauthenticated navigation to `/crew-resources/my-loot` or `/crew-resources/loot-distribution` is redirected to the login screen by the existing `ProtectedRoute` — confirm via existing frontend auth test coverage or targeted MSW test; no new code expected
- [X] T071 [P] Confirm immutability contract: grep the repository and endpoint files to verify no update or delete method exists for `loot_ledger` — `ILootLedgerRepository` must expose only `AddEntryAsync` + read methods (FR-004, research Decision 8); resolve any gap if found
- [X] T072 [P] Run full test suite: `dotnet test backend/NajaEcho.slnx` (unit + Testcontainers integration + API contract tests) and `cd frontend && npm run test` (Vitest/RTL/MSW) — all tests must be GREEN before merge
- [X] T073 Run quickstart.md end-to-end validation on a local stack with `AddLootLedger` migration applied — execute Scenarios 1–7 in sequence; confirm Claim Priority math table (Scenario 6) and auth boundaries (Scenario 7) pass; document any deviations

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user story phases
- **US1 (Phase 3)** and **US2 (Phase 4)**: Both P1; both depend on Foundational and can begin in parallel after Phase 2
- **US3 (Phase 5)** and **US4 (Phase 6)**: Both P2; depend on Foundational; benefit from US2's `MemberLedgerSheet` stub but can proceed in parallel with each other
- **US5 (Phase 7)**: Depends on US3 + US4 — validates combined Admin access delivered by their role-gate implementations
- **Polish (Phase 8)**: Depends on all user story phases complete

### User Story Dependencies

- **US1 (P1)**: Starts after Foundational — no dependency on other user stories
- **US2 (P1)**: Starts after Foundational — reuses `LedgerTable` and `ClaimPriorityBadge` from US1 if available, but independently implementable
- **US3 (P2)**: Starts after Foundational — wires `AddPointsDialog` into the `MemberLedgerSheet` stub created in US2
- **US4 (P2)**: Starts after Foundational — wires `AwardLootDialog` into `MemberLedgerSheet`; can run in parallel with US3
- **US5 (P3)**: Starts after US3 + US4 — no new backend code; tests combined Admin visibility already delivered

### Within Each User Story (TDD order — mandatory)

1. Write tests first (e.g., T018–T021) — confirm RED
2. Implement production code (e.g., T022–T034) — reach GREEN
3. Verify checkpoint before moving to next story

### Parallel Opportunities

- Domain entity tasks T003–T007 are fully parallelizable (distinct files)
- EF configuration tasks T008–T009 are parallelizable
- All test tasks within a story (e.g., T018–T021, T035–T037) are parallelizable
- Frontend schema, key factory, and API client tasks within a story are parallelizable
- US3 and US4 full implementation can proceed in parallel after Foundational is complete

---

## Parallel Example: User Story 1

```bash
# Write tests first (all parallelizable — different test files):
T018: ClaimPriorityTests          → backend/tests/NajaEcho.Application.Tests/Features/Loot/ClaimPriorityTests.cs
T019: LootLedgerRepositoryTests   → backend/tests/NajaEcho.Infrastructure.Tests/Loot/LootLedgerRepositoryTests.cs
T020: LootEndpointsTests (GET me) → backend/tests/NajaEcho.Api.Tests/Features/Loot/LootEndpointsTests.cs
T021: MyLootPage.test.tsx         → frontend/src/features/crew-resources/__tests__/MyLootPage.test.tsx

# Then implement in parallel (different files, no cross-dependency):
T022: GetMemberLedgerQuery+Handler → backend/src/NajaEcho.Application/Features/Loot/GetMemberLedger/
T024: API contract DTOs            → backend/src/NajaEcho.Api/Features/Loot/Contracts/
T027: Zod schemas                  → frontend/src/features/crew-resources/schemas/lootSchemas.ts
T028: lootKeys.ts                  → frontend/src/features/crew-resources/hooks/lootKeys.ts
T029: lootApi.ts                   → frontend/src/features/crew-resources/api/lootApi.ts
T031: LedgerTable component        → frontend/src/features/crew-resources/components/LedgerTable.tsx
T032: ClaimPriorityBadge component → frontend/src/features/crew-resources/components/ClaimPriorityBadge.tsx
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 (My Loot — read-only, own ledger)
4. Complete Phase 4: User Story 2 (Loot Distribution — read-only, all members + sheet)
5. **STOP and VALIDATE**: Run all P1 tests + quickstart Scenarios 1–2
6. Deploy/demo — read-only Crew Resources is fully useful to the org

### Incremental Delivery

1. Setup + Foundational → infrastructure ready
2. US1 → My Loot → test independently → deploy/demo
3. US2 → Loot Distribution + side sheet → test independently → deploy/demo
4. US3 → CRO OrgPoints writes → test independently → deploy/demo
5. US4 → QM LootPoints writes → test independently → deploy/demo
6. US5 → Admin combined access + role assignment UI → test → deploy/demo
7. Polish → full test run + quickstart validation → PR

### Parallel Team Strategy (two developers)

1. Both complete Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (My Loot) → US3 (CRO writes)
   - Developer B: US2 (Loot Distribution) → US4 (QM writes)
3. Both complete US5 + Polish together

---

## Notes

- `[P]` tasks = different files, no sequential dependency — safe to run concurrently
- `[USn]` label maps each task to a specific user story for traceability
- Tests MUST be written and confirmed FAILING before production code is written (Constitution Principle II — NON-NEGOTIABLE)
- Each story is independently testable without requiring other stories to be complete
- `MemberLedgerSheet` is introduced as a read-only stub in US2; Add Points and Award Loot are wired in US3 and US4 respectively — do not implement the dialogs in US2
- The `loot_member_standing` view and the `ClaimPriority` C# helper must implement the same formula (`org / NULLIF(loot, 0) ?? 100`); the view is the authoritative source; the helper is used only in per-member reads that do not go through the view
- Amounts are signed integers — Zod form schema MUST use `z.number().int()` not `z.number()` (FR-016, research Decision 3)
- Immutability is enforced by omission: `ILootLedgerRepository` has no update or delete methods; corrections are compensating entries (FR-004, research Decision 8)
- `AddLootLedger` migration is additive and non-destructive (one `CreateTable` + one `CREATE VIEW`); no special PR approval required per the constitution's destructive-migration rule
- The new `CrewResourceOfficer` role requires no migration — it is seeded by `RoleSeeder` at startup
