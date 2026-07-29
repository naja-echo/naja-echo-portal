# Feature Specification: Crafting Blueprint Import

**Feature Branch**: `020-crafting-blueprint-import`

**Created**: 2026-07-17

**Status**: Draft

**Input**: User description: "I want to upload a json file that contains the crafting requirements for various items in the game. These will be stored as part of the sc schema... This will go with the import pages under the admin section and the page should show a list of all known blueprints by name and allow to search by name." Plus clarifications: the file is a complete dataset document (version, meta, dismantle config, properties catalog, resources, items, blueprints); each blueprint's `guid` maps to the UUID in the existing `sc` items table; the option `resourceName`/`itemName` values are a separate crafting-material concept unrelated to the existing item/commodity catalogs. See [`contracts/star-citizen-blueprints.schema.json`](./contracts/star-citizen-blueprints.schema.json) for the authoritative file shape.

## Clarifications

### Session 2026-07-17

- Q: Expected dataset scale (blueprint count) per uploaded file? → A: Medium — ~500 to ~5,000 blueprints (with comparable counts of resources, items, and properties).
- Q: Upload processing model — synchronous request or background job? → A: Synchronous — process within the upload request and return the result summary in the response (matches existing import pages).
- Q: Import atomicity when an unexpected failure occurs partway through? → A: Atomic — reference data refresh plus all valid blueprint upserts commit in a single transaction; an unexpected error rolls the whole upload back (prior data unchanged); rejected malformed entries are skipped, not treated as transaction failures.

### Session 2026-07-18

- Q: Are crafting material names cross-linked to the existing catalogs? → A: Yes (supersedes the earlier "unrelated" stance): on import, each `resources`/`items` name is looked up by exact name — commodities catalog first, then items catalog — to resolve the catalog UUID; the resolved UUID and its source table are stored with the material and with each slot option that references it.
- Q: Lookup match rules? → A: Case-insensitive exact name match, excluding soft-deleted catalog rows; a commodity match whose UUID is empty falls through to the items catalog; a name matching nothing (or only rows without UUIDs) is stored unlinked (null UUID/source) and surfaced as a warning — never a rejection.
- Q: How is the nested structure stored, given future queries like "all blueprints using material X" and "total materials to craft items A, B, C"? → A: The blueprint record keeps its full nested structure; additionally tiers are normalized into their own records and each tier's slot options are flattened into one queryable record per option (slot name, option variant, quantity, min quality, material name, resolved catalog UUID + source). Modifiers remain part of the full nested structure only. The queries themselves are out of scope for v1; storage must support them.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Upload a Blueprint Dataset File (Priority: P1)

An admin opens the Data Import area, navigates to the Blueprints section, and selects a JSON dataset file exported from the game. The file contains a dataset version, summary totals, a dismantle configuration, a properties catalog, a resources list, an items list, and the list of crafting blueprints. They trigger the upload. The system validates the file against the expected dataset shape, stores the blueprints (with their full tier → slot → option/modifier structure) and all reference data, and shows a result summary reporting the dataset version and how many blueprints, resources, items, and properties were read, inserted, updated, and rejected — plus any reference data replaced.

**Why this priority**: Without ingesting the dataset there is nothing to view, search, or reference. This is the foundational capability and the entry point for the whole feature.

**Independent Test**: Can be fully tested by selecting a sample dataset file, triggering the upload, and confirming the result summary reports the correct counts and dataset version and that the imported blueprints and reference data are afterward retrievable.

**Acceptance Scenarios**:

1. **Given** an admin is on the Blueprints import section, **When** they select a valid dataset JSON file and trigger the upload, **Then** the blueprints and all reference data (properties, resources, items, dismantle config) are stored and a result summary shows the dataset version and counts read/inserted/updated/rejected.
2. **Given** a blueprint carries tiers with slots, each slot's resource/item options and property modifiers, **When** the file is imported, **Then** the full nested structure (tiers → slots → options + modifiers) is preserved and retrievable for that blueprint.
3. **Given** an upload is in progress, **When** the admin attempts another upload, **Then** the upload action is disabled until the current operation completes.
4. **Given** an upload completes, **When** the admin views the result, **Then** the summary includes per-collection counts and, for any rejected blueprint, an identifying value (product name and/or guid) and the reason it was rejected.

