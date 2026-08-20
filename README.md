# Orbit

Orbit is an all-in-one productivity app: notes, tasks, calendar, encrypted messaging, and location
sharing in a single account. The long-term target is a .NET MAUI client (mobile and desktop) backed
by a shared ASP.NET Core API, so every device stays in sync.

## Current status

This repository is an early-stage prototype. **Accounts, notes, tasks, a basic calendar, and
end-to-end-encrypted 1:1 chat** are implemented end to end, including the Blazor web client: register or
log in on `/register`/`/login`, and the dashboard, notes, tasks, calendar, and contacts/chat pages are
only reachable once signed in. Signing in lands on the dashboard (`/`, see "Dashboard" below), which
lists notes, task lists, and calendar events at a glance; each has its own page (`/notes`, `/tasks`,
`/calendar`) where it can be created, edited, or deleted. `/contacts` searches for other users and lists
existing conversations; see "Contacts and encrypted chat" below. Once a user approves browser
notifications, **push notifications** fire for approaching calendar events, new chat messages, and
overdue tasks - see "Push notifications" below. Location sharing and the MAUI client are not implemented
yet.

## Architecture

The solution is split into three layers:

- **`src/Server`** — the backend.
  - `Orbit.Api`: an ASP.NET Core minimal API exposing `/api/auth/register`, `/api/auth/login`,
    `/api/auth/refresh`, and `/api/auth/logout` (see "Authentication" below), the `/api/notes`,
    `/api/tasks`, `/api/calendar-events`, `/api/users`, `/api/chat`, and `/api/push` endpoints (all
    require a valid JWT and are scoped to the caller's own data), and a set of `/health*` endpoints
    (liveness, readiness, and a full report covering the database, disk space, external services, and
    background services). `/api/auth/*` is rate-limited (see "Authentication"). Logs through Serilog and
    emits OpenTelemetry traces, both at the lowest level.
  - `Orbit.Data`: EF Core persistence on SQLite, isolated behind `INoteRepository`/`ITaskRepository`/
    `ICalendarEventRepository`/`IUserRepository`/`IRefreshTokenRepository`/`IContactRepository`/
    `IChatMessageRepository`/`IPushSubscriptionRepository`/`IOverdueTaskNotificationRepository` so the
    domain layer in `Orbit.Core` never depends on the storage technology. Schema changes are applied
    through EF Core Migrations (see "Running locally").
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

`tests/Orbit.Api.Tests` covers the health check infrastructure and the accounts, notes, tasks,
calendar, contacts/chat, and push notification features on the API side (password hashing, refresh token
issuing/redeeming/revoking, registration, login, per-owner note access including deletion, per-owner
task list access including the checklist-completion rule, the task-list-linking rules below, and
deletion, per-owner calendar event access including the start-before-end validation rule and deletion,
the calendar event reminder scheduling logic below, exact-match user search including self-exclusion,
setting a user's public key, the chat message/contact handlers including the
first-message-creates-a-contact-in-both-directions rule and the push notification it sends the
recipient, subscribing/unsubscribing a push endpoint, `PushNotificationDispatcher`'s fan-out and
expired-subscription pruning, and the overdue-task notification scheduling logic below).
`tests/Orbit.Web.Tests` covers the Blazor client's auth wiring: the token store, the
handler that attaches the access token to outgoing requests and transparently refreshes it after a 401,
`AuthApiClient`, `OrbitAuthenticationStateProvider`, `PushNotificationApiClient`, and the
`Login`/`Register` pages themselves (rendered with [bUnit](https://bunit.dev)). Not covered by an
automated test: the `/api/auth/*` rate limiter and the exact 429 behavior, the client-side
retry-after-refresh path end-to-end through a real `HttpClientHandler` pipeline (both would need
HTTP-integration test infrastructure - `WebApplicationFactory` on the API side - that this project
doesn't have yet), actually sending an email through `SmtpEmailSender` or a push notification through
`VapidPushNotificationSender` (both need a real or fake server to connect to), the
`Notes`/`Tasks`/`Calendar`/`Dashboard` pages themselves (the delete-confirmation flow and the
dashboard's per-column rendering only have the command-handler tests above, not a bUnit test against the
actual pages, unlike `Login`/`Register`), and the `Contacts`/`Chat` pages, `PushNotificationManager`, and
`wwwroot/js/e2eeChat.js`/`wwwroot/js/pushNotifications.js`/`wwwroot/service-worker.js` (the
encryption/decryption round trip, key generation and persistence in IndexedDB, the polling UI, browser
notification permission handling, and the push subscription/service worker lifecycle have no automated
coverage at all - bUnit doesn't execute real browser crypto/IndexedDB/Push/Notification APIs, and this
project has no browser-driven test infrastructure like Playwright yet; only the server-side command/query
handlers and the pure-HTTP `PushNotificationApiClient` above are tested).

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

`DELETE /api/tasks/{id}` deletes a task list (and its items, via the `ON DELETE CASCADE` foreign key
from `TaskItemEntity` to its owning list - see `OrbitDbContext`); like the other endpoints, it 404s if
the id doesn't exist or isn't owned by the caller. Deleting a list that another list's item links to via
`linkedTaskListId` leaves that reference dangling rather than blocking the delete or cascading further -
`LinkedTaskCompletionResolver` already treats a link to a missing list as "not completed" instead of
failing, so this is safe, just something to be aware of if a list you expect to still be linkable is
gone. The Blazor client's task list page asks for confirmation before calling this endpoint.

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

Guests and reminders (`reminderMinutesBeforeStart`, minutes before the event starts) are edited in the
Blazor client as comma-separated text rather than as an add/remove list like task items are, since
neither needed per-item editing for a first pass.

`DELETE /api/calendar-events/{id}` deletes an event, 404ing under the same ownership rule as the other
endpoints. Any reminder claims already recorded for it in `EventReminderDeliveries` (see "Calendar event
reminders" below) are left in place rather than cleaned up, since they're not a foreign key relationship
and a deleted event simply stops producing new reminders. The Blazor client's calendar page asks for
confirmation before calling this endpoint.

### Calendar event reminders

Two independent notification emails can go to an event's owner (the account that created it - not the
`guests` list, which isn't wired to notifications yet), each gated by its own checkbox in the event
editor:

- **`notifyOnCreation`**: sent once, immediately, the first time the event is saved. Handled directly in
  `CreateCalendarEventCommandHandler` - not by the polling service below - since it's a one-off reaction
  to a single request rather than something that needs to be discovered later. A failure to send it (e.g.
  SMTP briefly unreachable) is logged but never fails the request: the event is already persisted by that
  point.
- **`notifyBeforeStart`**: sent per `reminderMinutesBeforeStart` entry, once its lead time before the
  event's start is reached. All-day events are anchored to local midnight (see `CalendarEventEditor.razor`),
  so a `0`-minutes entry on one of those fires "at 00:00" - except when the event is all-day *and* was
  created on the same calendar day it starts: that specific reminder is suppressed, since the creation
  email above already told the owner about an event starting the same day.

The "approaching event" side runs entirely inside Orbit.Api as `CalendarEventReminderBackgroundService`, a
`BackgroundService` that polls once a minute: sending real email needs SMTP credentials, and those must
never reach the Blazor WebAssembly client, so this can't live in Orbit.Web despite reminders being a
calendar-page feature. `EventReminderScheduler` (`Orbit.Core.Calendar.Reminders`) holds the actual
"what's due right now" logic (including the all-day/same-day suppression rule above), kept independent of
ASP.NET Core hosting so it's unit-testable on its own.

A reminder is due once `startUtc` minus its lead time has passed, and stays eligible for 5 minutes after
that (`LookBackWindow`) so a reminder isn't lost if a poll is briefly delayed - after that window it's
treated as missed rather than emailed late. A single poll sends at most 100 reminders
(`MaxRemindersPerPoll`), protecting against a burst of simultaneously due reminders overwhelming the SMTP
server; anything past that cap is simply picked up on the next minute's poll.

Each reminder is reserved before it's sent, not just recorded after: `EventReminderRepository.TryClaimAsync`
inserts its row into a dedicated `EventReminderDeliveries` table (unique-indexed on the event/lead-time
pair) *before* the email goes out, and only sends if that insert wins the race. A failed insert means
another worker already claimed the same reminder, so this one backs off instead of sending a duplicate -
the unique index is the actual concurrency guard, not the earlier existence check
(`HasBeenSentAsync`), which stays as a cheap pre-filter only. This is what makes it safe to eventually run
more than one instance of this background service at once, without needing a distributed lock or message
queue: whichever instance's insert lands first wins, everyone else backs off. If sending fails after the
claim succeeds (e.g. a transient SMTP error), `ReleaseClaimAsync` removes the reservation so the reminder
is retried on a later poll instead of being silently lost. Recurring events only get a reminder for the
single `startUtc` they carry, matching the existing limitation that recurring events aren't expanded into
individual occurrences server-side (see above).

Email is sent via [MailKit](https://github.com/jstedfast/MailKit) (`SmtpEmailSender`), configured
through the `Smtp` section (`Smtp:Host`, `Smtp:Port`, `Smtp:UserName`, `Smtp:FromAddress`,
`Smtp:FromDisplayName`, `Smtp:UseStartTls`) plus `Smtp:Password` from an environment variable or
user-secrets - never from a committed appsettings file (see "Running locally" below for exactly where).
Unlike the JWT signing key, SMTP isn't required to start the API: `SmtpEmailSender` just logs a warning
and skips sending when `Smtp:Host`/`Smtp:FromAddress` are unset, so a fresh local checkout still runs
without anyone having set up email delivery. The background service reports a heartbeat to the existing
`HostedServiceHealthTracker` on every poll (success or failure), so a crashed or stuck reminder loop
shows up in the `hosted-services` health check the same way any other background service would.

## Contacts and encrypted chat

`/contacts` (`Contacts.razor`) searches for another user by their **exact** email address or username
(`GET /api/users/search?query=`, tried as an email first and then as a username - no partial/fuzzy
matching, so it can't be used to enumerate the user base) and lists existing conversations
(`GET /api/chat/contacts`), ordered most-recently-active first. Selecting a search result or an existing
contact opens `/chat/{userId}` (`Chat.razor`). There is deliberately no separate "add contact" step: a
`Contact` row is only created (in both directions at once, via `SendMessageCommandHandler`) the moment
either side sends the first message between them - see `SendMessageCommand`.

Messages are genuinely end-to-end encrypted, not just transport-encrypted: encryption and decryption
happen entirely in the browser via the Web Crypto API (`wwwroot/js/e2eeChat.js`), and Orbit.Api only
ever stores and relays `ciphertextBase64`/`nonceBase64` (`ChatMessageEntity`) - it has no way to read a
message's content. Each browser generates a non-extractable ECDH (P-256) key pair on first use
(`ensureOwnPublicKey` in `e2eeChat.js`), persists the private key directly as a `CryptoKey` in
IndexedDB (it never leaves the browser, not even to be exported), and uploads only the public key
(`PUT /api/users/me/public-key`, stored on `User.PublicKeyBase64`) so other users can find it. Sending or
reading a message derives a shared AES-GCM key from the local private key and the other party's public
key (`deriveSharedKey`), and that key encrypts/decrypts the message text with a fresh random nonce per
message. `OwnEncryptionKeyProvider` (Blazor) makes sure this key pair exists and is published before
`Chat.razor` tries to send or receive anything.

Explicit scope limits for this first version, so they aren't mistaken for oversights: a single shared key
per user pair rather than Signal's rotating Double Ratchet, so there is **no per-message forward
secrecy** - compromising one derived key exposes the whole conversation with that person, not just one
message. There is also no separate identity-verification step (e.g. comparing key fingerprints out of
band), so the browser trusts whatever public key Orbit.Api currently reports for a user; a malicious or
compromised server could substitute a different key and intercept new messages (it still can't read
already-sent ciphertext without the right private key). Only 1:1 chats are supported, not groups.
Message delivery is polling-based (`Chat.razor` polls `GET /api/chat/messages/{otherUserId}?sinceUtc=`
every 3 seconds while a chat window is open), not push/real-time (no SignalR or WebSockets), and a
message sent to a user who has never opened `/chat` (and so has no `PublicKeyBase64` yet) can't be
encrypted - `Chat.razor` shows an explanatory message and disables sending in that case instead of
silently failing.

## Dashboard

`/` (`Dashboard.razor`) is the landing page after signing in, giving a single-page overview of
everything the signed-in user owns,
loading notes, task lists, and calendar events concurrently rather than one after another so the page's
load time is the slowest of the three calls rather than their sum. Each item type gets its own column,
but only if it actually has items in it - an empty column (e.g. no task lists yet) is left out entirely
rather than shown with a "brak..." placeholder, since the point of this page is a quick glance at what
exists, not a third copy of each list page's empty state. Clicking any item navigates straight to its
editor (`/notes/{id}`, `/tasks/{id}`, or `/calendar/{id}`), the same page `Notes`/`Tasks`/`Calendar`'s
own "Edytuj" button opens - the dashboard has no editing of its own.

## Push notifications

Orbit.Web can show real browser push notifications - delivered via a service worker, so they still
arrive while no Orbit.Web tab is open - for three activities: an approaching calendar event, a new chat
message, and a task item becoming overdue. Nothing is sent until the signed-in user explicitly turns
this on with the "Włącz powiadomienia push" button in the top bar (`MainLayout.razor`), which asks the
browser for notification permission and, once granted, registers a subscription with Orbit.Api.

**Client side.** `wwwroot/js/pushNotifications.js` wraps the browser's Notification and Push APIs;
`PushNotificationManager` (`Orbit.Web.Services`) drives it from C# and registers the resulting
subscription (its endpoint plus the P-256 `p256dh`/`auth` keys the Push API returns) with Orbit.Api via
`PushNotificationApiClient`. `wwwroot/service-worker.js` is the piece that actually receives a push
event and shows the notification (and reopens/focuses an Orbit.Web tab and navigates it to the relevant
page on click) - registered at the origin root (`/service-worker.js`) so its scope covers every route the
Blazor Router handles.

**Server side.** `PushSubscriptionEndpoints` (`/api/push/public-key`, `/api/push/subscriptions`) let the
client fetch the VAPID public key it needs to subscribe, and register/remove a subscription.
`PushNotificationDispatcher` (`Orbit.Core.Notifications`) is the single fan-out point every trigger below
goes through: given a user id and a notification payload, it sends to every subscription that user
currently has (more than one browser/device can each hold one), and prunes any subscription the push
service reports as permanently gone (HTTP 404/410) rather than retrying it forever.
`VapidPushNotificationSender` (`Orbit.Api.Notifications`) is the only piece that talks to a real push
service, via the [WebPush](https://github.com/web-push-libs/web-push-csharp) package, which implements
the RFC 8291 message encryption and RFC 8292 VAPID authentication a raw HTTP POST would otherwise have to
hand-roll - the same reasoning as MailKit for SMTP (see "Calendar event reminders" above). Like
`SmtpEmailSender`, it just logs a warning and skips sending when no VAPID key pair is configured, rather
than failing startup.

The three triggers:

- **Approaching events** ride along on the existing `CalendarEventReminderBackgroundService` poll (see
  "Calendar event reminders" above): whichever recipients of a due reminder have push enabled get a push
  notification alongside their email, reusing that same once-only claim.
- **New chat messages** are pushed from `SendMessageCommandHandler` itself, right after a message is
  stored - to the recipient, naming the sender's display name. The notification never includes the
  message itself: Orbit.Api only ever stores and relays ciphertext (see "Contacts and encrypted chat"
  above), so there is no plaintext to put in a push payload even if it wanted to.
- **Overdue tasks** get their own poller, `OverdueTaskNotificationBackgroundService`, mirroring
  `CalendarEventReminderBackgroundService`'s claim-before-send pattern (`TaskOverdueNotificationDeliveries`,
  unique-indexed on the task item) but with no look-back window: "overdue and not yet notified" is a
  durable state rather than a point in time that can be missed by a brief outage, so a task item stays
  eligible for exactly one notification for as long as it remains overdue and unnotified. A task item
  that links to another task list (see "Tasks" above) is excluded from this check, since its true
  completion depends on the list it links to, not its own stored (always-false) completion flag.

## Running locally

The simplest way to run the whole stack is Docker Compose, which builds the API and the web client
and wires them together:

```
cp .env.example .env   # then fill in JWT_SIGNING_KEY (required) and the SMTP_*/VAPID_* variables (optional)
docker compose up --build
```

Leaving the `SMTP_*`/`VAPID_*` variables blank is fine - see "Calendar event reminders" and "Push
notifications" above for what that means at runtime. This starts:

- the web client at `https://localhost:8443` (its one and only real entry point - see below);
  `http://localhost:8080` also answers, but only to redirect straight to `https://localhost:8443`
- the API at `http://localhost:8081` (`/health`, `/health/ready`, `/health/live`, `/api/auth/*`,
  `/api/notes`, `/api/tasks`, `/api/calendar-events`)
- the [Aspire dashboard](http://localhost:18888) for live logs and traces from the API

### Accessing Orbit.Web from another device on your network

The web client always calls the API under whatever origin you used to load the page - `orbit-web`'s own
nginx reverse-proxies `/api/*` to `orbit-api`, so the browser never has to know the API's separate host
or port and no CORS configuration is needed for this.

The chat needs a genuinely secure context (HTTPS) for the browser to expose the Web Crypto API its
end-to-end encryption depends on - a plain `http://<LAN-IP>:8080` origin doesn't qualify, so opening a
chat there would fail with a `crypto.subtle` error. To avoid that, `orbit-web` serves HTTPS on port 8443
as its one and only real entry point, and automatically redirects any plain-HTTP request on port 8080
straight to it - including `localhost`/`127.0.0.1`, even though those hosts would otherwise count as a
secure context on plain HTTP too. That redirect is deliberate, not just for `crypto.subtle`: the chat's
E2EE key pair is stored in the browser's IndexedDB, which is scoped per origin, so if the app were
reachable under both `http://localhost:8080` and `https://localhost:8443` interchangeably, opening it on
whichever port was on hand would silently mint a fresh key pair and permanently orphan every message
encrypted under the old one. Forcing everything through the single `:8443` origin avoids that trap:

1. Set `TLS_CERTIFICATE_HOSTNAME` in `.env` to this machine's LAN IP (e.g. `192.168.1.50`) -
   `orbit-web` generates a self-signed certificate covering that address on first startup (see
   `src/Clients/Orbit.Web/generate-certificate.sh`). Restart with `docker compose up -d --build` for a
   changed value to take effect on an already-created container.
2. From another device, open `http://<this-machine's-LAN-IP>:8080` (or `https://<...>:8443` directly) -
   either way you'll end up on `https://<this-machine's-LAN-IP>:8443`. The browser will warn that the
   certificate isn't trusted (it's self-signed, not issued by a real certificate authority) - accept the
   warning once per device to continue.

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
`dotnet user-secrets` first (see "Authentication" above); optionally configure SMTP and/or a VAPID key
pair the same way if you want to actually see reminder emails and push notifications locally - see
"Configuring SMTP for local development" and "Configuring push notifications for local development"
right below.

### Configuring SMTP for local development

`dotnet run`/VS Code's debugger set `ASPNETCORE_ENVIRONMENT=Development` (see the launch profiles in
`Properties/launchSettings.json`), so `appsettings.Development.json` is loaded on top of the tracked
`appsettings.json` and never committed (`*.Development.json` is in `.gitignore`) - this is where local,
per-developer SMTP settings belong, never in the tracked `appsettings.json`. `Smtp:Password` goes through
`dotnet user-secrets` instead, on top of both, so it never touches a file on disk that could be
accidentally committed or copied elsewhere:

```
cd src/Server/Orbit.Api
cp appsettings.Development.json.example appsettings.Development.json
# then edit appsettings.Development.json and fill in Smtp:Host/UserName/FromAddress/etc.
dotnet user-secrets set "Smtp:Password" "<your SMTP password>"
```

Leaving all of this unset is fine too - see "Calendar event reminders" above for what that means at
runtime.

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

Leaving this unset is fine too - see "Push notifications" above for what that means at runtime.

## Tests

```
dotnet test Orbit.sln
```

Also runs automatically in CI on every push and pull request to `main` (see
`.github/workflows/ci.yml`).

## License

MIT — see [LICENSE](LICENSE).
