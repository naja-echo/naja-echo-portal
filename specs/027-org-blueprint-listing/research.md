# Research: Org Blueprint Listing

## Decision 1: Org scoping via OrganizationMembership table

**Decision**: Filter org blueprints by joining `user_blueprints` through `organization_memberships` where `is_current = true` and `organization_id` matches the current user's org.

**Rationale**: The domain already has an `OrganizationMembership` entity with `user_id`, `organization_id`, and `is_current` fields. A partial unique index on `(user_id)` where `is_current = true` ensures each user has at most one active org. The current user's `user_id` is available from the auth claim; their `organization_id` is resolved via the membership table at query time.

**Alternatives considered**:
- Store org ID in session claims — rejected: would require claim changes and re-login for org switches, not needed now.
- Use `AppDbContext.CurrentOrganizationId` — feasible but adds coupling between the request context and raw SQL. Using a direct sub-query keeps the repository self-contained.

---

## Decision 2: New IOrgBlueprintRepository interface

**Decision**: Introduce a new `IOrgBlueprintRepository` interface in the Application layer with two methods: `GetListAsync(Guid userId)` and `GetDetailWithOwnersAsync(Guid userId, Guid blueprintId)`.

**Rationale**: The org blueprint use cases are read-only and conceptually distinct from personal blueprint management. Using the existing `IUserBlueprintRepository` would require adding org-specific parameters (userId used only to resolve orgId) alongside personal-use parameters, conflating two different access patterns. A dedicated interface keeps each repository focused and testable independently.

**Alternatives considered**:
- Add methods to `IUserBlueprintRepository` — rejected: that interface is scoped to a single user's personal list; adding cross-user queries would violate Single Responsibility.
- Add methods to `IBlueprintRepository` — rejected: that interface manages the catalog (import, search, list all), not user-ownership queries.

---

## Decision 3: Deduplicate blueprints in the listing at query level

**Decision**: Use `GROUP BY b.id` (or `DISTINCT ON (b.id)`) in the org list query so each blueprint appears once regardless of how many org members have it.

**Rationale**: The spec requires the listing to show unique blueprints (FR-002). Deduplication at query time is cheaper than in application code and avoids loading redundant rows.

**Alternatives considered**:
- Deduplicate in application code — rejected: unnecessary memory usage and complexity.

---

## Decision 4: Owners included in the detail response (not a separate endpoint)

**Decision**: The `GET /api/blueprints/org/{blueprintId}` endpoint returns the full detail including the owners list in a single response.

**Rationale**: The owners list is always shown alongside the blueprint detail. A single round-trip is simpler and avoids a loading state for a secondary request. The owners list is small (bounded by org size) and does not need pagination in the current scope.

**Alternatives considered**:
- Separate `GET /api/blueprints/org/{blueprintId}/owners` endpoint — rejected: over-engineering for a small list; adds a second round-trip for the common case.

---

## Decision 5: Display name field for owners

**Decision**: Use `ApplicationUser.DisplayName` (varchar 64) as the owner display value. Fall back to `DiscordUsername` if `DisplayName` is empty.

**Rationale**: `ApplicationUser` already has `display_name` and `discord_username` columns. `DisplayName` is the user-facing identity within the portal; it should be preferred. The fallback ensures no owner entry is blank.

**Alternatives considered**:
- Use `UserName` (Identity default) — rejected: may be an internal or OAuth-derived value not meaningful to org members.

---

## Decision 6: No EF Core migration required

**Decision**: No new tables or columns are needed. All queries use existing tables: `user_blueprints`, `organization_memberships`, `AspNetUsers`, `sc.blueprints`, `sc.blueprint_tiers`, `sc.blueprint_slot_options`.

**Rationale**: The feature is purely a new read path over existing data.

---

## Decision 7: New OrgBlueprintDetailPanel frontend component

**Decision**: Create a new `OrgBlueprintDetailPanel.tsx` rather than reusing `BlueprintDetailPanel.tsx`.

**Rationale**: The org detail panel differs from the personal panel in two ways: it omits the Remove button, and it adds an Owners section. Extending the personal panel with conditional props would create a confusing shared component. Two focused components are simpler and independently testable.

**Alternatives considered**:
- Single panel with `showRemove` and `showOwners` boolean props — rejected: violates YAGNI; the panel behaviours are distinct enough to warrant separate components for clarity.
