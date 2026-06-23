# Feature Specification: Member Loot Ledger

**Feature Branch**: `019-member-loot-ledger`

**Created**: 2026-06-22

**Status**: Draft

**Input**: User description: "@.specify/memory/brainstorms/019-member-registry-credits.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Member Views Their Own Loot Ledger (Priority: P1)

A member navigates to the My Loot page under Crew Resources and sees their full OrgPoints and LootPoints ledger history alongside their current Claim Priority. Each ledger entry shows the amount, reason, who posted it, and the date. The member cannot edit any entry.

**Why this priority**: My Loot is the primary transparency surface for members — it lets every member verify their standing without requiring admin intervention. It is independently useful even before any points have been awarded.

**Independent Test**: Can be fully tested by navigating to the My Loot page as any authenticated member and confirming OrgPoints history, LootPoints history, and Claim Priority are all displayed correctly.

**Acceptance Scenarios**:

1. **Given** an authenticated member navigates to My Loot, **When** the page loads, **Then** they see two ledger tables (OrgPoints history and LootPoints history) and their current Claim Priority.
2. **Given** a new member with no LootPoints entries, **When** they view My Loot, **Then** the LootPoints ledger is empty and Claim Priority shows 0.00.
3. **Given** a member views My Loot, **When** the page loads, **Then** only their own ledger data is visible — no other member's data is accessible from this page.
4. **Given** a Crew Resource Officer posts an OrgPoints entry for a member, **When** that member views My Loot, **Then** the new entry appears in the OrgPoints table and Claim Priority updates to reflect the change.
5. **Given** a member views My Loot, **When** they examine a ledger entry, **Then** they see the amount, reason, the name of the person who posted it, and the date.

---

### User Story 2 — Member Views the Loot Distribution Table (Priority: P1)

Any authenticated member can navigate to the Loot Distribution page and see a table of all org members showing each member's Claim Priority, total OrgPoints, and total LootPoints. Each row has a View icon action that opens a read-only side sheet showing that member's full ledger history.

**Why this priority**: Transparency is a core org principle — every member should be able to see everyone's standing, not just their own. This drives trust in the loot distribution process.

**Independent Test**: Can be fully tested by navigating to Loot Distribution as any authenticated member, confirming the all-member table appears with correct totals, and clicking View on a row to confirm the side sheet opens with the correct ledger entries.

**Acceptance Scenarios**:

1. **Given** any authenticated member navigates to Loot Distribution, **When** the page loads, **Then** a table of all members appears showing each member's display name, Claim Priority, OrgPoints total, and LootPoints total.
2. **Given** a member clicks the View icon on a row in the Loot Distribution table, **When** the side sheet opens, **Then** the full OrgPoints and LootPoints ledger entries for that member are displayed, and the sheet contains no entry-modification actions (it is read-only).
3. **Given** a member has a registered character, **When** they appear in the Loot Distribution table, **Then** their character name is used as the display name.
4. **Given** a member has no registered character, **When** they appear in the Loot Distribution table, **Then** their Discord display name is used instead.
5. **Given** a member with zero LootPoints entries appears in the table, **When** Claim Priority is computed, **Then** it shows 0.00 (treating an empty denominator as 100 to avoid divide-by-zero).

---

### User Story 3 — Crew Resource Officer Posts OrgPoints (Priority: P2)

A Crew Resource Officer clicks the Add Points button at the top of the Loot Distribution page, which opens a modal. They select a member from a searchable combobox, enter an amount and a required reason, and submit. The new OrgPoints entry is immediately reflected in that member's ledger and Claim Priority.

**Why this priority**: OrgPoints manual entry is the primary mechanism for recognizing member contributions. Without it, the ledger cannot be populated and Claim Priority remains meaningless.

**Independent Test**: Can be fully tested by a CRO posting an OrgPoints entry via the Add Points modal on the Loot Distribution page and then verifying the entry appears in that member's ledger and Claim Priority updates.

**Acceptance Scenarios**:

