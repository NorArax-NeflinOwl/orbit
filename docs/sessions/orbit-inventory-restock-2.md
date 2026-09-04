# Session handover: orbit-inventory-restock-2

Previous session: orbit-inventory-restock (unnamed at the time; this is the first handover in the series)
Date: 2026-09-04

## Branch and PR
- Branch: `chore/one-run-per-stage` (this handover is committed here; the branch carries the CI-minutes
  work that was already on it at the start, and has no PR of its own)
- Open PR: none inherited. Everything this session opened is merged - #101, #102, #103, #104, #106, #137.
  The two PRs open in the repository belong elsewhere: **#207** (`feat/orbit-web-2026-09-04`, another
  session's web work) and **#204** (the integration `Coding → main`). That leaves **one free slot** of
  the three - see `pr-workflow` before opening it.
- Uncommitted changes: none

## Goal of the work
Close the loop between a task list and the warehouse it is priced against, make the screens consistent
with each other, and stop Docker filling the laptop.

## Done
- **Rotatable permission codes** (#101). `IPermissionCodeRepository.SaveAsync` replaces add-if-absent,
  `PermissionCodeStore.RotateAsync` replaces one on purpose, startup still only fills in what is missing.
  SQL for reading and rotating by hand is in the git-ignored `notes/permissions.local.md`.
- **Generated inventory carries quantities** (#102). Minimum = what the tree calls for (repetition is
  quantity); `AddInventoryItemPosition` gives a shelf an order, dragging sets it in both editors, and the
  checklist gained "in list order / A to Z".
- **Dashboard menus and consistency** (#103). A menu for which parts of the page to show, a per-card
  filter (all / pinned / one priority), `ItemPriority` shared by notes, task lists and events
  (`AddNoteAndEventPriority`), the task pin moved to the card header, and group chats folded into one
  `ConversationList` on both chat screens.
- **A chat that will not open says so** (#104), instead of bouncing to Contacts in silence.
- **The restock round** (#106). `RestockTaskNaming` names the list after its warehouse and puts the
  quantity in each entry; crossing an errand off fills the shelf to its minimum; "Update stock levels"
  asks whether the whole round is done; `CompleteWorkCoveredByStock` crosses off what the shelf covers.
- **Docker housekeeping** (#137). `scripts/prune-docker-caches.sh` plus its fake-docker test and a
  LaunchAgent template.
- Verified with 986 automated tests, live scenario suites against the compose stack (17 assertions for
  the restock round alone), and browser checks of every screen that changed.

## Still failing / unknown
- **An established contact who has not unlocked `Contacts` is a 404**, so a conversation on the reader's
  own list cannot be opened. On Azure that is `himei_tores`. Ruled out: it is not a bug in the lookup -
  `UserVisibility` is doing what the permission model asks. The design question (should the gate apply to
  somebody you already talk to?) is written up in `info/future-plan.md`.
- **`FindableAmongAsync` is declared and never used**: the contact list is not filtered by visibility,
  which is why the list and the profile lookup disagree.
- **Dragging needs a mouse.** HTML5 drag-and-drop does not fire for touch, so a phone can read an
  arranged list but not arrange one - matters before `Orbit.Maui` reaches these screens.

## Rejected approaches (do not retry)
- **Rotating codes at startup.** Every restart would invalidate a code just after somebody was told it,
  and would undo a rotation done for security at the next redeploy. Startup fills in only what is missing.
- **Deleting `Permissions__Secret` / `permission-secret` from the Container App via `az`.** The commands
  are correct and documented in `info/azure-setup.md`, but this session's harness blocked the call. Ask
  the user to run them, or run them from an interactive terminal.
- **Naming a restock entry after the whole line.** Matching on the full text put a second copy on the
  list whenever a minimum changed; entries are matched on the product (`RestockTaskNaming.ProductIn`).
- **Leaving "Update stock levels" open when finishing a restock round.** The screen showed it ticked and
  the database did not; `RemindDaily` brings it back tomorrow either way.
- **A ternary of lambdas in a Razor `@onclick`** (`StaysOpen ? () => { } : Close`). It compiles and does
  the wrong thing silently - use a named method.
- **A plain attribute value for a `string` component parameter** (`Search="_chatSearch"`). Razor treats
  it as a literal; it renders the field's name. Prefix with `@`.

## Next step
Nothing is owed. If work continues on this area, the first candidate is the `Contacts` gate above:
decide whether being someone's established contact should survive their not holding the permission, and
either exempt that case in `GetUserByIdQueryHandler` or filter the contact list with
`FindableAmongAsync` so the two stop disagreeing.

## Environment facts confirmed this session
- Disk: 228 GB, was down to 51 GB free; Docker held 40 GB (27 GB of it build cache, 54 orphaned images).
  After cleaning: Docker 12 GB, 74 GB free. `Docker.raw` reports 228 GB but is sparse - measure with
  `du -sh ~/Library/Containers/com.docker.docker`.
- **The local database is a superset of the deployed one**: it carries `DiagnosticLogEntries`,
  `SyncTombstones` and `AddMobilePushTransport` from the mobile branch. The check is in
  `info/testing-and-running-locally.md`, "Keeping the local database honest". `GrantAdminAllPermissions`
  in that list is expected - the migration was deleted from the repository on purpose.
- Azure Postgres: `orbit-postgres-djgiwo.postgres.database.azure.com`, admin `orbitadmin`, connection
  string in the `orbit-db-connection-string` Container App secret; the firewall already allows this Mac.
- Permission unlock codes are rows: `SELECT "Permission", "Code" FROM "PermissionCodes";`. A code written
  by hand must be uppercase, and rotating several in one `UPDATE` can give them all the same value.
- Accounts on Azure: `admin` and `pudi` hold all four permissions; `himei_tores` and `ppudi7368` hold none.
- Local checking without HTTPS: publish `Orbit.Web` and serve it with a small proxying static server on
  8090 pointing `/api` at `http://127.0.0.1:8081`. The compose `orbit-web` redirects 8080 to 8443, and
  the browser pane will not take a self-signed certificate.
