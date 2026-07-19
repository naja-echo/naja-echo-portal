---
name: epic-planning
description: >
  Interactively shape a raw idea or epic into board-ready work on the "Naja Echo
  Planning" GitHub Project via a Socratic interview, then create/update its issue
  and set Readiness + Epic. USE WHEN: fleshing out an idea, planning or splitting
  an epic, grooming the backlog, or getting something ready for /speckit-specify.
  DO NOT USE FOR: writing a feature spec (use speckit-specify) or tasks.
---

# Epic & idea planning

Turn a fuzzy idea into a well-shaped item on the board through conversation — one
that's crisp enough to hand to `/speckit-specify`. This skill sits **upstream** of
Spec Kit: it produces work at readiness **Idea → Shaped → Ready**; Spec Kit takes
it from **Ready** onward.

## Golden rules

- **Interview, don't autocomplete.** Pull tacit knowledge out of the user with
  questions. Do not invent scope, acceptance criteria, or an epic assignment the
  user never confirmed.
- **Small batches.** Ask 2–4 questions at a time (use `AskUserQuestion` for
  choices), then react to answers. Never dump a questionnaire.
- **One idea at a time.** If the user brings a tangle, name the pieces and shape
  them one by one.
- **Confirm before any write.** Board mutations happen only after showing a summary
  and getting a yes.
- **Board only.** Never edit `specs/ROADMAP.md` — it's a hand-maintained narrative.
  Read it for context; leave it alone.

## Start of every session

1. Read `specs/ROADMAP.md` for current epics, their status, and the cadence rules
   (keep ≥2 items in Ready, ≥1 in Spec'd).
2. Run `scripts/board.sh show` to see the live `Epic` and `Readiness` option names.
3. Restate the user's idea back in one sentence and confirm you've got it before
   going deeper.

## Workflow

Move through these phases conversationally. Skip or compress phases that are
already settled; linger where the idea is thin. The technique bank in
**[references/board.md](references/board.md)** lists concrete questioning tools for
each phase — pull from it deliberately.

1. **Frame** — Why does this exist? Problem statement, who benefits, jobs-to-be-done.
   Refuse to design a solution until the problem is crisp. Identify which **Epic** it
   belongs to (existing option, or a genuinely new epic).
2. **Expand** — Generate breadth: assumptions, 5-whys, alternatives (SCAMPER),
   pre-mortem. Flag unvalidated + risky assumptions as candidate spikes.
3. **Shape** — Converge: a rough approach (one paragraph, not a design),
   Given/When/Then acceptance criteria, explicit **non-goals**, dependencies, risks.
4. **Decompose** *(only if epic-sized)* — Story-map into candidate features; sanity
   the split with INVEST; pick the walking-skeleton feature to pull toward Ready.
5. **Complete** — Run the completeness sweep (edge/error/empty states, NFRs,
   migration, operability). Surface gaps as open questions.
6. **Land it** — Assign a readiness level and write to the board (see below).

## Determining readiness

Set the item's `Readiness` to the honest current state — don't inflate it:

- **Idea** — captured, but problem/approach not yet agreed. Fine to stop here.
- **Shaped** — problem + rough approach agreed. No spec yet.
- **Ready** — approach, dependencies, and Given/When/Then acceptance criteria are
  all settled; `/speckit-specify` could start immediately. This is the bar; if
  acceptance criteria are still fuzzy, it's Shaped, not Ready.

## Landing it on the board (confirm first)

Compose a plain-language summary and get an explicit yes before running anything:

> Create issue "Low-stock alerts for commodities" · Epic **Warehouse & Inventory** ·
> Readiness **Shaped** · blocked-by **#18** — proceed?

Then:

- **New feature/idea issue:** create it, then
  `board.sh place <issue> --epic "<Epic>" --readiness "<level>"`.
- **New epic:** `board.sh new-epic "<Name>"` opens the `[Epic] Name` tracking issue.
  If it's a brand-new capability area, the `Epic` field option must be added in the
  board UI first (CLI can't add select options — see references/board.md), then
  `board.sh epic <feature> "<Name>"`.
- **Attach features to their epic:** `board.sh attach <feature> <epic>` (sub-issue,
  so epic progress rolls up).
- **Cross-epic dependency:** `board.sh blocked-by <issue> <blocker>`.
- **Just moving an existing item:** `board.sh readiness <issue> "<level>"`.

After landing, if the board now has <2 items in Ready or <1 in Spec'd, mention it —
that's the cue to shape the next thing rather than stopping cold.

## Handoff

When an item reaches **Ready**, the next step is `/speckit-specify` against it — say
so explicitly so the user knows the baton has passed. Don't write the spec here.

For field IDs, the full command surface, the technique bank, and CLI limits, see
**[references/board.md](references/board.md)**.
