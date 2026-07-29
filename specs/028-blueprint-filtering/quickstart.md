# Quickstart Validation Guide: Blueprint Filtering (028)

## Prerequisites

- Docker running with the stack started: `docker compose up -d`
- At least one user with saved blueprints (My Blueprints page has data)
- Blueprints must have a mix of `type` values for category filtering to be meaningful
- Frontend dev server running: `cd frontend && npm run dev`

## Scenario 1: Category filter narrows the My Blueprints list

1. Log in and navigate to **My Blueprints**.
2. Verify the full list loads (no filters active).
3. Observe the Category filter dropdown — it should be populated with distinct `type` values from the list.
4. Select a Category.
5. **Expected**: Only blueprints matching that category are shown. The Subcategory dropdown repopulates with subcategories from that category only.
6. Click the Category combobox again and select the same value to clear it (toggle behaviour).
7. **Expected**: Full list restored; Subcategory options return to the full set.

## Scenario 2: Subcategory filter works independently of Category

1. With no Category selected, open the Subcategory dropdown.
2. **Expected**: All distinct subcategories across the full list are shown.
3. Select a Subcategory without selecting a Category first.
4. **Expected**: List narrows to blueprints with that subcategory, regardless of their category.

## Scenario 3: Combined Category + Subcategory filter

1. Select a Category.
2. Select a Subcategory (now showing only subcategories within the selected category).
3. **Expected**: List shows only blueprints matching both.
4. Clear the Category.
5. **Expected**: List now shows all blueprints matching the Subcategory only; Subcategory options expand to the full set again.

## Scenario 4: Name search

1. Type a partial product name into the name search input.
2. **Expected**: List updates on every keystroke; only blueprints whose product name contains the typed text (case-insensitive) are shown.
3. Clear the input.
4. **Expected**: Full (or category-filtered) list restored immediately.

## Scenario 5: Combined name + category filter

1. Select a Category.
2. Type in the name search input.
3. **Expected**: Only blueprints satisfying both the category and name substring are shown.

## Scenario 6: Empty state

1. Type a name that no blueprint matches (e.g., "zzzzz").
2. **Expected**: An empty state message is shown (no error, no crash).

## Scenario 7: Org Blueprints page — identical behaviour

Repeat Scenarios 1–6 on the **Org Blueprints** page.
**Expected**: Identical filtering behaviour to My Blueprints.

## Scenario 8: Filter persistence through detail panel

1. On My Blueprints, select a Category filter.
2. Click a blueprint row to open the detail panel.
3. Close the detail panel.
4. **Expected**: Category filter is still selected and the list remains filtered.

## API verification (backend)

Confirm `subtype` appears in the list response:

```bash
# My Blueprints
curl -s -b <session-cookie> http://localhost:5000/api/blueprints/mine | jq '.blueprints[0]'
# Should include "subtype": "..." or "subtype": null

# Org Blueprints
curl -s -b <session-cookie> http://localhost:5000/api/blueprints/org | jq '.blueprints[0]'
# Should include "subtype": "..." or "subtype": null
```

## Contract reference

See [contracts/openapi.yaml](contracts/openapi.yaml) for the updated schema definitions.
See [data-model.md](data-model.md) for SQL and type changes.