---

### User Story 2 - Browse and Search Blueprints by Name (Priority: P1)

An admin viewing the Blueprints section sees a list of all blueprints currently known to the system, identified by name. Because a blueprint's product name can be absent, the displayed name falls back to the linked item's name, then the blueprint tag, then its guid. The admin types part of a name into a search box and the list narrows to matching blueprints so they can quickly confirm what has been imported.

**Why this priority**: Confirming what was imported — and finding a specific blueprint among many — is the primary way an admin verifies the import and locates a record. It delivers user-visible value on top of US1.

**Independent Test**: Can be fully tested by seeding known blueprints (some with null product names), opening the Blueprints section, and verifying the full list is shown with correct fallback names and that typing a partial name filters the list to matching blueprints.

**Acceptance Scenarios**:

1. **Given** blueprints have been imported, **When** an admin opens the Blueprints section, **Then** they see a list of all known blueprints identified by name.
2. **Given** a blueprint whose product name is null, **When** the list is shown, **Then** its name falls back to the linked item's name, else the tag, else the guid.
3. **Given** the blueprint list is shown, **When** the admin types a partial name into the search box, **Then** the list narrows to blueprints whose displayed name matches the search term (case-insensitive, substring).
4. **Given** the search term matches no blueprint, **When** the filtered list is shown, **Then** an empty state indicates no blueprints match.
5. **Given** no blueprints have been imported yet, **When** an admin opens the Blueprints section, **Then** an empty state explains that no blueprints exist and directs them to upload a file.

---

### User Story 3 - Access Control (Priority: P1)

The Blueprints import and listing surface, and all associated actions, are admin-only, using the same authorization behavior as the existing admin import pages.

**Why this priority**: Authorization is non-negotiable. Blueprint upload writes game data and must be restricted before any other story is considered complete.

**Independent Test**: Can be tested by logging in as a non-admin and attempting to access the Blueprints section or invoke the upload action, confirming access is denied.

**Acceptance Scenarios**:

1. **Given** an authenticated admin, **When** they open the Data Import area, **Then** the Blueprints section and its actions are accessible.
2. **Given** a non-admin authenticated user, **When** they attempt to access the Blueprints section or the upload action, **Then** they are denied using the same authorization behavior as the existing import pages.

---

### User Story 4 - Re-Upload to Update the Dataset (Priority: P2)

An admin uploads an updated dataset file after the game's crafting data has changed. Blueprints already known to the system (matched by `guid`) are updated in place with the latest values; blueprints new to the system are added. The reference data (properties catalog, resources, items, dismantle configuration) is refreshed to reflect the newly uploaded dataset. The admin sees, in the result summary, the new dataset version and how many blueprints were updated versus newly inserted.

**Why this priority**: Blueprint data changes across game versions; keeping it current is a repeatable maintenance action that builds on US1 but is not required for the first successful import.

**Independent Test**: Can be tested by importing a file, then importing a second file that contains the same blueprint with changed values plus one new blueprint and updated reference lists, and confirming the first is updated, the second inserted, and the reference data refreshed per the result counts.

**Acceptance Scenarios**:

1. **Given** a blueprint with a given `guid` already exists, **When** a file containing that same `guid` is imported, **Then** the stored blueprint is updated with the latest values (including its full tier/slot/option/modifier structure) and counted as updated.
2. **Given** a `guid` not yet known to the system, **When** a file containing it is imported, **Then** a new blueprint is inserted and counted as inserted.
3. **Given** a re-import replaces a blueprint's nested structure, **When** the update completes, **Then** the blueprint reflects only the tiers, slots, options, and modifiers from the latest file (no stale nested records remain).
4. **Given** an updated dataset with changed reference data, **When** the file is imported, **Then** the stored properties catalog, resources, items, and dismantle configuration reflect the latest uploaded dataset.