1. **Given** a Crew Resource Officer is on the Loot Distribution page, **When** they click Add Points, **Then** a modal appears with a member combobox, an amount field, and a reason field.
2. **Given** a CRO submits an Add Points entry, **When** the reason field is empty, **Then** the form is rejected with a validation message.
3. **Given** a CRO has the Add Points modal open, **When** no member is selected, **Then** the submit action is disabled until a member is chosen.
4. **Given** a CRO submits a valid Add Points entry, **When** it is saved, **Then** the new entry appears in the selected member's OrgPoints ledger immediately and Claim Priority updates.
5. **Given** a member who is not a CRO or Admin views the Loot Distribution page, **When** the page loads, **Then** the Add Points button is not visible.
6. **Given** a CRO posts a negative OrgPoints entry (debit), **When** the entry is saved, **Then** it appears in the ledger with the negative amount and the member's total reflects the deduction.
7. **Given** a CRO adds OrgPoints for a member whose LootPoints total is zero, **When** the entry is saved, **Then** the system also posts a 100 LootPoints entry with the reason "Default" so the member's Claim Priority becomes a real, finite ratio.

---

### User Story 4 — Quartermaster Awards LootPoints (Priority: P2)

A Quartermaster clicks the Award Loot button at the top of the Loot Distribution page, which opens a modal. They select a member from a searchable combobox, enter an amount and a required reason, and submit. The new LootPoints entry is immediately reflected in that member's ledger and Claim Priority.

**Why this priority**: LootPoints tracking is what drives the fairness calculation. Without the ability to post LootPoints entries, Claim Priority cannot be accurately maintained and loot distribution cannot be justified.

**Independent Test**: Can be fully tested by a Quartermaster posting a LootPoints entry via the Award Loot modal on the Loot Distribution page and verifying the entry appears in that member's ledger with an updated Claim Priority.

**Acceptance Scenarios**:

1. **Given** a Quartermaster is on the Loot Distribution page, **When** they click Award Loot, **Then** a modal appears with a member combobox, an amount field, and a reason field.
2. **Given** a Quartermaster submits an Award Loot entry, **When** the reason field is empty, **Then** the form is rejected with a validation message.
3. **Given** a Quartermaster has the Award Loot modal open, **When** no member is selected, **Then** the submit action is disabled until a member is chosen.
4. **Given** a Quartermaster submits a valid Award Loot entry, **When** it is saved, **Then** the new entry appears in the selected member's LootPoints ledger immediately and Claim Priority updates.
5. **Given** a member who is not a Quartermaster or Admin views the Loot Distribution page, **When** the page loads, **Then** the Award Loot button is not visible.
6. **Given** a Quartermaster posts an initial LootPoints entry for a member who had zero LootPoints, **When** the entry is saved, **Then** Claim Priority updates from 0.00 to the correct ratio. (Award Loot does not auto-seed a "Default" entry — only Add Points does.)

---

### User Story 5 — Admin Manages Both Ledgers (Priority: P3)

An Admin user has the combined capabilities of both Crew Resource Officer and Quartermaster — they can post OrgPoints entries and Award Loot entries for any member.

**Why this priority**: Admin authority over both ledgers is a safety valve for situations where the designated role-holder is unavailable. It is not a primary workflow.

**Independent Test**: Can be tested by an Admin loading the Loot Distribution page and confirming both the Add Points and Award Loot buttons are present and functional.

**Acceptance Scenarios**:

1. **Given** an Admin views the Loot Distribution page, **When** the page loads, **Then** both the Add Points and Award Loot buttons are visible at the top of the page.
2. **Given** an Admin assigns the CrewResourceOfficer role to a member via the existing role assignment interface, **When** the assignment is saved, **Then** the new CRO immediately gains the ability to post OrgPoints entries.

---

### Edge Cases

