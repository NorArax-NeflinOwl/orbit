# Testing and Running Locally

## Automated test coverage

Run the whole suite with:

```
dotnet test Orbit.CI.slnf
```

`Orbit.CI.slnf` is `Orbit.sln` minus `Orbit.Maui`: the solution carries the MAUI project so Visual
Studio can open and debug it, but building it needs the MAUI workloads and adds nothing to the suite
(the mobile logic under test lives in `Orbit.Mobile`, which the filter keeps). CI builds the same
filter. A project added to `Orbit.sln` belongs in the filter too, unless it genuinely cannot build
everywhere the suite runs.

This also runs automatically in CI, but **only on a push to `main`**: there is no `pull_request`
trigger anywhere in `.github/workflows` except `guard-main.yml`, which only comments on a pull request
aimed at `main` and runs no tests. So a feature branch, its pull request and the merge into `Coding`
are all checked on the machine that made the change and nowhere else - which is why running this
before opening a pull request is a rule in `.claude/CLAUDE.md` rather than a habit. A merge to `main`
that touches only `info/**` or `**/*.md` is skipped (`paths-ignore`), and runs are queued rather than
cancelled (`cancel-in-progress: false`), so a second merge does not cut the first one's deploy short.
See [Architecture — Continuous integration](architecture.md#continuous-integration) for what that
costs and why it is the trade it is.

### `tests/Orbit.Api.Tests`

Covers the health check infrastructure and the accounts, notes, tasks, calendar, contacts/chat, and
push notification features on the API side: password hashing; refresh token issuing, redeeming, and
revoking; registration; login; per-owner note access including deletion; per-owner task list access
including the checklist-completion rule, the task-list-linking rules (see
[Functionality — Tasks](functionality.md#tasks)), and deletion; per-owner calendar event access
including the start-before-end validation rule and deletion, the calendar event reminder scheduling
logic (see
[Functionality — Calendar event reminders](functionality.md#calendar-event-reminders));
sharing a note, task list, or calendar event and accepting the offered copy, including the
read-only-vs-can-edit access level rule (see
[Functionality — Sharing notes and task lists](functionality.md#sharing-notes-and-task-lists));
exact-match user search including self-exclusion; setting a user's public key; the chat
message/contact handlers including the first-message-creates-a-contact-in-both-directions rule and the
push notification it sends the recipient; subscribing/unsubscribing a push endpoint;
`PushNotificationDispatcher`'s fan-out and expired-subscription pruning; the overdue-task
notification scheduling logic (see
[Functionality — Push notifications](functionality.md#push-notifications)); the two delivery senders
against stand-ins for the services they talk to (`SmtpEmailSenderTests`, `VapidPushNotificationSenderTests`);
the auth rate limiter against the very policies `Program.cs` installs (`AuthRateLimiterTests`); and
handing a group's history to somebody who joined after it happened, including who may do it and what the
server refuses to take on their word (`ShareGroupHistoryTests`, see
[Functionality — Letting a new member read the history](functionality.md#letting-a-new-member-read-the-history)).

A few of these run against a real database rather than an in-memory double, because what they pin lives
in storage itself — the order a checklist comes back in, and which tables account deletion empties. They
use SQLite in a temporary file (`TemporarySqliteDatabase`), with **connection pooling turned off**. That
is not a detail: `Microsoft.Data.Sqlite` pools by default, so disposing the context hands its connection
back to the pool rather than closing the file, and the pooled handle outlives the test. Windows then
refuses to delete the file and the run fails in teardown with every assertion having passed; POSIX
unlinks an open file without complaint, so the same mistake is silent on macOS and Linux and waits for
somebody to run the suite on Windows. Anything else that needs a real database here should use that
class rather than opening its own connection.

### `tests/Orbit.Web.Tests`

Covers the Blazor client's auth wiring: the token store; the handler that attaches the access token to
outgoing requests and transparently refreshes it after a 401; `AuthApiClient`;
`OrbitAuthenticationStateProvider`; `PushNotificationApiClient`; and the `Login`, `Register`, `Calendar`
(including `CalendarEventEditor`), `Dashboard`, `Tasks`, `TaskListChecklist`, `TaskItemSummary`,
`Inventories`, `InventoryEditor`, `ContactInfo`, and the group-conversation pages themselves, rendered
with [bUnit](https://bunit.dev). Also the shared controls several screens reuse (`PinButton`,
`OverflowMenu`, `FeatureLocked`, `PresenceDot`, `LocationPickerOverlay`) and the device-local services
behind them (`PresenceService`, `AccentColorService`, `InventoryUnitOption`).

**The Polish dictionary is checked as a whole**, which is the only way some of its failure modes can be
found at all.

A key written twice is the quiet one, and the check for it **reads the source file** rather than the
built dictionary. The dictionary is written with indexer initialisers, which *overwrite* rather than
throw, so the second entry simply wins and the first leaves no trace anywhere in memory — nothing an
assertion about `ByEnglish` could ever see. Ten pairs had accumulated before anybody looked, four with
different Polish on each side; a group's roster was headed with the word meant for counting people.
Reading source off disk follows what `Orbit.Mobile.Tests`' own translation sweep already does, and like
that sweep it is guarded by a test that the file was found at all — otherwise a moved file would let the
check pass by finding nothing. (`Orbit.Web` no longer grants this project access to its internals: that
grant existed only for this dictionary, which is public since it moved to `Orbit.Localization` for the
phone clients to share.)

**Everything the web asks to be translated has to be translated**, which
`Orbit.Web.Tests`' own `TranslationCoverageTests` now checks the way the phone's has for months: it reads
every `T["…"]` and `T.Format("…", …)` out of `Orbit.Web`'s markup and code and looks each one up. A
missing translation is invisible by design — the English shows through, which is what makes it safe to
translate a screen at a time — so nothing but a sweep finds one. Without it the four pages behind the
footer sat entirely in English for as long as they existed, on a footer reachable from every screen; and
the first run of the sweep also found a sentence whose English had been reworded on the page while its
Polish stayed keyed to the old wording. Keys are compared as the *running app* asks for them, so an
escaped quote in the source is unescaped first.

**One English string means one thing.** Where two screens genuinely need different Polish for the same
English word, the answer is a second English key, not a second entry: the phone's sync row says
`No connection` ("Bez połączenia") rather than `Offline` ("Niedostępny", which is about a person), and
its group count says `People` ("Osób") rather than `Members` ("Członkowie", which is the roster heading
and the wrong form to put a number after).

Separately, a value referring to a placeholder its English does not supply throws when that line is
written, and every entry is formatted once to prove it cannot. Fewer placeholders than the English is
allowed and deliberate: Polish plurals do not map onto an English "list"/"lists".

**bUnit's `Click()` does not wait for the press.** While the renderer's dispatcher is busy it only
queues the event and returns, so an assertion straight after it can run before the handler has. That
is harmless on an idle component and a flake on one that is still finishing something - an
`OnAfterRenderAsync`, or a render a background task asked for through `InvokeAsync`. After waiting for
such a render, press with `await element.ClickAsync(new MouseEventArgs())`: `NameSuggestionSourceTests`
failed about one run in five with a second suite alongside, for exactly this, until 2026-09-21.

### `tests/Orbit.Mobile.Tests`

Covers the mobile client's platform-independent half (`src/Clients/Orbit.Mobile`): the API clients and
the authorization handler, the local SQLite store, the sync spine (delta pull, the outbox, conflict
policy), the crypto against the same vectors the web client is held to, the version gate, and the view
models behind each screen. What it cannot cover is `Orbit.Maui` itself — a MAUI head cannot be
referenced by an ordinary test project, which is why behaviour lives on this side of the split (see
[Architecture — Orbit.Mobile and Orbit.Maui](architecture.md#orbitmobile-and-orbitmaui)).

### What the deploy pipeline checks

`.github/workflows/main_orbit.yml` gates every push to `main`, in this order, so a failure costs as
little as possible:

1. **Every required Azure environment variable is present** - before spending minutes on image builds.
2. **The full test suite runs.** It did not, until a dependency cycle in the client's service graph
   reached production; `ClientServiceGraphTests` builds the container and would have stopped it here.
3. **`orbit-api` is smoke tested against a real PostgreSQL** and must report `/health/ready`.
4. **`orbit-web` must serve a page**, and then **must actually boot in a browser**
   (`ci/verify-app-boots.mjs`). These are not the same check: nginx falls back to `index.html` for every
   path, so a client that dies on startup still answers `200`. Only loading it in a browser and waiting
   for the start screen inside `#app` to be replaced tells the two apart - `.app-boot` is what the check
   waits to disappear, since the screen shown before Blazor starts is now a real one (Orbit's icon inside
   a turning ring, and the name) rather than the bare word "Loading…".
5. **After deploying**, both revisions must report `Healthy`, and the **deployed URL must boot** - same
   script, three attempts, against the real ingress. Container Apps reports `Healthy` when nginx is
   serving, which it does whether or not the app inside the page runs.

Anything failing after the deploy step rolls the affected app back to the image that was running a
moment before, and fails the run. Running it by hand:

```bash
node ci/verify-app-boots.mjs https://your-orbit-web-url/ 60000
```

## What is not covered by an automated test today

See [Future Plan — Testing gaps](future-plan.md#testing-gaps) for the reasoning behind each of these
and what closing them would take:

- `notificationclick` in `wwwroot/service-worker.js` — whether clicking a notification reuses an open
  Orbit tab or opens a new one. Nothing outside the operating system can raise a real click on a system
  notification, and Chrome DevTools has no command for it either, so this one branch is checked by hand.
  The rest of that file, and of `pushNotifications.js`, is covered — see below.
- The `Warning` the `Chat` page writes to the browser's own log when an account will not resolve. What
  the reader is *told* is covered (`ChatThreadTests`); the log line beside it is still read by hand.

What used to be on this list and no longer is: the chat thread and the `Chat` page's own explanation
for an account the API will not resolve (`ChatThreadTests` — see [The chat thread](#the-chat-thread)
below), push notifications end to end
(`ci/verify-push-notifications.mjs` and `PushNotificationManagerTests`, below), the `/api/auth/*` rate limiter
(`AuthRateLimiterTests`, against the very policies `Program.cs` installs), sending through
`SmtpEmailSender` and `VapidPushNotificationSender` (`SmtpEmailSenderTests` against a loopback SMTP
listener, `VapidPushNotificationSenderTests` against a stub transport), and `wwwroot/js/e2eeChat.js` —
see below. `Contacts` is covered by `ContactsGateTests` and `ContactInfoTests`.

### The chat thread

`ChatThreadTests` is the one test class in this project that **waits real seconds**, and it is worth
knowing why before anybody tries to speed it up. The thread's interesting behaviour is its poll loop,
which runs on a real one-second `PeriodicTimer`. There is no seam to shorten it, and adding one would
mean the tests exercised the seam rather than what is deployed. About fourteen seconds of the suite is
this class; the solution's wall clock does not change, because `dotnet test Orbit.sln` runs the three
projects side by side and `Orbit.Mobile.Tests` takes longer than that on its own.

Two things it does that are worth copying if this loop ever grows a sibling:

- **It waits for the loop, not for the clock.** A tick is counted off the one thing the loop does
  before deciding anything else - asking `./js/presence.js` whether the tab is in front of somebody -
  so a tick that went on to fetch nothing counts the same. That matters because "nothing was polled" is
  also true of a loop that never started, and a fixed delay cannot tell the two apart.
- **It does its own waiting.** bUnit's `WaitForAssertion` re-checks when the component renders, and a
  tick behind a hidden tab renders nothing at all - which is exactly the case being tested.
- **It counts on the renderer's dispatcher.** bUnit's `JSInterop.Invocations` is a plain list that a
  running loop adds to from the dispatcher; reading it from the test's thread while a tick was adding
  one threw "Collection was modified" about one run in fifteen. Anything that reads it while a timer is
  still going needs `Renderer.Dispatcher.InvokeAsync` around the read.

What it covers: nothing is polled behind a hidden tab and something is when the tab is in front; the
conversation list is read twice in ten ticks rather than on every one, while the messages are read on
each; leaving the page and opening a group each stop the loop; and an account the API will not resolve
is explained rather than opened as an empty thread. Each was checked by removing the behaviour from
`Chat.razor` and watching its own test go red.

An announcement over the live connection is delivered with `LiveUpdatesConnection.Announce`, the same
method the hub's handlers call. The page must read at once on hearing one, without waiting for a tick,
and must read nothing while the tab is behind others.

What it deliberately leaves out: the slower pace while connected, which needs a connection that is
really up, and the encryption, which is checked in a real browser by `ci/verify-browser-crypto.mjs`.

### The diagrams

`info/uml/` has no .NET test either, and a Mermaid block that will not parse renders on GitHub as an
error box rather than as nothing. `ci/verify-diagrams.mjs` parses every one of them:

```bash
npm install --no-save mermaid@11 jsdom
node ci/verify-diagrams.mjs
```

No browser, unlike the two verifiers below - Mermaid's parser wants a DOM but not a renderer, and jsdom
is enough. A machine with no node at all can still run it through Docker, and on a Windows checkout the
CRLF line endings matter to it: both are written up in
[info/uml/README.md](uml/README.md). `.github/workflows/verify-diagrams.yml` runs it on merges to `main` that touch `info/uml/`;
see [info/uml/README.md](uml/README.md) for why that is a workflow of its own.

### The links between the documents

The same problem in a second place, and `DocumentationLinkTests` closes it: nothing fails when a
cross-reference in `info/` goes stale, and a wrong one is still believed. A link to a section that has
been renamed renders on GitHub as an ordinary link, lands the reader at the top of the page, and leaves
them concluding the section does not exist. It runs in the ordinary suite - no browser, no npm - and
checks two things: that every `.md` a document links to is there, and that every `#section` names a real
heading, slugged the way GitHub slugs one.

It found two the day it was written, both the same mistake: a link to "§6" for a section since
renumbered to 7, and a link to a **bold paragraph** as though bold text made an anchor. It does not -
only a heading does, which is the trap worth knowing about before writing the next one.

It is deliberately narrow. It checks links between documents, **not** the code names the documents
quote: those name things deliberately removed ("`GoToTaskList`, now gone") and things not built yet
("`tests/Orbit.Maui.Tests` does not exist", which is the sentence saying so). A test refusing either
would be one nobody could keep green honestly, and both classes were real when this was measured.

### What one API instance cannot prove: run these by hand

Three things only make sense with a second replica, and all three fail *silently* when broken - live
updates crossing instances, a changed privacy choice clearing everywhere, and one rate limit budget
rather than one per process. What they rest on is PostgreSQL's own behaviour: `LISTEN`/`NOTIFY`
delivering to a listener on a different connection, and `INSERT ... ON CONFLICT DO UPDATE` counting
atomically under one row lock. A test double that accepted both would prove none of it.

So those tests live in the suite but **do nothing unless `ORBIT_TEST_POSTGRES` names a database**, and
report as passed while skipping. That keeps `dotnet test` a suite that needs no services - which is what
lets it be the check a change gets before `Coding` - at the price that **a green suite is not evidence
about any of this**. This is:

```bash
docker compose -p orbit up -d postgres
ORBIT_TEST_POSTGRES="Host=localhost;Port=5432;Database=orbit;Username=orbit;Password=<POSTGRES_PASSWORD>" \
  dotnet test tests/Orbit.Api.Tests --filter "FullyQualifiedName~PostgresLiveUpdateBackplane|FullyQualifiedName~PostgresRateLimitWindows"
```

Run it when touching `Orbit.Api.Instances`, `Orbit.Api.RateLimiting`, or the live update fan-out. The
database needs the migrations applied first (see [Database migrations](#database-migrations)).

### Driving the Android app by hand

Nothing in the suite can see a screen: `Orbit.Maui` is not in `Orbit.CI.slnf` at all, a local
`dotnet build -c Release -f net10.0-android` is the only thing that compiles its XAML, and four tests
read the markup off disk (`SpokenNameTests`, `TranslationCoverageTests`, `AvatarMenuBindingTests`,
`CalendarMonthLayoutTests`). Everything else about the phone is checked by walking it on an emulator.
This is what a walk needs, gathered from the sessions that did them so a new one does not rediscover it:

```bash
# The app talks to the compose stack's API on 8081; built without this it looks for 5080 and finds nothing.
dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-android -c Debug -t:Install \
  -p:OrbitDevelopmentApiPort=8081
adb shell am start -n "com.orbitmaui.android/crc64a05c27c563ec9e41.MainActivity"
```

- **Ask which activity rather than guessing:** `adb shell cmd package resolve-activity --brief
  com.orbitmaui.android`. `monkey -c LAUNCHER` does not start this package, and logcat prints a second,
  different hash that is not the launcher.
- **A walk of its own, without touching the Compose stack**, as used on 2026-09-21 for issues #293,
  #294 and #296: run `Orbit.Api` from the worktree on port 5099 against a database of its own
  (`ConnectionStrings__Orbit` = the user-secrets string with `Database=orbit_android`, and
  `WebClientOrigins=http://localhost:5098`; it migrates on start), build the phone with
  `-p:OrbitDevelopmentApiPort=5099`, and run `Orbit.Web` on 5098 pointed at it. **The web's API address
  cannot be set from outside at run time in .NET 10**: the WebAssembly app's environment is fixed at
  build time, so an `ASPNETCORE_ENVIRONMENT` or `Blazor-Environment` header is ignored and the app
  keeps asking port 5080. Pass `-p:WasmApplicationEnvironmentName=Android` to `dotnet run` and put
  `{"ApiBaseAddress": "http://localhost:5099/"}` in a local, uncommitted
  `wwwroot/appsettings.Android.json`. A preview entry in `.claude/launch.json` that runs a script needs
  Git's `bash.exe` by full path: a bare `bash` resolves to WSL's and fails with `execvpe /bin/bash`.
- **Whether the phone is really talking to that API is in the drawer, not the avatar menu**: the drawer
  heads with "Synced" (or why not) beside "Orbit", and ends with the build's hash. Check both before
  trusting anything seen - an app pointed at nothing still shows its local store and looks healthy.
- **A second `-t:Install` with unchanged sources pushes nothing**, and the emulator keeps running the
  previous build. Delete `obj/Debug/net10.0-android/upload.flag` and `.../devices.cache` first, and
  check it landed with `adb shell run-as com.orbitmaui.android ls files/.__override__/arm64-v8a`.
  **Never delete `files/.__override__` after an ordinary install**: under fast deployment that directory
  *is* the code, and the app then aborts with "No assemblies found... Assuming this is part of Fast
  Deployment", which reads exactly like a crash. Clearing it is only for a hand-installed
  `-p:EmbedAssembliesIntoApk=true` build.
- **Give a fast-deployed launch 25-30 seconds before touching the screen.** Taps aimed at the splash
  queue up and Android raises "Orbit isn't responding", which reads exactly like a crash caused by the
  change under test.
- **Read the screen, do not guess at it.** `uiautomator dump` writes the view hierarchy; match on
  `content-desc` for a control (its `SemanticProperties.Description`) and on `text` for a label - on a
  checklist row the circle carries the description and the words carry the text, so matching the wrong
  one ticks the entry instead of opening it. **`rm -f /sdcard/ui.xml` before every dump**: on a screen it
  cannot read the command returns without writing, and the previous dump is handed back as though the
  app had navigated somewhere it never left.
- **A screenshot is 1080 wide and comes back at 900** - multiply a coordinate read off the image by 1.2
  before `adb input tap`.
- **`adb shell input text` turns `%s` into a space** and decodes nothing else: `[` and `]` need
  `input keyevent KEYCODE_LEFT_BRACKET KEYCODE_RIGHT_BRACKET`. Seed test accounts with alphanumeric
  passwords, since `!` and `%` cannot be typed this way at all. **Clearing a field** is
  `adb shell input keycombination 113 29` (Ctrl+A) followed by `adb shell input keyevent 67` (Delete).
- **An AVD's `config.ini` must name the system image that is actually installed.** On the Windows
  machine only `google_apis` is, so an AVD whose `image.sysdir.1` or `tag.id` says
  `google_apis_playstore` will not start, and the emulator reports it as "Broken AVD system path" rather
  than as a missing image.
- **`dumpsys input_method | grep mServedView` is the truth about focus.** `uiautomator`'s
  `focused="true"` has sat on a button while typing went somewhere else entirely.
- **A worktree needs four gitignored files**, not three: `.env` and `docker-compose.override.yml` from
  the main checkout, and `Platforms/Android/google-services.json` plus
  `Platforms/Android/AndroidManifestOverlay.xml` from `secrets/` - see `secrets/README.md`. Without the
  overlay the map draws a label instead of tiles, which reads as an unfinished screen.
- **Reaching the awkward screens.** A *shared link* needs the build to know the host the intent filter
  matches (`-p:OrbitShareLinkHost=10.0.2.2`), then
  `adb shell am start -a android.intent.action.VIEW -d "https://10.0.2.2/s/<token>"`. A *copy review*
  needs a share that permits editing while editing is impossible: accept a share, then take the phone
  offline (`adb shell svc wifi disable; adb shell svc data disable`) and open it. The *About* screen
  lists documents only when built with `-p:OrbitWebBaseAddress=https://…/`.
- **Anything that moves, or is thinner than a few pixels, is read off the pixels** rather than looked
  at - see [android-ui-parity.md](android-ui-parity.md)'s "How to check it" for the method and the
  numbers it produced.

Two blind spots found on 2026-09-16, worth knowing before trusting a green run:

- **The in-app browser pane cannot check a form's implicit submit.** Its synthetic Enter never submits a
  form, so "Enter in an entry's box adds the next entry" (TaskEditor) has to be tried in a real browser.
- **The Polish dictionary's coverage sweep only sees literal keys.** A key reached through a method -
  `T[what.Label()]` - is invisible to it, so such keys are guarded by a test of their own
  (`FilteredCopyTests` does it for the four copy choices).

### The browser-side encryption, in a real browser

`ci/verify-browser-crypto.mjs` runs `Orbit.Web/wwwroot/js/e2eeChat.js` itself in headless Chromium. It
exists because every line of that file is Web Crypto and IndexedDB, bUnit executes neither, and the
whole chat's confidentiality rests on it. The .NET side is pinned against vectors generated *from* this
file (`tests/Orbit.Mobile.Tests/Crypto`), which proves the two agree — not that this file is right.

It serves `wwwroot` itself rather than booting Blazor: the module is a plain ES module, and `127.0.0.1`
is a secure context, which is all `crypto.subtle` and IndexedDB need. Fourteen checks cover the round
trip, a per-message nonce, a tampered message refusing to open, a stranger's key not opening one, two
accounts in one browser not sharing a key, the password-wrapped backup and its restore, and a key
surviving a page reload.

It runs in the `test` job of `main_orbit.yml`, so it gates the merge to `main` alongside the suite,
before any image is built.
Running it by hand needs the browser installed once:

```bash
npm install --no-save playwright@1 && npx playwright install chromium && node ci/verify-browser-crypto.mjs
```

### Push notifications, in a real browser

`ci/verify-push-notifications.mjs` does the same for the other two files bUnit cannot reach:
`wwwroot/service-worker.js` only ever runs inside a registered service worker handling a push event, and
`wwwroot/js/pushNotifications.js` is the Notification and Push APIs. The push events are delivered for
real, through Chrome DevTools' `ServiceWorker.deliverPushMessage`, so it is the registered worker being
exercised and not a copy of its source with a fake `self` around it.

Ten checks: a full payload showing the right title, body and link; a data-less push and a malformed one
each still showing something rather than nothing; a payload with no `url` still leading somewhere;
`isSupported` and `getPermissionState` agreeing with the browser they are asked about; nothing invented
for a browser that never subscribed or has nothing to unsubscribe; and a refused permission answering
with no subscription rather than half of one.

It launches the full Chromium rather than Playwright's default headless shell, which has no notification
service at all and reports `Notification.permission` as `denied` whatever is granted. Both are installed
by the same command:

```bash
npm install --no-save playwright@1 && npx playwright install chromium && node ci/verify-push-notifications.mjs
```

What happens on the C# side of that — no VAPID key meaning the browser is never prompted, a refusal
registering nothing, and what reaches `/api/push` when somebody does say yes — is
`PushNotificationManagerTests`, against a stub standing in for the module above.

### A note surviving its writing surface, in a real browser

`ci/verify-note-surface.mjs` is the third of these, and it exists because of a fault rather than a
feature. `wwwroot/js/checklistTextEditor.js` is the surface a note is written on, and `extractLines` —
the read of it that every keystroke and every save makes — knew a table and a picture and not a
separator, so a rule drawn across a note was stored as an empty line by the very next read. Nothing
threw, nothing logged, and no test in this repository could have seen it: the whole of it is in a
browser module. The user saw it twice, from both ends, and it took two days and issue #294 to name.

So the check is the round trip and nothing else. Fourteen kinds of line — ordinary writing, each style,
a ticked errand, a crossed one, words with a mark on them, a table, a rule with a stamp and a rule
without — go in through `initialize`, and `getLinesAsJson` reads the surface back. A line that comes
back saying something different is the failure, and a kind of line the read does not know comes back as
an empty one, which is exactly the shape of the fault.

**And one key pressed on an element's line**, added 2026-09-21 for the same kind of fault from the other
side. After a picture or a rule is put in, the caret is left on its line, before it, since neither has a
place for words - so the next key lands there. A picture's line was guarded: the key is stopped and
handed to C#, whose `NoteSurfaceEdits.Replace` puts the words under it. A rule's line was not, so the
first thing typed after drawing a rule went into the rule's own element, and the next read dropped it.
Three checks, a picture's line and both kinds of rule: nothing typed into the element's line, and the
key handed over as a `replace`. With the guard taken off the rule, exactly the two rule checks fail.

It runs in the `test` job of `main_orbit.yml` beside the other two, on the browser they already
installed. By hand:

```bash
npm install --no-save playwright@1 && npx playwright install chromium && node ci/verify-note-surface.mjs
```

### A panel landing where it belongs, in a real browser

`ci/verify-menu-anchor.mjs` covers `wwwroot/js/menuAnchor.js`, which is every edge of every panel drawn
outside the flow — the menus and the browsers are `position: fixed` because a dropdown inside a scroller
is clipped however high its z-index, and fixed means each of their edges is arithmetic done in that
module. All of it is `getBoundingClientRect` and `window.innerWidth`, and bUnit's DOM measures every box
as zero, so none of it was checkable in the suite.

Seven checks, and the two that matter most are about the floor added on 2026-09-18: a panel hanging off a
narrow button (the tag browser off "Add tag") is not squeezed below it, a panel asked for no floor is
still exactly its field's width (what the name suggestions rely on), and a floor wider than the window
gives way to the window rather than hanging off the edge it was meant to fit inside. The rest: a panel
takes a wide field's width, never hangs off the right, opens above its field when there is no room below,
and a menu anchored to a trigger near the right edge is pulled back on screen.

```bash
npm install --no-save playwright@1 && npx playwright install chromium && node ci/verify-menu-anchor.mjs
```

### The map's pins following a refresh, in a real browser

`ci/verify-map-markers.mjs` covers `updateLocations` in `wwwroot/js/locationMap.js`, which is what the
map's Refresh button actually calls. It is deliberately not a redraw: the pan and the zoom it was pressed
from are kept, a pin that moved is moved in place so an open popup somebody pressed to read survives, a
pin that arrived is added, and one that is gone is taken off. Every one of those is invisible from
outside — a refresh that silently did nothing looks exactly like a refresh with nothing new behind it,
which is how "Refresh on the map does nothing" (issue #293) came to be unreproducible from the code.

Seven checks, against a real Leaflet map built from `wwwroot/vendor/leaflet`. The tiles are stubbed
rather than fetched: they are the one third-party request Orbit makes, they say nothing about which pins
are on the map, and `mapTiles.js` is already the seam that lets a reader refuse them, so a map without
them is a state the page supports rather than one invented here.

The one to be careful with is the popup. A refresh that *replaced* a marker leaves the old one behind
still holding its popup open, which looks like the popup surviving — so that check also counts the pins,
and was written by putting the fault in and watching it go red.

```bash
npm install --no-save playwright@1 && npx playwright install chromium && node ci/verify-map-markers.mjs
```

### The five browser harnesses on a machine without node

Docker is enough, the same way the diagrams check runs without node (`uml/README.md`). The image
carries Chromium already, so nothing is downloaded but the npm package - and **the package has to be
pinned to the image's own version**. `playwright@1` pulls a newer one than the image's browser build and
every harness fails with *"Executable doesn't exist at /ms-playwright/chromium_headless_shell-…"*,
which reads like a broken image rather than a version mismatch.

```bash
docker run --rm -v "$PWD:/w:ro" mcr.microsoft.com/playwright:v1.56.0-noble sh -c \
  "mkdir -p /app/src/Clients/Orbit.Web && cp -r /w/ci /app/ci \
   && cp -r /w/src/Clients/Orbit.Web/wwwroot /app/src/Clients/Orbit.Web/wwwroot \
   && cd /app && npm install --no-save playwright@1.56.0 >/dev/null 2>&1 \
   && for h in browser-crypto push-notifications note-surface menu-anchor map-markers; do node ci/verify-\$h.mjs; done"
```

Only `ci/` and `wwwroot` are copied, into a container that is thrown away: the repository is mounted
read-only, and copying `src/` whole would drag every `bin` and `obj` along. On Windows, pass the path as
`-v "E:\path\to\worktree:/w:ro"`, and from Git Bash prefix the command with `MSYS_NO_PATHCONV=1` so the
`/w` is not rewritten into a Windows path.

## Running locally

The simplest way to run the whole stack is Docker Compose, which builds the API and the web client and
wires them together. For a full walkthrough from a fresh Windows or macOS machine (installing Docker,
generating secrets, first build), see [`info/build.md`](build.md).

```
cp .env.example .env   # then fill in JWT_SIGNING_KEY (required) and the SMTP_*/VAPID_* variables (optional)
docker compose up --build
```

Leaving the `SMTP_*`/`VAPID_*` variables blank is fine — see
[Functionality — Calendar event reminders](functionality.md#calendar-event-reminders) and
[Functionality — Push notifications](functionality.md#push-notifications) for what that means at
runtime. This starts:

- the web client at `https://localhost:8443` (its one and only real entry point — see below);
  `http://localhost:8080` also answers, but only to redirect straight to `https://localhost:8443`
- the API at `http://localhost:8081` (`/health`, `/health/ready`, `/health/live`, `/api/auth/*`,
  `/api/notes`, `/api/tasks`, `/api/calendar-events`)
- the [Aspire dashboard](http://localhost:18888) for live logs and traces from the API

### Accessing Orbit.Web from another device on your network

The web client always calls the API under whatever origin you used to load the page — `orbit-web`'s own
nginx reverse-proxies `/api/*` to `orbit-api`, so the browser never has to know the API's separate host
or port and no CORS configuration is needed for this.

The chat needs a genuinely secure context (HTTPS) for the browser to expose the Web Crypto API its
end-to-end encryption depends on — a plain `http://<LAN-IP>:8080` origin doesn't qualify, so opening a
chat there would fail with a `crypto.subtle` error. To avoid that, `orbit-web` serves HTTPS on port 8443
as its one and only real entry point, and automatically redirects any plain-HTTP request on port 8080
straight to it — including `localhost`/`127.0.0.1`, even though those hosts would otherwise count as a
secure context on plain HTTP too. That redirect is deliberate, not just for `crypto.subtle`: the chat's
E2EE key pair is stored in the browser's IndexedDB, which is scoped per origin, so if the app were
reachable under both `http://localhost:8080` and `https://localhost:8443` interchangeably, opening it on
whichever port was on hand would silently mint a fresh key pair and permanently orphan every message
encrypted under the old one. Forcing everything through the single `:8443` origin avoids that trap:

1. Set `TLS_CERTIFICATE_HOSTNAME` in `.env` to this machine's LAN IP (e.g. `192.168.1.50`) —
   `orbit-web` generates a self-signed certificate covering that address on first startup (see
   `src/Clients/Orbit.Web/generate-certificate.sh`). Restart with `docker compose up -d --build` for a
   changed value to take effect on an already-created container.
2. From another device, open `http://<this-machine's-LAN-IP>:8080` (or `https://<...>:8443` directly) —
   either way you'll end up on `https://<this-machine's-LAN-IP>:8443`. The browser will warn that the
   certificate isn't trusted (it's self-signed, not issued by a real certificate authority) — accept the
   warning once per device to continue.

For how to make Chrome trust that self-signed certificate — including for Service Worker registration,
which needs more than clicking through the browser warning — see
[`info/instructions.md`](instructions.md).

### Database migrations

Orbit.Api applies EF Core Migrations on startup (`Database.Migrate()`) — it creates the PostgreSQL
schema on first run and brings an existing database up to date with any migrations added since. **After
pulling changes that touch the EF Core model in `Orbit.Data`** (entities under
`src/Server/Orbit.Data/Entities`, or `OrbitDbContext`), generate the corresponding migration once with
the [`dotnet-ef` tool](https://learn.microsoft.com/ef/core/cli/dotnet)
(`dotnet tool install --global dotnet-ef` if it isn't installed yet):

```
dotnet ef migrations add <DescriptiveName> --project src/Server/Orbit.Data --startup-project src/Server/Orbit.Api
```

To reset a local database back to empty (e.g. to replay migrations from scratch), drop the
`orbit-postgres-data` Docker volume rather than deleting a file: `docker compose down -v` removes it
along with every other named volume, or `docker volume rm orbit_orbit-postgres-data` (the exact name is
whatever `docker compose config --volumes` prints) to remove just that one.

### Running without Docker

Each project can be run directly with `dotnet run` from its own folder (`src/Server/Orbit.Api`,
`src/Clients/Orbit.Web`) using the `https` launch profile; see `Properties/launchSettings.json` in each
project for the exact ports. Orbit.Api still needs a real Postgres to talk to even when run this way -
either start just that one container (`docker compose up -d postgres`, published on `localhost:5432`)
or point `ConnectionStrings:Orbit` at any other reachable PostgreSQL instance. Either way, set it via
`dotnet user-secrets` - there's no working default in `appsettings.json` on purpose, since a real
password can't live in a tracked file:

```
dotnet user-secrets set "ConnectionStrings:Orbit" "Host=localhost;Port=5432;Database=orbit;Username=orbit;Password=<your .env's POSTGRES_PASSWORD>" --project src/Server/Orbit.Api
```

Set the JWT signing key via `dotnet user-secrets` too (see
[Functionality — Authentication](functionality.md#authentication)); optionally configure SMTP and/or a
VAPID key pair the same way if you want to actually see reminder emails and push notifications locally
— see the two sections right below.

### Debugging from Visual Studio: the four F5 modes

Visual Studio's multi-project launch profiles (the dropdown next to the Start button) give one-keypress
debugging of a client together with Orbit.Api. The profiles are shared through `Orbit.slnLaunch` beside
the solution, which is committed — every machine gets the four modes with the clone. (A gitignored
`Orbit.slnLaunch.user` beside it holds any per-machine edits Visual Studio makes on top.) If the
dropdown does not show them, enable *Tools > Options > Preview Features > Enable Multi Launch
Profiles* and reopen the solution. All four start Orbit.Api under the debugger on
`https://localhost:7080` (plus `http://localhost:5080`, which is the address the Android emulator's
`10.0.2.2:5080` reaches); the pairs differ only in which client starts beside it and which database
the API opens:

| Mode | Client | Database |
| --- | --- | --- |
| `Orbit.Web local` | Orbit.Web dev server (`https://localhost:7081`) | local Postgres (`ConnectionStrings:Orbit`) |
| `Orbit.Web azure` | Orbit.Web dev server (`https://localhost:7081`) | Azure Postgres (`ConnectionStrings:OrbitAzure`) |
| `Android local` | Orbit.Maui on the Android emulator | local Postgres (`ConnectionStrings:Orbit`) |
| `Android azure` | Orbit.Maui on the Android emulator | Azure Postgres (`ConnectionStrings:OrbitAzure`) |

The azure pair works through the `https (Azure DB)` launch profile, which sets
`Database__ConnectionStringName=OrbitAzure` — `AddOrbitData` then reads that connection string instead
of `Orbit`, so the Azure credentials sit in user secrets next to the local ones and never in a tracked
file:

```
dotnet user-secrets set "ConnectionStrings:OrbitAzure" "Host=<server>.postgres.database.azure.com;Port=5432;Database=orbit;Username=orbit;Password=<password>;Ssl Mode=Require" --project src/Server/Orbit.Api
```

Two things the modes rely on:

- The Android emulator reaches the host's `localhost:5080` through `10.0.2.2:5080`, which is the MAUI
  debug build's baked-in default (see `OrbitApiSettings`); no extra configuration. A cold emulator is
  booted by Visual Studio as part of deploying — pick the device once in Orbit.Maui's debug-target
  dropdown and it sticks.
- The Azure Postgres server's firewall allows Azure IPs only, so the azure modes additionally need a
  firewall rule for your machine's public IP
  (`az postgres flexible-server firewall-rule create -g Orbit --server-name <server> --name <your-name> --start-ip-address <your-ip> --end-ip-address <your-ip>`)
  — and remember [the local database honesty rule](#keeping-the-local-database-honest): the azure modes
  point a development server at production data, so they are for reproducing production-shaped issues,
  not for routine work.
- The azure profile also sets `Database__ApplyMigrations=false`, so a debug session never changes the
  production schema. The flip side: a branch whose model is ahead of the deployed schema will fail its
  queries against the missing columns — that is the intended failure, not a bug. (Without the flag the
  first such session applies its branch's migrations to production on startup, which happened once,
  2026-09-07, with the additive folders migration.)

`Orbit.slnLaunch` holds four entries, each starting
`src\Server\Orbit.Api\Orbit.Api.csproj` (`DebugTarget` `https` for local, `https (Azure DB)` for azure)
plus either `src\Clients\Orbit.Web\Orbit.Web.csproj` (`DebugTarget` `https`) or
`src\Clients\Orbit.Maui\Orbit.Maui.csproj` (no `DebugTarget`, so the project's own device selection
applies). Editing the modes through *Configure Startup Projects… > Launch Profiles* updates the same
list — keep the *Share profile* box ticked so the change lands in the committed file rather than a
per-machine one.

### Configuring SMTP for local development

`dotnet run`/VS Code's debugger set `ASPNETCORE_ENVIRONMENT=Development` (see the launch profiles in
`Properties/launchSettings.json`), so `appsettings.Development.json` is loaded on top of the tracked
`appsettings.json` and never committed (`*.Development.json` is in `.gitignore`) — this is where local,
per-developer SMTP settings belong, never in the tracked `appsettings.json`. `Smtp:Password` goes through
`dotnet user-secrets` instead, on top of both, so it never touches a file on disk that could be
accidentally committed or copied elsewhere:

```
cd src/Server/Orbit.Api
cp appsettings.Development.json.example appsettings.Development.json
# then edit appsettings.Development.json and fill in Smtp:Host/UserName/FromAddress/etc.
dotnet user-secrets set "Smtp:Password" "<your SMTP password>"
```

Leaving all of this unset is fine too — see
[Functionality — Calendar event reminders](functionality.md#calendar-event-reminders) for what that
means at runtime.

### Configuring push notifications for local development

Same mechanism as SMTP above: `Vapid:PublicKeyBase64Url` and `Vapid:Subject` go in
`appsettings.Development.json`, `Vapid:PrivateKeyBase64Url` goes through `dotnet user-secrets`. Generate a
VAPID key pair once with, e.g., the `web-push` npm CLI:

```
npx web-push generate-vapid-keys
cd src/Server/Orbit.Api
cp appsettings.Development.json.example appsettings.Development.json
# then edit appsettings.Development.json and fill in Vapid:PublicKeyBase64Url/Subject
dotnet user-secrets set "Vapid:PrivateKeyBase64Url" "<your VAPID private key>"
```

Leaving this unset is fine too — see
[Functionality — Push notifications](functionality.md#push-notifications) for what that means at
runtime.

### Running the assistant's model locally

The `ollama` service in `docker-compose.yml` is the assistant's model on a developer machine — local
only, never deployed, and not started with the rest of the stack, since nothing else needs it:

```
docker compose up -d ollama
docker exec orbit-ollama ollama pull llama3.2:3b
```

The pulled model lives in the `orbit-ollama-models` volume, so `docker compose down` does not throw
away the two gigabytes. Point the API at it with `ASSISTANT_ENDPOINT=http://ollama:11434/v1` and
`ASSISTANT_MODEL=llama3.2:3b` in `.env` (from `dotnet run` outside Docker, use
`http://localhost:11434/v1` in `appsettings.Development.json` instead). Ollama authenticates nobody, so
`ASSISTANT_API_KEY` stays empty locally; against a hosted model it is a real secret and goes through
`dotnet user-secrets`, the way `Smtp:Password` does above.

Leaving all of it unset is fine, and is what a fresh checkout does: `POST /api/assistant/messages` then
answers 503 saying no model is configured, and nothing else changes.

What a reply actually costs on this hardware, and how badly a 3B model handles Polish, is measured in
[Local model measurements](ai-assistant-local-model-measurements.md).

## Keeping the local database honest

One Postgres serves whatever branch is checked out, and a migration applied by one branch stays applied
after you switch away from it: EF records what it ran, and nothing un-runs it. That is how this database
ended up carrying `DiagnosticLogEntries`, `SyncTombstones` and a mobile push column that `main` has never
heard of - they came from the mobile branch and outlived it. A local database that is a superset of the
deployed one is a local database that can prove the wrong thing: a query works here, and fails where it
matters.

What to check before trusting a local run - it needs no access to any deployment, since it compares the
database against the branch you are on:

```bash
docker compose exec -T postgres psql -U orbit -d orbit -tAc 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;' | sort > /tmp/applied.txt && ls src/Server/Orbit.Data/Migrations/*.cs | grep -v '\.Designer\.cs$' | xargs -n1 basename | sed 's/\.cs$//' | grep -v Snapshot | sort > /tmp/in-branch.txt && echo "applied here, not in this branch:" && comm -23 /tmp/applied.txt /tmp/in-branch.txt && echo "in this branch, not applied here:" && comm -13 /tmp/applied.txt /tmp/in-branch.txt
```

The first list is the one that matters. A name in it is either a migration from another branch (the
database has more schema than this code expects) or one that was deleted from the repository on purpose
- `GrantAdminAllPermissions` is the second kind and is expected to stay. The second list is normally
empty; anything in it means the API has not been started since the migration was added.

Starting over is the reliable fix, and costs nothing but the local data:

```bash
docker compose down -v && docker compose up -d
```

To avoid the drift in the first place, run a branch that carries its own migrations in its own stack
rather than this one - `docker compose` names its volumes after the project, so a checkout in its own
directory with its own project name gets its own database.

## Keeping Docker from eating the disk

Building this project's images is what fills a laptop. Every `docker compose build` leaves another layer
cache behind and orphans the image it replaced; on the machine this was written for that reached **27 GB
of build cache and 54 orphaned images**, with Docker's disk image at 40 GB.

`scripts/prune-docker-caches.sh` frees it, but only once there is something worth freeing:

```bash
scripts/prune-docker-caches.sh --dry-run        # what it would do, changing nothing
scripts/prune-docker-caches.sh                  # prune if over 25 GB
scripts/prune-docker-caches.sh --threshold-gigabytes 10
scripts/prune-docker-caches.sh --force          # prune whatever the size
```

It measures images, containers and build cache - what pruning can actually reclaim - and prunes in
order, stopping as soon as it is under: the build cache first, then orphaned layers, and only then
images no container is running, which is the one step that costs a re-pull. Stopped containers are left
alone: they are worth kilobytes, and removing them makes `docker compose ps` look like the stack was
never there.

**Named volumes are never touched.** That is where Postgres keeps the local database. `docker volume
prune` and `docker system prune --volumes` do not appear in the script at all, and volumes are left out
of the total it compares against - counting data it refuses to delete would have it clean up over and
over without ever getting under the threshold.

`scripts/test-prune-docker-caches.sh` drives all of that against a docker that only pretends, so the
rungs of the ladder - including the one that removes images - are exercised without removing anything.

### Running it by itself

```bash
sed "s|__REPOSITORY_PATH__|$PWD|g; s|__HOME__|$HOME|g" scripts/com.orbit.prune-docker-caches.plist \
  > ~/Library/LaunchAgents/com.orbit.prune-docker-caches.plist
launchctl load ~/Library/LaunchAgents/com.orbit.prune-docker-caches.plist
```

Hourly, and it writes to `~/Library/Logs/orbit-prune-docker-caches.log`. Under the threshold it exits in
well under a second without touching Docker, so the frequency costs nothing. To stop it:

```bash
launchctl unload ~/Library/LaunchAgents/com.orbit.prune-docker-caches.plist
```

macOS returns the freed space as Docker Desktop trims its own disk image, which can lag by a few
minutes - `du -sh ~/Library/Containers/com.docker.docker` is the number to watch, not the 228 GB
apparent size of `Docker.raw`, which is a sparse file.

## Further guides in this folder

- [`build.md`](build.md) — full machine setup and first build, from a fresh Windows or macOS
  installation (prerequisites, `.env` reference table, starting the stack, troubleshooting).
- [`instructions.md`](instructions.md) — trusting the local self-signed TLS certificate in Chrome on
  Windows, including the extra step Service Worker registration needs beyond the browser's own
  click-through warning.