---

### User Story 5 - Validation and Partial-Failure Handling (Priority: P2)

An admin uploads a file where some blueprints are malformed (missing required fields, a `guid` that is not a valid UUID, duplicate `guid` within the file, or an option that matches neither the resource nor the item shape). The system imports the valid blueprints, rejects the invalid ones without failing the whole upload, and reports which entries were rejected and why. If the top-level dataset itself is not valid or does not match the expected shape, the entire upload is rejected and nothing is stored.

**Why this priority**: Real exported files can contain bad or partial records; a single bad blueprint should not block an entire import. This hardens US1 but is secondary to getting a clean import working.

**Independent Test**: Can be tested by uploading a file mixing valid and invalid blueprint entries (and separately an invalid top-level document) and confirming valid blueprints are stored while invalid ones are reported, and that an invalid document is rejected wholesale.

**Acceptance Scenarios**:

1. **Given** a file whose blueprint list mixes valid and invalid entries, **When** it is imported, **Then** valid entries are stored and invalid entries are rejected and reported without aborting the import.
2. **Given** a file that is not valid JSON or is missing a required top-level section (version, meta, dismantle, properties, resources, items, or blueprints), **When** it is uploaded, **Then** the upload is rejected with a clear error and no data is stored.
3. **Given** a blueprint entry missing a required field or with a `guid` that is not a valid UUID, **When** the file is imported, **Then** that entry is rejected and reported while other entries continue to import.
4. **Given** the same blueprint `guid` appears more than once within a single file, **When** the file is imported, **Then** the duplication is handled deterministically and reported, rather than producing conflicting stored records.
5. **Given** the uploaded meta totals do not match the actual number of parsed blueprints/resources/items, **When** the import completes, **Then** the mismatch is surfaced in the result summary as a warning (it does not by itself fail the import).

---

### Edge Cases

- What happens when the uploaded file has an empty blueprint list (but valid top-level structure)? The upload completes successfully; reference data is still stored/refreshed and blueprint counts are zero.
- What happens when a blueprint's `guid` has no matching row in the `sc` items table? The blueprint is stored anyway and left unlinked; the missing link never blocks import and is not treated as an error.
- What happens when a slot option references a `resourceName` or `itemName` not present in the dataset's own resources/items lists? The name is stored as provided and still goes through the FR-009 catalog lookup on its own; option names are not required to be validated against the lists in v1 (a warning may be surfaced but does not fail the import).
- What happens when a modifier references a `propertyKey` not present in the properties catalog? The modifier is stored as provided; the missing catalog entry does not fail the import.
- What happens when a slot's `modifiers` is null versus an empty/absent array? Null modifiers are treated as "no modifiers" and stored as an empty set.
- What happens when optional blueprint fields (`isDefault`, `suggestedName`, `suggestedProductEntityClass`, `cigDataError`) are present or absent? Present values are preserved; absent values are treated as unset without error.
- What happens when two admins attempt uploads at the same time? Only one upload runs at a time; the second is blocked until the first completes.
- What happens when the file exceeds a reasonable size or contains a very large number of blueprints? The system imports what it can within its limits; files above the assumed maximum size are rejected with a clear message.

## Requirements *(mandatory)*

### Functional Requirements

#### Access & Upload

- **FR-001**: The Blueprints section MUST be reachable from the existing admin Data Import area and MUST be restricted to authorized admins using the same authorization behavior as the existing import pages.
- **FR-002**: The system MUST allow an admin to upload a single JSON dataset file whose top-level structure contains `version`, `meta`, `dismantle`, `properties`, `resources`, `items`, and `blueprints` sections.
- **FR-003**: Only one blueprint upload may run at a time; the upload action MUST be disabled while an upload is in progress.
- **FR-003a**: The upload MUST be processed synchronously within the upload request, returning the result summary in the response (no background job); the UI shows a loading state until the response arrives.
- **FR-004**: The system MUST reject the entire upload, storing no data, when the file is not valid JSON or is missing any required top-level section.

