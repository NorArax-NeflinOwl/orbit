# Session handover: orbit-web-12

Previous session: orbit-web-11's successor, working the branch below (no worktree; the main checkout)
Date: 2026-09-14

## Branch and PR

- Branch: `feat/future-plan-leftovers-without-sdk-or-android`, all pushed.
- Open PR: **#288 — "[Web] A note's lines get a style, an entry stands for any of its lists, the
  sixteen-item round"** (draft, `Coding` from that branch). The new session **inherits it** rather than
  opening its own - see `pr-workflow` - and keeps extending its description, which is written in batches
  (four so far). It is a **draft on purpose**: nothing on this branch has ever been compiled.
- Uncommitted changes: none.

## Goal of the work

A long round that began as "implement what future-plan says you can do without me", grew a sixteen-item
list of web and phone fixes, and ended in one large piece: **a note's content model**. The user asked for
"formatting, checkboxes, tables and attachments up to 50 MB a note, following Apple Notes", and then
answered four design questions that decide the rest of it (recorded in `info/future-plan.md`, *Settled
2026-09-14: what was asked and answered*).

## Done

Twenty-nine commits; PR #288 carries them in full. The four that shape the code:

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
- The Polish dictionary checked against its own test's rules: 1728 keys, 0 duplicates, 0 blanks, 0
  placeholder mismatches, every new key present.
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

## Next step

**Get the branch compiled and green before anything else is built on it.** Tables (commit `854d9fb`)
and pictures (`b16f49c`) went in after this handover was first written, both uncompiled like everything
before them; the branch now carries a real EF migration (`20260914210000_ANoteKeepsItsPictures`) and a
new NuGet package (`Azure.Storage.Blobs 12.29.2`). On a machine with the SDK: `dotnet test
Orbit.CI.slnf`, fix what it says, and only then go on.

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

## Environment facts confirmed this session

- No .NET SDK, no Android SDK; the proxy refuses the .NET download with 403.
- `node` is available, and `ci/verify-diagrams.mjs` runs.
- The storage account `orbitdownloads` was created with **public blob access on** and its `apps`
  container is anonymous-read **on purpose** (it hands out the Android APK). Pictures in notes need a
  **new** account with public access off - the user's call, and they asked to be shown the command
  before it is run.
- PR #288 is draft, based on `Coding`; the integration PR `Coding` → `main` is the other open one.
