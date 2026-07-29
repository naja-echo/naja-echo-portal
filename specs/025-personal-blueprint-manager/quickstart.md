# Quickstart & Validation Guide: Personal Blueprint Manager

## Prerequisites

- Backend running on `https://localhost:5001` (or configured port)
- Frontend running on `http://localhost:5173`
- PostgreSQL running with `najaecho` database (migrations applied)
- Blueprint catalog populated via admin import (at least a few blueprints with `product_name` set)
- Authenticated as a regular member (not required to be admin)

---

## 1. Apply the migration

```bash
cd backend
dotnet ef database update --project src/NajaEcho.Infrastructure --startup-project src/NajaEcho.Api
```

Verify the `user_blueprints` table was created:

```sql
SELECT column_name, data_type FROM information_schema.columns
WHERE table_name = 'user_blueprints'
ORDER BY ordinal_position;
```

Expected columns: `id`, `user_id`, `blueprint_id`, `added_at`.

---

## 2. Verify the search endpoint

```http
GET /api/blueprints/search?q=armor
```

Expected: `200 OK` with a `results` array containing blueprints whose `productName` contains "armor" (case-insensitive), max 20 entries.

```http
GET /api/blueprints/search?q=xxxxxxxxxnotexist
```

Expected: `200 OK` with an empty `results` array.

---

## 3. Add a blueprint

```http
POST /api/blueprints/mine
Content-Type: application/json

{ "blueprintId": "<valid-guid-from-search>" }
```

Expected: `201 Created` with the new `MyBlueprintListItem` including `ingredientCount`.

Attempt to add the same blueprint again:

```http
POST /api/blueprints/mine
Content-Type: application/json

{ "blueprintId": "<same-guid>" }
```

Expected: `409 Conflict`.

---

## 4. List personal blueprints

```http
GET /api/blueprints/mine
```

Expected: `200 OK` with a `blueprints` array. Each item has:
- `blueprintId` — the catalog blueprint GUID
- `productName` — the blueprint's product name (nullable)
- `type` — the blueprint's type (nullable)
- `ingredientCount` — integer ≥ 0

---

## 5. Frontend walkthrough

1. Log in as a member.
2. Click **My Blueprints** in the sidebar — the page loads with an empty state if no blueprints saved.
3. Click **Add Blueprint** — the modal opens with a search input.
4. Type a partial name (e.g., "armor") — matching blueprint names appear as suggestions within 500ms.
5. Click a suggestion — the input populates and the **Add Blueprint** button enables.
6. Click **Add Blueprint** — modal closes, new blueprint appears in the listing with Blueprint, Type, and Ingredients columns populated.
7. Click **Add Blueprint** again and select the same blueprint — an inline message prevents the duplicate.

---

## 6. Ingredient count spot check

Pick a blueprint from the list and note its `blueprintId`. Run:

```sql
SELECT COUNT(DISTINCT bso.slot_index) AS ingredient_count
FROM sc.blueprint_slot_options bso
JOIN sc.blueprint_tiers bt ON bt.id = bso.tier_id
WHERE bt.blueprint_id = '<blueprintId>'
  AND bt.tier_index = 0;
```

The result must match the `ingredientCount` shown in the UI.

---

## 7. Navigation visibility

- As any authenticated user: **My Blueprints** is visible in the sidebar under a "Blueprints" group.
- As an unauthenticated user: navigating to `/blueprints/mine` redirects to the login page.
