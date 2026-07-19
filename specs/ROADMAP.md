# NajaEchoPortal Roadmap & Planning Model

This document is the durable source of truth for how work is planned above the
individual-feature level. Individual specs live in `specs/NNN-name/`; this file
describes the **epic** layer and the **backlog pipeline** that sits above them.

## Planning hierarchy

```
Epic        capability area spanning multiple features
  Feature   one specs/NNN-name/ folder + one branch + one PR   (Spec Kit)
    Task    tasks.md                                            (Spec Kit)
```

Spec Kit already owns Feature and Task. This roadmap adds the Epic layer and a
readiness pipeline so there is always shaped work queued ahead of execution.

## Where it lives

Board: **[Naja Echo Planning](https://github.com/orgs/Deceptively-Clever/projects/3)**
(org project #3). Two custom fields carry all the structure:

- **`Readiness`** (single-select) — the readiness pipeline; also the kanban
  columns. (GitHub's built-in `Status` field can't be edited via CLI, so the
  pipeline lives in this dedicated field — set the board to group by it.)
- **`Epic`** (single-select) — which capability area an item belongs to.

Mechanics:

- **Epic** = a `[Epic] Name` tracking issue. Feature issues are attached as
  native **sub-issues** (`gh issue edit <feature> --parent <epic>`), so epic
  progress updates automatically as features close.
- **Feature** = an issue with the `Epic` field set, linked to its `specs/NNN-`
  folder and its PR.
- **Cross-epic dependencies** = native **blocked-by** links
  (`gh issue edit <n> --add-blocked-by <m>`).

**Board scope rule:** the board holds **present + future work only**. Completed
features and closed epics are *not* backfilled as issues — the `specs/NNN-`
folders are their archive. An epic appears on the board only once it has active
or upcoming work.

## The readiness pipeline (`Readiness`)

| Status | Meaning |
|--------|---------|
| **Idea** | rough one-liner; may never happen |
| **Shaped** | problem + rough approach agreed; not yet a spec |
| **Ready** | clear enough to run `/speckit-specify` immediately |
| **Spec'd** | `spec.md` + `plan.md` exist; waiting for a build slot |
| **Building** | branch open; actively in development |
| **In Review** | PR open |
| **Done** | merged |

**Cadence to stay ahead:** keep **≥2 items in Ready** and **≥1 in Spec'd** at all
times. When a feature merges, pull the next Spec'd item rather than shaping from
cold. This is what removes the one-feature-at-a-time tunnel vision.

## Epics

| Epic | Status | Built features | Notes |
|------|--------|----------------|-------|
| **Identity & Access** | dormant | 001 discord-auth, 002 identity-refactor, 015 character-registration, 017 admin-users | Role sync will consume the Discord service |
| **App Shell / UX** | **closed** | 003 theme, 004 dashboard-shell, 005 theme-toggle | Foundational scaffolding; archive only |
| **Hangar & Fleet** | dormant | 006 ship-import, 007 fleet-view, 008 hangar-json-import | |
| **Warehouse & Inventory** | active | 009 item-import, 010 commodity-import, 011 item-inventory, 012 ship-components, 013 item-quality, 014 materials, 016 star-systems/stations, 018 cities/locations | Import features fold into the epic they feed |
| **Org Economy** | active | 019 loot-ledger | Contribution tracking + payouts + loot priority = one system |
| **Crafting** | dormant | 020 blueprint-import | Blueprint import folded in; start of the epic |
| **Discord Integration Service** | active | — | Enabling epic; separate deployable service |

> **Game-data imports** are grouped with the feature they serve, not a standalone
> "catalog" epic — item/commodity/location imports live under Warehouse, ship
> import under Hangar, blueprint import under Crafting.

### Discord Integration Service — an *enabling* epic

A separate deployable service that is the portal's single interface to Discord.
The portal always routes Discord interactions through it (role sync, list
channels, post messages). It delivers little standalone user value but **unblocks
features in other epics**:

- **Role sync** → *Identity & Access*
- **Post message to channel** → *Warehouse* (low-stock alerts), *Org Economy* (payout announcements)
- **List channels** → config UI wherever a feature targets a channel

Because it is a separate deployable, its implementation plan must record why it
sits outside the constitution's modular-monolith default (bot-token isolation,
rate-limit handling, independent restart). Discord *auth* (001) already exists
and is distinct — the service is the broader ongoing integration layer.

## Seeded backlog

Epic tracking issues: **#15** Discord Integration Service · **#16** Warehouse &
Inventory · **#17** Org Economy.

| # | Item | Epic | Readiness | Blocked by |
|---|------|------|-----------|------------|
| #18 | Discord service: core (auth + post-message) | Discord Integration | Shaped | — |
| #19 | Discord service: list channels | Discord Integration | Shaped | #18 |
| #20 | Discord service: role sync | Discord Integration | Shaped | #18 |
| #21 | Low-stock alerts (commodities) | Warehouse & Inventory | Shaped | #18 |
| #22 | Contribution→payout system | Org Economy | Idea | #18 *(announce)* |
| #23 | Mining/salvage split calc | Org Economy | Idea | — |
