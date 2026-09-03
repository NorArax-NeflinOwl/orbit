---
name: orbit-module-scaffold
description: How to add a new functional module (or a new synchronizable entity) to Orbit across Orbit.Core, Orbit.Data, Orbit.Contracts, Orbit.Api, Orbit.Web and the mobile client, following the existing command/query dispatcher pattern. Use whenever the user asks to "add a module", "add a new entity", design a new table or API surface, or build a feature slice that spans server and clients.
---

# Adding a module to Orbit

A module is the same vertical slice everywhere: commands and queries in `Orbit.Core`, an entity and
repository in `Orbit.Data`, DTOs in `Orbit.Contracts`, an endpoint group in `Orbit.Api`, an API client
and pages in `Orbit.Web`, and (when the phone needs it) a client, local store, synchronizer and screens
in `Orbit.Mobile`/`Orbit.Maui`. Tasks is the largest reference implementation; Notes is the simplest.
Read one of them end to end before writing anything.

## Before writing code

1. Read `info/architecture.md` and `info/functionality.md` (what exists and how it is wired).
2. Read the reference module end to end at every layer listed below.
3. Confirm the scope of the first slice with the user. A module lands as a vertical slice
   (one entity, one endpoint group, one page), not as a full feature. One slice = one PR.

## The pattern, layer by layer

### `src/Shared/Orbit.Core/<Module>/` — domain, commands, queries

- Domain types at the folder root (`TaskList.cs`, `TaskItem.cs`, …) plus the repository *interface*
  (`ITaskRepository.cs`) — `Orbit.Core` never references EF or Npgsql.
- One folder per operation: `<Verb><Thing>/<Verb><Thing>Command.cs` (a sealed record implementing
  `IRequest<TResult>`, first parameter `Guid UserId`) and `<Verb><Thing>CommandHandler.cs`
  (`IRequestHandler<TCommand, TResult>`). Queries follow the same shape (`Get<Thing>s/...Query.cs`).
- Commands that a client initiates carry `[ClientAction(ClientActionCategory....)]` so
  `LoggingDispatcher` tags them in the log stream; everything goes through `IDispatcher`, which also
  gives tracing via the `"Orbit.Core"` ActivitySource for free.
- Register every handler in `OrbitCoreServiceCollectionExtensions`.
- Access control: every handler resolves access itself (see `TaskListAccessResolver`,
  `CalendarEventAccessResolver`, `WarehouseAccessResolver`) using the `UserId` the command carries.
  A request for somebody else's data ends in `InvalidRequestException` → 400.

### `src/Server/Orbit.Data/` — persistence (PostgreSQL via EF Core)

- `Entities/<Thing>Entity.cs`, mapped in `OrbitDbContext`.
- `Repositories/<Thing>Repository.cs` implementing the `Orbit.Core` interface (plus a
  `...EntityMapper.cs` beside it when the mapping is not trivial).
- Register in `OrbitDataServiceCollectionExtensions.AddOrbitData`.
- Migration: `dotnet ef migrations add <Name>` (exact command in `info/testing-and-running-locally.md`); migrations are applied
  automatically at API startup (`dbContext.Database.Migrate()` in `Program.cs`), so a migration file
  is all a deployment needs.

### `src/Shared/Orbit.Contracts/<Module>/` — DTOs

- Request records and DTOs only; no domain-entity leakage. Both web and mobile read these.
- Private ("sealed") items follow the existing pattern: the payload travels as encrypted content
  (`EncryptedContentDto`, `Sealed<Thing>` records) and the server stores ciphertext it cannot read.
  Sealing happens at the repository boundary; a private item is *absent* from anything the server
  assembles, not redacted.

### `src/Server/Orbit.Api/<Module>/` — endpoints

- `<Thing>Endpoints.cs`: a static `Map<Module>Endpoints(this WebApplication app)` with
  `app.MapGroup("/api/<module>").RequireAuthorization()`, each endpoint pulling `ClaimsPrincipal` and
  `IDispatcher`, sending a command/query with `GetUserId(user)`, and mapping results with `ToDto`.
- Register the map call in `Program.cs` next to the other modules; do not restructure registration.
- If the module syncs to the phone, add a `GET /changes?since=` endpoint (see `TaskEndpoints`):
  it returns changed items plus deletions from `ISyncTombstoneRepository`, and deletes write a
  `SyncTombstone` instead of leaving the client guessing.
- Rate limiting only where the operation warrants it (`RateLimiterPolicyNames` +
  `RateLimiterPolicies`).

### `src/Clients/Orbit.Web/` — web client

- `Services/<Module>ApiClient.cs` in the shape of the existing clients, registered in `Program.cs`;
  it uses the configured base address — never a hardcoded `localhost`.
- Pages under `Pages/`, components under `Components/`; bUnit tests in `tests/Orbit.Web.Tests`.

### `src/Clients/Orbit.Mobile/` + `src/Clients/Orbit.Maui/` — phone (offline-first)

- `Api/<Module>Client.cs` — the HTTP client for the module.
- `Data/Local<Thing>.cs` + `Data/Local<Thing>Repository.cs` — the on-device SQLite store.
- `Sync/<Thing>Synchronizer.cs` plugged into `EverythingSynchronizer`; offline writes go through the
  outbox (`OutboxReplay`) and `SyncState`/`SyncCursors` track progress. Reuse `OfflineEditPolicy`
  and `SyncGate` rather than inventing module-specific variants.
- View models in `Screens/<Module>/`; XAML pages in `Orbit.Maui/Features/<Module>/`. Actions that
  need the server are disabled through `ConnectionRequirement` with a reason.

## Checklist per slice

- Handlers registered in `OrbitCoreServiceCollectionExtensions`, repositories in
  `OrbitDataServiceCollectionExtensions`, endpoints mapped in `Program.cs`.
- Migration added and reversible; startup applies it.
- Deletes leave a `SyncTombstone` if the module syncs; `/changes` covers it.
- Private items stay sealed end to end — no plaintext of them in server logs, telemetry, or DTOs.
- Telemetry: dispatch through `IDispatcher` is enough; do not create a new exporter (see `telemetry`).
- Tests at every touched layer: handler tests with stub repositories and endpoint tests via
  `TestServer` in `tests/Orbit.Api.Tests/<Module>/`, bUnit in `tests/Orbit.Web.Tests`, view-model
  tests with the in-memory SQLite `LocalStore` in `tests/Orbit.Mobile.Tests`.
- Names follow `orbit-conventions`. Run the whole solution's tests before the PR.

## Module-specific notes

- **Calendar**: store times in UTC; recurrence as RFC 5545 `RRULE` strings, not expanded rows.
- **Chat/messaging**: encryption is client-side; the API stores ciphertext and key ids only.
  Never log message bodies.
- **Location sharing**: coordinates are sensitive; retention and sharing scope are explicit fields,
  and location rows stay out of generic "export everything" endpoints.
