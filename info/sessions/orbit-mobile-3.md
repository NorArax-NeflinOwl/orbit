# Session handover: orbit-mobile-3

Previous session: orbit-mobile-2 (building the written spec for the Android head)
Date: 2026-09-09

## Branch and PR

- Branch: `claude/mobile-classical-spec`, cut from `origin/Coding`.
- Open PR: **#265 — [Mobile] The written spec: the shell, and every screen it describes**, against
  `Coding`. The new session inherits it rather than opening its own (see `pr-workflow`): further work
  goes on that branch and #265's description is extended so it still documents everything it carries.
  The only other open PR is **#264**, the integration PR (`Coding` → `main`) a workflow keeps open, so
  one of the three slots is free.
- Uncommitted changes: none.

## Goal of the work

The Classical prototype was rejected as built - the previous session had read it as a style guide and
applied it to the screens that were already there (see `info/sessions/orbit-mobile-2.md`, which is the
handover that says so). The user replaced it with a **written, screen-by-screen description**, given in
two messages. The job was to build that description and nothing outside it - the user said so:
*"Nie rób niczego poza przedział."*

## Done

Eight commits on #265. Every screen the description reaches is built.

**The shell** (`2f6ee99c`)

- **The bar's back arrow is gone.** `≡` on every screen without exception; going back is
  `ScreenHistory` and Android's own gesture. `NavigationBarViewModel.CanGoBack`, `CanOpenDrawer` and
  `GoBackCommand` were deleted with it, and `Sections.IsInTheDrawer` with them.
- The bar's centre is `[‹] name [▾] [›]`. The arrows come from a new `Controls/ITitleSteps.cs` and are
  **hidden when their command cannot execute** - `NavigationBar.Step` binds the host's `IsVisible` to
  the button's `IsEnabled`, which is what lets one screen offer them only some of the time.
- **About is a screen** (`Features/About/AboutPage`, `Screens/About/AboutViewModel`, `Screen.About`),
  with the row of documents Orbit.Web's footer carries. Those are web-client pages, so a build is told
  where its web client is: new `OrbitWebBaseAddress` property + assembly metadata in `Orbit.Maui.csproj`,
  read by `Configuration/OrbitWebSettings.cs`, turned into links by `Screens/About/OrbitDocumentLinks`.
  `ORBIT_WEB_BASE_ADDRESS` was added to `.github/workflows/android-release.yml`. A build told nothing
  lists the licence alone.
