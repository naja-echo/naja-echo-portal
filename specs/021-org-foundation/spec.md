# Feature Specification: Organization Foundation & Admin Assignment

**Feature Branch**: `021-org-foundation`

**Created**: 2026-07-19

**Status**: Planned — spec, plan, and tasks complete; ready for `/speckit-implement`

**Input**: GitHub issue [#31 — Organization foundation & admin assignment](https://github.com/naja-echo/naja-echo-portal/issues/31), a Feature under Epic [#30 — Organization tenancy](https://github.com/naja-echo/naja-echo-portal/issues/30)

## Context

Naja Echo Portal serves exactly one organization today. There is no Organization concept
anywhere in the product: "org-wide" views simply mean "every registered member." This
feature installs the tenant boundary — the organization record, the membership between a
member and an organization, and the enforcement seam that keeps one organization's records
away from another — while the data model is still small enough to change cheaply.

Nothing in this feature changes what today's members see. It is the walking skeleton the
rest of Epic #30 attaches to: warehouse inventory (#32), hangar & fleet (#33), and the
loot ledger & economy (#34) each become organization-scoped in their own follow-up.

**This solves no present-day pain.** It is a deliberate, accepted YAGNI exception recorded
at the epic level: paying for the boundary now because retrofitting it later is far more
expensive than installing it today.

## Clarifications

### Session 2026-07-19

- Q: No entity becomes organization-scoped in this feature, so where does the
  unassigned-member experience belong? → A: Keep only the backend guarantee (an unassigned
  member receives no organization-scoped records, FR-021), which is testable today. The
  "contact an admin" messaging moves to #32–#34, each adding it to the views it scopes.
  Issue #31 needs that acceptance criterion relocated accordingly.
- Q: Membership and a denormalized current-organization on the member store the same truth
  in two places — which is authoritative? → A: Neither. Drop the current-organization
  association from the member entirely and mark currency on the membership relationship
  itself, with at most one membership per member marked current. This removes the second
  source of truth rather than reconciling it. Supersedes issue #31's
  `CurrentOrganizationId` on `ApplicationUser`.
- Q: Constitution Principle V requires observability, but the spec assumed no audit trail
  for assignment changes — which holds? → A: Emit a structured log event on every
  assignment change (acting admin, affected member, previous organization, new
  organization, timestamp). No persisted audit store and no in-product audit view; a
  queryable trail is deferred until there is more than one organization to investigate.

### Session 2026-07-19 (planning)

- Q: Does the default-on restriction actually cover every retrieval? → A: No. Research during
  `/speckit-plan` found the standard filtering mechanism covers only queries expressed
  through the framework's own query path; roughly 35 hand-authored database queries exist
  across the areas #32–#34 will scope, and those are never filtered. FR-020 was overclaiming
  and has been split: FR-020 states the default-on guarantee, FR-020a states the explicit
  obligation for hand-authored queries. A storage-level alternative (database row policies)
  that would have covered both paths was evaluated and deferred — with one organization there
  is nothing to leak to, and it remains available later. See `research.md` D6.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Existing members keep working, unchanged (Priority: P1)

Every member who uses the portal today continues to see exactly what they saw before. The
deployment that introduces organizations places all existing members into a single default
organization named "Naja Echo." No record changes hands, because no record is
organization-scoped yet. No member is asked to pick anything, and nobody encounters an
empty page they did not expect.

**Why this priority**: A tenancy boundary that disrupts the only live organization has
negative value. Invisible continuity is the entire bar for this release, and every other
story in this feature is built on top of the default organization existing.

**Independent Test**: Deploy against a copy of production data, sign in as several
existing members with differing roles, and confirm every view renders identical content
to the pre-deployment build. Fully testable on its own and delivers the whole "no
regression" guarantee.

**Acceptance Scenarios**:

1. **Given** a deployment carrying existing members and records, **When** the upgrade
   completes, **Then** an organization named "Naja Echo" exists and every existing member
   belongs to it.
2. **Given** an existing member who belonged to no organization before the upgrade,
   **When** they sign in afterward, **Then** they land on the same views with the same
   content and are never prompted to choose or join an organization.
3. **Given** the upgrade has already been applied, **When** it is applied a second time,
   **Then** no duplicate default organization is created and no member's membership
   changes.

---

### User Story 2 - Admin assigns a member to an organization (Priority: P2)

A global admin opens the existing Members page, sees which organization each member
belongs to, and can set or clear that assignment. The change takes effect for that member
without requiring them to sign out and back in.

Because this release ships only the default "Naja Echo" organization (FR-015), the
practical use is bringing a newly registered member into it — and, if needed, removing
them from it again.

**Why this priority**: Assignment is the only way membership is ever established — there
is no self-service path — so without it every member who registers after the upgrade is
permanently stranded. It ranks below P1 because the backfill already handles everyone who
exists today.

**Independent Test**: Register a new member, sign in as a global admin, assign them to
"Naja Echo" on the Members page, then confirm as that member that the change is reflected
on their very next request. Delivers working administration independent of any scoped data
existing yet.

**Acceptance Scenarios**:

1. **Given** a global admin on the Members page, **When** the page loads, **Then** each
   member row shows the organization that member belongs to, or shows clearly that they
   belong to none.
2. **Given** a global admin viewing an unassigned member, **When** they assign that member
   to an organization and confirm, **Then** the assignment is saved and the row reflects
   it.
3. **Given** a global admin viewing an assigned member, **When** they clear that member's
   organization and confirm, **Then** the member is left with no current membership and
   their owned records stay with the organization they were created in.
4. **Given** a member who already holds a current membership, **When** an admin assigns
   them to a different organization, **Then** the member holds exactly one current
   membership afterward, in the new organization.
5. **Given** a member whose organization was just changed by an admin, **When** that
   member makes their next request without signing out, **Then** the portal treats them as
   belonging to the new organization.
6. **Given** a signed-in member who is not a global admin, **When** they attempt to reach
   the organization assignment control by any route, **Then** the attempt is refused.

---

### User Story 3 - Scoping fails closed, not open (Priority: P3)

Records that belong to an organization are restricted to that organization by default,
without each individual screen or report having to remember to ask for it. A developer who
forgets to narrow a query gets no records rather than another organization's records.

**Why this priority**: This is the property that makes the whole epic trustworthy, but it
is listed last because in this feature it is demonstrable only through automated tests —
no live data is organization-scoped until #32, #33, and #34 land. It is nonetheless the
one thing this feature must get right, since every follow-up inherits it.

**Independent Test**: An automated test retrieves organization-scoped records while acting
as a member of one organization and confirms records belonging to another organization are
absent, with the retrieval deliberately written without any organization condition.

**Acceptance Scenarios**:

1. **Given** organization-scoped records existing for two different organizations,
   **When** a member of the first organization retrieves them without stating an
   organization condition, **Then** only the first organization's records are returned.
2. **Given** a member creating a new organization-scoped record, **When** the record is
   saved, **Then** it is attached to that member's organization without the member being
   asked which one.
3. **Given** an unauthenticated request, **When** it reaches organization-scoped records,
   **Then** no records are returned.
4. **Given** a member belonging to no organization, **When** they retrieve
   organization-scoped records, **Then** no records are returned rather than an error.
5. **Given** reference data such as ships, items, commodities, star systems, and
   locations, **When** any member retrieves it, **Then** the full catalog is returned
   regardless of organization, including for a member belonging to no organization.

---

### Edge Cases

- A member is assigned to a different organization while they have a page open — the
  change applies from their next request onward; already-rendered data is not retroactively
  hidden.
- A member is assigned to a different organization while owning records in their previous
  one — the reassignment succeeds and those records stay behind with the original
  organization, becoming invisible to the member (FR-016).
- Two admins assign the same member to different organizations concurrently — the last
  write wins and the Members page reflects the winner on reload. The member MUST NOT be
  left holding two current memberships under any interleaving (FR-004).
- A member already holds a membership in the organization an admin is assigning them to —
  the existing membership is marked current rather than a second one being created.
- A global admin is themselves a member of an organization and is scoped exactly like
  every other member; an admin belonging to no organization sees no organization-scoped
  records at all, while still being able to administer members.
- The last member of an organization is reassigned away, leaving records owned by nobody
  currently in that organization — the records remain, unreachable until a member is
  assigned back.
- An organization is deleted or renamed while members are assigned to it — deletion is out
  of scope for this feature; no path exists to remove an organization that has members.
- The default backfill runs against a deployment that has no members at all — the default
  organization is still created.
- A member's organization is cleared rather than reassigned — they become unassigned and
  every organization-scoped retrieval returns empty for them (FR-021).

## Requirements *(mandatory)*

### Functional Requirements

**Organization and membership**

- **FR-001**: The system MUST represent an organization as a distinct record with a name
  that identifies it to admins.
- **FR-002**: The system MUST record membership as a relationship between a member and an
  organization, structured so that a member holding more than one membership later is a
  presentation change rather than a data migration.
- **FR-003**: The membership relationship itself MUST carry whether it is the member's
  current one. The system MUST NOT store a current-organization value anywhere else, so
  that membership is the single source of truth for scoping.
- **FR-004**: The system MUST enforce that a member has at most one membership marked
  current. Marking a membership current MUST clear any other current membership for that
  member in the same operation, leaving no window in which two are current.
- **FR-005**: The system MUST allow a registered member to have no current membership, and
  MUST treat that as a valid, non-error state rather than an error or a redirect.
- **FR-006**: The system MUST resolve the acting member's organization on every request
  from their current membership, without the member selecting or supplying it.

**Default organization and backfill**

- **FR-007**: The system MUST create an organization named "Naja Echo" as part of the
  upgrade that introduces organizations.
- **FR-008**: The system MUST give every member who exists at upgrade time a current
  membership in the default organization.
- **FR-009**: The upgrade MUST be safe to apply more than once, creating no duplicate
  default organization, no duplicate membership, and altering no membership already
  established.
- **FR-010**: The upgrade MUST NOT change what any existing member sees in any view.

**Administration**

- **FR-011**: The Members page MUST display each member's current organization, or show
  clearly that they have none.
- **FR-012**: A global admin MUST be able to set a member's current organization and to
  clear it.
- **FR-013**: The system MUST reject any organization assignment attempt from a member who
  is not a global admin, regardless of how the attempt is made.
- **FR-014**: A change to a member's current membership MUST take effect on that member's
  next request without requiring them to sign out and sign back in, matching how role
  changes already behave.
- **FR-015**: The default "Naja Echo" organization MUST be the only organization this
  release produces. The system MUST NOT offer any way to create, rename, or delete an
  organization. The assignment control therefore presents a single organization plus the
  option to leave a member unassigned.
- **FR-016**: When a member's current membership changes from one organization to another,
  the organization-scoped records they own MUST remain with the organization those records
  were created in. The member MUST NOT carry inventory, fleet entries, loot ledger
  entries, or loot standing across an organization boundary. No record is organization-scoped
  in this feature, so this requirement is unverifiable here; the obligation to demonstrate it
  falls on the features that scope each area (#32–#34), as with FR-020a.
- **FR-017**: Reassignment MUST NOT be blocked by the existence of records the member
  owns; the records simply stay behind. This feature satisfies its half — assignment consults
  no owned records and cannot be refused because of them — while the records-stay-behind half
  is demonstrated by #32–#34 alongside FR-016.
- **FR-018**: Every change to a member's current membership MUST emit a structured log
  event identifying the acting admin, the affected member, the previous organization, the
  new organization, and when it happened. Clearing a membership MUST be logged the same
  way, with no new organization.
- **FR-019**: That log event MUST carry no authentication secrets, consistent with the
  existing rule that tokens, session cookies, and authorization headers are scrubbed from
  log output.

**Scoping and enforcement**

- **FR-020**: The system MUST restrict organization-scoped records to the acting member's
  current organization by default, so that a retrieval which omits an organization
  condition returns nothing outside that organization rather than everything. This
  default-on guarantee applies to retrievals expressed through the standard data-access
  path. Retrievals written as hand-authored database queries bypass it — see FR-020a.
- **FR-020a**: Any hand-authored database query against an organization-scoped record MUST
  carry an explicit organization condition AND be covered by a test asserting it cannot
  return another organization's records. This obligation falls on the features that scope
  each area (#32–#34); this feature introduces no such query.
- **FR-021**: The system MUST return no organization-scoped records for a member who has
  no current membership, and MUST treat that as an empty result rather than an
  error. How that empty result is presented in the interface is owned by the features
  that scope each view (#32–#34), not by this one.
- **FR-022**: The system MUST NOT scope reference data — ships, items, item categories,
  commodities, star systems, stations, cities, and crafting blueprints remain visible to
  every member.
- **FR-023**: The enforcement mechanism MUST be in place and demonstrably working in this
  feature, even though no live entity is organization-scoped until the follow-up features
  land.
- **FR-024**: The restriction MUST apply to every member regardless of role, including
  global admins. No role grants a view of another organization's records, and the system
  MUST NOT provide a way to bypass the restriction.
- **FR-025**: Administration of members MUST remain unaffected by that restriction: the
  Members page lists every registered member and their current organization, because
  member administration is not itself organization-scoped data.

### Key Entities

- **Organization**: A tenant boundary. Carries a human-readable name used by admins to
  tell organizations apart. One organization, "Naja Echo," exists after the upgrade.
- **Organization Membership**: The link between a member and an organization, and the sole
  source of truth for which organization a member acts in. Modeled as a standalone
  relationship rather than a field on the member so that multi-organization membership is
  a later UI concern, not a later migration. Carries a currency marker: at most one of a
  member's memberships is current at a time, and a member may have none current.
- **Member (existing)**: Unchanged in shape. The member record deliberately does NOT carry
  an organization field — scoping resolves through the member's current membership so
  there is only one place the answer can live.
- **Organization-scoped record (category)**: Any owned or held record that belongs to an
  organization — warehouse inventory, hangar and fleet entries, loot ledger entries and
  standings. None are actually scoped in this feature; the category is defined here so the
  follow-up features have a contract to satisfy.
- **Reference record (category)**: Catalog data imported from external sources. Explicitly
  never organization-scoped.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of members existing before the upgrade hold a current membership in the
  default organization after it, with zero members left without one by the upgrade itself.
- **SC-008**: Zero members ever hold more than one current membership, asserted across
  assignment, reassignment, clearing, and concurrent-change tests.
- **SC-009**: 100% of assignment changes are reconstructable from log output alone — who
  changed whom, from which organization to which, and when.
- **SC-002**: Zero user-visible differences between the pre-upgrade and post-upgrade build
  for an assigned member, confirmed across every view in the portal.
- **SC-003**: A global admin can find a member and change their organization in under 30
  seconds from landing on the Members page, timed during the manual walkthrough of the
  assignment flow (quickstart V2) rather than by an automated test.
- **SC-004**: An organization assignment is reflected for the affected member within one
  request of the change, with no sign-out required.
- **SC-005**: Zero organization-scoped records are returned across organization boundaries
  in automated tests, including tests that deliberately omit an organization condition.
- **SC-006**: 100% of reference-data views remain fully populated for a member belonging
  to no organization.
- **SC-007**: A member belonging to no organization receives an empty result, never an
  error, from every organization-scoped retrieval.

## Assumptions

- The upgrade runs against a deployment serving exactly one real organization, so a single
  default organization is the correct backfill target for every existing member. Records
  are not stamped with an organization by this feature; each of #32–#34 backfills the
  rows it scopes.
- "Global admin" means the existing Admin role, which is already treated as a superset of
  every other role. This feature introduces no new role.
- Organization assignment lives on the existing Members page rather than a new
  administration screen, since that page already carries per-member administrative actions.
- Organization changes propagate using the same live-refresh mechanism that already keeps
  role changes current, so no new session-invalidation approach is needed.
- Organization names are not required to be unique in this release; only one organization
  exists and no creation path can produce a second.
- Cross-organization isolation is verified through automated tests that construct a second
  organization directly, since the product offers no way to create one (FR-015).
- A member who owns records and is then unassigned leaves those records intact but
  unreachable; no cleanup, transfer, or reclaim flow is in scope.
- Members are not notified when their organization changes; the change is silent and
  visible only through what data they can see.
- Assignment changes are observable through structured log events (FR-018) rather than a
  persisted audit trail. A queryable, in-product audit history is deferred until there is
  more than one organization to investigate across.
- Organization deletion, renaming, per-organization settings, theming, and billing are all
  out of scope, consistent with the epic's non-goals.
- Character records and pending character registrations are not organization-scoped by
  this feature; they follow their owning member.

## Dependencies

- The existing Members administration page and its per-member actions (spec 017).
- The existing role model and the live claim-refresh mechanism that lets role changes take
  effect without a sign-out.
- The follow-up features #32, #33, and #34 depend on this one; none of them can begin until
  the enforcement seam defined here exists.

## Out of Scope

- An organization switcher or any notion of an "active organization" chosen per session.
- Multi-membership user experience.
- An organization-scoped admin role — administration remains global.
- Self-service organization creation, invitations, or join requests.
- Per-organization theming, settings, or billing.
- Applying organization scoping to warehouse inventory, hangar and fleet, or the loot
  ledger and economy — each is its own follow-up feature.
- Any interface messaging for a member belonging to no organization. This feature
  guarantees only the empty backend result (FR-021); the "contact an admin" state is added
  by each of #32–#34 to the views it scopes.
- Making the Discord integration organization-aware; that is a constraint on #18, not a
  retrofit here.
