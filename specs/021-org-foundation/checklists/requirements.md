# Specification Quality Checklist: Organization Foundation & Admin Assignment

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-19
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

**Iteration 1 (2026-07-19)** — three [NEEDS CLARIFICATION] markers raised.

**Iteration 2 (2026-07-19)** — all three resolved by user decision:

| Marker | Decision | Landed in |
|--------|----------|-----------|
| Organization creation | Default "Naja Echo" only; no create/rename/delete path in this release | FR-015 |
| Reassignment data semantics | Owned records stay with the organization they were created in; reassignment is never blocked | FR-016, FR-017 |
| Admin visibility | Global admins are scoped identically to every other member; no bypass exists. Member administration itself stays unscoped | FR-024, FR-025 |

**Iteration 3 (2026-07-19)** — `/speckit-clarify`, 3 questions asked and answered:

| # | Question | Answer | Landed in |
|---|----------|--------|-----------|
| Q1 | No entity is organization-scoped in this feature, so where does the unassigned-member experience belong? | Keep only the backend guarantee (empty result, testable now); move the "contact an admin" messaging to #32–#34 | FR-021, US3 AC-4; removed old US3 and its two FRs |
| Q2 | Membership vs. a denormalized current-organization on the member — which is authoritative? | Neither: drop the member-side field and mark currency on the membership relationship, at most one current per member | FR-003, FR-004, FR-006, SC-008 |
| Q3 | Principle V observability vs. the "no audit trail" assumption | Structured log event per assignment change; no persisted audit store | FR-018, FR-019, SC-009 |

Consistency edits made alongside those answers:

- Old User Story 3 (unassigned dead end) removed; old User Story 4 promoted to P3.
- Context and Assumptions corrected — they claimed records were backfilled into the default
  organization, but no record gains an organization until #32–#34.
- Functional requirements renumbered twice; all 25 are contiguous and every in-text
  cross-reference re-pointed.
- Edge cases added for the at-most-one-current invariant under concurrent admin writes, and
  for re-assigning a member to an organization they already hold a membership in.

**Two upstream follow-ups this created** (neither blocks planning):

1. Issue #31 lists the unassigned empty state as an acceptance criterion. Per Q1 that
   criterion belongs on #32–#34 and should be moved off #31.
2. Issue #31 specifies `CurrentOrganizationId` on `ApplicationUser`. Per Q2 that field is
   superseded by a currency marker on the membership relationship. The issue's Approach
   section should be corrected before someone implements from it.

**Design language deliberately excluded**: issue #31's `Organization` entity,
`OrganizationMembership` join table, and EF Core global query filter are implementation and
belong in `/speckit-plan`. The spec states the outcomes they must produce — notably FR-002
on membership shape, FR-004 on the single-current invariant, and FR-020 on default-on
restriction — so the plan can be checked against them.

All 16 checklist items pass. Spec is ready for `/speckit-plan`.
