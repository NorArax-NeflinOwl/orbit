# Session handover: orbit-web-4

Previous session: the one that opened PR #246 and PR #252 (worktree
`youthful-aryabhata-1778f7`)
Date: 2026-09-07

## Branch and PR

- Branch: `feat/plans-on-the-map-and-folders`, cut from `origin/Coding` and rebased onto it once
  (2026-09-07, after #250 merged).
- Open PR: **#252, "[Web] Plans on the map, folders as tabs, advertising slots, and three fixes"** —
  twelve commits, unmerged. **The new session inherits it** rather than opening its own (see
  `pr-workflow`): further work goes on this branch and the description is extended.
- Uncommitted changes: none at the time of writing (this file is the exception - commit it).
- Also opened and merged this session: PR #246 (test coverage for the half of a share that can be
  answered).

## Goal of the work

A queue of user asks, taken in order as they arrived - plans on the map, folders as tabs, advertising
slots, hiding the Debugger permission - followed by six defects the user reported while the branch was
open, and two items taken from `info/future-plan.md` between them.

## Done

**Plans on the map (`62b64a10`).** `/map` lists every calendar event with an address and every task entry
that raised one, soonest first, in the appointment's own colour, and pins them. Pressing a row centres
the map (or hands off to the phone's map app, the rule a shared position already follows); the arrow
opens the thing where the calendar and the dashboard already open it and comes back to `/map`. What has
already happened is off until the page's own menu asks for it.

**Folders as tabs (`b3f81845`).** One row of tabs shared by the dashboard, the notes and the task lists
(`FolderState`, scoped). **Three folders have no rows at all** (`BuiltInFolder`: Finished → the item's
own `FolderId` → Private → Public, first match wins), so this shipped with nothing to backfill and no way
for the folder to disagree with the item. Folders somebody makes are rows (`OP_FOLDERS`, migration
`FileNotesAndListsInFolders`); deleting one empties it rather than taking what was in it. Filing travels
on its own request (`PUT /api/{notes,tasks}/{id}/folder`). Two behaviours changed and are easy to
"fix" back by mistake: the `/tasks` **Completed** chip is gone, and the dashboard no longer keeps a
finished list because it is pinned.

**A copy where a move is refused (`87f22367`).** Moving an entry between two lists with different owners
is refused with a reason now instead of `NotFound`, and `POST /api/tasks/{id}/items/{itemId}/copy`
writes a second entry with every field the first had, minus the three things that are references (the
appointment, the shelf item, the lists it stands for).

**Advertising, and the Debugger permission (`1bd51ae7`).** A rail down the right of a wide window, a bar
across the foot of a narrow one and of the phone's main screens, and one dialog a visit (never for an
account holding Debugger - `AdInterruption`). Everything shown is Orbit's own (`HouseAds`) and leads to a
path on this Orbit: **no third-party script**, deliberately, and every slot says "Ad". Debugger is no
longer listed among the permissions until it is unlocked, in both clients (`PermissionListing`); the code
box stays, since that is what the code is typed into.

**Marking and settling notifications.** The calendar's list marks the card the bell is talking about
(`c7b3be2f`), asking both of an appointment's addresses; the storage list and the dashboard mark the
storage a warning is about, now that an expiry warning names it (`c77b0bdf`); and reaching a thing
settles the notification, including by the second address a task entry stands for (`673bd5e5`,
`NewsSettler`).

**Three reported defects (`673bd5e5`).** The panel of words a category is chosen from asked for a
`--surface` this stylesheet has never defined - a missing CSS variable drops the whole declaration, so it
drew with **no background**; `StylesheetTokenTests` now reads `app.css` and refuses a variable nothing
defines. Upcoming listed appointments already crossed off. A calendar entry's summary said "No date set"
about an appointment that plainly had one (`EventWhen`).

**The chat loop from 2026-09-05 (`f59f5220`).** Not a render loop: marking a conversation read published
a live "chat changed" to the other party **whether or not anything had been read**, and a window answers
an announcement by polling, which marks read, which announces back. Both handlers publish only when a row
actually changed now.

