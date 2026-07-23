# Quickstart: Blueprint Detail Panel

## Prerequisites

- Backend running with EF Core migrations applied (no new migration needed for this feature)
- At least one blueprint imported into `sc.blueprints` with at least one `blueprint_tiers` row and `blueprint_slot_options` rows
- At least one authenticated user with a row in `user_blueprints` linking them to that blueprint
- Frontend dev server running

---

## Scenario 1 — Route Rename Redirect

**Goal**: Confirm `/blueprints/mine` redirects to `/blueprints/personal`.

1. Navigate to `http://localhost:5173/blueprints/mine` while authenticated.
2. **Expected**: Browser redirects to `/blueprints/personal` and the page loads normally.

---

## Scenario 2 — Open Blueprint Detail Panel

**Goal**: Clicking a blueprint row slides in the detail panel from the right.

1. Navigate to `/blueprints/personal`.
2. Click any row in the blueprint listing.
3. **Expected**: A panel slides in from the right showing:
   - Blueprint name at the top (or "—" if null)
   - A row with three labelled values: **Type** / **Craft Time** / **Ingredients**
   - An ingredient list below

---

## Scenario 3 — Verify Detail Endpoint Directly

```bash
# Replace <session_cookie> and <blueprintId> with real values
curl -s -b "__Host-session=<session_cookie>" \
  http://localhost:5000/api/blueprints/mine/<blueprintId> | jq .
```

**Expected response shape**:
```json
{
  "blueprintId": "...",
  "productName": "Quantum Drive",
  "type": "Component",
  "craftTimeSeconds": 330,
  "ingredientCount": 5,
  "slots": [
    {
      "slotIndex": 0,
      "slotName": "Cast Iron",
      "options": [
        {
          "optionIndex": 0,
          "materialName": "Injector Nozzles",
          "kind": "material",
          "quantity": 1.72
        }
      ]
    }
  ]
}
```

**Spot check**: confirm `ingredientCount` equals the number of objects in `slots`.

---

## Scenario 4 — Ingredient List Renders Correctly

1. Open the detail panel for a blueprint with known ingredients.
2. **Expected**:
   - Each slot appears as a top-level row showing the `slotName` and `quantity`.
   - Each option within a slot appears indented below it showing `materialName`, `kind`, and `quantity`.
3. If a blueprint has no ingredients, the section shows "No ingredients listed".

---

## Scenario 5 — Craft Time Formatting

1. Open the detail panel for a blueprint with a known `craft_time_seconds` value (e.g., 330 seconds).
2. **Expected**: Craft Time column shows a human-readable string (e.g., "5m 30s").
3. If `craftTimeSeconds` is null, the column shows "—".

---

## Scenario 6 — Remove a Blueprint

1. Open the detail panel for any blueprint.
2. Click the **Remove** button (bottom-right of the panel).
3. **Expected**: An inline confirmation appears within the panel ("Remove this blueprint?", with "Confirm" and "Cancel").
4. Click **Confirm**.
5. **Expected**: Panel closes; the blueprint no longer appears in the listing without a page reload.

**Verify via API**:
```bash
curl -s -o /dev/null -w "%{http_code}" -b "__Host-session=<session_cookie>" \
  -X DELETE http://localhost:5000/api/blueprints/mine/<blueprintId>
# Expected: 204
```

---

## Scenario 7 — Remove Non-Existent Blueprint

```bash
curl -s -o /dev/null -w "%{http_code}" -b "__Host-session=<session_cookie>" \
  -X DELETE http://localhost:5000/api/blueprints/mine/00000000-0000-0000-0000-000000000001
# Expected: 404
```

---

## Scenario 8 — Panel Close Behaviour

1. Open the detail panel.
2. Click the **X** button — panel closes.
3. Open it again, then click outside the panel — panel closes.
4. **Expected**: Both dismiss methods work; the listing remains visible after close.

---

## Scenario 9 — Unauthenticated Access

```bash
curl -s -o /dev/null -w "%{http_code}" \
  http://localhost:5000/api/blueprints/mine/<blueprintId>
# Expected: 401
```

---

## Scenario 10 — Detail for Blueprint Not in User's List

```bash
# Use a valid blueprintId that exists in sc.blueprints but NOT in user_blueprints for this user
curl -s -o /dev/null -w "%{http_code}" -b "__Host-session=<session_cookie>" \
  http://localhost:5000/api/blueprints/mine/<other_blueprintId>
# Expected: 404
```