#### Parsing & Storage

- **FR-005**: The system MUST parse each blueprint and its full nested structure: the blueprint's tiers, each tier's `craftTimeSeconds` and slots, each slot's `name`, options, and modifiers, each option's variant (resource or item) with its `quantity`, `minQuality`, and referenced `resourceName`/`itemName`, and each modifier's quality range, start/end modifier values, `propertyName`, `propertyKey`, and optional `additive` flag.
- **FR-006**: The system MUST preserve each blueprint's descriptive fields — `tag`, `productEntityClass`, `gear`, `type`, `subtype`, `productName`, `manufacturer` — and its optional fields `isDefault`, `suggestedName`, `suggestedProductEntityClass`, and `cigDataError`, treating nullable/absent values as unset rather than errors.
- **FR-007**: The system MUST store a slot's `modifiers` value of null as "no modifiers" (an empty set), distinct from a populated modifier list.
- **FR-008**: The system MUST store the dataset's reference data as first-class persisted data: the properties catalog (each `propertyKey` → `name`, `unit`, `category`, and optional `nameOverrides`), the resources name list, the items name list, and the dismantle configuration (`efficiency`, `dismantleTimeSeconds`, `blacklistedResources`, `blacklistedEntityClasses`).
- **FR-009**: During import, the system MUST resolve each crafting `resources` and `items` name against the existing catalogs by exact name match (case-insensitive, excluding soft-deleted rows) — checking the commodities catalog first and, when no commodity match with a UUID exists, the items catalog — and MUST store the resolved catalog UUID and its source (commodity or item) with the material; a name matching nothing is stored unlinked (null UUID and source) and reported as a warning, never a rejection.
- **FR-009a**: Each stored slot option MUST carry, in addition to its `resourceName`/`itemName`, the resolved catalog UUID and source from FR-009 (null when unmatched), so that material-based queries (e.g., all blueprints using a given material, aggregate material requirements) can be served from stored data.
- **FR-009b**: In addition to the blueprint's full nested structure, the system MUST store each blueprint's tiers as individually queryable records (tier order, craft time) and each tier's slot options as one flat queryable record per option (slot name, option variant, quantity, min quality, material name, resolved UUID + source). These records MUST be rebuilt from the latest file whenever a blueprint is inserted or updated. Modifiers remain preserved in the full nested structure only.
- **FR-010**: The system MUST record the dataset `version` with the imported data and surface it in the result summary.
- **FR-011**: All imported blueprint and reference data MUST be stored as part of the `sc` schema alongside other imported game catalog data.

#### Identity & Linking

- **FR-012**: The system MUST use the blueprint `guid` as the stable identity for a stored blueprint.
- **FR-013**: On import, a blueprint whose `guid` already exists MUST be updated in place with the latest values; a blueprint whose `guid` is new MUST be inserted.
- **FR-014**: When an existing blueprint is updated, its stored nested structure (tiers, slots, options, modifiers) MUST reflect only the contents of the latest file, leaving no stale nested records from a prior import.
- **FR-015**: The system MUST associate a blueprint with the existing `sc` items table when a row with a UUID equal to the blueprint `guid` exists, and MUST store the blueprint unlinked otherwise; a missing item link MUST NOT reject or block the import.
- **FR-016**: On re-upload, the stored reference data (properties catalog, resources, items, dismantle configuration) MUST be refreshed to reflect the newly uploaded dataset.

#### Validation & Result

