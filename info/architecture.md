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
- `/health*` endpoints — liveness, readiness, and a full report covering the database, disk space,
  external services, and background services.

Logs through Serilog and emits OpenTelemetry traces, both at the lowest level (see
[Deployment topology](#deployment-topology-docker-compose-local) for where those traces go locally).

### Orbit.Data

EF Core persistence on SQLite, isolated behind repository interfaces
(`INoteRepository`, `ITaskRepository`, `ICalendarEventRepository`, `IUserRepository`,
`IRefreshTokenRepository`, `IContactRepository`, `IChatMessageRepository`,
`IPushSubscriptionRepository`, `IOverdueTaskNotificationRepository`) so the domain layer in
`Orbit.Core` never depends on the storage technology. Schema changes are applied through EF Core
Migrations — see [Testing and Running Locally](testing-and-running-locally.md#database-migrations).

### Orbit.GoogleIntegration

An empty placeholder project for the future Google Calendar/Contacts sync referenced by the calendar
feature. See [Future Plan](future-plan.md#planned-features) and
[Functionality — Calendar](functionality.md#calendar) for what's implemented so far without it.

## `src/Clients`

### Orbit.Web

A Blazor WebAssembly client, currently the only client, served as static files through nginx in the
Docker image. Unlike Orbit.Api, it only logs errors to the browser console. A MAUI client is planned
but not started — see [Future Plan](future-plan.md#planned-features).

## `src/Shared`

### Orbit.Core

Domain entities, command/query handlers, and a minimal in-process dispatcher (`IDispatcher`) that
routes each command/query to its handler and wraps every call with logging and timing, without
pulling in a full mediator library.

### Orbit.Contracts

The DTOs and request/response shapes the API and the Blazor client both reference, so the two can't
drift out of sync.

## Test projects

`tests/Orbit.Api.Tests` and `tests/Orbit.Web.Tests` mirror the production project layout. See
[Testing and Running Locally](testing-and-running-locally.md#automated-test-coverage) for exactly
what each one covers.

## Deployment topology (Docker Compose, local)

`docker-compose.yml` at the repository root wires three containers together for local development:

- **`orbit-aspire-dashboard`** (`mcr.microsoft.com/dotnet/aspire-dashboard`) — a local, visual view of
  everything Orbit.Api logs and traces, at `http://localhost:18888`. Orbit.Api sends both structured
  logs (Serilog OTLP sink) and traces (OpenTelemetry SDK) here, correlated by trace/span id, so a
  single user action shows up as one connected timeline. Dashboard authentication is disabled for this
  local prototype setup only (`DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`) — this must never be set
  in a non-local environment.
- **`orbit-api`** — built from `src/Server/Orbit.Api/Dockerfile`, published on `http://localhost:8081`.
  Configuration (JWT signing key, SMTP, VAPID keys, connection strings) is injected entirely through
  environment variables sourced from `.env` — see
  [Testing and Running Locally](testing-and-running-locally.md). The SQLite database file is bind-mounted
  from `./data` on the host rather than a named volume, so it can be inspected with an external SQLite
  client while the container runs.
- **`orbit-web`** — built from `src/Clients/Orbit.Web/Dockerfile`, serving the Blazor client through
  nginx on `https://localhost:8443` (its one real entry point) and `http://localhost:8080` (redirects
  to the HTTPS port). Depends on `orbit-api`'s `/health/live` check reporting healthy before starting,
  not just on the container existing. TLS certificates live in a named volume
  (`orbit-web-certs`) and are self-signed on first startup — see
  [Testing and Running Locally](testing-and-running-locally.md#accessing-orbit.web-from-another-device-on-your-network).

`orbit-web`'s nginx reverse-proxies `/api/*` to `orbit-api`, so the browser always calls the API under
the same origin it loaded the page from and no CORS configuration is needed for the Docker Compose
topology.

## Production deployment (Azure Container Apps)

See [Azure Container Apps setup](azure-setup.md) for the full checklist of environment variables,
secrets, ingress settings, and persistent storage that have to be configured on the Container Apps
themselves - none of it is set up by the pipeline below.

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

Every push to `main` deploys straight to production - there is no staging slot. The workflow's job
targets a `production` GitHub Environment, which supports adding a required-reviewer gate (a human
must approve the run before it deploys) under the repo's Settings > Environments > production; this
isn't configured by default, since the workflow file alone can't turn it on.

## Continuous integration

`.github/workflows/ci.yml` runs on every push and pull request targeting `main` (and can also be
triggered manually): it restores, builds (`Release` configuration), and runs the full test suite
(`dotnet test Orbit.sln`) on `ubuntu-latest` with .NET SDK 10.