**A share's notification leads to what was shared** (`b0d6246d`, `aecbb319`, `dd512ffd`), which the user
asked for on 2026-09-06 and approved the design of on 2026-09-07.
`/invitation/{kind}/{shareId}/{sharerUserId}` (`ShareInvitation.razor`) names who offered what, takes it
up, and lands on the thing itself. `GET /api/shares/{kind}/{shareId}` answers with the offer - one
endpoint for all four kinds - while accepting stays on each section's own, where that kind's rules live.
Accepting in the conversation settles the same notification. `SharedItemPath` holds the kind↔path
mapping that five places read.

**Coverage taken from the plan (`45889756`).** All five screens that post a share invitation are covered
now; each was checked by removing the `SendAsync` call and watching its own test go red.

### Verified

- `dotnet test Orbit.sln` — 3314 passed, 0 failed (Web 926, Mobile 1175, Api 1213).
- `dotnet build Orbit.sln --configuration Release` — clean. Release is the configuration that gates:
  `TreatWarningsAsErrors` is Release-only.
- `dotnet build src/Clients/Orbit.Maui -f net10.0-android -c Release` — clean (Orbit.Maui is outside the
  solution and must be built separately).
- The folder migration was applied by a real `Orbit.Api` startup against the local PostgreSQL container:
  `OP_FOLDERS` exists with its primary key and `IX_OP_FOLDERS_OP_F_USERID`.
- `node ci/verify-diagrams.mjs` — 16 diagrams, 0 failed.

## Still failing / unknown

- **Nothing was verified in a signed-in browser.** Every page this touched is behind a login, and
  signing in means creating an account and typing a password, which this session does not do. The looks
  worth a human glance: the folder tab row, the advertising rail and bar, the map's plans list, and the
  invitation page.
- **The phone has no invitation screen.** It reads the same notification path, takes the sharer's id off
  the end and opens the conversation, where its own Accept sits - so nothing is lost, but a phone that
  cannot read the sealed chat message still cannot accept. The user was asked and had not answered by
  the end of the session.

## Rejected approaches (do not retry)

- **Pointing a share's notification at the item itself.** The item is not the recipient's to open until
  they accept, so it lands on "no longer exists". The invitation page exists for exactly that reason.
- **Making the notification path `/invitation/{kind}/{shareId}` without the sharer's id.** The phone
  reads a closed set of paths and has no invitation screen; without that last segment it has nowhere to
  send anybody, which is worse than the conversation it used to open.
- **Changing the four `shares/{id}/status` endpoints to return an object.** A deployed older APK parses
  a bare boolean from them. The offer got a new endpoint instead.
- **Reading a share's title through `PublicSharedItemReader`.** It projects a whole item for an
  anonymous page and refuses anything private for that reason; `SharedItemName` answers the narrower
  question.
- **Marking the map page's plans list behind the Location permission gate** was left as it is, not
  fixed: splitting that gate changes what a permission means, which is the user's call.

## Next step

Ask the user whether to build the phone's invitation screen (the last piece of the share-notification
work), or take the next item from `info/future-plan.md`. Do not open a new PR: #252 is this session's and
is still open.

## Environment facts confirmed this session

- Local Postgres runs from the **main checkout's** `.env`:
  `docker compose -p orbit --env-file /Users/patrykpudwel/Desktop/orbit/.env up -d postgres`.
- `Orbit.Api` needs `ASPNETCORE_ENVIRONMENT=Development` to read its user-secrets connection string, and
  the web client's `appsettings.Development.json` expects the API on **http://localhost:5080**.
- The MAUI workloads (android, ios, maui) are installed on this machine, so the Android head can be
  built and its compile errors are real.
- `dotnet ef migrations add <Name> --project src/Server/Orbit.Data --startup-project src/Server/Orbit.Api`
  works from the worktree; the host aborting with `HostAbortedException` afterwards is normal.