- What happens when a member has no OrgPoints or LootPoints entries? → My Loot page shows empty ledger tables and displays Claim Priority as 0.00.
- What happens when LootPoints total is zero but OrgPoints total is positive? → For computation, Claim Priority falls back to OrgPoints ÷ 100. In addition, the first time a CRO adds OrgPoints to such a member, the system seeds a real 100 LootPoints "Default" entry so the denominator becomes an actual ledger value rather than an implicit fallback.
- What happens when both totals are zero? → Claim Priority is 0.00.
- What if a CRO does not hold the Quartermaster role, or a Quartermaster does not hold the CRO role? → Each only sees the button for the action they are authorized for; the Add Points button requires CRO/Admin and the Award Loot button requires Quartermaster/Admin.
- What if an unauthenticated user attempts to view My Loot or Loot Distribution? → They are redirected to the login screen.
- Can a ledger entry be edited or deleted after posting? → No. All ledger entries are permanent. Corrections must be made by posting a compensating entry with a reason explaining the adjustment.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a new "Crew Resources" top-level navigation group visible to all authenticated members, containing "My Loot" and "Loot Distribution" pages. "Crew Resources" appears as a peer group alongside all other existing top-level navigation sections.
- **FR-002**: The system MUST display a member's My Loot page showing their complete OrgPoints ledger history, LootPoints ledger history, and current Claim Priority. Both ledger tables MUST default to newest-first order (most recent entry at the top).
- **FR-003**: Claim Priority MUST be computed as the sum of all OrgPoints entries divided by the sum of all LootPoints entries, using 100 as the denominator when a member's LootPoints total is zero. Claim Priority MUST be displayed to exactly 2 decimal places (e.g., 1.23).
- **FR-004**: Ledger entries MUST be immutable — once posted, they cannot be edited or deleted.
- **FR-005**: Each ledger entry MUST record the amount (positive or negative), a required reason, the identity of the person who posted it, and the timestamp. The poster's display name MUST use the same resolution logic as all other member display names: character name when available, falling back to Discord display name.
- **FR-006**: The system MUST provide a Loot Distribution page showing all org members with their Claim Priority, OrgPoints total, and LootPoints total. The table MUST default to ascending Claim Priority order (lowest ratio first), surfacing the highest-priority members at the top. All members MUST load at once — no server-side pagination.
- **FR-007**: Any authenticated member MUST be able to view the Loot Distribution table and open a per-member ledger sheet.
- **FR-008**: The per-member ledger sheet MUST display the full OrgPoints and LootPoints history for the selected member, with both ledger tables sorted newest-first (most recent entry at the top). The sheet MUST be read-only and MUST NOT contain entry-modification actions. It is opened from a View icon action on each Loot Distribution row.
- **FR-009**: The system MUST allow Crew Resource Officers and Admins to post OrgPoints entries (positive or negative) for any member, with a required reason.
- **FR-010**: The system MUST allow Quartermasters and Admins to post LootPoints entries (positive or negative) for any member, with a required reason. Negative entries are the correction mechanism for over-awards, consistent with the immutable ledger design.
- **FR-011**: OrgPoints and LootPoints entry forms MUST reject submissions where the reason field is empty.
- **FR-016**: OrgPoints and LootPoints amounts MUST be whole integers (no decimal values). Entry forms MUST reject non-integer input.
- **FR-012**: The system MUST support a new "Crew Resource Officer" role, assignable through the existing role management interface.
- **FR-013**: Member display names MUST use the member's registered character name when available, falling back to their Discord display name when no character is registered.
- **FR-014**: The Loot Distribution page MUST show the Add Points button (at the top of the page) only to Crew Resource Officers and Admins.
- **FR-015**: The Loot Distribution page MUST show the Award Loot button (at the top of the page) only to Quartermasters and Admins.
- **FR-017**: The Add Points and Award Loot actions MUST be initiated from buttons at the top of the Loot Distribution page (not from the per-member ledger sheet). Each button MUST open a modal containing a searchable member combobox, an amount field, and a reason field.
- **FR-018**: The Add Points and Award Loot modals MUST require a member to be selected before submission is allowed.
- **FR-019**: When an OrgPoints entry is posted (Add Points) for a member whose LootPoints total is zero, the system MUST also post a LootPoints entry of 100 with the reason "Default", attributed to the same actor. This MUST be enforced server-side and MUST NOT occur when the member already has a non-zero LootPoints total, nor when awarding LootPoints directly (Award Loot).
- **FR-020**: Each row in the Loot Distribution table MUST expose a View icon action that opens the read-only per-member ledger sheet.

### Key Entities

