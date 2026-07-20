# Board mechanics & field reference

This is the *operational* reference: exact type/field semantics and the `board.sh`
command surface. The model itself is summarized in `SKILL.md` and, for humans, in
`specs/ROADMAP.md`. **The board is the source of truth for live state** — read it
(don't rely on a doc) for what themes/features exist and where they sit.

## The board

- **Project:** [Naja Echo Planning](https://github.com/orgs/naja-echo/projects/3), org `naja-echo`, project #3.
- **Repo issues live in:** `naja-echo/naja-echo-portal`.
- **Views:** a **Pipeline** view (board layout, filtered to issue types Feature and
  Bug, grouped by **Readiness**) is the day-to-day kanban; a **Roadmap** view (grouped
  by **Parent issue**, surfacing the built-in **Sub-issues progress** field) shows the
  Theme → Epic → Feature hierarchy with rollup %. Both filters/groupings are set in the
  view UI — confirm the exact type filter there rather than assuming a query string.

## Structure = native issue types + sub-issues (not a custom field)

The hierarchy is carried entirely by GitHub **issue types** and **sub-issue** links:

| Type | Role | Readiness? |
|------|------|-----------|
| **Theme** | major capability area, long-lived | no |
| **Epic** | bounded initiative, one finish line (only when 3+ features) | no |
| **Feature** | one `specs/NNN-` + branch + PR | **yes** |
| **Task** | `tasks.md` item | (usually off-board) |
| **Bug** | defect | **yes** |

- **Parent/child** = native **sub-issue** link → progress rolls up automatically.
- **Cross-theme dependency** = native **blocked-by** link.
- There is **no `Epic` custom field** — issue type + parent replace it.

## The one custom field: `Readiness`

Pipeline / kanban columns. Set on **Features and Bugs only**; leave empty on
Themes/Epics (they roll up).

| Value | Meaning | Skill sets this when… |
|-------|---------|----------------------|
| Idea | rough one-liner; may never happen | intake only; not yet shaped |
| Shaped | problem + rough approach agreed; not yet a spec | interview reached agreed problem + approach |
| Ready | clear enough to run `/speckit-specify` immediately | approach, deps, and acceptance criteria all settled |
| Spec'd | spec.md + plan.md exist | (set by Spec Kit flow, not this skill) |
| Building / In Review / Done | branch / PR / merged | (set during execution) |

This skill only ever lands items at **Idea**, **Shaped**, or **Ready**.

## board.sh command surface

`scripts/board.sh` resolves project/field/option IDs live (never stale) and
fuzzy-matches Readiness option names case-insensitively.

```bash
board.sh show                                      # issue types + Readiness options
board.sh themes                                    # existing Theme/Epic issues (for parenting)
board.sh add Feature "Low-stock alerts" --readiness "Shaped" --parent 16   # create + place + set, one shot
board.sh new-theme "Warehouse & Inventory"         # create a Theme + add to board
board.sh new-epic  "Discord Service v1"            # create an Epic + add to board
board.sh place <issue> [--readiness "Shaped"] [--parent <n>]   # place an EXISTING issue / set fields
board.sh readiness <issue> "Ready"                 # update readiness only
board.sh type      <issue> "Feature"               # (re)set issue type
board.sh parent    <child> <parent>                # child becomes a sub-issue of parent
board.sh blocked-by <issue> <blocker>              # add blocked-by link
```

`<issue>` accepts `123`, `#123`, or a full URL. `place` adds the issue to the
project if it isn't already on the board.

## Two CLI limits to know

- **Issue types are org-level.** `Theme`/`Epic`/`Feature`/`Task`/`Bug` already exist
  on the org and are set per-issue with `gh issue edit --type`. Creating a *brand-new
  type* (rare) is done in **org settings → Issue types**, not the CLI.
- **The built-in `Status` field is not CLI-editable** — that's why the pipeline lives
  in the dedicated `Readiness` field. Never try to set `Status`.

---

# Interview technique bank

Pull from these deliberately — don't run all of them. Match the tool to where the
idea is thin. Ask in **small batches** (2–4 questions), never a wall.

## Framing (does the idea justify itself?)
- **Problem statement first:** "What breaks / is painful today, for whom, and what
  happens if we don't build this?" Refuse to discuss solution until this is crisp.
- **Jobs To Be Done:** "When [situation], the user wants to [motivation], so they can
  [outcome]." Forces off features, onto need.
- **Who benefits + how often:** frequency and blast radius drive priority.

## Expanding (generate breadth before narrowing)
- **5 Whys:** chase the stated need down to the real driver.
- **Assumption surfacing:** "What has to be true for this to work?" Flag each as
  known / unvalidated. Unvalidated + high-risk → a spike, not a feature.
- **SCAMPER** for alternatives: Substitute, Combine, Adapt, Modify, Put-to-other-use,
  Eliminate, Reverse. Use when there's obviously more than one way to skin it.
- **Inversion / pre-mortem:** "It shipped and flopped a year later — why?"

## Shaping (converge toward a spec-able unit)
- **Rough approach, not design:** one paragraph of "probably do X against Y." Enough
  to size, not to build.
- **Given/When/Then** acceptance criteria: the testable exit conditions. This is the
  gate between Shaped and Ready.
- **Explicit non-goals (MoSCoW "Won't"):** the boundary is as valuable as the scope.
- **Dependencies & risks:** what must exist first (→ blocked-by), what could sink it.

## Decomposing (only when it's epic-sized)
- **Story mapping:** backbone activities → tasks → thin slices, sliced into releases.
- **Walking skeleton:** smallest end-to-end path that proves the concept — often the
  first feature to pull to Ready.
- **INVEST** test on each candidate feature: Independent, Negotiable, Valuable,
  Estimable, Small, Testable. Fails Small/Independent → split further.
- **Epic vs flat:** if the slices are 3+ features toward one finish line, wrap them in
  an **Epic**; otherwise parent them straight under the Theme.

## Completeness sweep (the things ideas forget)
Edge cases · error & empty states · non-functional needs (perf, security,
accessibility, observability, scale) · migration/backfill · who operates it.
