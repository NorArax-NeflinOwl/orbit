# Session handover: orbit-web-12

Previous session: orbit-web-11's successor, working the branch below (no worktree; the main checkout)
Date: 2026-09-14, kept current through 2026-09-15

## Branch and PR

- Branch: `feat/future-plan-leftovers-without-sdk-or-android`, all pushed.
- Open PR: **#288 — "[Web+Phone] Folders for events and inventories; a note's content model: styles,
  marks, tables, pictures; the sixteen-item round; the 2026-09-15 audit and its leftovers"** (draft,
  `Coding` from that branch). The new session **inherits it** rather than opening its own - see
  `pr-workflow` - and keeps extending its description, which is written in batches (seven so far). It is
  a **draft on purpose**: nothing on this branch has ever been compiled.
- Uncommitted changes: none.

## Goal of the work

A long round that began as "implement what future-plan says you can do without me", grew a sixteen-item
list of web and phone fixes, and then a note's content model; on 2026-09-15 it gained folders for the
calendar and the shelves, an audit of a twenty-four-item list the user read back, and two things about
the phone the user reported. The note model came with four design questions the user answered
(recorded in `info/future-plan.md`, *Settled 2026-09-14: what was asked and answered*).

**The PR description is the record**, batch by batch, and is kept current as the branch grows. What
follows is only what a successor needs that the description does not say.

## Done

Sixty-odd commits; PR #288 carries them in full. The four that shape the code:

- **A line says what kind of line it is** (`NoteLineStyle`): Title, Heading, Subheading, Body,
  Monospaced and the three lists, which is Apple Notes' own Format menu. The "Aa" tool opens them in the
  browser; the phone opens the same eight in a sheet (`NoteDetailViewModel.StyleChoices`/`Restyle`).
  Enter follows the style, a numbered line's number is worked out rather than stored, and every screen
  that shows a note draws them - the editor, the note's page, a share link, both of the phone's screens.

- **A stretch of words inside a line can carry a mark** (`NoteTextMark`, `NoteTextRun`,
  `NoteTextMarks`, `NoteLineText`): bold, italic, underlined, struck through. The marks sit **beside**
  the text rather than folded into it, so everything that reads a line's words still reads a plain
  string. The browser draws a marked stretch in real elements and reads the marks back out of what it
  drew; Ctrl+B and the browser's own format commands are routed through the same C# edit. A line being
  read draws them through `MarkedText.razor`.

- **No migration for either**, because a note's content is JSON on the server (`NoteEntity.ContentJson`)
  and on the phone alike. Both are stored and sent **as words**, never numbers, and a word this build
  does not know reads as Body / as no mark rather than throwing.

- **The sixteen-item list** and the batches before it - see the PR description, which is the record.

### Verified (there is no compiler in these sessions)

- `node ci/verify-diagrams.mjs` - 18 diagrams, 0 failed.
- The Polish dictionary checked against its own test's rules: 1746 keys by the end, 0 duplicates,
  0 blanks, every string either client asks to translate present in it, and the sweep for text a page
  states rather than asks for at 0.
- `node --check` on `checklistTextEditor.js` after every change to it; a brace/comment balance pass over
  `app.css`; both changed XAML files parsed.
- **The mark arithmetic was reimplemented in Python and run** against the same 22 expectations the C#
  tests assert, before the C# was written - that is what caught the rule that a mark sticks to the
  character before what arrives.
- Tests written this round: `NoteLineStyleTests`, `NoteTextMarkTests`, `NoteSurfaceMarkTests`,
  `NoteDetailScreenTests.Style`, additions to `NoteEditorTests` and `NoteDetailBindingTests`.

## Still failing / unknown

- **Nothing on this branch has been compiled or run.** There is no .NET SDK in these sessions and the
  network policy refuses the download (`builds.dotnet.microsoft.com` → 403). `dotnet test
  Orbit.CI.slnf` is the check this work has never had; running it is the first thing a machine with the
  SDK should do with this branch.
- **Not seen in a browser or on a device.** The format panel, the styles on every read-only page, and
  the phone's style sheet have never been looked at.
