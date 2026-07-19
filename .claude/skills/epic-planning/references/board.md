# Board mechanics & field reference

The durable narrative lives in `specs/ROADMAP.md` — read it at the start of a
session for current epic status and cadence rules. This file is the *operational*
reference: exact field semantics and the `board.sh` command surface.

## The board

- **Project:** [Naja Echo Planning](https://github.com/orgs/Deceptively-Clever/projects/3), org `Deceptively-Clever`, project #3.
- **Repo issues live in:** `Deceptively-Clever/naja-echo-portal`.
- Group the board by the **`Readiness`** field (not the built-in `Status`).

## Two custom fields carry all structure

**`Readiness`** — the pipeline / kanban columns:

| Value | Meaning | Skill sets this when… |
|-------|---------|----------------------|
| Idea | rough one-liner; may never happen | intake only; not yet shaped |
| Shaped | problem + rough approach agreed; not yet a spec | interview reached agreed problem + approach |
| Ready | clear enough to run `/speckit-specify` immediately | approach, deps, and acceptance criteria all settled |
| Spec'd | spec.md + plan.md exist | (set by Spec Kit flow, not this skill) |
| Building / In Review / Done | branch / PR / merged | (set during execution) |

This skill only ever lands items at **Idea**, **Shaped**, or **Ready**. Handing a
Ready item to `/speckit-specify` is the downstream boundary.

**`Epic`** — capability area. Options: Identity & Access · Hangar & Fleet ·
Warehouse & Inventory · Org Economy · Crafting · Discord Integration Service.
(Closed/archived epics like *App Shell / UX* are intentionally absent — the board
holds present + future work only.)

## Structural links (native GitHub, not fields)

- **Epic** = a `[Epic] Name` tracking issue. Features attach as **sub-issues** so
  epic progress rolls up automatically.
- **Cross-epic dependency** = native **blocked-by** link.

## board.sh command surface

`scripts/board.sh` resolves project/field/option IDs live (never stale) and
fuzzy-matches option names case-insensitively.

```bash
board.sh show                                          # list Epic + Readiness option names
board.sh place  <issue> --epic "Crafting" --readiness "Shaped"
board.sh readiness <issue> "Ready"                     # update one field
board.sh epic      <issue> "Warehouse"                 # fuzzy match ok
board.sh new-epic  "Logistics"                         # opens [Epic] Logistics tracking issue
board.sh attach    <feature> <epic>                    # feature -> sub-issue of epic
board.sh blocked-by <issue> <blocker>                  # add blocked-by link
```

`<issue>` accepts `123`, `#123`, or a full URL. `place` adds the issue to the
project if it isn't already on the board.

## Two CLI limits to know

- **New Epic option:** `board.sh new-epic` opens the tracking *issue*, but GitHub's
  CLI can't add a new **option** to the `Epic` single-select field. After creating a
  brand-new epic, tell the user to add the matching option in the board UI
  (Project settings → Epic field), then re-run `board.sh epic <feature> "<Name>"`.
- The built-in `Status` field is likewise not CLI-editable — that's *why* the
  pipeline lives in the dedicated `Readiness` field. Never try to set `Status`.

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

## Completeness sweep (the things ideas forget)
Edge cases · error & empty states · non-functional needs (perf, security,
accessibility, observability, scale) · migration/backfill · who operates it.
