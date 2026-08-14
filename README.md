# Orbit

Orbit is an all-in-one productivity app: notes, tasks, calendar, encrypted messaging, and location
sharing in a single account. The long-term target is a .NET MAUI client (mobile and desktop) backed
by a shared ASP.NET Core API, so every device stays in sync.

## Current status

This repository is an early-stage prototype. **Accounts and notes** are implemented end to end,
including the Blazor web client: register or log in on `/register`/`/login`, and the notes pages are
only reachable once signed in. Tasks, calendar, encrypted messaging, location sharing, and the MAUI
client are not implemented yet.

## Architecture

The solution is split into three layers:

- **`src/Server`** — the backend.
  - `Orbit.Api`: an ASP.NET Core minimal API exposing `/api/auth/register` and `/api/auth/login`
    (issuing JWTs), the `/api/notes` endpoints (require a valid JWT, scoped to the caller's own notes),
    and a set of `/health*` endpoints (liveness, readiness, and a full report covering the database,
    disk space, external services, and background services). Logs through Serilog and emits
    OpenTelemetry traces.
  - `Orbit.Data`: EF Core persistence on SQLite, isolated behind `INoteRepository`/`IUserRepository` so
    the domain layer in `Orbit.Core` never depends on the storage technology.
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

`tests/Orbit.Api.Tests` covers the health check infrastructure and the accounts and notes features on
the API side (password hashing, registration, login, and per-owner note access). `tests/Orbit.Web.Tests`
covers the Blazor client's auth wiring: the token store, the handler that attaches it to outgoing
requests, `AuthApiClient`, `OrbitAuthenticationStateProvider`, and the `Login`/`Register` pages
themselves (rendered with [bUnit](https://bunit.dev)).

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
  `/api/notes`)
- the [Aspire dashboard](http://localhost:18888) for live logs and traces from the API

If you already had the stack running before the accounts feature was added, or before login-by-username
was added, delete `data/orbit.db` (and any `orbit.db-shm`/`orbit.db-wal` next to it) first — the API
creates its SQLite schema once on first run (`EnsureCreated`, not migrations) and won't add the new
`Users` table, or the `UserName` column on it, to an existing database file.

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
