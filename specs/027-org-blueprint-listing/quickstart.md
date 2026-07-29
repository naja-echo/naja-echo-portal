# Quickstart: Org Blueprint Listing (Feature 027)

## Prerequisites

- Backend API running with a valid session cookie (Discord OAuth login completed)
- At least two test users in the same organization, each with personal blueprints added
- `blueprintId` of a blueprint owned by one or more test users

## Validation Scenarios

### Scenario 1: Org Blueprint Listing loads

**Test**: `GET /api/blueprints/org`
**Expected**: 200 response with `{ blueprints: [...] }` containing all unique blueprints from all org members. Each item has `blueprintId`, `productName`, `type`, `ingredientCount`.

```
GET /api/blueprints/org
Authorization: (session cookie)

200 OK
{
  "blueprints": [
    { "blueprintId": "...", "productName": "Widget Mk1", "type": "Weapon", "ingredientCount": 3 },
    { "blueprintId": "...", "productName": "Hull Panel", "type": null, "ingredientCount": 0 }
  ]
}
```

**Deduplication check**: If User A and User B both have `Widget Mk1`, it appears once.

### Scenario 2: Blueprint detail with owners

**Test**: `GET /api/blueprints/org/{blueprintId}` for a blueprint held by two org members
**Expected**: 200 response with full detail including `slots` array and `owners` array listing both members.

```
GET /api/blueprints/org/11111111-1111-1111-1111-111111111111
Authorization: (session cookie)

200 OK
{
  "blueprintId": "11111111-1111-1111-1111-111111111111",
  "productName": "Widget Mk1",
  "type": "Weapon",
  "craftTimeSeconds": 330,
  "ingredientCount": 2,
  "slots": [
    {
      "slotIndex": 0,
      "slotName": "Cast Iron",
      "options": [
        { "optionIndex": 0, "materialName": "Injector Nozzles", "kind": "material", "quantity": 1.72 }
      ]
    }
  ],
  "owners": [
    { "userId": "aaaaaaaa-...", "displayName": "Nashtok" },
    { "userId": "bbbbbbbb-...", "displayName": "CaptainJex" }
  ]
}
```

### Scenario 3: Blueprint not in org returns 404

**Test**: `GET /api/blueprints/org/{blueprintId}` for a blueprint no org member has
**Expected**: 404

### Scenario 4: Unauthenticated access rejected

**Test**: `GET /api/blueprints/org` without a session cookie
**Expected**: 401

### Scenario 5: Frontend — Org Blueprints page

1. Navigate to `/blueprints/org`
2. Verify the page loads with the nav item "Org Blueprints" highlighted
3. Verify a table of blueprints is shown (Blueprint, Type, Ingredients columns)
4. Click a blueprint row — verify a slide-in panel opens from the right
5. Verify the panel shows: blueprint name, Type / Craft Time / Ingredients summary row, ingredient list, and an Owners section at the bottom
6. Verify no Remove button is visible
7. Click X or outside the panel — verify it closes

### Scenario 6: Empty org state

1. In a fresh environment with no personal blueprints
2. Navigate to `/blueprints/org`
3. Verify an empty-state message is displayed (e.g., "No blueprints found in your org.")

## API Contract Reference

See [contracts/openapi.yaml](contracts/openapi.yaml) for full schema definitions.

## Data Model Reference

See [data-model.md](data-model.md) for entity descriptions and table relationships.
