# Orbit

Orbit is an all-in-one productivity app: notes, tasks, calendar, encrypted messaging, and location
sharing in a single account. The long-term target is a .NET MAUI client (mobile and desktop) backed
by a shared ASP.NET Core API, so every device stays in sync.

## Current status

This repository is an early-stage prototype. The only feature implemented end to end so far is
**notes** (create, edit, list), served through a web client while the MAUI client is still to come.
Tasks, calendar, encrypted messaging, and location sharing are not implemented yet.

## Architecture

The solution is split into three layers:

- **`src/Server`** — the backend.
  - `Orbit.Api`: an ASP.NET Core minimal API exposing the `/api/notes` endpoints and a set of
    `/health*` endpoints (liveness, readiness, and a full report covering the database, disk space,
    external services, and background services). Logs through Serilog and emits OpenTelemetry traces.
  - `Orbit.Data`: EF Core persistence on SQLite, isolated behind `INoteRepository` so the domain layer
    in `Orbit.Core` never depends on the storage technology.
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

`tests/Orbit.Api.Tests` covers the health check infrastructure; the notes feature does not have
automated tests yet.

## Running locally

The simplest way to run the whole stack is Docker Compose, which builds the API and the web client
and wires them together:

```
docker compose up --build
```

This starts:

- the web client at `http://localhost:8080`
- the API at `http://localhost:8081` (`/health`, `/health/ready`, `/health/live`)
- the [Aspire dashboard](http://localhost:18888) for live logs and traces from the API

Alternatively, each project can be run directly with `dotnet run` from its own folder
(`src/Server/Orbit.Api`, `src/Clients/Orbit.Web`) using the `https` launch profile; see
`Properties/launchSettings.json` in each project for the exact ports.

## Tests

```
dotnet test Orbit.sln
```

Also runs automatically in CI on every push and pull request to `main` (see
`.github/workflows/ci.yml`).

## License

MIT — see [LICENSE](LICENSE).