- **OrgPoints Entry**: A single credit or debit record for a member's org participation standing. Contains: the member it belongs to, a signed integer amount, a required reason, the person who posted it, and the timestamp. Cannot be modified after creation.
- **LootPoints Entry**: A single record representing loot value received by a member. Same schema as an OrgPoints Entry (signed integer amount). Cannot be modified after creation.
- **Claim Priority**: A computed ratio — not stored — representing a member's relative priority for receiving loot. Equals the member's total OrgPoints divided by their total LootPoints (or 100 if LootPoints total is zero). Lower is higher priority (the member has contributed more relative to what they've received).
- **Crew Resource Officer (Role)**: A new org role that grants the ability to post OrgPoints entries for any member. Sits alongside the existing Admin and Quartermaster roles.
- **Default LootPoints Entry**: An automatically generated LootPoints entry of 100 with the reason "Default", created server-side the first time OrgPoints are added to a member who has zero LootPoints. It establishes a real denominator for Claim Priority. It is an ordinary, immutable ledger entry once created.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Any authenticated member can navigate to My Loot and see their own ledger and Claim Priority without assistance from an admin.
- **SC-002**: Any authenticated member can navigate to Loot Distribution, view all members' standings, and open any individual member's ledger sheet — all in under 30 seconds of navigation.
- **SC-003**: A Crew Resource Officer or Admin can post an OrgPoints entry for a member (with reason) and see it reflected in the member's ledger and Claim Priority within the same session, without a page reload.
- **SC-004**: A Quartermaster or Admin can post a LootPoints entry for a member (with reason) and see it reflected in the member's ledger and Claim Priority within the same session, without a page reload.
- **SC-005**: Claim Priority is always computed correctly — 100% of members with at least one entry have an accurate displayed Claim Priority at time of page load.
- **SC-006**: Unauthorized users (wrong role or unauthenticated) cannot access or submit ledger modification forms — 0% unauthorized modifications.
- **SC-007**: All ledger entries persist with complete audit data (amount, reason, actor, timestamp) — 100% of entries are auditable to their origin.

## Assumptions

- The existing role assignment interface (from feature 017) will be extended to include CrewResourceOfficer without any structural changes to the role management flow.
- A single registered character per member is assumed for v1; the character name is taken from the member's only registered character. Character names are sourced from an existing `Character` entity (characters table) already linked to the authenticated users table — no new character registration capability is introduced in this feature.
- Claim Priority is computed on the fly at the time data is requested — it is never stored and is always up to date.
- The "Crew Resources" navigation group will be visible to all authenticated members (not gated by role), since both pages it contains are intentionally transparent.
- LootPoints entries posted by a Quartermaster represent the value of actual loot awarded to that member; the amount is always positive for an initial award (a negative/debit entry would represent a correction).
- The Add Points and Award Loot actions appear as buttons at the top of the Loot Distribution page; a user who holds both Quartermaster and CRO roles (or is an Admin) will see both buttons. Member selection happens inside each modal via a combobox rather than by opening a per-member sheet.
- Existing Admin and Quartermaster roles are already in the system; only the CrewResourceOfficer role is new in this feature.
- The Loot Distribution page lists all members who have accounts, regardless of whether they have any ledger entries yet.

## Clarifications

### Session 2026-06-22

- Q: What should the default sort order be for the Loot Distribution table? → A: Claim Priority ascending — lowest ratio first (highest loot priority at the top).
- Q: Should LootPoints entries allow negative amounts, or are they positive-only? → A: Allow negative LootPoints — same sign rules as OrgPoints. Negative entries serve as the correction mechanism for over-awards (consistent with the immutable ledger design).
- Q: What should the default sort order be for ledger entries on My Loot and the Loot Distribution side sheet? → A: Newest-first — most recent entry at the top (descending by timestamp).
- Q: Should OrgPoints and LootPoints amounts allow decimal values? → A: Integers only — amounts must be whole numbers; entry forms reject non-integer input.
- Q: Should the Loot Distribution table use pagination? → A: No pagination — all members load at once; client-side sort only.
- Q: To how many decimal places should Claim Priority be displayed? → A: 2 decimal places (e.g., 1.23).

### Session 2026-06-22 (continued)

- Q: Where do registered character names come from — is there an existing data source? → A: An existing `Character` entity (characters table) is already linked to the authenticated users table. Character names are read from that table; no new character registration is introduced in this feature.
- Q: What display name format should be used for the "posted by" identity in ledger entries? → A: Character name falling back to Discord display name — the same resolution logic used for all other member display names in this feature.
- Q: Where does the "Crew Resources" section sit in the navigation hierarchy? → A: New top-level navigation group, appearing as a peer alongside all other existing top-level nav sections.
- Q: Should Add Points and Award Loot forms show a confirmation step before submitting (given entries are immutable)? → A: No confirmation dialog — submit directly on form submit; the required reason field is the sufficient guard against accidental entries.

### Session 2026-06-23

- Q: Where should the Add Points and Award Loot actions live? → A: As buttons at the top of the Loot Distribution page, each opening a modal with a member combobox plus amount and reason fields. They are no longer on the per-member side sheet, which is now read-only and opened via a View icon on each row.
- Q: Should adding points auto-seed default loot points? → A: Yes — when Add Points (OrgPoints) is posted for a member whose LootPoints total is zero, the system also posts a 100 LootPoints entry with reason "Default". This applies to Add Points only (not Award Loot) and is enforced server-side.
