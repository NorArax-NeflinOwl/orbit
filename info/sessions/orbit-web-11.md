# Session handover: orbit-web-11

Previous session: orbit-web-10, the worktree `orbit-web-improvements-d8efe1`
Date: 2026-09-10

## Branch and PR

- Branch: `claude/orbit-web-improvements-d8efe1`, 36 commits ahead of `origin/Coding`, all pushed.
- Open PR: **#272 — "[Web] Wait on the Orbit mark, choose a language before signing in, and be taken to
  a pin"** (`Coding` from that branch). The new session inherits it rather than opening its own - see
  `pr-workflow` - and extends its description as work lands; it is written in batches, seven so far,
  each with its own "Verified for this batch" block. The only other open PR is **#273**, the integration
  PR the workflow keeps open, so one slot is free if a genuinely separate line of work starts.
- Uncommitted changes: none.

## Goal of the work

A list of web and phone defects and improvements the user gave in Polish over one long session, which
grew into three larger pieces: places as a thing Orbit keeps on its own account, field descriptions
folded behind a "?" everywhere, and places sealed by default.

## Done

Thirty-six commits; the PR description carries them in full. The three that shape the code:

- **Places** (`Orbit.Core/Places`, `OP_PLACES`, `OP_PLACES_SHARED`). A place is a name, an address, a
  point and a description, kept for its own sake rather than because something is happening there. It
  has the whole ladder: a form and a list on the map, a dashboard card, sharing and re-sharing, a public
  link, a copy, and the phone's own screen with `LocalPlace` + `PlaceSynchronizer` behind the Location
  permission. `TaskItemKind.Location` was added the same day so an entry can say where without saying
  when - see the open question below.

- **Field descriptions behind a mark** (`Orbit.Web/Components/FieldHint.razor`,
  `Orbit.Maui/Controls/FieldHint.xaml`). Every field's small print folds behind a "?" beside its label,
  opened by hover in a browser and by a tap on the phone; a statement about the screen right now folds
  behind a "!" in the warning colour instead (`Warns`). The scope was the whole application, Options
  included. The bubble is `position: fixed` and placed by measured JS (`wwwroot/js/fieldHint.js`), and
  it is faded with `opacity` rather than hidden with `display:none`, so a screen reader still reads it.

- **A place is private unless its owner says otherwise** (the last commit, `fd87356a`). `IsPrivate`
  defaults to true on both `Place` and `SavePlaceRequest`; sealing empties the name, the description
  **and** the point, and everything readable comes back out of `SealedPlace` through
  `PrivateContentSealer`. A sealed place cannot be shared, copied server-side or published as a link -
  refused in the handlers, not merely hidden in the UI.

Verified before each push: `dotnet test Orbit.CI.slnf` (last run **3691 passed, 0 failed** - Web 1071,
Mobile 1291, Api 1329), `dotnet build Orbit.CI.slnf -c Release` (0 warnings), the Android head (0
errors; its 19 warnings are all in files this branch does not touch), `node ci/verify-diagrams.mjs` (18
diagrams, 0 failed), and a walk in a browser against the local stack with the rows read back out of
Postgres. All test data was deleted afterwards.

## Still failing / unknown

- Nothing failing. One open **question for the user, not a defect**: a Location entry carries a line of
  text, not a point, so it cannot be drawn on the map. Written up in `info/future-plan.md` under
  "Noticed while working" with the two shapes worth weighing; it needs a decision before anything is
  built, because the obvious fix - coordinates on every task item - would store a point in a second
  place.
- The map's Start and Share still do nothing on a phone and are still hidden below 680px. Unchanged by
  this session; the write-up in `info/future-plan.md` is still the state of it.

## Rejected approaches (do not retry)

- **Chasing a clipped Leaflet popup inside the popup.** `panInside`, a `popupopen` handler that measured
  and called `panBy`, `requestAnimationFrame`, `invalidateSize` - none of them worked, and all were
  removed. The cause was elsewhere: `showLocations` fits the map around every pin *with animation*, so
  that fit landed after `focusOn` and dragged the map back. `animate: false` on the `fitBounds` and on
  `focusOn`'s `setView` is the whole fix (`wwwroot/js/locationMap.js`).
- **Setting the phone's hint colours in XAML markup.** A value set in markup is a *local* value, and in
  MAUI a local value beats a dynamic resource, so the theme could never move it. `FieldHint` sets them
  from code with `SetAppTheme(...)` instead.
- **Giving the page header's toolbar the existing `.page-header-actions` row.** It made that row
  unconditional and broke the "no actions ⇒ no row" promise two test files assert. The toolbar has its
  own `.page-header-toolbar` slot.
- **A second live `PageHeader` in one test.** `SectionOutlet` allows one subscriber per section id; a
  page holds one `PageHeader` at a time, and a bUnit test that renders two needs `DisposeComponents()`
  between them.
- **Adding a second Polish `["Location"]` key.** `PolishTranslationsTests` fails on a duplicate. The
  existing key now reads "Lokalizacja" (it used to say "Położenie", which reads as a coordinate).

## Next step

Ask the user what they want next - the list they gave is finished and pushed, and nothing is
half-built. If they have none, the useful work is the two decisions written down rather than made: what
a Location entry should do about a point (above), and the parity items already in
`info/future-plan.md`.

## Environment facts confirmed this session

- Local stack for the walk-throughs: API on `http://localhost:5080`, web on `http://localhost:5081`,
  Postgres reached with `docker compose -p orbit exec postgres psql -U orbit orbit`.
- A sealed place in Postgres: `OP_P_NAME=''`, `OP_P_ADDRESS=''`, `OP_P_LATITUDE=0`, `OP_P_ISPRIVATE=t`,
  plus `OP_P_ENCRYPTEDCIPHERTEXT` / `OP_P_ENCRYPTEDNONCE`.
- Migrations added this session: server `HandAPlaceToSomebody` and
  `SealAPlaceUnlessItsOwnerSaysOtherwise`; phone `KeepPlacesOnThePhone` and `SealAPlaceOnThePhoneToo`.
- The browser pane's click coordinates are not the page's when a viewport size is emulated (a factor of
  about 3.94 at 1440px). Measure with `getBoundingClientRect()` and convert, or drive the page through
  `javascript_exec`.
- The user signs in themselves when a session needs one; do not type their password.
