# Session handover: orbit-mobile-2

Previous session: orbit-mobile-1 (the Classical redesign of Orbit.Maui)
Date: 2026-09-09

## Branch and PR

- Branch: `claude/mobile-five-undesigned-screens`
- Open PR: **#262 — [Mobile] The five screens the design never drew**, against `Coding`.
  The new session inherits it rather than opening its own (see `pr-workflow`). The earlier PR from
  this line of work, **#259**, is already merged into `Coding` and `main`.
  One other PR is open and belongs to another session: #263 (`feat/ten-from-the-user`, web). That
  makes two of the three slots taken.
- Uncommitted changes: none.

## Goal of the work

The user designed a mobile-specific look for the Android head in Claude Design — the **Classical**
system — and handed it over as a working HTML prototype of nineteen screens. The job is to build it.

## Done

Merged in #259 (foundations, shell, first screens):

- **Tokens.** Lora + Cormorant Garamond (`MauiProgram.cs`, behind the existing `OrbitBody` /
  `OrbitDisplay` aliases, plus a new `OrbitDisplayLight`); neutral ground `#F3F2F2` / `#1C1A19`; the
  three ink levels as alpha over the ink (`#9E`, `#73`, `#29`); radius 4 everywhere; every filled
  button became an outlined one. `Resources/Styles/Colors.xaml` and `Styles.xaml`.
- **The shell.** `Controls/NavigationBar.xaml` is `[≡ or ‹] · the page's Title · avatar`;
  `Controls/Drawer.xaml` + `DrawerEntry.xaml` hold the eight sections, the notification count and
  About; a screen's own menu hangs under its name (`Controls/ITitleMenu.cs`); `Controls/Fab.xaml`
  replaced the header `+`; `Controls/EditorRail` is deleted.
- **A navigation stack.** `ScreenHistory` replaced `UpNavigation` at the user's explicit request
  ("Dodaj stos nawigacji"). A root arrival clears, a drawer destination resets to
  `[Dashboard, itself]`, a detail pushes; re-showing the screen on top replaces it. Android's gesture
  and the bar's arrow pop the same stack. `UpNavigationTests` (6) became `ScreenHistoryTests` (11).
- **Every screen repainted** in the new tokens, and several restructured: dashboard, notes list, note,
  tasks list and entry, calendar and event, inventory and shelf, chat, notifications, sign-in, account.

On the open PR #262:

- The five screens the prototype never drew (copies ×2, diagnostics, update, shared link, place
  picker), plus eight leftover "Back" ways out (six buttons, two title-menu entries).
- **A real crash fixed:** `PlacePickerPage` built its `maps:Map` unconditionally. On a build with no
  Google Maps key that throws from inside Play Services and ends the process — `MapPage` and the task
  entry had guarded against this for a long time and the place picker never did. It guards now.

Verified: `dotnet build -c Release -f net10.0-android` clean (19 pre-existing warnings, unchanged);
`dotnet test Orbit.CI.slnf` 3363 pass; every screen walked on the `Orbit_Pixel_8_API_36` emulator
against the docker API, both themes.

## Still failing / unknown

**The user rejected the result.** Verbatim: *"UI i UX Orbit.Maui jest niedopuszczalny! Nie
przeniosłeś widoku z projektu!"*

The cause is not a bug. It is what the previous session decided and did not ask about:

> The prototype was treated as a **style guide** and its rules were applied to each screen's
> **existing layout**, rather than as a **specification** whose composition was to be reproduced.
> `orbit-maui-mobile-app-design/README.md` says "recreate them pixel-perfectly"; that did not happen.

The divergences confirmed by reading the prototype's source against the built screens:

| Screen | The prototype | What was built |
| --- | --- | --- |
| Notes, a row | pin **only when pinned**, filled; title 19; tag badge at the right; **preview line, 13px, justified**; updated 11px. **No `⋯` on the row.** | pin always shown; title; **a `⋯` on every row**; no preview; updated |
| Dashboard, the day's counts | **one horizontal row**, gap 18, numbers 20px Cormorant 600, tabular | a stacked column, numbers at 14 |
| Tasks, a row | title, "Next: …", **full-width progress bar** | title, a status badge, "Done: x of y", a short bar at the right |
| Conversation | bubbles at most 78% wide, radius `10 10 2 10` | the app's existing bubble shapes, repainted |
| Account | a segmented System/Light/Dark control; accent swatches as a **ring with a filled centre** for the chosen one; **outlined** switch tracks | Android's own theme dialog; filled discs; Material switches |
| Calendar | Day/Month/Year inside one bordered group | three loose words |

Three further decisions the previous session took **against** the prototype, on its own authority,
and should have asked about:

- a back arrow in the bar at all (the prototype's bar only ever shows `≡`);
- a section screen keeping the drawer rather than the arrow;
- keeping the per-row `⋯` menus on lists where the prototype has none.

Nothing is known to be broken. The complaint is about fidelity, not correctness.

## Rejected approaches (do not retry)

- **Do not "apply the design system" screen by screen again.** That is exactly what produced the
  rejected result. The prototype's markup is the specification; read the `sc-if` block for the screen
  being built and reproduce its composition, then map the data onto it.
- **Do not use the design's hard-coded dark accent `#9F8DE8`.** The user chose to keep
  `AccentPalette`'s derivation from the reader's hue, so the accent picker keeps working
  (`AccentColorTests` pins the derived value).
- **Do not put a filled colour anywhere.** Two attempts got this wrong and were fixed: `Border`
  ignores a colour resource on its `Background` while honouring one on its `Stroke` (use an `Ellipse`
  and `Shape.Fill`), and a value written in the markup is a *local* value that beats a dynamic
  resource outright (`ClearValue` first — see `Controls/CheckCircle.xaml.cs`).
- **Do not reach for `MenuPlacement.FromTheFoot` for a screen's own menu.** It is for a row's menu.
- **Do not `git stash`** in the shared checkout, and do not commit to
  `claude/orbit-maui-mobile-redesign-34f351` — it is merged, and a hook refuses it.

## Next step

**Ask the user for the per-screen description they said they would attach, and rebuild the screens
against the prototype's own markup, one screen per commit.** Do not start until that description is
in hand — the previous session's failure was choosing an interpretation without asking, and the user
has already said so.

When it arrives, the order that has worked is: dashboard → notes list → note → tasks → calendar →
inventory → chat → account, taking each screen's `<!-- NAME -->` block out of
`Orbit Mobile.dc.html` and building to it.

## Environment facts confirmed this session

**The design bundle.** `~/Downloads/Orbit.Maui mobile app design-handoff.zip`. Unpack it into the
session scratchpad (it is not in the repository). The screens are one file,
`orbit-maui-mobile-app-design/project/Orbit Mobile.dc.html`, each in an
`<sc-if value="{{ is.<screen> }}">` block, with a `<script type="text/x-dc">` at the foot holding the
sample data. `_ds/classical-*/styles.css` is the token sheet; `android-frame.jsx` frames it at
412×892, which is the Pixel 8's size in device points — so the emulator and the prototype can be
measured against each other rather than eyeballed.

**Building and running.**

- The API runs in docker on **host port 8081**, so the app must be built with
  `-p:OrbitDevelopmentApiPort=8081` or it will look for 5080 and find nothing.
- `dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-android -c Debug -t:Install
  -p:OrbitDevelopmentApiPort=8081`
- Launch: `adb shell am start -n "com.orbitmaui.android/crc64a05c27c563ec9e41.MainActivity"`.
  The package is `com.orbitmaui.android`; `monkey -c LAUNCHER` does **not** work on it.
- Emulator `Orbit_Pixel_8_API_36`, 1080×2400 at density 2.625. A screenshot is 1080 wide while the
  image shown back is 900 — **multiply by 1.2** to turn a read-off coordinate into an `adb input tap`.
- `Orbit.Maui` is not in `Orbit.CI.slnf`. A local `-c Release` build is the only thing that compiles
  the XAML; Debug hides warnings-as-errors.

**Driving the app.** Blind taps at guessed coordinates wasted a lot of time. Two helpers, written to
`/tmp`, made it reliable — they dump the view hierarchy and tap a node by name:

- by `content-desc` (a control's `SemanticProperties.Description`), and
- by `text` (a label). The two matter separately: on a checklist row the circle carries the
  description and the label carries the text, so matching the wrong one toggles the item instead of
  opening it.

`adb shell input text` **turns `%s` into a space**, which silently corrupted a password and cost
twenty minutes. Avoid `!` and `%` in anything typed this way; seed accounts with alphanumeric
passwords instead.

**Accounts seeded on the local API** (all on the docker Postgres, not Azure):

- `ola.n` / `OrbitPassword2026` — the account the emulator is signed in as. Notes, task lists, two
  inventories. Contacts, Sharing, Location, Chat and Debug all unlocked.
- `anna.k` / `Orbit!2026pass` — the sharer. Contacts and Sharing unlocked. The `!` cannot be typed
  through adb, so sign in as this account only through the API.

**Permission codes** live in `OS_PERMISSIONS_CODES` (not `PermissionCodes`):
`docker exec orbit-postgres psql -U orbit -d orbit -c 'SELECT * FROM "OS_PERMISSIONS_CODES";'`
Debug is `D65N72CMKTC0`. Redeem with `POST /api/users/me/permissions/redeem`.

**The login endpoint rate-limits.** Repeated logins start returning non-JSON; get one token and hold
it in a file rather than logging in per command.

**Reaching the awkward screens.**

- *Shared link*: build with `-p:OrbitShareLinkHost=10.0.2.2`, create a share with
  `POST /api/share-links`, then
  `adb shell am start -a android.intent.action.VIEW -d "https://10.0.2.2/s/<token>" -n <activity>`.
  Without the host property the intent filter is `orbit.invalid` and nothing routes.
- *Copy review and copy history*: the copy offer needs a share that **permits** editing while editing
  is impossible. Share a note, accept it with `POST /api/notes/shares/{id}/accept` (the share id is in
  `OP_NOTES_SHARED`), set `OP_NS_ACCESSLEVEL` to `CanEdit`, then take the phone offline
  (`adb shell svc wifi disable; adb shell svc data disable`) and open the note — "Make a copy you can
  write in while you are offline?" appears. Write a line, come back online, and both screens have
  content. A `ReadOnly` share never offers a copy, which is what made this look impossible at first.
- *Diagnostics*: behind the Debug permission, on a **Debugger** tab that only appears once the code is
  redeemed and the app restarted.
- *Place picker*: from a task entry whose Type is changed to Calendar, then "Show map".

**Inventory items** need a `unit` from `Piece, Kilogram, Milligram, Litre, Millilitre, Pack` — "pcs"
is refused with a 400.

**Left on the emulator** by this session, and harmless: the `anna.k` account, a note shared to
`ola.n`, a copy of it awaiting review, and the entry "Flour, rice, olive oil" switched to a Calendar
type. Clear the app's data if a clean state is wanted.