- **FR-017**: The system MUST validate each blueprint and reject entries that are missing a required field, have a `guid` that is not a valid UUID, or have an option matching neither the resource nor the item variant — without aborting the import of remaining valid entries.
- **FR-018**: The system MUST handle a blueprint `guid` appearing more than once within a single file deterministically and MUST report the duplication rather than storing conflicting records.
- **FR-019**: After an upload completes, the system MUST display a result summary including the dataset version and, per collection (blueprints, resources, items, properties), counts of records read, inserted, updated, and rejected, plus a note when reference data was replaced.
- **FR-020**: For each rejected blueprint, the result summary MUST include an identifying value (product name and/or `guid`) and the reason for rejection.
- **FR-021**: The system MUST compare the dataset `meta` totals against the actual parsed counts and surface any mismatch in the result summary as a warning that does not by itself fail the import.
- **FR-021a**: A successful upload's reference-data refresh and all valid blueprint upserts MUST commit as a single atomic transaction. An unexpected failure partway through MUST roll the entire upload back, leaving previously stored data unchanged. Skipping/rejecting malformed blueprint entries is a normal outcome and MUST NOT trigger a rollback of the valid entries.

#### Listing & Search

- **FR-022**: The system MUST provide a listing of all known blueprints identified by name, where the displayed name is `productName` when present, otherwise the linked item's name, otherwise the blueprint `tag`, otherwise the `guid`.
- **FR-023**: The blueprint listing MUST support a case-insensitive substring text search over the displayed name, narrowing the list to matching blueprints.
- **FR-024**: When no blueprints exist, the listing MUST show an empty state directing the admin to upload a file; when a search matches nothing, the listing MUST show an empty state indicating no matches.

### Key Entities

- **Blueprint Dataset**: One uploaded snapshot. Attributes: `version` (semantic version string), `meta` totals (`totalBlueprints`, `totalProducts`, `totalResources`, `totalItems`). Wraps the reference data and blueprints below.
- **Blueprint**: The crafting requirements for one craftable game product. Stable identity is `guid`, which also links to the existing `sc` items table UUID when a match exists. Descriptive attributes: `tag`, `productEntityClass` (UUID), `gear`, `type`, `subtype`, `productName`, `manufacturer`. Optional: `isDefault`, `suggestedName`, `suggestedProductEntityClass` (UUID), `cigDataError`. Contains an ordered set of tiers. Belongs to the `sc` schema.
- **Tier**: One crafting tier of a blueprint. Attributes: `craftTimeSeconds`. Contains a set of slots.
- **Slot**: A named component slot within a tier. Attributes: `name`. Contains a set of options and an optional set of modifiers (null = none).
- **Slot Option**: One way to satisfy a slot, in one of two variants — **Resource** (`type = "resource"`, `quantity`, `minQuality`, `resourceName`) or **Item** (`type = "item"`, `quantity`, `minQuality`, `itemName`, with modifiers always null). Stored both within the blueprint's nested structure and as a flat queryable record carrying the resolved catalog UUID + source (null when unmatched).
- **Modifier**: A property adjustment applied over a quality range. Attributes: `startQuality`, `endQuality`, `modifierAtStart`, `modifierAtEnd`, `propertyName`, `propertyKey`, optional `additive`.
- **Property Definition**: A catalog entry keyed by `propertyKey`. Attributes: `name`, `unit` (nullable), `category`, optional `nameOverrides` map. Modifiers reference these by `propertyKey`.
- **Crafting Resource**: A named crafting resource from the dataset's `resources` list. Referenced by resource-option `resourceName`. Carries the catalog UUID + source resolved per FR-009 (null when unmatched).
- **Crafting Item**: A named crafting item from the dataset's `items` list. Referenced by item-option `itemName`. Carries the catalog UUID + source resolved per FR-009 (null when unmatched).
- **Dismantle Configuration**: Global dismantle settings for the dataset. Attributes: `efficiency`, `dismantleTimeSeconds`, `blacklistedResources` (each `guid`, `name`), `blacklistedEntityClasses` (each `guid`, `name`, `nameKey`).
- **Import Result**: A transient record describing the outcome of one upload: dataset version, per-collection counts (read, inserted, updated, rejected), reference-data-replaced note, meta mismatch warnings, and per-rejection detail. Not persisted between sessions in v1.

