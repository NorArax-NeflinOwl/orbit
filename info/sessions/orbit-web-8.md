# Session handover: orbit-web-8

Previous session: orbit-web-7 (worktree `android-windows-rebuild-2cb886`)
Date: 2026-09-09

## Branch and PR

- Branch: `feat/folders-per-page`, ten commits ahead of `origin/Coding`, all pushed.
- Open PR: **#266 — "[Web] Folders per page, duplicates, task completion and the map"**
  (`Coding` from `feat/folders-per-page`). The new session inherits it rather than opening its own;
  extend its description as work lands. The only other open PR is the integration one (#264), so one
  slot is free if this branch is merged and something genuinely unrelated turns up.
- Uncommitted changes: none. The working tree is clean and `origin/feat/folders-per-page` matches HEAD.

## Goal of the work

Seventeen separate items the user asked for in one sitting - all of them on the web client, one commit
each. Everything asked for is done; the list is closed unless the user reopens part of it.

## Done

Nine commits, oldest first:

1. **`Give every folder the page it is a tab on`** - `FolderScope` (`OP_F_SCOPE`, `Notes`/`Tasks`),
   `FolderPage`/`FolderPages` on the client, `FolderState.ChosenOn(page)`. Finished is the task lists'
   tab only. Filing beats finishing in `FolderPlacement`. Under Finished only All/Shared/Group are
   offered. Migration `FoldersBelongToOnePage` **backfills rather than defaulting**: a folder that held
   notes moves to the notes, and one that held both becomes two with the notes moved into the copy.
   Also drops a duplicate `["Menu"]` key from `PolishTranslations`, which the coverage test was already
   failing on before this branch existed.
2. **`Move a folded card's "nothing left" line into its footer`** - the row names an entry either way.
3. **`Let a list be unfinished with every entry ticked off`** - `TaskListCompletion`
   (`FromTheEntries`/`Finished`/`Unfinished`, `OP_T_COMPLETION`, replacing `OP_T_ISMARKEDCOMPLETED`),
   new `TaskListStatus.Incomplete` drawn as "Not finished". The box ticks itself once every entry is
   ticked. Migration adds the column, fills it from the old flag, and only then drops it.
4. **`Fill a banner notification's holes before showing it`** - the rule moved out of the bell's panel
   into `NotificationWording`, which `MainLayout`'s banner now reads too.
5. **`Give the map an eye per list and a day to show the past from`** - `PlacedPlan.IsDone`,
   `MapPinVisibility` (localStorage, per browser), and a "Show from" date above the plans list.
6. **`Say nothing about work somebody has already finished`** - the filter is in the query in
   `OverdueTaskNotificationRepository`, `DailyTaskReminderRepository` and `EventReminderRepository`.
7. **`Add Duplicate to notes, task lists, inventories and events`** - four
   `Duplicate*Command`/`Handler` pairs, `POST /api/{kind}/{id}/duplicate`, one `DuplicateRequest`,
   a `DuplicateLabel`/`OnDuplicate` slot on `ObjectMenu`. `Calendar.razor`'s hand-written event menu
   became an `ObjectMenu` in the same commit.
8. **`Account for a whole week the way a day is accounted for`** - plus the sticky-footer fix
   (`align-self: stretch` on `.main-content`).
9. **`Let the chat fill the height it is given rather than guess it`** -
   `.main-content-page:has(.chat-layout)`.

A tenth commit followed the user's closing answers: the two decisions written into
`info/future-plan.md`, and **a defect the third commit had introduced on the phone**. The Android
client's `TaskListView.Describe` ended in `_ => translations["Completed"]`, so the new `Incomplete`
status - every entry ticked off and the list still open - would have been drawn there as *done*, the
one thing it is most costly to be wrong about. Every status is named now and the catch-all is New,
which is the rule the web already followed.

Verified:

- `dotnet test Orbit.CI.slnf` — **3463 passed, 0 failed** (1270 API, 990 Web, 1203 Mobile).
- `dotnet build Orbit.CI.slnf -c Release` — clean, warnings-as-errors included.
- New tests: `MakingASecondOneTests` (duplicating, all four modules),
  `NothingIsAnnouncedAboutFinishedWorkTests` (the three notification queries, against real repositories
  on SQLite - an in-memory double cannot cover a filter that lives in a query),
  `NotificationWordingTests`, plus additions to `FolderTabsTests`, `FolderPlacementTests`, `TasksTests`,
  `MapPageTests`, `TaskEditorItemFormTests` and `MarkingAListFinishedTests`.
- The two CSS fixes were checked in the browser pane against a page reproducing the real
  `.app-shell`/`.app-body`/`.main-content`/`.chat-layout` rules, screenshotted with the fix and again
  with it stripped out. Both bugs were real: the footer floated mid-window on a short page, and the chat
  overflowed the viewport so the composer sat below the fold.
- `info/functionality.md` and `info/uml/database.md` updated in the same commits as the code.

## Still failing / unknown

- **Nothing has been run against a real Postgres or a browser running the actual app.** Both migrations
  are hand-written SQL over uppercase column names and were reasoned about rather than executed; the
  test suite runs migrations on SQLite only. The first thing worth doing on a real database is applying
  them and checking `OP_FOLDERS` and `OP_TASKS`.
- The `FoldersBelongToOnePage` backfill uses `gen_random_uuid()`, which is built in from PostgreSQL 13.
  Azure Flexible Server is well past that, but it has not been confirmed on the actual instance.

## Rejected approaches (do not retry)

- **Giving the dashboard its own folder scope.** Asked and answered by the user: it draws the notes'
  and the task lists' tabs together and makes none of its own. A third scope would need a second
  "folder" field on both editors or its tabs would always be empty.
- **Keeping `IsMarkedCompleted` as a `bool?` for the three-way answer.** The update request already uses
  null for "not provided" (see `UnchangedWhenNotProvidedTests`), so a nullable bool cannot tell "the
  entries decide" from "the caller said nothing". Hence the named enum stored as text.
- **Letting the server name a copy `"X (copy)"`.** It does not know the reader's language, and a sealed
  item's name is inside a payload it cannot open. The client names it; null means "keep the name".
- **Copying an event's guests, or an entry's `LinkedCalendarEventId`, into a duplicate.** The first
  would re-invite everybody to something nobody was told about; the second would make which entry owns
  an event a matter of iteration order in `CalendarEventDestination.RaisedBy`.
- **Emptying `_placed` when the map's plans eye is off.** That took the list off the page along with the
  pins, leaving nothing to press to get them back. The eye is applied in `MapPoints` instead.
- **A `file://` repro in the scratchpad directory.** The browser pane renders anything outside the
  project as a static snapshot and refuses both `javascript_tool` and screenshots on it. Put the repro
  in a gitignored directory *inside* the worktree - `src/Clients/Orbit.Web/obj/` works - and delete it
  afterwards.

## Next step

Nothing is outstanding, and the two questions that were open have been answered - see below. Wait for
the user to merge #266 into `Coding`. If they ask for more work, it goes on this same branch and PR.

## Decisions the user confirmed at the end of the session

Both were open questions when the work landed; neither is any longer, and neither needs revisiting.

- **"Weekend view" meant the week view.** What was built is what was wanted:
  `Calendar.IsAStretchToAccountFor` covers Day and Week. **Month and year stay filtered on purpose** -
  they are read to find something rather than to account for a stretch. Do not "finish the job" by
  adding them.
- **Calendar events will not get folders.** The Finished tab's old wording said it concerned tasks and
  events; it concerns task lists. `OP_EVENTS` has no folder column and is not getting one. Both are
  written down in `info/future-plan.md` under "Known scope cuts and rough edges", where they will be
  found by somebody who mistakes them for omissions.

## Environment facts confirmed this session

- `origin/Coding` was **ahead of `main`** at the start (`a9c6b0ef` vs `3030300c`). The worktree's branch
  had been cut from `main`, so it needed a rebase onto `origin/Coding` and the rebase conflicted:
  `Coding` had meanwhile added `PhoneToolbar` around every page's `FolderTabs` and had already scoped
  the task page's chip counts to the open folder. **Cut from `origin/Coding`, not from `main`.**
- `dotnet ef` 10.0.11 is installed and works from this worktree:
  `dotnet ef migrations add <Name> --project src/Server/Orbit.Data --startup-project src/Server/Orbit.Api`.
  The `HostAbortedException` it prints is normal. After a rebase that brings in somebody else's
  migration, **delete your migration's two files and regenerate them** - `ef migrations remove` did not
  delete them here, and a stale `*.Designer.cs` makes the next migration re-emit the other session's
  change.
- Column names in migrations are uppercase (`OP_F_SCOPE`, `OP_T_COMPLETION`), table names are not
  (`OP_FOLDERS`, `OP_TASKS`) - see `OrbitStorageNames`.
- **No Python and no Node on this machine.** `perl -0pi -e` works for multi-line edits; heredocs into
  `cat >` work; the Write tool is the reliable option (see the standing note about bulk edits on this
  checkout).
- The web CSS already uses `:has()` for page-shaped exceptions (`.main-content:has(.map-page)`), so the
  chat rule follows an existing house pattern rather than introducing one.