- The phone draws marks on a line nobody is writing in (`MarkedLabel`, 2026-09-15), writes in a
  table's cells (`NoteTableCellField`, same day) and fetches and keeps a note's pictures
  (`NotePictureCache`, same day); none of it has been seen on a device. Inside a cell, and while a line
  is being written in, marks are carried and moved but look plain: a MAUI `Entry` renders one face for
  the whole field.

## Rejected approaches (do not retry)

- **Folding the text into a list of pieces** (a line as spans rather than text plus marks). It would
  have rewritten every search, preview, copy and test to say what they already say. Marks beside the
  text cost one custom `Equals` and nothing else.
- **A tick box as a ninth style.** A box is what a line is answered in, not what kind of line it is;
  `IsChecklistItem` predates all of this and everything from the phone's rows to the restock list reads
  it.
- **Letting the browser handle Ctrl+B.** It puts tags of its own choosing into the line and the phone
  never hears about it - so `formatBold` and its three friends are stopped in `onBeforeInput` and asked
  of C# like every other edit.
- **`TakesStyles` on `TitledDescription`.** A list's or a storage's description is stored as one plain
  string, so a style set there would be dropped by the save - the same reason `[]` stays words there.
  That is what decision 3 below is about.

## Half-built on purpose, and the first thing to finish

**Archiving has a server and no clients** (2026-09-15). The user chose "Archived is a built-in folder"
over "a state of its own", so everything a reader needs of it already exists as tabs and counts. What is
built: `IsArchived` with `Archive(bool)` on all four aggregates, `BuiltInFolder.Archived` at the head of
`FolderPlacement` (it beats even a folder somebody made, and leaves the folder id untouched so bringing
something back puts it where it was), one command and one `PUT .../{id}/archived` per kind, the four
columns (`ThingsCanBePutAwayRatherThanDeleted`), the flag on each DTO, and the tab in both clients' tab
rows.

**Nothing sets it.** No Archive action in any menu on either client, no local column on the phone, no
outbox operation, no phone migration, and nothing in the export. **So the tab is drawn on every page and
is always empty** - which is the state to finish rather than to discover. `info/future-plan.md` says the
same under the archiving entry.

## Next step

**Get the branch compiled and green before anything else is built on it.** It has grown a great deal
since this handover was first written and none of it has been compiled either: tables (`854d9fb`),
pictures (`b16f49c`), folders for the calendar and the shelves, the audit's leftovers, and the phone's
periodic sync. The branch now carries **two** server EF migrations
(`20260914210000_ANoteKeepsItsPictures`, `20260915090000_EventsAndShelvesAreFiledInFolders`), one phone
migration (`20260915100000_FileEventsAndShelvesInFoldersOnThePhone`) and a new NuGet package
(`Azure.Storage.Blobs 12.29.2`). On a machine with the SDK: `dotnet test Orbit.CI.slnf`, fix what it
says, and only then go on.

Where to look first if it does not build, in the order the risk sits: the six page constructors that
gained a `SyncState` (`ScreenKeptInStep`); `PeriodicSync`'s registration in `MauiProgram`, which is
built by hand rather than resolved; the `UpcomingThing` rework of the phone's Upcoming card; and the two
hand-written migrations' `.Designer.cs`, which were generated from the model snapshot by hand and diffed
rather than by `dotnet ef`.

Then, in order:

1. **The storage account for pictures** - decision 1, and the user's call because it bills. The exact
   commands are in `info/azure-setup.md`, "Where a note's pictures are kept". Until it exists, pictures
   on Azure go to a directory inside the container and are lost on the next revision; locally they are on
   a named volume and fine.
2. **Descriptions as lines** - decision 3, the one that touches the database. The shape is worked out in
   `info/future-plan.md` under that decision (beside the text, not instead of it; the keep-what-is-stored
   rule for the two writers; the editors; the phone carrying). Start with the server: four nullable JSON
   columns in one migration, the domain rule with tests, then the contracts, then `TitledDescription`.
