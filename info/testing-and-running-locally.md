# Testing and Running Locally

## Automated test coverage

Run the whole suite with:

```
dotnet test Orbit.sln
```

This also runs automatically in CI, but only on a push to `main` - nothing runs on a pull request or on `Coding` - so a
branch is checked before it lands rather than after. Documentation-only branches are skipped, and a
pull request run is cancelled by the next push to the same branch. See
[Architecture — Continuous integration](architecture.md#continuous-integration) for what that costs and
why it is affordable now when it was not before.

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

**One English string means one thing.** Where two screens genuinely need different Polish for the same
English word, the answer is a second English key, not a second entry: the phone's sync row says
`No connection` ("Bez połączenia") rather than `Offline` ("Niedostępny", which is about a person), and
its group count says `People` ("Osób") rather than `Members` ("Członkowie", which is the roster heading
and the wrong form to put a number after).

Separately, a value referring to a placeholder its English does not supply throws when that line is
written, and every entry is formatted once to prove it cannot. Fewer placeholders than the English is
allowed and deliberate: Polish plurals do not map onto an English "list"/"lists".

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
   for `#app` to stop saying "Loading…" tells the two apart.
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

- The `Chat` page saying why a conversation cannot be opened (an account the API will not resolve),
  which is checked by hand in a browser: the message on screen and the `Warning` it writes to this
  browser's own log. Rendering that page under bUnit means standing up seventeen injected services and
  the browser crypto behind them.
- `notificationclick` in `wwwroot/service-worker.js` — whether clicking a notification reuses an open
  Orbit tab or opens a new one. Nothing outside the operating system can raise a real click on a system
  notification, and Chrome DevTools has no command for it either, so this one branch is checked by hand.
  The rest of that file, and of `pushNotifications.js`, is covered — see below.
- The chat thread, whose interesting behaviour is timing: it is a polling component.

What used to be on this list and no longer is: push notifications end to end
(`ci/verify-push-notifications.mjs` and `PushNotificationManagerTests`, below), the `/api/auth/*` rate limiter
(`AuthRateLimiterTests`, against the very policies `Program.cs` installs), sending through
`SmtpEmailSender` and `VapidPushNotificationSender` (`SmtpEmailSenderTests` against a loopback SMTP
listener, `VapidPushNotificationSenderTests` against a stub transport), and `wwwroot/js/e2eeChat.js` —
see below. `Contacts` is covered by `ContactsGateTests` and `ContactInfoTests`.

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

It runs in the `test` job of `main_orbit.yml`, so it gates every pull request rather than only a deploy.
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
