# Feature Specification: Personal Blueprint Manager

**Feature Branch**: `025-personal-blueprint-manager`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: "Create a personal blueprint manager. When the user clicked 'My Blueprints' in the menu, they are taken to blueprints page. There should be a button to Add Blueprint which would display a modal. The modal should be similar to the Add Ship Component modal, where the user can input the name of their blueprint. The input field should have an autosuggest feature similar to Add Ship Component. The names are pulled from the db, sc.blueprints.product_name. Once they add the name, they can click the Add Blueprint button which will add the blueprint and close the modal. The page should display all of their personal blueprints in a listing with three columns. The listing headers should be 'Blueprint' (using the db table sc.blueprints.product_name), 'Type' (sc.blueprints.type) and 'Ingredients'. For Ingredients, it should display the count of how many ingredients are used to make the item. I believe these can be a count of slots found in sc.blueprints.tiers."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View My Blueprints Page (Priority: P1)

An authenticated user navigates to "My Blueprints" from the sidebar navigation and sees a listing of all blueprints they have personally added, displayed in a three-column table.

**Why this priority**: This is the core page that delivers value — without it, no other stories are meaningful.

**Independent Test**: Can be fully tested by navigating to "My Blueprints" with at least one blueprint already saved, and verifying the listing renders correctly with Blueprint, Type, and Ingredients columns.

**Acceptance Scenarios**:

1. **Given** a logged-in user has saved blueprints, **When** they click "My Blueprints" in the navigation, **Then** they are taken to the My Blueprints page displaying their blueprints in a three-column table with headers "Blueprint", "Type", and "Ingredients".
2. **Given** a logged-in user has no saved blueprints, **When** they navigate to "My Blueprints", **Then** an empty state is shown indicating no blueprints have been added yet.
3. **Given** a blueprint row, **When** viewed, **Then** the Blueprint column shows the blueprint's product name, the Type column shows the blueprint's type, and the Ingredients column shows a numeric count of how many ingredient slots the blueprint requires.

---

### User Story 2 - Add a Blueprint (Priority: P1)

An authenticated user clicks "Add Blueprint" on the My Blueprints page, types a blueprint name into an autosuggest input, selects a result, and confirms to add it to their personal list.

**Why this priority**: Adding blueprints is required to populate the list, making this an essential flow alongside the listing.

**Independent Test**: Can be fully tested by opening the Add Blueprint modal, searching for a known blueprint name, selecting it, submitting, and verifying the new entry appears in the listing.

**Acceptance Scenarios**:

1. **Given** the My Blueprints page is open, **When** the user clicks "Add Blueprint", **Then** a modal opens containing a blueprint name search input and a disabled "Add Blueprint" submit button.
2. **Given** the Add Blueprint modal is open, **When** the user types at least one character into the search field, **Then** a dropdown of matching blueprint names from the catalog appears as suggestions.
3. **Given** the user has typed a partial name, **When** they select a suggestion from the dropdown, **Then** the input is populated with that blueprint's name and the "Add Blueprint" button becomes enabled.
4. **Given** a valid blueprint is selected, **When** the user clicks "Add Blueprint", **Then** the blueprint is saved to their personal list, the modal closes, and the new blueprint appears in the listing.
5. **Given** the modal is open, **When** the user closes or cancels without selecting, **Then** no blueprint is added and the modal closes.

---

### User Story 3 - Prevent Duplicate Blueprints (Priority: P2)

The system prevents a user from adding the same blueprint to their personal list more than once.

**Why this priority**: Duplicates degrade list quality and cause confusion but do not block core usage.

**Independent Test**: Can be tested by adding a blueprint, then attempting to add the same blueprint again and verifying the system prevents it.

**Acceptance Scenarios**:

1. **Given** a user already has a specific blueprint in their list, **When** they attempt to add the same blueprint again, **Then** the system displays an appropriate message and does not add a duplicate.

---

### Edge Cases

- What happens when no blueprints match the user's search input? The dropdown shows an empty/no-results state.
- What happens if the blueprint catalog is empty (no blueprints imported)? The autosuggest returns no results and the user cannot add a blueprint.
- What if a blueprint has no type set? The Type column displays a blank or dash for that row.
- What if a blueprint has no ingredient slots defined? The Ingredients column displays 0.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The navigation MUST include a "My Blueprints" item that routes authenticated users to the My Blueprints page.
- **FR-002**: The My Blueprints page MUST display the authenticated user's saved blueprints in a table with three columns: Blueprint, Type, and Ingredients.
- **FR-003**: The Blueprint column MUST display the blueprint's product name from the catalog.
- **FR-004**: The Type column MUST display the blueprint's type from the catalog.
- **FR-005**: The Ingredients column MUST display a numeric count of ingredient slots required to craft the item (derived from the blueprint's tier slot data).
- **FR-006**: The My Blueprints page MUST include an "Add Blueprint" button that opens a modal.
- **FR-007**: The Add Blueprint modal MUST contain a text input with autosuggest functionality that queries the blueprint catalog by product name.
- **FR-008**: The autosuggest MUST show matching blueprint names as the user types and allow the user to select one.
- **FR-009**: The "Add Blueprint" submit button within the modal MUST remain disabled until a valid blueprint has been selected from the autosuggest.
- **FR-010**: Upon submission, the selected blueprint MUST be associated with the authenticated user's personal list and persisted.
- **FR-011**: After a successful add, the modal MUST close and the newly added blueprint MUST appear in the listing without requiring a full page reload.
- **FR-012**: The system MUST prevent a user from adding the same blueprint to their personal list more than once.
- **FR-013**: When a user has no saved blueprints, the My Blueprints page MUST display an empty state message.

### Key Entities

- **User Blueprint**: A saved association between an authenticated user and a catalog blueprint. Tracks which blueprints belong to a specific user.
- **Blueprint Catalog Entry**: A read-only record from the shared blueprint catalog containing product name, type, and tier/slot data. Not owned or modified by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can navigate to "My Blueprints" and see their blueprint list in under 2 seconds under normal load.
- **SC-002**: Blueprint name autosuggest returns matching results within 500 milliseconds of the user pausing typing.
- **SC-003**: Users can complete the full add-a-blueprint flow (open modal → search → select → submit) in under 60 seconds on first use.
- **SC-004**: Ingredient counts are accurately reflected for all blueprints that have slot data in the catalog.
- **SC-005**: Duplicate blueprints are rejected 100% of the time — no user's list ever contains the same blueprint twice.

## Assumptions

- The blueprint catalog (`sc.blueprints`) is already populated via the admin import flow; this feature is read-only with respect to catalog data.
- Ingredient count is the total number of distinct ingredient slots across the blueprint's tiers (using the first tier as the representative count where tiers differ is an acceptable simplification if needed — to be confirmed during planning).
- Only authenticated users can access the My Blueprints page; unauthenticated users are redirected to the login flow.
- The feature is scoped to personal list management only — no sharing, visibility controls, or collaborative lists in this iteration.
- Desktop layout is the primary target; responsive/mobile adaptation follows the project's existing dashboard shell behavior.
- The autosuggest searches only `product_name`; searching by type, manufacturer, or other fields is out of scope.