- Drawer: Contacts before Map, About as an entry, the version on the last line. Avatar menu trimmed to
  the four blocks the design names (the sync line went - the drawer's header already says it).

**Notes** (`2548140d`)

- The list row is pin, name, one tag, the note's first line, the date - and no `⋯`. `NoteListItem`
  gained `Preview`, `Priority`, `PriorityValue`.
- Sort and filter under the screen's name, kept on the device: new `Screens/ListArrangement.cs`,
  `Screens/ListArrangements.cs`, `IListArrangementStore` + `Platform/PreferencesListArrangementStore`.
  Shared with Tasks by design.
- **The editor is one surface.** `NoteDetailViewModel` gained `AddLineAfter`, `MergeIntoTheLineAbove`,
  `IsWritingAChecklist`, and a `Watch`/`WhenALineChanges` pair that turns a typed `[]` into a real tick
  box. `SaveAsync` was split into `WriteAsync` (write only, keeps the caret) and `SaveAsync`
  (write + read back, for a change to what the note *is*).
- Backspace at the head of a line joins it to the one above. MAUI has no key events, so this is
  `Controls/NoteLineKeys.cs` (what a field asks with) + `Platforms/Android/NoteLineBackspace.cs` (a
  handler mapper, like `FieldBox`), wired in `MauiProgram`.
- `Fab` gained `IsOnTheLeft`, `Diameter` and `IsOn`; `ItemCard` gained a `Trailing` slot on the name's
  line.

**Dashboard** (`c279ac8c`), **tasks** (`313b8067`), **calendar** (`d2b1852e`), **map and contacts**
(`79a36a45`), **inventory** (`9133a9ae`) - each described in its own commit message. The two worth
knowing about here:

- **The calendar's scroll-minimise is gone.** `CalendarViewMode` gained `Week`; `MinimisedCalendar`
  became `CalendarWeek` (only `Of` survives - `HourOf` and `MonthOf` went with the gesture);
  `IsMinimised`, `OnListScrolled` and `ScrollBeforeMinimising` are deleted. Seven tests in
  `CalendarScreenTests` were rewritten as week-view tests.
- **The map takes the whole screen.** Its panels moved under the screen's name, and the two lists open
  as a new screen: `Features/Location/LocationSharesPage` + `Screen.LocationShares` +
  `IScreenNavigator.ShowLocationShares(bool theirs)`. That page is the same page twice, told by the
  navigator which of its two lists to draw.

**Documentation** (`0194a9f4`) - `android-ui-parity.md`, `current-status.md`, `orbit-maui-plan.md`
§14.1, `future-plan.md` and `uml/components.md` all said the calendar shrinks as you scroll and/or that
the bar has a back arrow. All five now say what is true and why it changed.

Verified: `dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-android -c Release` clean,
**19 warnings - the same 19 as before this work**; `dotnet test Orbit.CI.slnf` 3389 pass.

## Still failing / unknown

- **Nothing has been walked on the emulator.** This is the one gap and it is the important one: the
  previous session's failure was a fidelity failure, and a build that compiles says nothing about
  whether a screen reads right. Everything below is therefore *believed*, not seen:
  - the note editor's caret behaviour - Enter, backspace-joins, and where focus lands after a merge
    (`_fields`, the row → `Entry` dictionary in `NoteDetailPage`);
  - the Android backspace hook firing at all (`NoteLineBackspace`), and firing exactly once per press;
  - the week view's first row, and the period label for a week straddling two months;
  - the map with the card overlaid on it, on a build **with** a Maps key (the emulator build has none,
    so `MapArea` holds a label instead and the overlay has never been seen over tiles).
- **Folders are not built.** The design names them in three menus; the mobile client has no folders at
  all. Written up in `info/future-plan.md` under "Noticed while working", with what it would take.
- **The map's two list screens were invented**, to satisfy "the lists open as their own page". The user
  has not described them and may want something else.

## Rejected approaches (do not retry)

- **Do not put `TextDecorations` on an `Entry`.** MAUI has it on `Label` and nowhere else - the build
  fails with MAUIX2002. A ticked checklist line is drawn as a `Label` in the field's place; see
  `NoteLineRow.IsOpenForWriting`, which is which of the two is showing.
- **Do not try to make the note editor a single `Editor` control.** A text box cannot hold an
  interactive child, which is the same wall Orbit.Web hit - it uses `contenteditable` + JS interop
  (`ChecklistTextEditor.razor`). The phone's answer is one field per line, drawn so the column reads as
  one surface, with the key handling above making it behave like one.
- **Do not re-derive the web address from `OrbitApiBaseAddress`.** On Azure the two are different
  container apps and the API host serves no pages; `OrbitShareLinkHost` already derives from the API
  address and is wrong for the same reason (pre-existing, not fixed here).
- **Do not read the design bundle as a style guide.** That is what produced the rejected version. The
  user's written description is the specification now.
- **Do not `git stash`** in the shared checkout, and commit only from a worktree.

## Next step

**Build, install and walk the app on `Orbit_Pixel_8_API_36`, screen by screen against the written
description**, starting with the note editor - it is the biggest rewrite and the one with behaviour no
test covers. Then report back what is wrong before writing anything else.

```bash
dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-android -c Debug -t:Install -p:OrbitDevelopmentApiPort=8081
```

```bash
adb shell am start -n "com.orbitmaui.android/crc64a05c27c563ec9e41.MainActivity"
```

After that, the user asked for the list of screens the description has not reached; it was given in
chat and written to the foot of `info/android-ui-parity.md`. They said they would send descriptions for
those next.

## Environment facts confirmed this session

Everything in `info/sessions/orbit-mobile-2.md` under the same heading still holds - the API on host
port **8081**, the emulator's 1080/900 screenshot ratio (multiply a read-off coordinate by 1.2), the
`ola.n` / `OrbitPassword2026` account, `adb shell input text` turning `%s` into a space. Plus:

- **`Orbit.Maui` is not in `Orbit.CI.slnf`.** A local `-c Release` build is the only thing that
  compiles the XAML, and it is the only guard against a typo in a binding path. The four tests that do
  read the markup off disk are `SpokenNameTests`, `TranslationCoverageTests`, `AvatarMenuBindingTests`
  and `CalendarMonthLayoutTests` - `AvatarMenuBindingTests` is the one that catches a shell binding to
  a command that does not exist, which MAUI fails silently on.
- **The warning count is the regression signal.** 19 before, 19 after. Two new ones appeared while
  `Fab.Size` cast `StrokeShape` unguarded and were fixed rather than left; if the count moves, look at
  what was just added.
- A `perl -0pi -e` edit against these files needs `\r\n` in the pattern - the repository is CRLF with
  `core.autocrlf=true`. Two edits landed in the wrong place before that was accounted for; the Edit
  tool handles it and is what the rest of the session used.
