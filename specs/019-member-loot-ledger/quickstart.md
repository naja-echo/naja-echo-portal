# Quickstart & Validation: Member Loot Ledger

Validation guide for feature 019. Proves the two ledgers, computed Claim Priority, role gating, and
the Crew Resources UI work end-to-end. See [data-model.md](./data-model.md),
[contracts/openapi.yaml](./contracts/openapi.yaml), and [research.md](./research.md) for details.

## Prerequisites

- Backend running (`backend/`), PostgreSQL up (`docker-compose up -d db`), `AddLootLedger`
  migration applied (`./migrate.sh` or `dotnet ef database update`).
- Frontend running (`cd frontend && npm run dev`).
- At least three Discord-authenticated members. Assign roles via the existing **Users** admin page
  (`/dashboard/admin/users` → Assign Roles): one **CrewResourceOfficer**, one **Quartermaster**,
  one **Admin**. One member with a registered character, one without.

## Build & test

```bash
# Backend
cd backend && dotnet build NajaEcho.slnx
dotnet test                         # unit + Testcontainers integration + API contract tests

# Frontend
cd frontend && npm run test         # Vitest + RTL + MSW
npm run build
```

## Scenario 1 — My Loot (US1, FR-002/003/005)

1. Sign in as any member, open **Crew Resources → My Loot** (`/crew-resources/my-loot`).
2. **Expect**: two tables (OrgPoints history, LootPoints history) and a Claim Priority value.
3. New member with no entries → both tables empty, Claim Priority **0.00**.
4. Each entry row shows amount, reason, **posted-by name**, and date; newest entry on top.
5. No control to edit/delete any row.

## Scenario 2 — Loot Distribution table (US2, FR-006/013)

1. Open **Crew Resources → Loot Distribution** (`/crew-resources/loot-distribution`).
2. **Expect**: a row for **every** member — display name, Claim Priority, OrgPoints total,
   LootPoints total. Default order: Claim Priority **ascending**.
3. Member with a registered character shows the **character name**; one without shows the **Discord
   display name**.
4. Click **View** on a row → side sheet opens with that member's full OrgPoints and LootPoints
   ledgers (newest first).

## Scenario 3 — CRO posts OrgPoints (US3, FR-009/011/016)

1. Sign in as the **CrewResourceOfficer**, open a member's sheet in Loot Distribution.
2. **Add Points** is visible → enter amount `50`, leave reason empty → submit **rejected** with a
   validation message.
3. Enter a non-integer amount (`5.5`) → **rejected** (FR-016).
4. Enter `50` + reason "March op participation" → submit. Entry appears immediately in that member's
   OrgPoints ledger; Claim Priority updates **without a page reload** (SC-003).
5. Post `-10` with a reason → appears as a negative entry; total drops by 10 (US3#5).
6. As a non-CRO/non-Admin member, open the same sheet → **Add Points is not shown** (FR-014); the
   `POST …/org-points` endpoint returns **403** if called directly (SC-006).

## Scenario 4 — Quartermaster awards LootPoints (US4, FR-010/011)

1. Sign in as the **Quartermaster**, open a member's sheet.
2. **Award Loot** visible → empty reason rejected; valid amount + reason saves and the LootPoints
   ledger + Claim Priority update live (SC-004).
3. First LootPoints entry for a member who had 0 → Claim Priority moves from `Org/100` to
   `Org/newTotal` (US4#5).
4. Non-QM/non-Admin: **Award Loot not shown** (FR-015); direct `POST …/loot-points` → **403**.

## Scenario 5 — Admin does both (US5, FR-014/015)

1. Sign in as **Admin**, open any member's sheet → **both** Add Points and Award Loot are visible
   and functional.
2. Admin grants **CrewResourceOfficer** to a member via the Users page → that member can
   immediately post OrgPoints (US5#2, FR-012).

## Scenario 6 — Claim Priority math (FR-003, SC-005)

| OrgPoints total | LootPoints total | Expected Claim Priority |
|-----------------|------------------|-------------------------|
| 0               | 0                | 0.00                    |
| 150             | 0                | 1.50  (150 / 100)       |
| 150             | 300              | 0.50                    |
| -20             | 100              | -0.20                   |

Confirmed on both My Loot and Loot Distribution, displayed to exactly 2 decimals.

## Scenario 7 — Auth boundaries (SC-006)

- Unauthenticated `GET /api/loot/me`, `/distribution`, `/{userId}` → **401**; the SPA redirects to
  login.
- Immutability (FR-004): no API path updates or deletes a ledger row; corrections are new entries.
