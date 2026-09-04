# Architecture

The solution is split into three layers: a backend (`src/Server`), the client(s) (`src/Clients`), and
code shared between them (`src/Shared`).

## `src/Server`

### Orbit.Api

An ASP.NET Core minimal API exposing:

- `/api/auth/register`, `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout` — see
  [Functionality — Authentication](functionality.md#authentication). Rate-limited to 5 requests per
  minute per client IP.
- `/api/notes`, `/api/tasks`, `/api/calendar-events`, `/api/users`, `/api/chat`, `/api/push` — all
  require a valid JWT and are scoped to the caller's own data.
- `/api/live` — a SignalR hub the web client holds open so it can be told what changed instead of
  polling for it. Announcements only, never content; see
  [Functionality — Live updates](functionality.md#live-updates). Authenticated from the query string,
  because a browser cannot put a header on a WebSocket handshake, and only on this path.
- `/health*` endpoints — liveness, readiness, and a full report covering the database, disk space,
  external services, and background services.

Logs through Serilog and emits OpenTelemetry traces, both at the lowest level (see
[Deployment topology](#deployment-topology-docker-compose-local) for where those traces go locally).

### Orbit.Data

EF Core persistence on PostgreSQL, isolated behind repository interfaces
(`INoteRepository`, `ITaskRepository`, `ICalendarEventRepository`, `IUserRepository`,
`IRefreshTokenRepository`, `IContactRepository`, `IChatMessageRepository`,
`IPushSubscriptionRepository`, `IOverdueTaskNotificationRepository`) so the domain layer in
`Orbit.Core` never depends on the storage technology. Schema changes are applied through EF Core
Migrations — see [Testing and Running Locally](testing-and-running-locally.md#database-migrations).

Entity classes sit in `Entities/` under three folders that mirror the table prefixes below — `Data/`,
`Links/` and `Setups/`. The folders group; they are not namespaces, so every entity stays in
`Orbit.Data.Entities` and no repository needs a second `using`.

#### Table and column names

Physical names live in one place, `OrbitStorageNames`, which renames the finished model at the end of
`OnModelCreating`. Nothing is named by EF's defaults, and an entity missing from that map throws at
startup rather than drifting out of the convention.

A table reads as `prefix_midfix[_postfix]`:

| Prefix | Holds | Examples |
| --- | --- | --- |
| `OP_` | what the user works on | `OP_NOTES`, `OP_TASKS_ITEMS`, `OP_INVENTORIES_SHARED` |
| `OL_` | rows that only join two of those tables | `OL_PUBLIC_SHARES`, `OL_CHATS_MEMBERS` |
| `OS_` | accounts, permissions, settings, bookkeeping | `OS_USERS`, `OS_SYNC_TOMBSTONES` |

A column repeats its table's prefix, shortens the midfix to initials, and ends with the property name
in upper case: `OP_NOTES.OP_N_ID`, `OP_NOTES_SHARED.OP_NS_ACCESSLEVEL`. Initials are taken letter by
letter from the midfix and postfix (`OP_TASKS_ITEMS` → `OP_TI_`); where two tables under one prefix
would collide, the midfix contributes its first three consonants (`OP_NOTIFICATIONS` → `OP_NTF_`) and a
run-together name its initials (`OS_REFRESH_TOKENS` → `OS_RT_`). The point is that a column carries its
table with it, so a query joining several reads without aliases.

One value deliberately keeps the old wording: `SharedItemType.Warehouse` is stored as text in
`OL_PUBLIC_SHARES` and travels inside chat payloads that were delivered before the rename, so renaming
it would orphan every public link handed out so far.

### Orbit.GoogleIntegration

Holds what Orbit needs from Google. Today that is authentication only: `GoogleIdentityVerifier`
validates the ID token the browser gets from Google Identity Services, and `GoogleAuthSettings` carries
the client id it checks against — see
[Functionality — Authentication](functionality.md#authentication). Kept in its own project rather than
in `Orbit.Api` so the Google SDK dependency stays off the API's own surface.

The Google Calendar/Contacts sync this project was originally reserved for still hasn't been started,
and shares nothing with the sign-in code beyond living here. See
[Future Plan](future-plan.md#planned-features) and
[Functionality — Calendar](functionality.md#calendar) for what the calendar does without it.

## `src/Clients`

### Orbit.Web

A Blazor WebAssembly client, served as static files through nginx in the Docker image. Unlike
Orbit.Api, it only logs errors to the browser console.

Two things it does that the API deliberately has no part in:

- **Encryption.** Chat messages, private notes/task lists/inventories, and shared positions are sealed and
  opened here, never on the server — see
  [Functionality](functionality.md#private-notes-and-task-lists).
- **The Google hand-off links.** `GoogleCalendarEventLink` and `GoogleMapsLink` build ordinary URLs in the
  browser. No Google API is called from anywhere in Orbit, and `Orbit.GoogleIntegration` on the server
  does nothing but verify a sign-in token.

### Orbit.Mobile and Orbit.Maui

The mobile client, split in two. `Orbit.Mobile` (`net10.0`) holds everything decided without a device
— view models, the local SQLite store, the outbox and sync spine, the crypto — and is in `Orbit.sln`,
so tests reach it. `Orbit.Maui` (`net10.0-android`, `net10.0-ios`) holds the two app heads and is
deliberately outside the solution: CI runs on `ubuntu-latest`, which can build neither.

It encrypts the same things Orbit.Web does, against the same wire format — a message sealed in one
opens in the other. See [Orbit.Maui — Plan](orbit-maui-plan.md) and
[Current Status](current-status.md#the-mobile-client).

## `src/Shared`

### Orbit.Core

Domain entities, command/query handlers, and a minimal in-process dispatcher (`IDispatcher`) that
routes each command/query to its handler and wraps every call with logging and timing, without
pulling in a full mediator library.

### Orbit.Contracts

The DTOs and request/response shapes the API, the Blazor client and the mobile client all reference,
so they cannot drift out of sync.

## Test projects

`tests/Orbit.Api.Tests`, `tests/Orbit.Web.Tests` and `tests/Orbit.Mobile.Tests` mirror the production
project layout. See
[Testing and Running Locally](testing-and-running-locally.md#automated-test-coverage) for exactly
what each one covers.

## Deployment topology (Docker Compose, local)

`docker-compose.yml` at the repository root wires four containers together for local development:

- **`orbit-aspire-dashboard`** (`mcr.microsoft.com/dotnet/aspire-dashboard`) — a local, visual view of
  everything Orbit.Api logs and traces, at `http://localhost:18888`. Orbit.Api sends both structured
  logs (Serilog OTLP sink) and traces (OpenTelemetry SDK) here, correlated by trace/span id, so a
  single user action shows up as one connected timeline. Dashboard authentication is disabled for this
  local prototype setup only (`DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`) — this must never be set
  in a non-local environment.
- **`postgres`** (`postgres:18-alpine`) — the same engine used in production (see below), published on
  `localhost:5432` so `dotnet run` outside Docker can reach it too. Data lives in the named volume
  `orbit-postgres-data`.
- **`orbit-api`** — built from `src/Server/Orbit.Api/Dockerfile`, published on `http://localhost:8081`.
  Configuration (JWT signing key, SMTP, VAPID keys, connection strings) is injected entirely through
  environment variables sourced from `.env` — see
  [Testing and Running Locally](testing-and-running-locally.md).
- **`orbit-web`** — built from `src/Clients/Orbit.Web/Dockerfile`, serving the Blazor client through
  nginx on `https://localhost:8443` (its one real entry point) and `http://localhost:8080` (redirects
  to the HTTPS port). Depends on `orbit-api`'s `/health/live` check reporting healthy before starting,
  not just on the container existing. TLS certificates live in a named volume
  (`orbit-web-certs`) and are self-signed on first startup — see
  [Testing and Running Locally](testing-and-running-locally.md#accessing-orbitweb-from-another-device-on-your-network).

`orbit-web`'s nginx reverse-proxies `/api/*` to `orbit-api`, so the browser always calls the API under
the same origin it loaded the page from and no CORS configuration is needed for the Docker Compose
topology.

## Production deployment (Azure Container Apps)

See [Azure Container Apps setup](azure-setup.md) for the full checklist of environment variables,
secrets, ingress settings, and persistent storage that have to be configured on the Container Apps
themselves - none of it is set up by the pipeline below.

Work does not reach `main` one pull request at a time. Every branch is merged into **`Coding`**
first, which costs a build and deploys nothing; `.github/workflows/integration-pr.yml` then keeps a
single draft pull request open from `Coding` to `main`, rewriting its description on each push and
closing it once the two agree. Merging that one is what deploys, so a run of feature work reaches
production as one deploy rather than as many - which is the point, since a deploy is the expensive
operation here.

That workflow needs one repository setting to be on: **Settings > Actions > General > "Allow GitHub
Actions to create and approve pull requests"**. It is off by default, and without it every run fails
on `GitHub Actions is not permitted to create or approve pull requests` - the integration pull request
then has to be opened by hand (`gh pr create --base main --head Coding --draft`).

`Coding` is the repository's default branch, so a new pull request proposes it without anyone
choosing. Being the default branch also decides which copy of a *scheduled* workflow runs: the nightly
branch cleanup executes `Coding`'s version of `cleanup-merged-branches.yml`, not main's. What holds the arrangement together beyond habit is `.github/workflows/guard-main.yml`,
which closes any pull request aimed at `main` from a branch other than `Coding` unless it carries the
`hotfix` label. It exists in place of branch protection, which this repository cannot have: GitHub
gates both classic protection and rulesets behind Pro for private repositories. A *direct push* to
`main` therefore remains possible and still deploys - no workflow can intercept one, since it runs
after the push has landed.

`.github/workflows/main_orbit.yml` builds and deploys Orbit on every push to `main`, matching the
local Docker Compose topology of two separate containers (rather than the single combined
App Service the project started with):

1. Logs into Azure via OIDC (`azure/login`) — no client secret is generated, stored, or rotated.
2. Builds the `orbit-api` and `orbit-web` images directly on the GitHub Actions runner and pushes them
   to Azure Container Registry, tagged both with the commit SHA and `latest` (ACR Tasks/`az acr build`
   is blocked on the Azure Free Trial subscription this project runs on, regardless of role
   assignments).
3. Updates the `orbit-api` and `orbit-web` Azure Container Apps to run the image tagged with the
   current commit SHA, so the deployed version is always traceable back to the workflow run that
   produced it.

Every push to `main` deploys straight to production - there is no staging slot, and no manual approval
gate. A GitHub Environment (`environment: production` on the job) would normally add one, but the
workflow deliberately does not use it here - see the comment on the `build-and-deploy` job for why
that broke `azure/login`'s OIDC federation the one time it was tried.

## Continuous integration

`.github/workflows/main_orbit.yml` runs on every push to `main` or `Coding` and on every pull request
into either (and can be triggered manually); only its deploy job is restricted to `main`. Its
`test` job restores, builds (`Release` configuration), and runs the full test suite
(`dotnet test Orbit.sln`) on `ubuntu-latest` with .NET SDK 10, then runs the two harnesses covering the
parts of the client no .NET test can reach, since bUnit executes none of the browser APIs they are made
of: `ci/verify-browser-crypto.mjs` for `wwwroot/js/e2eeChat.js` (Web Crypto and IndexedDB) and
`ci/verify-push-notifications.mjs` for `wwwroot/service-worker.js` and `wwwroot/js/pushNotifications.js`
(a registered service worker receiving real push events, and the Notification and Push APIs). Every
later job depends on this one, so a failure here stops the deploy before an image is built.

**The pull request trigger was removed once and put back.** It went because every minute is billed on a
private repository and a day of ordinary work exhausted the allowance, stopping Actions outright; it
came back because a branch unchecked until it lands stopped being theoretical - `main` sat red for a day
with nobody told. What made it affordable is that a run now costs a fraction of what it did: the
`android` job looks before it builds and does nothing when nothing it builds from changed, a pull
request run is cancelled by the next push to the same branch, and documentation-only branches are
skipped outright.

The `deploy` job stays out of it either way — guarded on the event as well as gated on the suite, so a
branch stops at the tests rather than deploying itself. Running the suite locally before opening a pull
request is still worth doing; it is no longer the only check a branch gets — see
[Testing and Running Locally](testing-and-running-locally.md#automated-test-coverage).
