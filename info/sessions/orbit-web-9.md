# Session handover: orbit-web-9

Previous session: orbit-web-9's predecessor, the worktree `tasklist-item-completion-light-edit-6eb818`
Date: 2026-09-09

## Branch and PR

- Branch: `claude/tasklist-item-completion-light-edit-6eb818`, fourteen commits ahead of
  `origin/Coding`, all pushed.
- Open PR: **#269 — "[Web][Android] Cross an entry off where it is read, and the batch that followed"**
  (`Coding` from that branch). The new session inherits it rather than opening its own - see
  `pr-workflow` - and extends its description as work lands. The other open PR is **#268** (another
  session's mobile work); the integration PR is not open, so one slot is free.
- Uncommitted changes: none, apart from this file.

## Goal of the work

Eleven separate things the user asked for in three sittings, one commit each: crossing an entry off
where it is read, a third answer for every tick box, the browser's note editor rebuilt to the phone's
shape, autolinks, and a batch of dashboard/folder/inventory rules. Everything asked for is done; the
list is closed unless the user reopens part of it.

## Done

Fourteen commits, oldest first. Grouped by what they are about:

**The entry and its tick**

1. **`Move ticking a task entry off into a service of its own`** — `Orbit.Web/Services/TaskItemCompletion.cs`
   holds the save, the "Update stock levels" restock question and the wording of every refusal; the
   checklist ticks through it. One behaviour change: a 403 used to pass for a save and is now worded
   from the server's own reason.
2. **`Cross a task entry off on its own page`** + **`Press the tick on the phone's entry screen`** —
   `/tasks/{listId}/items/{itemId}` and `TaskItemSummaryPage` cross the entry off. The web re-reads the
   list after a tick; the phone writes locally, queues, and says when a share or the offline policy
   refused it.
3. **`Let a task entry be crossed out rather than ticked off`** (+ `Draw the third answer in the browser`,
   `Cross an entry out on the phone as well`) — `OP_TI_ISFAILED` beside the tick, migration
   `AnEntryCanBeCrossedOutRatherThanTickedOff`, `TaskItem.IsResolved`, and
   `Orbit.Core.Abstractions.TickState` for the one press cycle (nothing → done → given up on) both
   clients share. `TickBox.razor` on the web, `CheckCircle.IsFailed` on the phone, and the note's own
   contenteditable surface builds the same box by hand in `checklistTextEditor.js`. **Given up on means
   finished with and not done**: it closes a list, silences reminders and never counts as work done.
4. **`Let an entry wait for another entry of the same list`** — `OL_TASKS_STEPS`, migration
   `AnEntryCanWaitForAnotherOnTheSameList`, rule in `Orbit.Core.Tasks.TaskListSteps` (applied wherever a
   list is built or saved, so a tick on something still waiting is taken back rather than refused with
   an error). Picker in the web editor; both clients name what an entry is waiting for; the phone's
   `FakeTasksServer` keeps the same rule.

**Notes in the browser**

5. **`Give a note the whole panel, and the folder's notes beside it`** — one field filling its side of
   the screen, tools over its bottom-left corner (text style / checklist / table / attachment; only the
   tick box works, the rest say "not implemented yet"), the checklist tool types `[]` which the surface
   interprets, settings moved into the rail's menu, and the folder's other notes in a column at 20%
   width, dropped below 1100px. The editor now loads per note id in `OnParametersSetAsync` (a route
   parameter changing does not remake a Blazor component), releasing the previous note's lock.

**Everything else**

6. **`Count the day as done over due on both dashboards`** — "1/3 tasks due today", "1/2 events today".
7. **`Press the addresses in a message, on both clients`** — `LinksInText` moved to `Orbit.Core.Text`;
   `LinkedLabel` (MAUI `Label` writing `FormattedText`) is the phone's `TextWithLinks`.
8. **`Keep the advertising rail on the screen, and remember the past`** — `.main-content` had
   `flex-shrink: 0` over `width: 100%`, so the rail was laid out past the right edge on every window
   between 1200px and ~1500px; and the map's "show places already past" (with its from-day) is kept in
   `MapPinVisibility`.
9. **`Offer a storage only where there is something to build one from`** — `GeneratedInventorySource`,
   asked by both clients' menus.
10. **`Read the dashboard one folder at a time, and keep folders off it`** — opening a made folder
    leaves only the card it is about; "Hide on the dashboard" in the folder's own menu, stored per
    device in `DashboardCardPreferences`.

**Verified**

- `dotnet test Orbit.CI.slnf` — 3529 passed, 0 failed (Web 1013, Mobile 1232, Api 1284).
- `dotnet build Orbit.CI.slnf -c Release` — 0 warnings, 0 errors.
- `dotnet build src/Clients/Orbit.Maui -c Release -f net10.0-android` — 0 errors, 19 pre-existing
  warnings. This is the only automated guard the XAML has.
- Both migrations applied to the local Postgres with `dotnet ef database update`, and the column and
  the table read back with `psql`.
- The CSS fix was checked in a browser against a reproduction of the two rules at 1300px.

## Still failing / unknown

- **Nothing was walked in a real browser or on a device this session.** bUnit renders the real pages and
  drives the real controls, and the Android build only proves the XAML compiles. The three worth
  clicking through first: the rebuilt note editor, the three-state box, and the dashboard narrowing to
  one card.

## Rejected approaches (do not retry)

- **Replacing `IsCompleted` with a status enum** for the third tick state. It is threaded through 25
  server files, the wire, both clients, archives, public shares and sync; a second flag beside it is
  additive, leaves every existing query meaning what it said, and reads as "not failed" for every
  existing row. The impossible state is closed in `TaskItem`'s constructor instead (a tick wins).
- **Refusing a save that ticks a blocked entry.** The whole list is saved wholesale, so one bad tick
  would fail the lot. `TaskListSteps` takes the tick back instead, which is what `TaskItem.Create`
  already does for a linked entry's completion.
- **`CheckRow` cycling three states everywhere.** Two of its four callers (the chat's member picker,
  the inventory editor's list picker) select rather than finish something, and "given up on" means
  nothing there. It draws a `TickBox` only when the caller hands in a `State`.
- **A second `JSInterop.SetupModule("./js/dashboardCards.js")` in a test.** It replaces the handler set
  up in `OrbitTestContext` and takes its other answers with it - use the `DashboardCards` property that
  context now exposes.

## Next step

Ask the user what they want next. Nothing is half-finished: if they have no new list, the useful work is
the two parity items written down in `info/future-plan.md` — the phone's picker for what an entry waits
for, and `LinkedLabel` on the phone's remaining read-only descriptions.

## Environment facts confirmed this session

- Local Postgres is the docker compose one on `localhost:5432` (`orbit-postgres`, up from another
  session), and `dotnet ef database update` against it works from this worktree with the API's
  user-secrets connection string. `orbit-api` is on `localhost:8081`.
- `dotnet ef` is 10.0.11; MAUI, android and ios workloads are installed. An Android Release build takes
  about 3½ minutes.
- Adding a property to an entity gives the column its name automatically from `OrbitStorageNames`
  (`OP_TI_ISFAILED`); a new entity has to be listed there or startup fails.
- The translation coverage test (`TranslationCoverageTests`) fails the build for any new `T["..."]`
  string with no Polish, and it is the fastest way to find one you forgot.