### Scale

- A single uploaded dataset is expected to contain roughly 500–5,000 blueprints, with comparable counts of resources, items, and properties. The system MUST handle datasets in this range; storage, upload processing, and the listing/search experience are designed for this order of magnitude (not tens of thousands).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An admin can upload a blueprint dataset file and see a complete result summary — including dataset version and per-collection counts — within a single page interaction with no page reload.
- **SC-002**: Every valid blueprint in an uploaded file — including its full tier/slot/option/modifier structure — is stored and afterward retrievable, verified by the result counts and by locating the blueprint in the listing.
- **SC-003**: All dataset reference data (properties catalog, resources, items, dismantle configuration) is stored and refreshed on re-upload, verified by the result summary and by retrieving the stored reference data.
- **SC-004**: Re-uploading a file that changes an existing blueprint updates it in place (counted as updated) and adds any new blueprints (counted as inserted), with no duplicate or stale nested records remaining.
- **SC-005**: An upload containing a mix of valid and invalid blueprint entries stores all valid entries and reports every invalid entry with a reason, without aborting the import; an invalid top-level document is rejected wholesale with nothing stored.
- **SC-006**: A blueprint whose `guid` matches an `sc` items row is linked to that item, and a blueprint whose `guid` has no match is still stored (unlinked) — both verified without any import failure.
- **SC-007**: An admin can locate a specific blueprint by typing a partial name (including blueprints whose name is a fallback value) and having the list narrow to matches, within a single page interaction.
- **SC-008**: Non-admin users cannot access the Blueprints section or invoke the upload action; the denial matches the behavior already in place for the existing import pages.
- **SC-009**: Only one blueprint upload can be active at any moment; the UI prevents a second concurrent upload.
- **SC-010**: When an upload fails unexpectedly partway through, the previously stored dataset remains fully intact (no partial reference-data or blueprint changes persist), verified by comparing stored data before and after a forced mid-import failure.

## Assumptions

- The existing admin Data Import area already enforces admin-only access via a reusable pattern; the Blueprints section extends that pattern rather than reimplementing authorization.
- All imported data is stored in the application's own `sc` schema, consistent with other imported Star Citizen catalog data.
- The uploaded file is a complete dataset snapshot for a given `version`. Blueprints are upserted by `guid`; reference data (properties, resources, items, dismantle config) is refreshed to match the latest upload. Blueprints present in storage but absent from the latest upload are retained (not deleted) in v1 — removal/soft-deletion of absent blueprints is out of scope.
- The blueprint `guid` equals the `sc` items table UUID for the corresponding product; the link is stored when a matching item exists and is otherwise left empty, without failing the import.
- Crafting `resources` and `items` names are resolved against the existing commodity and item catalogs at import time (commodities first — FR-009); the stored UUID/source is a snapshot as of that import and is refreshed on the next blueprint upload, not when the catalogs themselves are re-imported. Unmatched names are expected and produce warnings, not failures. Material-based query features (blueprints-by-material, aggregate material requirements) are out of scope for v1; v1 only guarantees the storage supports them.
- Option `resourceName`/`itemName` and modifier `propertyKey` values are stored as provided; referential consistency against the dataset's own resources/items/properties lists is surfaced as warnings at most, not enforced as hard validation in v1.
- The listing is admin-facing (part of the import area). A member-facing crafting/blueprint browsing experience, and per-blueprint detail/recipe views, are out of scope for v1 unless later requested.
- The listing shows and searches blueprints by name only; additional filters (by gear, type, subtype, manufacturer) are out of scope for v1.
- Import result summaries are shown in the UI for the current session only and are not persisted as import history in v1.
- A reasonable maximum upload file size applies, consistent with limits used by the existing import surfaces; files above the limit are rejected with a clear message.
- The dataset `meta` totals are treated as an informational cross-check; a mismatch is reported as a warning and does not fail an otherwise valid import.
