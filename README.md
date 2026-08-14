# Orbit

Orbit is an all-in-one productivity app: notes, tasks, calendar, encrypted messaging, and location
sharing in a single account. The long-term target is a .NET MAUI client (mobile and desktop) backed
by a shared ASP.NET Core API, so every device stays in sync.

## Current status

This repository is an early-stage prototype. **Accounts, notes, and tasks** are implemented end to
end, including the Blazor web client: register or log in on `/register`/`/login`, and the notes and
tasks pages are only reachable once signed in. Calendar, encrypted messaging, location sharing, and the
MAUI client are not implemented yet.

## Architecture

The solution is split into three layers:

- **`src/Server`** — the backend.
  - `Orbit.Api`: an ASP.NET Core minimal API exposing `/api/auth/register` and `/api/auth/login`
    (issuing JWTs), the `/api/notes` and `/api/tasks` endpoints (both require a valid JWT and are
    scoped to the caller's own data), and a set of `/health*` endpoints (liveness, readiness, and a
    full report covering the database, disk space, external services, and background services). Logs
    through Serilog and emits OpenTelemetry traces.
  - `Orbit.Data`: EF Core persistence on SQLite, isolated behind `INoteRepository`/`ITaskRepository`/
    `IUserRepository` so the domain layer in `Orbit.Core` never depends on the storage technology.
  - `Orbit.GoogleIntegration`: an empty placeholder project for the future Google Calendar/Contacts
    integration referenced by the calendar feature.
- **`src/Clients/Orbit.Web`** — a Blazor WebAssembly client, currently the only client, served as
  static files through nginx in the Docker image. A MAUI client is planned but not started.
- **`src/Shared`** — code shared across server and clients.
  - `Orbit.Core`: domain entities, command/query handlers, and a minimal in-process dispatcher
    (`IDispatcher`) that routes each command/query to its handler and wraps every call with logging
    and timing, without pulling in a full mediator library.
  - `Orbit.Contracts`: the DTOs and request/response shapes the API and the Blazor client both
    reference, so the two can't drift out of sync.

`tests/Orbit.Api.Tests` covers the health check infrastructure and the accounts, notes, and tasks
features on the API side (password hashing, registration, login, per-owner note access, and per-owner
task list access including the checklist-completion rule). `tests/Orbit.Web.Tests` covers the Blazor
client's auth wiring: the token store, the handler that attaches it to outgoing requests,
`AuthApiClient`, `OrbitAuthenticationStateProvider`, and the `Login`/`Register` pages themselves
(rendered with [bUnit](https://bunit.dev)).

## Authentication

`POST /api/auth/register` (`email`, `userName`, `displayName`, `password`) and `POST /api/auth/login`
(`emailOrUserName`, `password`) both return `{ token, userId, email, displayName }` on success. Login
accepts either the account's email address or its username in the same field - both are unique, so
there's no ambiguity. Send the token on every `/api/notes` request as `Authorization: Bearer <token>`;
without it, the API returns 401.

The Blazor client handles this itself once signed in: `/login` and `/register` call the endpoints
above, store the returned token in `localStorage`, and a `DelegatingHandler` attaches it as a bearer
token to every subsequent API call. Any page that isn't explicitly public redirects to `/login` when
there's no valid token.

The JWT signing key is a secret and is never checked into source control:

- **Docker Compose**: copy `.env.example` to `.env` and fill in a random value for `JWT_SIGNING_KEY`
  (e.g. `openssl rand -base64 48`). Compose loads `.env` automatically.
- **`dotnet run` outside Docker**: `dotnet user-secrets set "Jwt:SigningKey" "<a long random string>"`
  from `src/Server/Orbit.Api`. The API fails fast on startup with a clear error if the key is missing
  or too short.

`requests.http` at the repo root has ready-to-run register/login/notes requests (works with Visual
Studio's built-in HTTP file support or VS Code's "REST Client" extension).

## Tasks

`POST /api/tasks` and `PUT /api/tasks/{id}` both take `{ title, items }`, where each item is
`{ description, dueDateUtc, isCompleted }` (`dueDateUtc` is optional). `GET /api/tasks` and
`GET /api/tasks/{id}` return the same shape back, plus `isCompleted` on the task list itself - this is
derived automatically (a list is complete only once every item on it is checked off) and can't be set
directly. Updating a task list always replaces its whole checklist rather than patching individual
items, since the client always sends the full current list back.

The domain type behind this is named `TaskList`, not `Task`: `Orbit.Core.Tasks.Task` would collide with
`System.Threading.Tasks.Task`, which every async method in the codebase returns.

In the Blazor client, each item's due date and time are edited separately (`InputDate` plus a native
`<input type="time">`) and combined into one timestamp on save; a date picked without a time is stored
as midnight.

## Running locally

The simplest way to run the whole stack is Docker Compose, which builds the API and the web client
and wires them together:

```
cp .env.example .env   # then fill in JWT_SIGNING_KEY
docker compose up --build
```

This starts:

- the web client at `http://localhost:8080`
- the API at `http://localhost:8081` (`/health`, `/health/ready`, `/health/live`, `/api/auth/*`,
  `/api/notes`, `/api/tasks`)
- the [Aspire dashboard](http://localhost:18888) for live logs and traces from the API

If you already had the stack running before the accounts feature was added, before login-by-username
was added, or before tasks were added, delete `data/orbit.db` (and any `orbit.db-shm`/`orbit.db-wal`
next to it) first — the API creates its SQLite schema once on first run (`EnsureCreated`, not
migrations) and won't add the new `Users` table, the `UserName` column on it, or the `Tasks`/`TaskItems`
tables, to an existing database file.

Alternatively, each project can be run directly with `dotnet run` from its own folder
(`src/Server/Orbit.Api`, `src/Clients/Orbit.Web`) using the `https` launch profile; see
`Properties/launchSettings.json` in each project for the exact ports. Set the JWT signing key via
`dotnet user-secrets` first (see "Authentication" above).

## Tests

```
dotnet test Orbit.sln
```

Also runs automatically in CI on every push and pull request to `main` (see
`.github/workflows/ci.yml`).

## License

MIT — see [LICENSE](LICENSE).
