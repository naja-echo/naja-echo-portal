# Feature Specification: Blueprint Filtering

**Feature Branch**: `028-blueprint-filtering`

**Created**: 2026-07-29

**Status**: Draft

**Input**: User description: "Create a feature specification for Blueprint-Filtering. This feature adds filtering to Org Blueprints and My Blueprints. The user should be able to choose a Category and Subcategory as well as search by name."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Filter Blueprints by Category (Priority: P1)

A user on either the My Blueprints or Org Blueprints page wants to narrow down the list to a specific type of craftable item. They select a Category (e.g., "Ship") from a dropdown and the list updates to show only blueprints of that category. If they want to narrow further, they select a Subcategory (e.g., "Component") and the list narrows again.

**Why this priority**: Category and Subcategory are the primary axes for organizing blueprints in the game. Users need this to find relevant blueprints quickly, especially as blueprint counts grow. This is the core filtering capability.

**Independent Test**: Can be fully tested by navigating to either blueprint listing page, selecting a Category value, and confirming that only blueprints matching that category are shown. Delivers the primary filtering value independently of name search.

**Acceptance Scenarios**:

1. **Given** the My Blueprints page is loaded with at least two blueprints of different categories, **When** the user selects a Category from the filter dropdown, **Then** only blueprints whose category matches the selection are displayed in the list.
2. **Given** a Category is already selected, **When** the user selects a Subcategory from the subordinate dropdown, **Then** the list is further narrowed to only blueprints that match both the Category and the Subcategory.
3. **Given** a Category is selected, **When** the user clears the Category selection, **Then** the Subcategory dropdown reverts to showing all available subcategories and the list shows all blueprints (unless a name search is also active).
4. **Given** no Category is selected, **When** the Subcategory dropdown is displayed, **Then** it is populated with all distinct subcategories across the entire blueprint list.
5. **Given** a Category is selected, **When** the Subcategory dropdown is displayed, **Then** only subcategories that exist within the selected Category are shown as options.
6. **Given** either blueprint page is loaded, **When** no filters are applied, **Then** all blueprints are displayed (existing behaviour is preserved).

---

### User Story 2 - Search Blueprints by Name (Priority: P2)

A user knows the name (or partial name) of the blueprint they are looking for. They type into a search input and the list immediately narrows to blueprints whose product name contains the typed text. The list updates on every keystroke with no delay.

**Why this priority**: Name search provides fast, direct access to a known blueprint without browsing by category. It is highly useful but less foundational than category-based navigation.

**Independent Test**: Can be fully tested by typing a partial product name into the search box and confirming the list filters accordingly, with no category selection needed.

**Acceptance Scenarios**:

1. **Given** the My Blueprints or Org Blueprints page is loaded, **When** the user types text into the name search input, **Then** only blueprints whose product name contains the typed text (case-insensitive) are displayed.
2. **Given** a name search is active, **When** the user clears the search input, **Then** the full (unfiltered) list is restored.
3. **Given** a name search is active alongside a Category selection, **When** both are applied simultaneously, **Then** only blueprints matching both the name fragment and the category are shown.

---

### User Story 3 - Filter Persistence Within Session (Priority: P3)

A user sets a Category filter and navigates away from the page (e.g., to a blueprint detail panel and back). When they return to the listing, their filter selections are still in place.

**Why this priority**: Preserves user context during normal workflow (select filter → open detail → close detail → continue browsing), reducing friction. Lower priority because basic filtering already provides value without this.

**Independent Test**: Can be tested by selecting a filter, opening a blueprint's detail panel, closing it, and confirming the filter is still applied on the listing page.

**Acceptance Scenarios**:

1. **Given** the user has selected a Category filter, **When** they open and then close a blueprint detail panel on the same page, **Then** the Category filter remains selected and the list remains filtered.

---

### Edge Cases

