# Testing and Running Locally

## Automated test coverage

Run the whole suite with:

```
dotnet test Orbit.sln
```

This also runs automatically in CI on every push and pull request to `main` — see
[Architecture — Continuous integration](architecture.md#continuous-integration).

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
`PushNotificationDispatcher`'s fan-out and expired-subscription pruning; and the overdue-task
notification scheduling logic (see
[Functionality — Push notifications](functionality.md#push-notifications)).

### `tests/Orbit.Web.Tests`

Covers the Blazor client's auth wiring: the token store; the handler that attaches the access token to
outgoing requests and transparently refreshes it after a 401; `AuthApiClient`;
`OrbitAuthenticationStateProvider`; `PushNotificationApiClient`; and the `Login`, `Register`, `Calendar`
(including `CalendarEventEditor`), `Dashboard`, `Tasks`, and `TaskListChecklist` pages themselves,
rendered with [bUnit](https://bunit.dev).

### What is not covered by an automated test today

See [Future Plan — Testing gaps](future-plan.md#testing-gaps) for the reasoning behind each of these
and what closing them would take:

- The `/api/auth/*` rate limiter and the exact 429 behavior.
- The client-side retry-after-refresh path end-to-end through a real `HttpClientHandler` pipeline.
- Actually sending an email through `SmtpEmailSender` or a push notification through
  `VapidPushNotificationSender`.
- The `Contacts`/`Chat` pages, `PushNotificationManager`, and
  `wwwroot/js/e2eeChat.js`/`wwwroot/js/pushNotifications.js`/`wwwroot/service-worker.js` — the
  encryption/decryption round trip, key generation and persistence in IndexedDB, the polling UI,
  browser notification permission handling, and the push subscription/service worker lifecycle have no
  automated coverage at all.

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

## Further guides in this folder

- [`build.md`](build.md) — full machine setup and first build, from a fresh Windows or macOS
  installation (prerequisites, `.env` reference table, starting the stack, troubleshooting).
- [`instructions.md`](instructions.md) — trusting the local self-signed TLS certificate in Chrome on
  Windows, including the extra step Service Worker registration needs beyond the browser's own
  click-through warning.
