# Feature Specification: Org Blueprint Listing

**Feature Branch**: `027-org-blueprint-listing`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: "Create an Org Blueprint listing. The user should be able to navigate to the Org Blueprints and see all of the blueprints available. Where as Personal Blueprints only displays the blueprints belonging to the user, the Org Blueprints shows all of the blueprints from all of the users in the org. The listing functions similar to the Personal Blueprints. When a user clicks on the blueprint, a sliding window appears with the blueprint details. At the bottom, there should be a listing of members who have the blueprint. The purpose of this is so that if a user wants to know who could craft them a specific item, they can look up the blueprint owners. There is no need for a remove button since personal users control that."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Org Blueprint Listing Page (Priority: P1)

An authenticated org member navigates to the Org Blueprints section and sees a table listing every unique blueprint that any member of the org has added to their personal collection. The table shows the blueprint name, type, and ingredient count — the same columns as the Personal Blueprints view. The page is read-only; there are no add or remove controls.

**Why this priority**: This is the entry point to the feature. Without the listing, nothing else is accessible. It delivers immediate value by giving any member visibility into the org's collective crafting capabilities.

**Independent Test**: Navigate to the Org Blueprints page — a table of all blueprints across org members loads. Each row shows blueprint name, type, and ingredient count.

**Acceptance Scenarios**:

1. **Given** an authenticated org member, **When** they navigate to Org Blueprints, **Then** they see a table listing every unique blueprint held by any org member.
2. **Given** the org has no blueprints at all, **When** a member navigates to Org Blueprints, **Then** they see an empty-state message indicating no blueprints are available.
3. **Given** two org members both have the same blueprint, **When** viewing the Org Blueprint listing, **Then** that blueprint appears only once in the list.
4. **Given** an unauthenticated user, **When** they attempt to access Org Blueprints, **Then** they are redirected to the login page.

---

### User Story 2 — Blueprint Detail Panel (Priority: P1)

Clicking any row in the Org Blueprint listing opens a slide-in panel from the right showing the full blueprint details: the blueprint name at the top, a summary row with Type / Craft Time / Ingredient Count, and an ingredient listing below a divider. The panel closes when the user clicks the X button or clicks outside it. There is no Remove button.

**Why this priority**: The listing alone is not enough to evaluate a blueprint. The detail panel provides the crafting information a member needs to decide whether to request a craft from a colleague.

**Independent Test**: Click a blueprint row — a panel slides in showing the blueprint name, type, craft time, ingredient count, and ingredient list. Clicking X or outside closes the panel.

**Acceptance Scenarios**:

1. **Given** a blueprint row is clicked, **When** the panel opens, **Then** the blueprint name appears at the top of the panel.
2. **Given** the panel is open, **When** data has loaded, **Then** Type, Craft Time, and Ingredient Count are displayed in a summary row with their values beneath each heading.
3. **Given** a blueprint with ingredients, **When** the panel loads, **Then** the ingredient list is shown with slot names and material details.
4. **Given** a blueprint with no ingredients on record, **When** the panel loads, **Then** a "No ingredients listed" placeholder is shown.
5. **Given** the panel is open, **When** the user clicks the X button or clicks outside the panel, **Then** the panel closes.
6. **Given** the panel is open, **Then** there is no Remove button visible.

---

### User Story 3 — Blueprint Owners List (Priority: P2)

At the bottom of the blueprint detail panel, below the ingredient listing, a section displays the org members who have this blueprint in their personal collection. This allows any org member to quickly identify who can craft a given item.

**Why this priority**: This is the primary purpose of the Org Blueprint feature. Once the listing and detail panel are working, adding the owners list completes the feature's core value proposition.

**Independent Test**: Open a blueprint detail panel for a blueprint held by multiple members — a list of those members' names appears at the bottom of the panel.

**Acceptance Scenarios**:

1. **Given** a blueprint is owned by multiple org members, **When** the detail panel opens, **Then** a list of those members' display names appears at the bottom of the panel under an "Owners" or "Members" heading.
2. **Given** a blueprint is owned by only one org member, **When** the detail panel opens, **Then** that single member's name is listed.
3. **Given** no org members currently have a blueprint (edge case — e.g., all removed it since the listing loaded), **When** the detail panel opens, **Then** a placeholder such as "No members currently have this blueprint" is shown.

---

### Edge Cases

- What happens when the org has no members with any blueprints? → Empty-state message on the listing page.
- What happens if a blueprint's owner list changes while the panel is open? → The panel shows data as of the time it was opened; a manual refresh will show updated data.
- What if a blueprint has null type or null craft time? → Display "—" for any missing values, consistent with the Personal Blueprints panel.
- What if the user's session expires while browsing Org Blueprints? → Redirect to login on next attempted data fetch.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a navigation entry for "Org Blueprints" accessible to all authenticated org members.
- **FR-002**: The Org Blueprints page MUST display a deduplicated list of all blueprints held by any org member, showing blueprint name, type, and ingredient count per row.
- **FR-003**: The Org Blueprints page MUST show an empty-state message when no org members have any blueprints.
- **FR-004**: Each blueprint row MUST be clickable and open a detail panel sliding in from the right.
- **FR-005**: The detail panel MUST display the blueprint name, type (or "—" if absent), craft time formatted as "Xm Ys" (or "—" if absent), and ingredient count.
- **FR-006**: The detail panel MUST display the ingredient list using the same two-level structure as the Personal Blueprint detail panel (slot name with material options).
- **FR-007**: The detail panel MUST display a list of org members who have this blueprint in their personal collection.
- **FR-008**: The detail panel MUST show a placeholder when no members currently have the blueprint.
- **FR-009**: The detail panel MUST NOT include a Remove button or any other mutation controls.
- **FR-010**: The detail panel MUST close when the user clicks the X button or clicks outside the panel.
- **FR-011**: All data on the Org Blueprints page MUST be restricted to authenticated users; unauthenticated access MUST be rejected.

### Key Entities

- **OrgBlueprint**: A unique blueprint that exists in at least one org member's personal collection. Identified by blueprint ID; carries name, type, craft time, ingredient count, and ingredient slots.
- **BlueprintOwner**: An org member who has a given blueprint in their personal collection. Carries the member's display name (and optionally their user ID for linking).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An org member can identify all blueprints available in the org and the members who hold them without leaving the portal.
- **SC-002**: The Org Blueprint listing loads and displays results without noticeable delay under normal org sizes (up to several hundred blueprints).
- **SC-003**: A member can open a blueprint detail panel, review ingredients and owners, and close the panel in under 30 seconds.
- **SC-004**: Unauthenticated requests to Org Blueprint data are rejected 100% of the time.
- **SC-005**: The owners list correctly reflects the current personal blueprint collections of all org members at the time of the request.

## Assumptions

- All authenticated users of the portal are considered org members; no additional org-membership check beyond authentication is required for this feature.
- "Display name" for a blueprint owner is the member's username or Discord display name already stored in the system — no new profile fields are required.
- The Org Blueprint listing deduplicates by blueprint ID; if two members have the same blueprint, it appears once in the list.
- The ingredient data shown in the detail panel is the same sc-schema data used by the Personal Blueprint detail panel — no new data source is required.
- The Org Blueprints route will be `/blueprints/org` and will appear in the navigation alongside Personal Blueprints.
- Mobile support follows the same responsive behaviour as the rest of the dashboard shell; no special mobile layout is in scope for this feature.
- Sorting and filtering of the org blueprint listing are out of scope for this initial version.
