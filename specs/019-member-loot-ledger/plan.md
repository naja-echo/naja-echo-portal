# Implementation Plan: Member Loot Ledger

**Branch**: `019-member-loot-ledger` | **Date**: 2026-06-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/019-member-loot-ledger/spec.md`

## Summary

Add two immutable, insert-only member ledgers — **OrgPoints** and **LootPoints** — plus a computed
**Claim Priority** (`SUM(OrgPoints) / SUM(LootPoints)`, using 100 as the denominator when LootPoints
total is 0, never stored). A new **CrewResourceOfficer** role manages OrgPoints; the existing
**Quartermaster** role manages LootPoints; **Admin** can do both. A new **Crew Resources** top-level
navigation group (visible to all authenticated members) delivers two pages: **My Loot** (the
caller's own two ledgers + Claim Priority) and **Loot Distribution** (all-member table sorted by
Claim Priority ascending, with a per-member drill-down sheet that surfaces the role-gated **Add
Points** / **Award Loot** actions).

Three pieces of work:

1. **Backend ledger + computed priority.** New `Loot` domain folder with a single `LootLedgerEntry`
   entity discriminated by a `LootLedgerKind` enum (`OrgPoints` | `LootPoints`) — one polymorphic
   `loot_ledger` table (research Decision 1); one additive migration `AddLootLedger` in the default
   schema that creates the table **and** a per-member rollup **view** `loot_member_standing`
   (member, `SUM` OrgPoints, `SUM` LootPoints, computed Claim Priority — research Decision 7), read
   through a keyless EF entity. An `ILootLedgerRepository` (insert + reads, no update/delete —
   research Decisions 7, 8). Five feature-sliced use cases under `Application/Features/Loot/`.
   Display-name resolution (`COALESCE(character.name, user.display_name)`) lives in the view and in
   the per-member raw-SQL read, reusing the 017 `UserRepository` pattern for both the member subject
   and the entry poster (FR-005, FR-013).

2. **Role + auth.** Add `"CrewResourceOfficer"` to `RoleSeeder.Roles`, `AssignRolesHandler.ValidRoles`,
   and a new `CrewResourceOfficer` authorization policy (`CrewResourceOfficer` OR `Admin`, mirroring
   the `Quartermaster` policy). A new `/api/loot` minimal-API group: reads require auth only; the
   OrgPoints write requires the CRO policy; the LootPoints write requires the Quartermaster policy
   (research Decisions 4–6).

3. **Frontend Crew Resources feature.** New `features/crew-resources/` folder (pages, hooks,
   query-key factory, api client, Zod schemas), two `navItems` entries grouped under
   "Crew Resources" with **no** access rule, and two protected routes inside the dashboard shell.
   The distribution drill-down uses the shadcn `Sheet`; in-sheet write actions are gated by
   `session.user.roles`. Extend `roleDisplayNames`/`availableRoles` so the existing 017
   `AssignRolesDialog` can grant the new role (research Decision 10).

**API contract changes ARE required** — five new `/api/loot/*` endpoints, defined in
[`contracts/openapi.yaml`](./contracts/openapi.yaml) before implementation. The constitution's
"No API contract changes required" UI-only exemption does **not** apply.

## Technical Context

**Language/Version**: C# on .NET (`net10.0` per the solution) backend; TypeScript (strict) frontend.

**Primary Dependencies**: Backend — ASP.NET Core Minimal APIs, ASP.NET Core Identity (`AspNetUsers`
/`AspNetRoles`, Discord OAuth, cookie auth), EF Core + `Npgsql` (snake_case), Serilog. Frontend —
React 19 (Vite), React Router data APIs, Tailwind CSS, shadcn/ui (`Sheet`, `Dialog`, `Table`,
`Checkbox`), Lucide, TanStack Query, React Hook Form + Zod, `apiFetch`. **No new backend or frontend
dependency** (YAGNI) — ledgers reuse Identity/EF, the UI reuses existing primitives and the 017
role-assignment surface.

**Storage**: PostgreSQL 16, **default (public) schema** (with `characters`/Identity, not the UEX
`sc` catalog — research Decision 2). One additive, forward-only, **non-destructive** migration
`AddLootLedger` creating the single `loot_ledger` table (PK, `kind` discriminator, two FKs to
`AspNetUsers`, composite `(member_id, kind, created_at DESC)` index, kind + reason check
constraints) **and** the `loot_member_standing` view (grouped by member; per-kind sums + Claim
Priority) via `migrationBuilder.Sql`. No migration for the new role (seeded at startup).

**Testing**: Backend — xUnit + FluentAssertions.
- Application/Domain unit tests with fakes: Claim Priority math (both-zero → 0.00; loot total 0 →
  Org/100; normal ratio; negative totals — FR-003, SC-005); CRO/Admin gate on OrgPoints, QM/Admin
  gate on LootPoints (FR-009/010/014/015); empty/whitespace reason rejected (FR-011); non-integer
  amount rejected (FR-016); negative amount accepted (US3#5).
- ≥1 Testcontainers (PostgreSQL) integration test: insert `loot_ledger` rows of each kind and read
  back the `loot_member_standing` view (per-kind sums + Claim Priority, including the loot-total-0 →
  /100 case) and the per-member newest-first lists; display-name `COALESCE` resolves character vs
  Discord name (FR-013); member with zero entries still appears in the view.
- API contract tests: all five endpoints; reads require auth (401); `POST …/org-points` forbidden
  for non-CRO (403) and `POST …/loot-points` forbidden for non-QM (403, SC-006); 201 persists with
  full audit data (amount, reason, actor, timestamp — SC-007); 404 for unknown member; 422 on empty
  reason / non-integer amount.

Frontend — Vitest + RTL + MSW: My Loot renders both ledgers + Claim Priority and the empty state
(0.00); Loot Distribution renders all members sorted by Claim Priority ascending and resolves
display names; View opens the sheet with the correct member's entries; Add Points shown only for
CRO/Admin, Award Loot only for QM/Admin; empty-reason and non-integer submissions blocked client
side; nav shows the Crew Resources group for all authenticated members.

**Target Platform**: Linux server (containerized API) + browser SPA.

**Performance Goals**: Member counts are small (org-scale). Distribution is a single LEFT-JOIN
aggregate read returned unpaginated and sorted client-side (FR-006); per-member reads hit the
`(member_id, created_at DESC)` index. All targets are interaction-time (SC-002: under 30s
navigation; SC-003/004: live update without reload via TanStack Query invalidation).

**Constraints**:
- One polymorphic `loot_ledger` table with a `kind` discriminator (research Decision 1); the
  per-kind write-auth gate is enforced at the endpoint/handler layer, not the table.
- Ledgers are **append-only**: no update/delete code path exists; corrections are compensating
  entries (FR-004, research Decision 8).
- Amounts are signed **integers**; spec FR-016 overrides the brainstorm's "decimal" (research
  Decision 3).
- Claim Priority is **computed every read, never stored** (FR-003).
- Crew Resources nav group has **no** role gate (all authenticated members); only the in-sheet
  write actions are role-gated (FR-001, FR-014/015).
- Display name = character name → Discord display name fallback, applied to both the member and the
  entry poster (FR-005, FR-013).

### Verified existing facts (from codebase inspection)

- **Roles/seeding** (`Infrastructure/Identity/RoleSeeder.cs`): seeds `["Admin", "Quartermaster"]`
  idempotently at startup via `RoleExistsAsync`. Add `"CrewResourceOfficer"` here (research
  Decision 4). `Program.cs:229` resolves and runs `RoleSeeder` at startup.
- **Authorization** (`Api/Authorization/AuthorizationPolicies.cs`): `Admin` = `RequireRole(Admin)`;
  `Quartermaster` = `RequireRole(Quartermaster, Admin)`. Add a `CrewResourceOfficer` const + policy
  = `RequireRole(CrewResourceOfficer, Admin)`.
- **Role assignment** (`Application/.../AssignRoles/AssignRolesHandler.cs`): `ValidRoles` hashset
  `{Admin, Quartermaster}` — add `CrewResourceOfficer`. UI: `features/admin/lib/roleDisplayNames.ts`
  (`{Admin, Quartermaster}`) drives `availableRoles` consumed by `AssignRolesDialog` — add the new
  entry; no structural change (spec Assumption, FR-012).
- **Member identity** = ASP.NET Identity `AspNetUsers` (`ApplicationUser`, `DisplayName`). No
  separate "Member"/"Organization" entity (consistent with project memory). `LocalUser(Id,
  DisplayName, DiscordUsername)`; session exposed to SPA via `CurrentUserResponse(Id, DisplayName,
  DiscordUsername, Roles[])` and `useCurrentUser`.
- **Character** (`Domain/Characters/Character.cs`): `Id, OwnerUserId, Name, Handle, CreatedAt`,
  table `characters` FK `owner_user_id → AspNetUsers`. Source of the display name (no new character
  capability — spec Assumption).
- **Display-name join precedent** (`Infrastructure/Identity/UserRepository.cs`): raw SQL
  `db.Database.SqlQuery<Row>` LEFT JOINing `AspNetUsers`, `AspNetUserRoles`, `AspNetRoles`,
  `characters`. The distribution/ledger reads copy this technique with `COALESCE(c.name,
  u.display_name)`.
- **Endpoint convention** (`Api/Features/Warehouse/WarehouseEndpoints.cs`): static
  `Map…Endpoints` extension, `MapGroup("/api/…").RequireAuthorization()`, per-route
  `.RequireAuthorization(AuthorizationPolicies.Quartermaster)` overrides; registered in
  `Program.cs` (`app.MapWarehouseEndpoints()` at the endpoint block, ~line 264). `LootEndpoints`
  follows this exactly.
- **DI** (`Infrastructure/DependencyInjection.cs`): handlers + repositories registered `AddScoped`
  (e.g. `GetUsersHandler`, `AssignRolesHandler`, `IUserRepository`, `RoleSeeder`). Add the five Loot
  handlers + `ILootLedgerRepository` → `LootLedgerRepository`.
- **EF entity precedent** (`Domain/Warehouse/WarehouseInventoryEntry.cs` +
  `Persistence/Configurations/WarehouseInventoryEntryConfiguration.cs`): plain POCO; config maps
  snake_case columns, `HasCheckConstraint`, named indexes, FK with `OnDelete`. The `loot_ledger`
  config mirrors this (plus an enum-to-string `kind` conversion); migration mirrors
  `20260620230020_AddCharacterRegistration` (additive `CreateTable` + FKs/indexes).
- **Frontend feature folders** (`features/{warehouse,admin,hangar,characters}/`): each owns
  `pages?/components/hooks/api/schemas/__tests__`; nav is data-driven from
  `features/dashboard/navigation/navItems.ts` (`label, path, icon, end?, access?, group?`);
  `ProtectedRoute`/`AdminRoute` guard routes in `routes/AppRouter.tsx`. New `features/crew-resources/`
  mirrors this; nav entries use `group: 'Crew Resources'` with no `access`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. API-Contract-First | PASS | Five new `/api/loot/*` endpoints fully specified in `contracts/openapi.yaml` before implementation. Not UI-only — exemption correctly **not** invoked. |
| II. Test-First / TDD | PASS | Failing tests first: Claim Priority math + edge cases, role gates, reason/integer validation, immutability-by-omission, Testcontainers aggregate reads + display-name resolution, API auth/403/404/422, and the frontend page/sheet/nav/role-gating tests. |
| III. Frontend/Backend Separation | PASS | Backend computes totals + Claim Priority; SPA only displays and posts via `apiFetch`/TanStack Query. **Approved deviation — hand-written Zod schemas** reviewed against the contract instead of codegen (established 017/018 pattern); any contract change ships with the matching schema in the same PR. No server-rendered HTML; no DB access from the SPA. |
| IV. Simplicity / YAGNI | PASS | One polymorphic `loot_ledger` table (one entity, config, repository, migration) instead of two duplicated parallel tables — auth gates live at the endpoint layer, so the table stays single (research Decision 1); one new role added by string, no new dependency, no stored priority column. Out-of-scope items (decay, automated event/claim entries, bulk import, edit/delete) explicitly excluded per brainstorm. |
| V. Observability | PASS | Each write logs structured `outcome=` lines with caller/target/amount (mirroring `AssignRoles`/warehouse handlers); every ledger row is self-auditing (actor, reason, timestamp). No sensitive auth data in logs. |
| VI. Modular Monolith + Clean Architecture | PASS | `Loot` entities + keyless `LootMemberStanding` read model in Domain; `ILootLedgerRepository` port + `Features/Loot/*` use cases in Application; `LootLedgerRepository` (EF insert + `loot_member_standing` view read + raw SQL) in Infrastructure; `LootEndpoints` in Api. The view is a persistence-layer read model mapped via a keyless entity — no inward dependency leak. Frontend logic in feature-owned hooks/schemas; thin routes; data-driven nav single source of truth. |

**Migration governance note**: `AddLootLedger` is **additive only** (one `CreateTable` + one
`CREATE VIEW`, no drops or lossy type changes) — it does **not** trigger the constitution's
destructive-migration approval requirement. Forward-only; `Down` drops the view then the table.

**Result**: PASS — no unjustified violations. Complexity Tracking intentionally empty.

## Project Structure

### Documentation (this feature)

```text
specs/019-member-loot-ledger/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 — technical decisions
├── data-model.md        # Phase 1 — ledger entities, computed priority, migration
├── quickstart.md        # Phase 1 — validation scenarios
├── contracts/
│   └── openapi.yaml     # Phase 1 — five /api/loot endpoints
└── tasks.md             # Phase 2 (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

```text
backend/src/
├── NajaEcho.Domain/
│   └── Loot/
│       ├── LootLedgerEntry.cs                               # NEW — Id, MemberId, Kind, Amount(int), Reason, ActorId, CreatedAt
│       ├── LootLedgerKind.cs                                # NEW — enum { OrgPoints, LootPoints }
│       └── LootMemberStanding.cs                            # NEW — keyless read model for the loot_member_standing view
├── NajaEcho.Application/
│   ├── Abstractions/
│   │   └── ILootLedgerRepository.cs                         # NEW — AddEntry(kind), GetDistribution (view), GetMemberLedger (insert + reads only)
│   └── Features/Loot/
│       ├── GetDistribution/{GetDistributionQuery,Handler}.cs + DistributionRowDto.cs   # NEW — reads view, priority asc
│       ├── GetMemberLedger/{GetMemberLedgerQuery,Handler}.cs + MemberLedgerDto, LedgerEntryDto.cs  # NEW — serves /me and /{userId}
│       ├── AddOrgPoints/{AddOrgPointsCommand,Handler}.cs + MemberNotFoundException.cs  # NEW
│       ├── AddLootPoints/{AddLootPointsCommand,Handler}.cs # NEW
│       └── ClaimPriority.cs                                 # NEW — pure calc helper (Org / (Loot==0 ? 100 : Loot)); mirrors the view formula for /me
├── NajaEcho.Infrastructure/
│   ├── Loot/
│   │   └── LootLedgerRepository.cs                          # NEW — EF insert; read loot_member_standing view + per-member raw-SQL read (COALESCE display name)
│   ├── Identity/RoleSeeder.cs                               # + "CrewResourceOfficer" (edited)
│   ├── Persistence/
│   │   ├── AppDbContext.cs                                  # + DbSet<LootLedgerEntry>, keyless DbSet<LootMemberStanding> (edited)
│   │   ├── Configurations/
│   │   │   ├── LootLedgerEntryConfiguration.cs              # NEW — loot_ledger map, kind enum→string, FKs, composite index, kind+reason checks
│   │   │   └── LootMemberStandingConfiguration.cs           # NEW — HasNoKey().ToView("loot_member_standing")
│   │   └── Migrations/*_AddLootLedger.cs                    # NEW — additive: create loot_ledger + CREATE VIEW loot_member_standing (DROP VIEW in Down)
│   └── DependencyInjection.cs                               # + ILootLedgerRepository + 4 Loot handlers (edited)
└── NajaEcho.Api/
    ├── Authorization/AuthorizationPolicies.cs               # + CrewResourceOfficer policy (edited)
    ├── Features/Loot/
    │   ├── LootEndpoints.cs                                 # NEW — /api/loot group; reads auth-only; writes CRO/QM-gated
    │   └── Contracts/                                       # NEW — LootDistributionResponse, MemberLedgerResponse, LedgerEntryResponse, AddLedgerEntryRequest
    └── Features/Admin/Users/AssignRoles handler            # (Application) ValidRoles += CrewResourceOfficer (edited)

backend/tests/
├── NajaEcho.Application.Tests/Features/Loot/                # ClaimPriority math; role gates; reason/integer validation
├── NajaEcho.Infrastructure.Tests/                          # Testcontainers: ledger insert + distribution/per-member reads + display-name COALESCE
└── NajaEcho.Api.Tests/Features/Loot/                       # 5 endpoints: auth/403/404/422/201 audit persistence

frontend/src/
├── features/crew-resources/
│   ├── pages/
│   │   ├── MyLootPage.tsx                                   # NEW — thin route: two ledger tables + Claim Priority
│   │   └── LootDistributionPage.tsx                         # NEW — thin route: all-member table + drill-down sheet
│   ├── components/
│   │   ├── LedgerTable.tsx                                  # NEW — shared OrgPoints/LootPoints table (newest first)
│   │   ├── ClaimPriorityBadge.tsx                           # NEW — 2-dp formatting
│   │   ├── MemberLedgerSheet.tsx                            # NEW — Sheet w/ both ledgers + role-gated actions
│   │   ├── AddPointsDialog.tsx                              # NEW — CRO/Admin only (RHF + Zod, no confirm step)
│   │   └── AwardLootDialog.tsx                              # NEW — QM/Admin only
│   ├── hooks/{lootKeys.ts,useDistribution.ts,useMyLoot.ts,useMemberLedger.ts,useAddOrgPoints.ts,useAwardLootPoints.ts}.ts  # NEW
│   ├── api/lootApi.ts                                       # NEW — apiFetch wrappers for the 5 endpoints
│   ├── schemas/lootSchemas.ts                               # NEW — Zod (responses + AddLedgerEntry form, int + non-empty reason)
│   └── __tests__/                                           # NEW — pages, sheet, role gating, validation, nav
├── features/dashboard/navigation/navItems.ts               # + 'Crew Resources' group (My Loot, Loot Distribution), no access (edited)
├── features/admin/lib/roleDisplayNames.ts                  # + CrewResourceOfficer display name (edited)
└── routes/AppRouter.tsx                                     # + two protected crew-resources routes (edited)
```

**Structure Decision**: Backend follows the established four-project Clean Architecture split — new
`Loot` domain folder (one `LootLedgerEntry` entity + `LootLedgerKind` enum, single `loot_ledger`
table, plus a keyless `LootMemberStanding` read model over the `loot_member_standing` view), an
`ILootLedgerRepository` port with five `Features/Loot/*` use cases, an Infrastructure repository (EF
insert + view read + per-member raw-SQL read), and a `LootEndpoints` minimal-API group registered
in `Program.cs`. The two write use cases (`AddOrgPoints`, `AddLootPoints`) stay separate for their
distinct auth gates but both insert one `loot_ledger` row with the matching `kind`. The new role threads through `RoleSeeder`, `AuthorizationPolicies`,
`AssignRolesHandler.ValidRoles`, and the frontend `roleDisplayNames` so the existing 017 assignment
UI grants it unchanged. Frontend gets a self-contained `features/crew-resources/` folder with thin
routes, two new data-driven nav entries, and the drill-down built on the shadcn `Sheet`.

## Complexity Tracking

> No unjustified constitution violations — table intentionally empty. The single polymorphic
> `loot_ledger` table is the YAGNI-aligned choice (one entity/config/repository/migration; per-kind
> write-auth gates live at the endpoint layer — research Decision 1), not a complexity exception. The
> `loot_member_standing` view centralizes the Claim-Priority rollup in one authoritative place
> (research Decision 7) rather than adding code abstraction. The `AddLootLedger` migration (table +
> view) is additive/non-destructive and needs no special approval.
