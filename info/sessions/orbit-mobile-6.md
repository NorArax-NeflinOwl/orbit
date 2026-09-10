# Session handover: orbit-mobile-6

Previous session: orbit-mobile-5 (the design's last per-screen corrections, the whole day, folders)
Date: 2026-09-10

## Branch and PR

- Branch: `claude/mobile-handover-6`, cut from `origin/Coding` - it carries this file and nothing else.
- Open PR: **none of this session's own.** #271 was merged into `Coding` while the session was ending, so
  the branch it was on (`claude/orbit-maui-project-1f021a`) is finished; a commit added to it now would be
  refused by the pre-commit hook and would reach nothing. Two PRs are open and neither is this session's:
  **#273** (the integration PR, `Coding` → `main`, kept by a workflow) and **#272** (a Web session's).
  One slot is free, so the new session may open its own.
- Uncommitted changes: none.

## Goal of the work

Finish the Classical design's per-screen corrections on the Android head, then build what the design
asked for that was not a correction at all: folders.

## Done

`info/android-design-deltas.md` is the map, and it is now **reconciled rather than merely marked off** -
nine of its entries turned out to be stale, wrong about the design, or deliberate departures. Read it
before believing that anything on it is owed.

Eighteen commits on #271, in three groups.

**Per-screen corrections, all walked on the device.** The note editor's arrows (up and down between
lines, keeping the column; up from the first line reaches the note's name). The dashboard's counter
strip on one baseline with the colour on the dot. A note's "copy" tag on the title's line as a chip.
The theme as a strip of three rather than a `Picker`. An inventory's "3 LOW" chip. A task list's
progress bar. The conversation's header (circle, name, "Offline · end-to-end encrypted") and its day
dividers. The event screen's accent bar, its line of location links and its two `When` boxes. The task
list's menu in three groups. The calendar list's date column, colour bar and title-over-place.

**The day view, asked for outright mid-session.** Midnight to midnight, all twenty-four hours, opening
scrolled to the day's first thing; under it only what has no hour - all-day events *and* deadlines,
which the calendar files by date alone. The list of the whole period is gone from that view.

**Folders on the phone**, end to end and offline: `LocalFolder`, `FolderId` on notes and lists, a local
migration, `FoldersClient`, `FolderSynchronizer`, `OutboxOperation.File`, and screens on Notes, Tasks
and the Dashboard. `FolderKey`, `FolderPlacement` and `FolderPages` moved from `Orbit.Web.Services` into
`Orbit.Core.Folders`.

Verified end to end on 2026-09-10 against the local stack: a folder made on the phone reached the server
(`POST /api/folders` 201), came back on the pull, and a note filed into it landed as
`MoveNoteToFolderCommand` with the row visible in Postgres.

Documentation: `info/functionality.md` (Folders), `info/android-design-deltas.md`, `info/future-plan.md`,
and **three diagrams** - `uml/components.md` (why those types went to Core), `uml/flows.md` (folders as
the one exception to the change feed and to tombstones), `uml/database.md` (a new section on the phone's
own store: two ids on everything, and `FolderId` locally meaning a *local* id). All 17 diagrams parse.

## Still failing / unknown

- **Two things are built and tested but never seen on a device**, both for want of data rather than
  code: the **"copy" chip** on a note (a note is a copy only after somebody else's has been taken for
  offline editing) and the **78% bubble cap** (needs a message long enough to reach it, and nobody to
  exchange one with).
- **A create that gets a 4xx escapes the outbox's own rules.** `EnsureSuccessStatusCode` throws
  something non-retryable, so `OutboxReplay` does not catch it, the entry stays queued, is re-sent on
  every sync, and nothing says so. This is the **existing** pattern for notes, lists, events and
  inventories, not something folders introduced - it is why a stale `orbit-api` answering 404 to
  `POST /api/folders` looked like nothing happening at all. Worth its own task; deliberately not fixed
  in passing.
- **Renaming a folder has no screen.** The server, `FoldersClient.RenameAsync` and
  `LocalFolderRepository.RenameAsync` all exist and are unused. The app has no text-prompt dialog and
  Android's own would sit badly beside Orbit's panel; the same unfolding row "New folder" uses would do it.
- **Hiding a folder on the dashboard** is in the browser's tab menu and not on the phone.

## Rejected approaches (do not retry)

- **Do not use MAUI's `ProgressBar` for the design's bars.** It draws Android's, whose track is a
  mid-grey the platform picks and `BackgroundColor` does not reach - three points of `#686565` reads as
  a rule between two things. Two `BoxView`s, the filled one in a Grid whose columns are the fraction in
  **star units** (`ProgressColumnsConverter`).
- **Do not compute a bar's fill from a measured width.** A `MultiBinding` of the track's own `Width` and
  the fraction drew no filled half at all, silently. Star units need no measurement.
- **Do not `rm -rf files/.__override__` after an ordinary `-t:Install`.** That directory *is* the code
  under fast deployment; the app then aborts with "No assemblies found... Assuming this is part of Fast
  Deployment" and it reads exactly like a crash. That advice applies only to a hand-installed
  `-p:EmbedAssembliesIntoApk=true` build.
- **Do not build a back control anywhere**, including the `‹` the design draws in the conversation's
  header. The one ✕ that stays is the event form's cancel FAB, which is a form's cancel rather than a
  back control.
- **Do not add the contact row's last message and its time on the phone alone.** Orbit.Web's own
  `PersonRow` has neither half, and a preview means decrypting one message per contact. It is written up
  in `future-plan.md` as work for both clients.
- **Do not put `TextAlignment="Justify"` on a Label.** MAUI has Start, Center and End and nothing else,
  which is why the design's justified note preview is recorded as unbuildable.

## Next step

Open a PR for this handover against `Coding` (one slot is free), then pick from "Still failing" - the
**4xx-create escape** is the one with teeth, since it silently wedges a queue entry forever and affects
every entity type, not just folders.

## Environment facts confirmed this session

Everything in `orbit-mobile-5.md` still holds. Plus:

- **`dotnet build -t:Install` does not re-push assemblies on a second run with unchanged sources.**
  Delete `obj/Debug/net10.0-android/upload.flag` and `.../devices.cache` first, or the emulator keeps
  running the previous build. Check it landed with
  `adb shell run-as com.orbitmaui.android ls files/.__override__/arm64-v8a`.
- **Give a fast-deployed launch 25-30 seconds before touching the screen.** Taps aimed at the splash
  queue up and Android raises "Orbit isn't responding"; *Wait* recovers it, and a clean force-stop and
  relaunch always did. Twice this looked like a hang caused by the change under test and was not.
- **The local `orbit-api` container was weeks behind** and answered **404 to `POST /api/folders`** while
  `OP_FOLDERS` existed in the database - so the table being there proves nothing about the endpoints.
  `docker compose -p orbit up -d --no-deps --build orbit-api` from the worktree fixed it. See
  [[local-database-must-match-deployed]] in the session memory for the general shape of this.
- **The emulator's app is signed in as `test`, not `ola.n`.** The session before this one lost the
  `ola.n` session mid-walk and could not restore it - entering a password is not something the assistant
  does - and the user signed in as `test`. That account has the Debug permission but **not** the Google
  extras, so "Open in Google Maps" and "Directions" do not appear on an event.
- **Left on the emulator, and harmless:** a folder **"Work"** on the notes (synced, with the note
  "Shopping" filed into it), a "Dentist" event on 10 September carrying "Marszałkowska, Warszawa,
  Poland", a contact "Chat Partner" seeded into `OL_CONTACTS` in the local Postgres with two messages
  between them, one of which was backdated two days to make the chat's day dividers visible.
- **A stale `uiautomator` dump and a stale `screencap` both still lie**, and the drawer is the worst
  place for it: a tap at the Notes entry's coordinates lands on nothing if the drawer has not finished
  opening, and the next dump shows the dashboard as though nothing was pressed. Dump the drawer, read
  the entry's real bounds, then tap.
