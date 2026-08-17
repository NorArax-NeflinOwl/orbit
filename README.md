# Orbit

Orbit is an all-in-one productivity app: notes, tasks, calendar, encrypted messaging, and location
sharing in a single account. The long-term target is a .NET MAUI client (mobile and desktop) backed
by a shared ASP.NET Core API, so every device stays in sync.

## Current status

This repository is an early-stage prototype. **Accounts, notes, tasks, and a basic calendar** are
implemented end to end, including the Blazor web client: register or log in on `/register`/`/login`,
and the notes, tasks, and calendar pages are only reachable once signed in. Encrypted messaging,
location sharing, and the MAUI client are not implemented yet.

## Architecture

The solution is split into three layers:

- **`src/Server`** — the backend.
  - `Orbit.Api`: an ASP.NET Core minimal API exposing `/api/auth/register`, `/api/auth/login`,
    `/api/auth/refresh`, and `/api/auth/logout` (see "Authentication" below), the `/api/notes`,
    `/api/tasks`, and `/api/calendar-events` endpoints (all require a valid JWT and are scoped to the
    caller's own data), and a set of `/health*` endpoints (liveness, readiness, and a full report
    covering the database, disk space, external services, and background services). `/api/auth/*` is
    rate-limited (see "Authentication"). Logs through Serilog and emits OpenTelemetry traces, both at
    the lowest level.
  - `Orbit.Data`: EF Core persistence on SQLite, isolated behind `INoteRepository`/`ITaskRepository`/
    `ICalendarEventRepository`/`IUserRepository`/`IRefreshTokenRepository` so the domain layer in
    `Orbit.Core` never depends on the storage technology. Schema changes are applied through EF Core
    Migrations (see "Running locally").
  - `Orbit.GoogleIntegration`: an empty placeholder project for the future Google Calendar/Contacts
    sync referenced by the calendar feature (see "Calendar" below for what's implemented so far without it).
- **`src/Clients/Orbit.Web`** — a Blazor WebAssembly client, currently the only client, served as
  static files through nginx in the Docker image. Unlike Orbit.Api, it only logs errors to the browser
  console. A MAUI client is planned but not started.
- **`src/Shared`** — code shared across server and clients.
  - `Orbit.Core`: domain entities, command/query handlers, and a minimal in-process dispatcher
    (`IDispatcher`) that routes each command/query to its handler and wraps every call with logging
    and timing, without pulling in a full mediator library.
  - `Orbit.Contracts`: the DTOs and request/response shapes the API and the Blazor client both
    reference, so the two can't drift out of sync.

`tests/Orbit.Api.Tests` covers the health check infrastructure and the accounts, notes, tasks, and
calendar features on the API side (password hashing, refresh token issuing/redeeming/revoking,
registration, login, per-owner note access, per-owner task list access including the
checklist-completion rule and the task-list-linking rules below, per-owner calendar event access
including the start-before-end validation rule, and the calendar event reminder scheduling logic below).
`tests/Orbit.Web.Tests` covers the Blazor client's auth wiring: the token store, the
handler that attaches the access token to outgoing requests and transparently refreshes it after a 401,
`AuthApiClient`, `OrbitAuthenticationStateProvider`, and the `Login`/`Register` pages themselves
(rendered with [bUnit](https://bunit.dev)). Not covered by an automated test: the `/api/auth/*` rate
limiter and the exact 429 behavior, the client-side retry-after-refresh path end-to-end through a
real `HttpClientHandler` pipeline (both would need HTTP-integration test infrastructure -
`WebApplicationFactory` on the API side - that this project doesn't have yet), and actually sending an
email through `SmtpEmailSender` (needs a real or fake SMTP server to connect to).

## Authentication

`POST /api/auth/register` (`email`, `userName`, `displayName`, `password`) and `POST /api/auth/login`
(`emailOrUserName`, `password`) both return `{ token, refreshToken, userId, email, displayName }` on
success. Login accepts either the account's email address or its username in the same field - both are
unique, so there's no ambiguity. Send the access token on every `/api/notes`-style request as
`Authorization: Bearer <token>`; without it, the API returns 401.

`token` is a short-lived JWT (15 minutes by default, `Jwt:ExpiryMinutes`). `refreshToken` is a
long-lived (30 days), single-use, opaque value: `POST /api/auth/refresh` (`refreshToken`) exchanges it
for a new `{ token, refreshToken, ... }` pair and revokes the one that was redeemed, so a leaked refresh
token that gets replayed after the legitimate client already used it is rejected. `POST /api/auth/logout`
(`refreshToken`) revokes it outright. Only the SHA-256 hash of a refresh token is ever stored - a
database leak alone can't be used to sign in as a user, the same way a leaked password hash can't be
used to log in directly.

The Blazor client handles all of this itself once signed in: `/login` and `/register` call the
endpoints above and store both returned tokens in `localStorage`; a `DelegatingHandler` attaches the
access token as a bearer token to every subsequent API call, and if a call comes back 401 (the access
token expired), transparently redeems the refresh token for a new pair and retries the call once before
giving up. Logging out revokes the refresh token on the API and clears both tokens locally. Any page
that isn't explicitly public redirects to `/login` when there's no valid access token.

`/api/auth/register`, `/api/auth/login`, `/api/auth/refresh`, and `/api/auth/logout` are all rate
limited to 5 requests per minute per client IP address (no queueing - an excess request gets an
immediate 429), as brute-force protection for login attempts in particular.

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
`{ description, dueDateUtc, isCompleted, linkedTaskListId }` (`dueDateUtc` and `linkedTaskListId` are
both optional). `GET /api/tasks` and `GET /api/tasks/{id}` return the same shape back, plus
`isCompleted` on the task list itself - this is derived automatically (a list is complete only once
every item on it is checked off) and can't be set directly. Updating a task list always replaces its
whole checklist rather than patching individual items, since the client always sends the full current
list back.

The domain type behind this is named `TaskList`, not `Task`: `Orbit.Core.Tasks.Task` would collide with
`System.Threading.Tasks.Task`, which every async method in the codebase returns.

An item can instead reference another of the user's task lists via `linkedTaskListId`, rather than
being independently completable. A linked item's `isCompleted` is entirely derived - it follows the
referenced list's own completion (true only once every item on that list is checked off) and is
resolved live on every read (`LinkedTaskCompletionResolver`), the same "never trust the persisted
completion column, always recompute it" approach `TaskList.IsCompleted` already used, extended
transitively across a chain of linked lists. Because of this, a linked item's completion **cannot be
set manually** - `isCompleted` in the request is ignored for a linked item and it is always stored as
not completed; the only way to complete it is to complete every item on the list it links to.

`linkedTaskListId` is validated on create and update (`TaskListLinkValidator`): it must reference a
task list that exists and is owned by the same user, an item can't link to the list it belongs to, and
a link can't close a cycle between task lists (directly, or transitively through a chain of other
links) - either of the last two would make completion resolution loop forever without this check. A
validation failure throws `ArgumentException`, which is not caught anywhere and surfaces as an
unhandled 500, matching how `CalendarEvent`'s start-before-end validation already behaves in this
codebase. The Blazor client's task editor only excludes linking a list to itself from its dropdown of
linkable lists; it does not check for longer cycles client-side, so building one still relies on the
API's validation and surfaces as a failed save rather than a client-side error message - a known rough
edge, not a silent gap.

In the Blazor client, each item's due date and time are edited separately (`InputDate` plus a native
`<input type="time">`) and combined into one timestamp on save; a date picked without a time is stored
as midnight.

## Calendar

`POST /api/calendar-events` and `PUT /api/calendar-events/{id}` both take `{ details }`, where `details`
is `{ title, description, location, color, startUtc, endUtc, isAllDay, recurrence, guests,
reminderMinutesBeforeStart }` (`description`, `location`, `color`, and `recurrence` are all optional).
`GET /api/calendar-events` and `GET /api/calendar-events/{id}` return the same shape back, wrapped with
`id`, `createdAtUtc`, and `updatedAtUtc`. The fields are grouped under `details` on the wire, not spread
across the request body, because there are enough of them that flattening them out would be harder to
read - see `CalendarEventDetails` in `Orbit.Core.Calendar` for the same grouping on the domain side.

`endUtc` can't be before `startUtc` (`CalendarEvent.ValidateTimeRange`) - checked both server-side, where
a violation throws `ArgumentException` and surfaces as an unhandled 500, and client-side in
`CalendarEventEditor.razor`, which shows an inline error instead of submitting a request that's bound to
fail.

`location`, when present, is `{ address, latitude, longitude }` - `address` is optional (reverse
geocoding can fail to resolve one), `latitude` and `longitude` are always required and validated to be
within their valid ranges (±90/±180 degrees). Unlike the rest of the form, this isn't free text: the
Blazor client's event editor has a "Wybierz na mapie" button that opens an embedded
[Leaflet](https://leafletjs.com) map (OpenStreetMap tiles, loaded from a CDN - no API key needed, see
`wwwroot/index.html` and `wwwroot/js/mapPicker.js`). Clicking a point on the map stores its coordinates
and resolves an address for them via OpenStreetMap's free Nominatim reverse-geocoding endpoint
(`GeocodingApiClient`); typing directly into the address field only relabels an already-picked point; it
doesn't set a location on its own; and the Nominatim call intentionally does not go through
`AuthorizationMessageHandler`, so Orbit's own bearer token is never sent to that third-party host.
Nominatim's usage policy caps this to light, non-commercial traffic - a deployment with real volume
should self-host it instead (see https://operations.osmfoundation.org/policies/nominatim/).

`recurrence`, when present, is `{ frequency, intervalCount, untilUtc }` (`frequency` is `"Daily"`,
`"Weekly"`, or `"Monthly"`; `untilUtc` is optional). A recurring event is stored as a single event
carrying this rule - the API does not expand it into individual occurrences, so the calendar page lists
each recurring event once with the rule described in text (e.g. "co tydzień, do 20.12.2026") rather than
showing every future occurrence. Turning that into a real occurrence expansion, and a month/week grid
view to place them on, is follow-up work.

Like notes and tasks, calendar events can be created and updated but not deleted yet. Guests and
reminders (`reminderMinutesBeforeStart`, minutes before the event starts) are edited in the Blazor
client as comma-separated text rather than as an add/remove list like task items are, since neither
needed per-item editing for a first pass.

### Calendar event reminders

Each `reminderMinutesBeforeStart` entry results in one email to the event's owner (the account that
created it - not the `guests` list, which isn't wired to notifications yet), sent once its lead time is
reached. This runs entirely inside Orbit.Api as `CalendarEventReminderBackgroundService`, a
`BackgroundService` that polls once a minute: sending real email needs SMTP credentials, and those must
never reach the Blazor WebAssembly client, so this can't live in Orbit.Web despite reminders being a
calendar-page feature. `EventReminderScheduler` (`Orbit.Core.Calendar.Reminders`) holds the actual
"what's due right now" logic, kept independent of ASP.NET Core hosting so it's unit-testable on its own.

A reminder is due once `startUtc` minus its lead time has passed, and stays eligible for 5 minutes after
that (`LookBackWindow`) so a reminder isn't lost if a poll is briefly delayed - after that window it's
treated as missed rather than emailed late. Each event/lead-time pair is recorded in a dedicated
`EventReminderDeliveries` table once sent (unique-indexed on the pair), so the same reminder is never
emailed twice even across restarts. Recurring events only get a reminder for the single `startUtc` they
carry, matching the existing limitation that recurring events aren't expanded into individual
occurrences server-side (see above).

Email is sent via [MailKit](https://github.com/jstedfast/MailKit) (`SmtpEmailSender`), configured
through the `Smtp` section (`Smtp:Host`, `Smtp:Port`, `Smtp:UserName`, `Smtp:FromAddress`,
`Smtp:FromDisplayName`, `Smtp:UseStartTls`) plus `Smtp:Password` from an environment variable or
user-secrets - never from a committed appsettings file (see "Running locally" below for exactly where).
Unlike the JWT signing key, SMTP isn't required to start the API: `SmtpEmailSender` just logs a warning
and skips sending when `Smtp:Host`/`Smtp:FromAddress` are unset, so a fresh local checkout still runs
without anyone having set up email delivery. The background service reports a heartbeat to the existing
`HostedServiceHealthTracker` on every poll (success or failure), so a crashed or stuck reminder loop
shows up in the `hosted-services` health check the same way any other background service would.

## Running locally

The simplest way to run the whole stack is Docker Compose, which builds the API and the web client
and wires them together:

```
cp .env.example .env   # then fill in JWT_SIGNING_KEY (required) and the SMTP_* variables (optional)
docker compose up --build
```

Leaving the `SMTP_*` variables blank is fine - see "Calendar event reminders" above for what that means
at runtime. This starts:

- the web client at `http://localhost:8080`
- the API at `http://localhost:8081` (`/health`, `/health/ready`, `/health/live`, `/api/auth/*`,
  `/api/notes`, `/api/tasks`, `/api/calendar-events`)
- the [Aspire dashboard](http://localhost:18888) for live logs and traces from the API

Orbit.Api applies EF Core Migrations on startup (`Database.Migrate()`) rather than the prototype
`EnsureCreated()` approach used previously - it creates the SQLite schema on first run and brings an
existing database up to date with any migrations added since. **After pulling changes that touch the EF
Core model in `Orbit.Data`** (entities under `src/Server/Orbit.Data/Entities`, or `OrbitDbContext`),
generate the corresponding migration once with the [`dotnet-ef` tool](https://learn.microsoft.com/ef/core/cli/dotnet)
(`dotnet tool install --global dotnet-ef` if it isn't installed yet):

```
dotnet ef migrations add <DescriptiveName> --project src/Server/Orbit.Data --startup-project src/Server/Orbit.Api
```

If you already had the stack running before EF Core Migrations replaced `EnsureCreated()`, delete
`data/orbit.db` (and any `orbit.db-shm`/`orbit.db-wal` next to it) once before starting the updated API
- a database created by `EnsureCreated()` has no migration history table, so `Migrate()` would otherwise
try to create tables that already exist and fail.

Alternatively, each project can be run directly with `dotnet run` from its own folder
(`src/Server/Orbit.Api`, `src/Clients/Orbit.Web`) using the `https` launch profile; see
`Properties/launchSettings.json` in each project for the exact ports. Set the JWT signing key via
`dotnet user-secrets` first (see "Authentication" above); optionally set `Smtp:Password` the same way
(`dotnet user-secrets set "Smtp:Password" "<your SMTP password>"`) alongside the rest of the `Smtp`
section in `appsettings.Development.json` if you want to actually see reminder emails locally.

## Tests

```
dotnet test Orbit.sln
```

Also runs automatically in CI on every push and pull request to `main` (see
`.github/workflows/ci.yml`).

## License

MIT — see [LICENSE](LICENSE).