- What happens when the selected Category has no results after applying a name search? The list shows an empty state message rather than an error.
- What happens if a blueprint has no Category (null Type)? It does not match any Category selection; it appears only when no category filter is active.
- What happens if a blueprint has no Subcategory but a Category is selected? It appears when the Category filter matches and no Subcategory filter is applied; it does not appear when a Subcategory filter is active.
- What happens if a user selects a Subcategory with no Category selected? The list shows all blueprints matching that subcategory, regardless of their category.
- What happens when the user types a name that matches no blueprints? An empty state message is shown (no error).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Both the My Blueprints page and the Org Blueprints page MUST expose a Category filter control that allows the user to select from the distinct set of categories present in the displayed blueprint list.
- **FR-002**: The Subcategory filter control MUST always be visible and active. When no Category is selected, it MUST be populated with all distinct subcategories across the full blueprint list. When a Category is selected, it MUST be repopulated with only the subcategories that exist under that Category.
- **FR-003**: Selecting a Subcategory without a Category selected MUST filter the list to only blueprints matching that subcategory, regardless of their category.
- **FR-004**: Both pages MUST expose a name search input that filters the blueprint list by product name using a case-insensitive substring match.
- **FR-005**: All active filters (Category, Subcategory, name search) MUST be applied simultaneously, with the result being blueprints that satisfy all active criteria.
- **FR-006**: Clearing a filter control (Category, Subcategory, or name) MUST immediately update the list to reflect the removal of that filter.
- **FR-007**: Clearing the Category filter MUST reset the Subcategory options to the full set of available subcategories (but MUST NOT automatically clear an active Subcategory selection).
- **FR-008**: Filter controls MUST be visible on the page without requiring the user to open a separate panel or dialog.
- **FR-009**: Filter controls MUST be consistent in appearance and behaviour across the My Blueprints and Org Blueprints pages.
- **FR-010**: Filter options for Category and Subcategory MUST be derived from the actual data returned for the current user/org — no hardcoded lists.
- **FR-011**: There is no combined "Reset all filters" control. Each filter (Category, Subcategory, name search) MUST be individually clearable.

### Key Entities *(include if feature involves data)*

- **Blueprint**: A craftable game product. Relevant attributes for filtering: product name (display label), category (the `Type` field from the game data), subcategory (the `Subtype` field from the game data).
- **Filter State**: The combination of an optional category selection, an optional subcategory selection, and an optional name search string. Determines which subset of blueprints is displayed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can reduce a full blueprint list to only those matching a chosen category within 2 interactions (select category, list updates).
- **SC-002**: A user can further reduce a category-filtered list to a specific subcategory within 1 additional interaction.
- **SC-003**: The list updates without a full page reload after any filter change.
- **SC-004**: Filtering by name returns only blueprints whose product name contains the search text, with no false positives.
- **SC-005**: All filters work correctly on both the My Blueprints and Org Blueprints pages with no functional difference between the two.
- **SC-006**: When all filters are cleared, the complete unfiltered list is restored.

## Assumptions

- Filtering is performed client-side on the already-fetched blueprint list. The existing API endpoints return all blueprints for the user/org in a single response, and the dataset is small enough (hundreds, not tens of thousands) that client-side filtering is sufficient and preferable to adding query parameters to the API. No new backend endpoints or query parameter changes are required.
- "Category" maps to the `type` field on the blueprint list item as currently returned by the API. "Subcategory" maps to the `subtype` field. If `subtype` is not currently returned by the API list endpoints, it must be added to the existing response contracts.
- Filter state is held in component-level React state, not in the URL or global store. Navigation away from the page (e.g., to another route) may reset filters; only same-page interactions (detail panel open/close) are required to preserve filter state.
- The existing "Type" column in both blueprint tables refers to the same `type` field being used as the Category filter.
- Both pages display the same columns and behave identically with respect to filtering.

## Clarifications

### Session 2026-07-29

- Q: When no Category is selected, should the Subcategory control be hidden, disabled, or always active? → A: Always visible and active. When no category is selected, Subcategory is populated with all available subcategories. When a category is selected, Subcategory is narrowed to only subcategories within that category. Selecting a Subcategory without a Category is valid and filters the list independently.
- Q: Should there be a single "Reset all filters" button in addition to individual clear controls? → A: No — individual controls only; no combined reset button.
- Q: Should the name search filter update the list instantly on every keystroke or after a brief pause? → A: Instant — list updates on every keystroke with no delay (filtering is client-side, no API cost per keystroke).
