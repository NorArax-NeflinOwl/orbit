# Session handover: orbit-backlog-1

Previous session: project-migration-macos-windows
Date: 2026-09-10

## Branch and PR
- Branch: `fix/web-revalidates-hand-written-files`, in the worktree
  `.claude/worktrees/orbit-android-ui-continue-a9292f`.
- Open PR: **#276** — "[Android][Web] Seven defects, the orbit-api nginx can be pointed at, and the
  phone's parity list". 27 commits, everything pushed, description rewritten to match. The new session
  inherits it rather than opening its own — see `pr-workflow`.
- Uncommitted changes: none.

## Goal of the work
Move the project off the Mac onto Windows, prove Orbit can be built, debugged and run here, then work
`info/future-plan.md` and the handovers in `info/sessions/` in the order the user agreed: defects first,
then the deployment step that needs no decision, then the phone's parity list, then the small fixes that
need no decision either.

## Done
The Windows environment is complete and verified: `dotnet test`, `dotnet build -c Release`, the Android
Release build, the docker compose stack, an emulator, mkcert TLS, and the debug keystore from the Mac
installed at `%LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore`. `PRZENOSINY.local.md` and
`secrets/README.md` were rewritten for this machine. A full two-client walk (web and Android side by
side against the local stack) was run.

Then, on PR #276:

- **Seven defects.** A refused create escaping the outbox's rules (`SyncFailure.StaysInTheOutbox`);
  browsers running a stale `js/` module after a deploy (nginx `Cache-Control`); the phone resetting the
  five restock settings it does not draw; ticking an entry by `IndexOf` on a stale copy ticking nothing
  and reporting success (`TaskItemCompletion`, now by id); a `JsonException` escaping
  `EverythingSynchronizer.SynchroniseAsync`; the invitation page treating an unknown kind as an
  inventory; a dashboard test that failed for the last three hours of every day.
- **Deployment, stage 1 of the two-environment plan.** `nginx.azure.conf` carried the test
  environment's `orbit-api` FQDN in three places; it is `__ORBIT_API_HOST__` now, filled in at container
  start by `point-nginx-at-the-api.sh`. Unset means the environment running today, so nothing already
  deployed has to be configured. Three actions bumped off the Node 20 deprecation.
- **Nine phone-parity items**, each struck off in `info/future-plan.md`: whether a list is finished; an
  entry's description; one categories box on a shelf entry; every deadline opening the entry it is;
  renaming a folder; hiding a folder on the dashboard; addresses pressable; describing a product before
  a shelf exists; asking what to build before generating a storage.
- **A wording rename** (eleven strings still calling an inventory a "storage") and **four test doubles
  made to answer like the server** (`FakeTasksServer`, `FakeInventoryServer`, the new
  `FakeFoldersServer`, `InMemoryChosenFolderStore`).
- **Three more, at the end of the session:**
  - `ReturnTo` at the notification call sites — the bell panel names the page it was pressed on, the
    notifications page names itself. `ReturnTo.Link` assumed no query on the path it was given, and a
    notification about a shared place is `/map?place={id}`.
  - The phone's advertising bar opens what it names (`HouseAdLink`), using `ClientFlagsDto.WebAddress`.
  - The phone's own invitation screen (`InvitationViewModel`, `InvitationPage`, `ShareOfferClient`).
- **Documentation:** fifteen merged handovers deleted from `info/sessions/`; what was reusable in them
  is now "Driving the Android app by hand" in `info/testing-and-running-locally.md`. `functionality.md`,
  `current-status.md`, `build.md`, `azure-setup.md`, `uml/deployment.md` and `.env.example` kept current
  in the same changes.

Verified at the end: `dotnet test Orbit.CI.slnf -c Release` — **3743 passed, 0 failed** (Api 1329, Web
1076, Mobile 1338). `dotnet build Orbit.CI.slnf -c Release` — 0 warnings, 0 errors.
`dotnet build src/Clients/Orbit.Maui -c Release -f net10.0-android` — 0 errors, 19 pre-existing
warnings. `ci/verify-diagrams.mjs` through the Docker recipe — 18 diagrams, 0 failed.

## Still failing / unknown
- **The map's Start and Share do nothing on a phone-sized window.** Two of the three candidate causes
  are ruled out, measured live at 375×812: the button is on top (`elementFromPoint` returns
  `BUTTON.btn-primary map-panel-start`) and it is not disabled. Pressing it produces *"Orbit isn't
  allowed to use your location. Turn it on in Options first."* 72px below it — off the fold on that
  viewport. The third cause is unconfirmed and needs the **user**, not a session: turn Options →
  Location **on**, press Start, and read the line under it. If the message is gone, the cover comes off.
- **A create the outbox has given up on leaves a row that never syncs.** The local row keeps no
  `ServerId`, reads like any other note, and every later edit queues an update — which is `Abandoned`
  quietly on a row the server never saw. True of every entity type. Written up at
  `info/future-plan.md`; the two designs it names are a decision the user was asked for and has not
  answered.

## Rejected approaches (do not retry)
- **Ticking an entry by position.** `TaskItemCompletion` used `IndexOf` on the list the entry was read
  from. It is by id now, and the class comment says why; the failure it hid was a *stale copy* matching
  nothing, not two identical entries — records compare by every field, ids included.
- **A permissive test double.** `RespondingWith([])` for folders threw `JsonException` on a create and
  would then have deleted the folder it made, because folders have no change feed. Doubles must refuse
  what the server refuses *and* answer what it answers — four client bugs hid behind that in one day.
- **`perl -0pi -e` for bulk edits on this checkout.** Mixed LF/CRLF and encoding make a pattern match
  nothing silently; the em-dash rename matched nowhere and reported success. Use `Write`/`Edit`, or make
  the script die on no-match.
- **Chat opening a shared thing as "one `ReturnTo.Link` at the call site".** There is no call site: a
  share notice carries the *share's* id, accepting answers `bool`, and there is no item address to
  offer. It would take the five accept endpoints answering with the item's id. `future-plan.md` now
  says so.

## Next step
Ask the user which of the two designs to build for **the create the outbox gave up on** — either the
repository queueing a *create* rather than an update when the row has no `ServerId` (silent second try),
or a mark on the row with "send again" under its menu (the reader decides). Then build it across every
entity type, not only notes.

## Environment facts confirmed this session
- Windows 11, repository at `E:\Git\orbit`; sessions work from worktrees under `.claude/worktrees/`.
- The Android debug keystore in use is the **Mac's**, installed to
  `%LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore`; both SHA-1s are recorded in `secrets/`.
  Google Cloud project **181624005200** owns the Maps key and the OAuth clients.
- The emulator AVD's `config.ini` must name `google_apis`, not `google_apis_playstore` — only
  `google_apis` is installed here, and the mismatch reads as "Broken AVD system path".
- `adb shell input text` drops spaces: `ui.sh`'s `type_text` converts them to `%s`. Clearing a field is
  `input keycombination 113 29` then `keyevent 67`.
- Compose must be run with `-p orbit` and the main checkout's `.env`, and it serves **the last image
  built** — a stack built from the root checkout was 113 commits behind and answered 404 to
  `/api/places/changes`, which the phone showed as "Couldn't sync".
- `ClientFlagsDto.WebAddress` is how the phone learns the web client's address; it comes from the
  server's flat `WebClientBaseUrl` setting.
- `ORBIT_API_HOST` on `orbit-web` is optional and unset means the environment running today.
