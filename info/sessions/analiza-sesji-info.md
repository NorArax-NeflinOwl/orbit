# Session handover: the session after analiza-sesji-info

Previous session: analiza-sesji-info (worktree `orbit-android-ui-continue-a9292f`)
Date: 2026-09-21

## Branch and PR

- Branch: `claude/checklist-dependencies-notifications-4r85ht`
- Open PR: **#305** — *Reminders about entries that stand for lists, and one notice per minute*, against
  `Coding`. It was opened by a remote session (no SDK there); this session was told by the user to put
  its work on it, verified it, and added ten commits. The new session inherits it rather than opening
  its own (see `pr-workflow`). Its description is current: the original two sections, a "Verified
  2026-09-21" paragraph, "The calendar and the shelves say it once too", "Added by the second session"
  and a "Walked on the running stack" paragraph. Extend it; do not rewrite it.
- **Check `gh pr view 305 --json state` before pushing** - a push to a merged branch succeeds silently.
- Uncommitted changes: none. The local walk environment is stopped and its two local files removed.

## Goal of the work

Read `info/sessions/`, carry on from it, then verify #305 (never compiled by its author) and finish
what was open: the Orbit.Web test flakes, the three issues waiting on a device, and the reminder
services #305 left out.

## Done

- **`info/sessions/` emptied** of five handovers whose PRs were merged; the three facts only they held
  moved into `info/` (Docker + pinned Playwright for the browser harnesses, the Android re-push trap,
  the Razor `@section` / remote-SDK notes).
- **#305 verified**: builds Release clean, full suite green, Android head Release clean, diff read.
- **All three Orbit.Web flakes caught with their messages and fixed** (none was what it was suspected
  of): `ChatThreadTests` read bUnit's non-thread-safe `JSInterop.Invocations` off the dispatcher;
  `GroupConversationPagesTests` found rows by a 360-hue avatar colour of random ids;
  `NameSuggestionSourceTests` used bUnit's `Click()`, which only queues on a busy dispatcher. Each proven
  both ways; stress runs 21/21 and 20/20 (with the Mobile suite alongside) afterwards.
- **Issues #293, #294, #296 walked on a running stack** (API on 5099 with its own database, web on 5098,
  Android emulator, the user signed in): none reproduces. Results are posted as a comment on each; the
  issues are left open for the user to close.
- **A real fault found on that walk and fixed**: text typed straight after drawing a rule in the browser
  was lost (`checklistTextEditor.js` guarded a picture's line, not a rule's). `ci/verify-note-surface.mjs`
  has three new checks; 19/19.
- **The calendar and the shelves gather their reminders** into one notice per reader, as #305 did for
  tasks; 13 new tests; walked live (one notice each, right destinations on the phone, no resend).
- **The phone's note tool row fades at its trailing edge** while there are tools past it (the user's
  choice of the three ways out, after the separator tool was found wholly off-screen at 1080 px).
  `ToolRowFade` in `NoteDetailPage.xaml`, painted and shown from the code-behind; the wrapping `Grid` is
  44 high on purpose - without a height it filled the screen and lifted the row to the middle. Seen on
  the emulator: faded over the table tool, gone once scrolled to the end.
- Last full run: `dotnet test Orbit.CI.slnf` 5179 passed / 0 failed (Api 1644, Web 1766, Mobile 1769);
  Release build 0 warnings; diagrams 18/18.

## Still failing / unknown

- Nothing failing.
- **Nothing gathers across the four reminder services**: an overdue notice and an expiry warning in the
  same minute are still two. Written down as not worth a notice queue unless it is seen in practice.
- Start/Share on the map from a phone is still unwalked (needs a real device's location).

## Rejected approaches (do not retry)

- **Widening a flaky test's timeout.** None of the three flakes was a deadline; each was a thread or a
  queued event. Catch the failure's message first (run the web suite 3 abreast, or with the Mobile suite
  alongside, 15-20 times), then read it.
- **Pointing a `dotnet run` Orbit.Web at another API with `ASPNETCORE_ENVIRONMENT`.** .NET 10 fixes the
  WebAssembly environment at build time; use `-p:WasmApplicationEnvironmentName=...` (see
  `info/testing-and-running-locally.md`, "A walk of its own").
- **A start time in the calendar's collective notice.** The server does not know the reader's time zone;
  the notice names the events and the calendar it leads to says when.

## Next step

**The phone still deletes a folder that holds something** (`info/future-plan.md`, "The phone still
deletes a full folder"): the browser refuses it (`FolderTabs.StillHolds`); the phone's four pages
(`NotesPage.xaml.cs`, `TasksPage.xaml.cs`, `CalendarPage.xaml.cs`, `InventoryPage.xaml.cs`) should ask the
same question of their own list before offering the entry. Decided already on the browser side, so no
question for the user. Build the Android head in Release afterwards - `Orbit.Maui` is outside the suite.

## Environment facts confirmed this session

- No node here; the browser harnesses and the diagram check run in Docker (commands in
  `info/testing-and-running-locally.md` and `info/uml/README.md`).
- The walk setup: database `orbit_android` in the `orbit-postgres` container, API port 5099, web 5098,
  AVD `Orbit_Pixel_API_36_pr288` (the only AVD whose system image, `google_apis`, is installed). The
  account signed in there is `Test` (`d823c800-6afe-4139-9dde-47ee356c0410`), granted Contacts, Chat,
  Sharing, Location and Debug by SQL this session. **Test data left in `orbit_android`, harmless**: the
  note "Before the ruleAfter the rule isY", the list "Check 296", the events "Dentist 297" /
  "Standup 297", the storage "Fridge 297" and the place "Check 293".
- The phone's sync state is in the **drawer** ("Synced" beside "Orbit", build hash at the foot), not
  the avatar menu.
- `adb` is at `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe`, not on PATH; from Git Bash prefix
  `/sdcard` paths with `MSYS_NO_PATHCONV=1`. A sideways swipe while the soft keyboard is up can type.
