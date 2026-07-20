# NajaEchoPortal Planning Model

How work is planned above the individual feature. The **board is the source of
truth for live state**; this file just defines the model. Individual specs live
in `specs/NNN-name/`.

## Hierarchy

Native GitHub **issue types**, linked by **sub-issues** (progress rolls up):

```
Theme      major capability area                 long-lived
  Epic     bounded initiative, one finish line    optional; only when 3+ features
    Feature  one specs/NNN- + branch + PR         (Spec Kit)
      Task   tasks.md item                        (Spec Kit)
Bug        defect, parented wherever it belongs
```

Default shape is **Theme → Feature flat**. Add an Epic only when an initiative is
several features driving to one outcome — don't create ceremony that isn't earned.

## Readiness pipeline

A **`Readiness`** custom field carries the pipeline (and the kanban columns). It
applies to **Features and Bugs only** — Themes and Epics are tracked by rollup %.

| Stage | Meaning |
|-------|---------|
| Idea | rough one-liner; may never happen |
| Shaped | problem + rough approach agreed; not yet a spec |
| Ready | clear enough to run `/speckit-specify` immediately |
| Spec'd | `spec.md` + `plan.md` exist; awaiting a build slot |
| Building | branch open, in development |
| In Review | PR open |
| Done | merged |

**Cadence:** keep ≥2 items in Ready and ≥1 in Spec'd so there's always shaped work
ahead of execution.

## Board scope

Present + future work only. Completed features and closed themes are **not**
backfilled — the `specs/NNN-` folders are their archive. A theme appears on the
board only once it has active or upcoming work.

## Where things live

- **Board — [Naja Echo Planning](https://github.com/orgs/Deceptively-Clever/projects/3)** (org project #3): all live state.
- **`epic-planning` skill:** how to shape a raw idea into board-ready work, plus
  the full board mechanics and `board.sh` command surface.
- **`specs/NNN-name/`:** the spec, plan, and tasks for each built feature.