3. **The phone's own halves**: ~~writing in a table's cells, drawing marks, fetching and caching
   pictures~~ (all three done 2026-09-15 - `NoteTableCellField`, `MarkedLabel`, `NotePictureCache`),
   setting a mark on the phone, putting a picture into a note from the phone - each written down in
   `info/future-plan.md` with what it takes.
4. **Finish archiving's client half** - see above. It is the largest thing left half-done.
5. **Ask about the three open questions** left from the audit above. There is no more of that list to build
   without an answer, so a successor that starts guessing is building the wrong thing.

**Also carried on this branch, asked for on 2026-09-15**: folders for calendar events and for
inventories, done the way the notes and the lists have them, and folders carried through the export and
the import. Four commits, server first, then the browser, then the phone's data and its screens. This
**reverses a decision the user made on 2026-09-09** ("calendar events are not filed in folders, and will
not be"); the entry in `info/future-plan.md` says so and says what was kept from it - a calendar tab
narrows the grid and the list together, and the dashboard draws no calendar tabs. Two things about it
are worth knowing before touching it again: `FolderPages` now answers a third question
(`HasAPrivateTab`, false only for the calendar, an event being one of the kinds Orbit never seals), and
the archive names a folder **by name** because a file carries no ids - a name the file did not carry
comes back unfiled.

## The 2026-09-15 audit, and what it leaves for the user

The user read back a **twenty-four-item list** and asked which were built. Most were. Everything the
check found missing is written into `info/future-plan.md` under *What the user's list of 2026-09-15
still leaves open*, **with the evidence each was checked against**, so a successor does not have to look
again: anything not listed there was found built and is described in `info/functionality.md`.

Everything on that list that needed **no decision** has since been built (the phone's Upcoming horizon,
"needs all of them" on the phone, "copy the text" and the done count on the phone, the place form's
sticky Save, tests for "New sublist", and the deadlines the phone's Upcoming card was missing).

**The user answered four of the seven on 2026-09-15**, and each answer is written into
`info/future-plan.md` with what it costs: Archived is a built-in folder (built server-side, see above);
the Group View box ticks itself in `TaskList` rather than in the form, with a migration and unpressable
while an entry names a list (built); a note separator's date is stamped once (not built); and the
Options page keeps its three inline Saves, so there is nothing to build there at all.

**Three still want an answer**, and a successor should ask rather than guess: choosing several things at
once and doing one thing to all of them; filtered copy and paste-from-the-clipboard; and editing a group
list's children in the heavy editor. Each is written out with what the choice costs.

## The phone's second list, 2026-09-15

Two things the user reported, both fixed the same day and worth knowing because the second changes how
the app behaves at rest:

- **The Upcoming card was showing work already finished.** Three faults at once - an appointment whose
  end had passed, a repeat drawn at the date it is stored under rather than at its next turn, and an
  appointment a task list raised and has since ticked off. The phone asked none of the three, on a
  comment claiming the divergence was deliberate and giving "Orbit.Web shows the lot" as the reason,
  which is not what Orbit.Web does. `DashboardViewModel.StillToDo` now asks all three.
- **Syncing lagged by days.** The only thing that synchronised was a screen being opened.
  **`PeriodicSync`** now runs everything every five minutes and once immediately, started and stopped
  with the window beside the presence heartbeat; a screen left open redraws itself when a run brings
  something down (`SyncState.BroughtSomethingNew`, `ScreenKeptInStep`, attached by the page rather than
  the view model - see its comment for why). The platform's own background sync (Android's
  `WorkManager`, iOS's app-refresh task or a silent push) is **still not built**, and
  `info/orbit-maui-plan.md` §5.6 says so.

## Environment facts confirmed this session

- No .NET SDK, no Android SDK; the proxy refuses the .NET download with 403.
- `node` is available, and `ci/verify-diagrams.mjs` runs.
- The storage account `orbitdownloads` was created with **public blob access on** and its `apps`
  container is anonymous-read **on purpose** (it hands out the Android APK). Pictures in notes need a
  **new** account with public access off - the user's call, and they asked to be shown the command
  before it is run.
- PR #288 is draft, based on `Coding`; the integration PR `Coding` → `main` is the other open one.
