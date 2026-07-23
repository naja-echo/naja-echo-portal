# Feature Specification: Blueprint Detail Panel

**Feature Branch**: `026-blueprint-detail-panel`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: "Create a blueprint detail window for the personal view. Change the frontend route to be blueprints/personal instead of blueprints/mine. Each listing on blueprints/personal should be clickable. When a user clicks on a listing, a sliding window should come in from right. At the top should be the blueprint name. The next row should have the Type, Craft Time, and Ingredient Count. The data should be under each heading. Example: Category and under it is quantomdrive. Under that row should be the ingredient listing. I believe these can be found in sc.blueprints.tiers with slots. Ex: Case Iron 4.28 scu and under that Injector Nozzles   Iron  1.72 scu. On the next row justified right should be a Remove button that removes the blueprint from the user's personal blueprints. If the user clicks the an x (part of the shad ui component I think) or clicks off the window, the sliding window closes."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Route Rename and Clickable Blueprint Rows (Priority: P1)

An authenticated user navigates to `/blueprints/personal` (renamed from `/blueprints/mine`) and sees their blueprint listing. Every row in the listing is now a clickable entry point. Clicking any row opens a detail panel that slides in from the right side of the screen, displaying the full blueprint details.

**Why this priority**: The route rename and the click-to-open interaction are the foundation everything else builds on. Without these, no detail panel exists.

**Independent Test**: Navigate to `/blueprints/personal` — page loads. Click any blueprint row — a detail panel slides in from the right showing at minimum the blueprint name at the top.

**Acceptance Scenarios**:

1. **Given** a user is on `/blueprints/mine`, **When** they navigate to that URL, **Then** they are redirected to `/blueprints/personal` (or the old URL no longer works and the new one does).
2. **Given** a user is on `/blueprints/personal` with at least one blueprint in their list, **When** they click a blueprint row, **Then** a panel slides in from the right.
3. **Given** the detail panel is open, **When** the user clicks the close button (X) or clicks outside the panel, **Then** the panel closes and the listing is visible again.

---

### User Story 2 - Blueprint Detail Summary (Priority: P1)

When the detail panel opens, the user sees a structured summary of the blueprint at the top: the blueprint name as a heading, followed by three labelled data points displayed side-by-side — Type, Craft Time, and Ingredient Count.

**Why this priority**: This is the primary informational value of the panel. Without this row, the detail view is empty of meaning.

**Independent Test**: Open the detail panel for a known blueprint — verify the three labelled values (Type, Craft Time, Ingredient Count) are present and match the expected data.

**Acceptance Scenarios**:

1. **Given** the detail panel is open, **When** it renders, **Then** the blueprint name appears prominently at the top.
2. **Given** the detail panel is open, **When** it renders, **Then** a row shows three labelled columns: "Type" with the blueprint type value below it, "Craft Time" with the craft time value below it, and "Ingredients" with the ingredient count below it.
3. **Given** a blueprint has a null type or null craft time, **When** the panel renders, **Then** a dash (—) is shown in place of the missing value.

---

### User Story 3 - Ingredient Listing (Priority: P2)

Below the summary row, the panel displays the full ingredient breakdown for the blueprint. Ingredients are organized hierarchically: a top-level slot (e.g., "Cast Iron — 4.28 scu") with its sub-components indented beneath it (e.g., "Injector Nozzles — Iron — 1.72 scu"). The list covers the base crafting tier.

**Why this priority**: This is the primary reason a user would open the detail panel — to see what materials are needed. It is high value but not blocking; the panel can render with "no ingredients" while this is implemented.

**Independent Test**: Open the detail panel for a blueprint with known ingredients — verify the ingredient rows appear in a two-level hierarchy matching the database records.

**Acceptance Scenarios**:

1. **Given** the detail panel is open for a blueprint that has ingredients, **When** it renders, **Then** the ingredient section displays top-level slots each showing the material name and quantity.
2. **Given** a top-level slot has sub-components, **When** rendered, **Then** the sub-components appear indented beneath the parent slot, each showing the sub-material name, material type, and quantity.
3. **Given** a blueprint has no ingredients recorded, **When** the panel renders, **Then** the ingredient section shows a "No ingredients listed" message.

---

### User Story 4 - Remove Blueprint from Personal List (Priority: P2)

At the bottom of the detail panel, aligned to the right, is a "Remove" button. Clicking it removes the blueprint from the user's personal list and closes the panel. The listing page updates to reflect the removal without a full page reload.

**Why this priority**: This is the primary destructive action for the feature. Users need to be able to curate their list.

**Independent Test**: Open a blueprint's detail panel, click Remove, confirm — the panel closes and the blueprint no longer appears in the listing.

**Acceptance Scenarios**:

1. **Given** the detail panel is open, **When** the user clicks the "Remove" button, **Then** a confirmation prompt appears asking them to confirm the removal.
2. **Given** the confirmation prompt is shown, **When** the user confirms, **Then** the blueprint is removed from their personal list, the panel closes, and the listing updates without a page reload.
3. **Given** the confirmation prompt is shown, **When** the user cancels, **Then** nothing is removed and the panel remains open.
4. **Given** the remove request fails due to a server error, **When** the error is returned, **Then** an inline error message is shown within the panel and the blueprint remains in the listing.

---

### Edge Cases

- What happens if the blueprint no longer exists in the catalog when the panel tries to load its details? Show an error state within the panel.
- What happens if two panel opens are triggered rapidly (e.g., double-click)? Only one panel should be open at a time; the second click is a no-op or replaces the first.
- What if a user removes a blueprint while it is the last one in their list? The listing transitions to the empty state after the panel closes.
- What if the detail data load fails (network error)? The panel shows an error state with a retry option.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The frontend route for the personal blueprint listing MUST change from `/blueprints/mine` to `/blueprints/personal`; the old route MUST redirect to the new one.
- **FR-002**: Each blueprint row in the listing MUST be interactive — clicking it MUST open the detail panel for that blueprint.
- **FR-003**: The detail panel MUST slide in from the right side of the screen and overlay the listing without full navigation.
- **FR-004**: The detail panel MUST be dismissible by clicking the close (X) button or clicking outside the panel area.
- **FR-005**: The detail panel MUST display the blueprint name as a prominent heading at the top.
- **FR-006**: The detail panel MUST display three labelled data points in a horizontal row: Type, Craft Time, and Ingredient Count.
- **FR-007**: Null values for Type or Craft Time MUST display as a dash (—).
- **FR-008**: The detail panel MUST display the ingredient listing organized by top-level slots (material name, quantity) with sub-components indented beneath each slot (sub-material name, type, quantity), sourced from the base crafting tier.
- **FR-009**: When a blueprint has no ingredients, the ingredient section MUST display a "No ingredients listed" placeholder message.
- **FR-010**: The detail panel MUST include a "Remove" button aligned to the right edge of the panel footer.
- **FR-011**: Clicking "Remove" MUST present a confirmation prompt before executing the removal.
- **FR-012**: On confirmed removal, the blueprint MUST be deleted from the user's personal list, the panel MUST close, and the listing MUST update without a full page reload.
- **FR-013**: If the server returns an error during removal, an inline error message MUST be displayed within the panel; the blueprint MUST remain in the listing.
- **FR-014**: Only one detail panel MUST be open at a time.
- **FR-015**: A new backend endpoint MUST return the full blueprint detail (summary fields + hierarchical ingredient list) for a given blueprint ID.
- **FR-016**: A new backend endpoint MUST support removing a blueprint from a user's personal list (already partially exists; confirm deletion semantics).

### Key Entities

- **Blueprint Detail**: A view combining blueprint metadata (name, type, craft time) and its hierarchical ingredient list for the base crafting tier.
- **Blueprint Slot (top-level)**: A crafting ingredient slot with a material name and a quantity (e.g., "Cast Iron, 4.28 scu").
- **Blueprint Sub-component**: A nested item within a slot with a sub-material name, material type, and quantity (e.g., "Injector Nozzles, Iron, 1.72 scu").
- **UserBlueprint**: The association between an authenticated user and a catalog blueprint; the target of the Remove action.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can open a blueprint detail panel and view all summary fields in under 1 second on a standard connection.
- **SC-002**: 100% of blueprint rows in the listing are clickable and open the correct detail panel.
- **SC-003**: The Remove flow (click → confirm → removed from list) completes in under 3 user interactions.
- **SC-004**: The ingredient list accurately reflects the base-tier crafting data from the game catalog for all blueprints in the user's list.
- **SC-005**: Navigating to `/blueprints/mine` redirects correctly to `/blueprints/personal` with no broken-link experience.

## Assumptions

- Craft time is stored in the `sc.blueprints` table (or a related table accessible via the existing blueprint data model) and can be returned alongside type and name without schema changes.
- The ingredient hierarchy is two levels deep: top-level slots and their direct sub-components. Deeper nesting, if it exists in the data, is out of scope for this feature.
- "Base crafting tier" corresponds to `tier_index = 0` in `sc.blueprint_tiers`, consistent with how ingredient count is already calculated in the current listing.
- Quantity units (e.g., "scu") are stored alongside the quantity value and are returned as part of the ingredient record.
- The Remove action deletes the `user_blueprints` row; there is no soft-delete or undo mechanism in this version.
- The confirmation prompt for removal uses the application's standard inline confirmation pattern (not a separate modal dialog), keeping the interaction within the slide-over panel.
- The new detail endpoint is authenticated and scoped to the requesting user; a user cannot load or remove another user's blueprint associations.
- Unauthenticated access to `/blueprints/personal` redirects to the login page, consistent with the existing protected route pattern.
