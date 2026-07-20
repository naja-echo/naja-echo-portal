---
name: epic-planning
description: >
  Interactively shape a raw idea into board-ready work on the "Naja Echo Planning"
  GitHub Project via a Socratic interview, then create/update its issue, set its
  issue type (Theme/Epic/Feature) and Readiness, and parent it correctly. USE WHEN:
  fleshing out an idea, planning or splitting an epic, grooming the backlog, or
  getting something ready for /speckit-specify. DO NOT USE FOR: writing a feature
  spec (use speckit-specify) or tasks.
---

# Epic & idea planning

Turn a fuzzy idea into a well-shaped item on the board through conversation — one
that's crisp enough to hand to `/speckit-specify`. This skill sits **upstream** of
Spec Kit: it produces work at readiness **Idea → Shaped → Ready**; Spec Kit takes
it from **Ready** onward.

## The planning model (what the board encodes)

Work is a hierarchy of native GitHub **issue types**, linked by **sub-issues** so
progress rolls up:

```
Theme      major capability area                 long-lived
  Epic     bounded initiative, one finish line    optional; only when 3+ features
    Feature  one specs/NNN- + branch + PR
      Task   tasks.md item
Bug        defect, parented wherever it belongs
```

- Default shape is **Theme → Feature flat**. Introduce an Epic only when an
  initiative is several features driving to one outcome — never for its own sake.
- **`Readiness`** (custom field) is the pipeline + kanban, and applies to
  **Features and Bugs only**. Themes/Epics are tracked by rollup %, not readiness.
- Board holds **present + future work only**; `specs/NNN-` folders are the archive.

`specs/ROADMAP.md` is a thin human-facing summary of this same model — it carries no
live state. Full mechanics and the `board.sh` surface are in
**[references/board.md](references/board.md)**.

## Golden rules

- **Interview, don't autocomplete.** Pull tacit knowledge out of the user with
  questions. Do not invent scope, acceptance criteria, or a theme/epic assignment
  the user never confirmed.
- **Small batches.** Ask 2–4 questions at a time (use `AskUserQuestion` for
  choices), then react to answers. Never dump a questionnaire.
- **One idea at a time.** If the user brings a tangle, name the pieces and shape
  them one by one.
- **Confirm before any write.** Board mutations happen only after showing a summary
  and getting a yes.

## Start of every session

1. Run `scripts/board.sh show` to see live issue types and the `Readiness` options.
2. Run `scripts/board.sh themes` to see which Themes/Epics already exist (so you
   parent into an existing one rather than inventing a duplicate).
3. Restate the user's idea back in one sentence and confirm you've got it before
   going deeper.

## Workflow

Move through these phases conversationally. Skip or compress phases that are
already settled; linger where the idea is thin. The technique bank in
**[references/board.md](references/board.md)** lists concrete questioning tools for
each phase — pull from it deliberately.

1. **Frame** — Why does this exist? Problem statement, who benefits, jobs-to-be-done.
   Refuse to design a solution until the problem is crisp. Identify which **Theme**
   it belongs to (existing, or a genuinely new capability area).
2. **Expand** — Generate breadth: assumptions, 5-whys, alternatives (SCAMPER),
   pre-mortem. Flag unvalidated + risky assumptions as candidate spikes.
3. **Shape** — Converge: a rough approach (one paragraph, not a design),
   Given/When/Then acceptance criteria, explicit **non-goals**, dependencies, risks.
4. **Decompose** *(only if epic-sized)* — Story-map into candidate features; sanity
   the split with INVEST; pick the walking-skeleton feature to pull toward Ready.
   If the initiative is genuinely several features toward one finish line, create an
   **Epic** to hold them; otherwise keep them flat under the Theme.
5. **Complete** — Run the completeness sweep (edge/error/empty states, NFRs,
   migration, operability). Surface gaps as open questions.
6. **Land it** — Assign a readiness level and write to the board (see below).

## Determining readiness

Set a Feature's/Bug's `Readiness` to the honest current state — don't inflate it:

- **Idea** — captured, but problem/approach not yet agreed. Fine to stop here.
- **Shaped** — problem + rough approach agreed. No spec yet.
- **Ready** — approach, dependencies, and Given/When/Then acceptance criteria are
  all settled; `/speckit-specify` could start immediately. This is the bar; if
  acceptance criteria are still fuzzy, it's Shaped, not Ready.

(Themes and Epics get no Readiness — leave the field empty on them.)

## Landing it on the board (confirm first)

Compose a plain-language summary and get an explicit yes before running anything:

> Create **Feature** "Low-stock alerts for commodities" · parent Theme **#16
> Warehouse & Inventory** · Readiness **Shaped** · blocked-by **#18** — proceed?

Then:

- **New Feature/Bug:** one shot — create it typed, place it, set readiness + parent:
  `board.sh add <Type> "<title>" --readiness "<level>" --parent <n>`.
- **New Theme:** `board.sh new-theme "<Name>"` — creates a `type:Theme` issue and
  puts it on the board (no readiness).
- **New Epic** *(only when earned):* `board.sh new-epic "<Name>"` then
  `board.sh parent <epic> <theme>` to nest it under its Theme.
- **Parent / attach:** `board.sh parent <child> <parent>` (sub-issue, so progress
  rolls up).
- **Cross-theme dependency:** `board.sh blocked-by <issue> <blocker>`.
- **Just moving an existing item:** `board.sh readiness <issue> "<level>"`.

After landing, if the board now has <2 items in Ready or <1 in Spec'd, mention it —
that's the cue to shape the next thing rather than stopping cold.

## Handoff

When an item reaches **Ready**, the next step is `/speckit-specify` against it — say
so explicitly so the user knows the baton has passed. Don't write the spec here.

For field IDs, the full command surface, the technique bank, and CLI limits, see
**[references/board.md](references/board.md)**.
