# Functionality

This document describes what each implemented feature does and how it behaves, in detail. For where
each piece lives in the codebase, see [Architecture](architecture.md).

## Authentication

`POST /api/auth/register` (`email`, `userName`, `displayName`, `password`) and `POST /api/auth/login`
(`emailOrUserName`, `password`) both return `{ token, refreshToken, userId, email, displayName }` on
success. Login accepts either the account's email address or its username in the same field — both are
unique, so there's no ambiguity. Send the access token on every `/api/notes`-style request as
`Authorization: Bearer <token>`; without it, the API returns 401.

`token` is a short-lived JWT (15 minutes by default, `Jwt:ExpiryMinutes`). `refreshToken` is a
long-lived (30 days), single-use, opaque value: `POST /api/auth/refresh` (`refreshToken`) exchanges it
for a new `{ token, refreshToken, ... }` pair and revokes the one that was redeemed, so a leaked refresh
token that gets replayed after the legitimate client already used it is rejected. `POST /api/auth/logout`
(`refreshToken`) revokes it outright. Only the SHA-256 hash of a refresh token is ever stored — a
database leak alone can't be used to sign in as a user, the same way a leaked password hash can't be
used to log in directly.

The Blazor client handles all of this itself once signed in: `/login` and `/register` call the
endpoints above and store both returned tokens in `localStorage`; `AuthorizationMessageHandler` (a
`DelegatingHandler`) attaches the access token as a bearer token to every subsequent API call, and if a
call comes back 401 (the access token expired), transparently redeems the refresh token via
`TokenRefreshService` for a new pair and retries the call once before giving up. Logging out revokes the
refresh token on the API and clears both tokens locally. Any page that isn't explicitly public redirects
to `/login` when there's no valid access token.

**Noticing a session has ended.** Two independent mechanisms exist because neither alone covers every
case:
- `AuthorizationMessageHandler` calls `OrbitAuthenticationStateProvider.NotifyAuthenticationStateChanged()`
  whenever a request's access *and* refresh tokens both turn out to be dead — this is what makes
  `MainLayout`'s sidebar disappear and `[Authorize]`-gated routes redirect to `/login` in the same
  instant a page's own API call (e.g. `Dashboard.razor`'s 3-second poll) discovers the session is over,
  rather than only the page content changing while the sidebar keeps rendering as if still signed in.
- `MainLayout` also runs its own 60-second heartbeat (`CheckSessionHeartbeatAsync`) that re-checks the
  stored access token's own `exp` claim even when nothing is actively polling — this is what catches a
  tab merely left idle past `Jwt:ExpiryMinutes` (15 minutes today) with no API calls happening at all.
  On each tick, if the access token has locally expired, it first tries a silent refresh (the same
  recovery `AuthorizationMessageHandler` does on-demand) before treating it as a real sign-out, so an
  idle tab doesn't lose a session that's still well within the refresh token's much longer 30-day
  lifetime.

Both mechanisms depend on `TokenStore`, `TokenRefreshService`, and `OrbitAuthenticationStateProvider`
all being registered as **Singleton** in `Program.cs`, not Scoped: `AddHttpMessageHandler<T>()` builds
`AuthorizationMessageHandler` from `IHttpClientFactory`'s own internal, periodically-rotating DI scope,
not the app's one real scope, so a Scoped dependency would silently be a throwaway instance disconnected
from the one `MainLayout` is actually subscribed to — the notification would fire, but into the void.
`AuthorizationMessageHandler` itself stays Transient (the one exception), since
`IHttpClientFactory` mutates a handler's `InnerHandler` while assembling each client's pipeline and
rejects reusing one instance across more than one.

`/api/auth/register`, `/api/auth/login`, `/api/auth/refresh`, and `/api/auth/logout` are all rate
limited to 5 requests per minute per client IP address (no queueing — an excess request gets an
immediate 429), as brute-force protection for login attempts in particular.

The JWT signing key is a secret and is never checked into source control:

- **Docker Compose**: copy `.env.example` to `.env` and fill in a random value for `JWT_SIGNING_KEY`
  (e.g. `openssl rand -base64 48`). Compose loads `.env` automatically.
- **`dotnet run` outside Docker**: `dotnet user-secrets set "Jwt:SigningKey" "<a long random string>"`
  from `src/Server/Orbit.Api`. The API fails fast on startup with a clear error if the key is missing
  or too short.

`requests.http` at the repo root has ready-to-run register/login/notes requests (works with Visual
Studio's built-in HTTP file support or VS Code's "REST Client" extension).

## Notes

`POST /api/notes` and `PUT /api/notes/{id}` both take `{ title, content }`, where `content` is
free-form text. `GET /api/notes` and `GET /api/notes/{id}` return the same shape back, plus `id`,
`createdAtUtc`, and `updatedAtUtc`. `DELETE /api/notes/{id}` deletes a note, 404ing under the same
ownership rule as every other endpoint; the Blazor client's notes page asks for confirmation before
calling it.

### Sharing notes and task lists

Notes and task lists can be shared with another user, on the same offer/accept mechanism as calendar
events (see [Calendar](#calendar) below for the fuller explanation of that mechanism): `POST
/api/notes/{id}/shares` (or `/api/tasks/{id}/shares`) offers access to `recipientUserId` and returns a
share id; the owner's client notifies the recipient with an encrypted chat message carrying that id
(`NoteShareMessagePayload`/`TaskListShareMessagePayload`); `Chat.razor` renders an "Accept" action for
it, which calls `POST /api/notes/shares/{shareId}/accept` (or the task-list equivalent) to record the
grant as accepted.

**Sharing is live, not a copy.** There is exactly one row per note/task list/calendar event — accepting a
share does not create a second copy of it. `NoteShare`/`TaskListShare`/`CalendarEventShare` are
persistent access grants: a row that stays around for as long as access should, recording who owns the
item, who it was offered to, and at what level. Every read of a shared item (`GetNoteByIdQuery`,
`GetNotesQuery`, and the task-list/calendar-event equivalents) is resolved through a domain-specific
access resolver (`NoteAccessResolver`/`TaskListAccessResolver`/`CalendarEventAccessResolver`), which
loads the *one* underlying row — whether the caller owns it or only holds an accepted grant — and stamps
it with caller-relative context (`IsShared`, `SharedByUserName`, `AccessLevel`) via
`Note.SetAccessContext` before returning it. That context is never persisted; it's recomputed fresh for
whoever is asking, so the same note reads as "yours" for the owner and "shared by {owner}" for a
grantee, from the same row. Because everyone with access reads and writes the same row, an edit by one
party is immediately visible to every other party with access — there's no separate copy to fall out of
sync, which is also why an edit lock is needed (see below).

Every share carries an **access level**, chosen when the share is offered: `ReadOnly` (the default),
`Share`, or `CanEdit` (`Orbit.Core.Abstractions.ShareAccessLevel`, declared in that order since the
underlying int value doubles as a rank: `ReadOnly < Share < CanEdit`). Only `CanEdit` unlocks actually
editing the item — `UpdateNoteCommandHandler`/`UpdateTaskListCommandHandler`/`UpdateCalendarEventCommandHandler`
all return "not found" for an update attempt by a grantee whose access level is `ReadOnly` *or* `Share`,
and the Blazor editor pages disable their form (via a `<fieldset disabled>`) for the same grantees.

Every editor page shows a "shared by {name}" banner on any item the current user doesn't own, regardless
of access level — not just the restricted ones — with the wording adapting to what the current access
level actually allows ("read-only", "you can share it further, but not edit it", or "you can edit it").
This banner is the *only* indication a `CanEdit` grantee is looking at someone else's item at all, since
its form is otherwise fully editable and looks identical to something the user created themselves.

**Re-sharing.** `Share` sits strictly between the other two levels for one purpose: a grantee needs at
least `Share` to re-share the item with someone else at all (a `ReadOnly` grantee can't share it
further), and can never grant a level higher than their own — a `Share`-level grantee can only offer
`ReadOnly` or `Share` onward, never `CanEdit`, while a `CanEdit` grantee can offer any of the three, same
as the true owner. `ShareNoteCommandHandler`/`ShareTaskListCommandHandler`/`ShareCalendarEventCommandHandler`
enforce this identically, and additionally refuse to let *anyone* — owner included — share back to the
item's actual owner (`Note.UserId`/`TaskList.UserId`/`CalendarEvent.UserId`): since there's only ever one
underlying row now, its owner already has full access, so offering it back to them would be meaningless
at best and a way to bypass the level cap above at worst. All of these are "not found" responses rather
than a distinct "forbidden," so a caller can't tell "doesn't exist" apart from "exists but you can't
share it" by probing ids. The Blazor editor pages mirror this: the sharing section is hidden entirely for
a `ReadOnly` grantee, the access-level dropdown only offers levels the current user is allowed to grant,
and the contact picker excludes the owner.

**Duplicate offers.** Sharing something that was already offered to the same recipient — accepted or
still pending — doesn't create a second `NoteShare`/`TaskListShare`/`CalendarEventShare` row.
`INoteShareRepository.FindExistingAsync` (and its task-list/calendar-event equivalents) looks up an
existing offer for the same (item, recipient) pair first; if one exists, the handler returns it instead
of creating a new one (`ShareOutcome.AlreadyShared = true`). The client still sends a chat notice either
way, but reuses the *existing* share's id rather than minting a new one, so it lands as a reminder
pointing at the original offer instead of a confusing duplicate invite — the note/task-list editor pages
show "Already shared with that contact - sent a reminder" in that case instead of implying a fresh share
was created.

### Edit locking

Because a shared note, task list, or calendar event is a single live row rather than a per-user copy, two
people with `CanEdit` access could otherwise open the same item at the same time and silently overwrite
each other's changes. To prevent that, opening an editable item acquires a short-lived, per-item edit
lock (`Note.LockedByUserId`/`LockedByUserName`/`LockExpiresAtUtc`, mirrored on `TaskList` and
`CalendarEvent`): `NoteEditor.razor`/`TaskEditor.razor`/`CalendarEventEditor.razor` call `POST
/api/notes/{id}/lock` (or the task-list/calendar-event equivalent) in `OnInitializedAsync` whenever the
current user has `CanEdit` access, then re-send the same call every 20 seconds for as long as the editor
stays open (a `PeriodicTimer` heartbeat, the same pattern `Chat.razor` uses for polling). Each successful
acquire extends the lock 60 seconds into the future (`AcquireNoteLockCommandHandler.LockDuration`), so a
lock outlives any single heartbeat gap but expires on its own — no explicit release needed — if the
holder's browser closes, crashes, or goes to sleep before it can release.

Acquiring a lock already held by someone else, or saving into one, returns HTTP 409 with a
`LockConflictDto { lockedByUserName }` body (`Orbit.Core.Abstractions.EditOutcome`/`EditOutcomeKind`,
mapped onto HTTP responses by each domain's `ToApiResult` helper: `Success` → 204, `Locked` → 409,
anything else → 404). The editor pages surface this as a banner — "**{name}** is currently editing this
note - you can't edit it right now." — and disable the form's `<fieldset>` for as long as someone else
holds the lock, on top of (and independent from) the `ReadOnly`/`Share` access-level gating described
above. Re-acquiring a lock you already hold is not a conflict — it's how the heartbeat refreshes your own
TTL — and saving successfully releases your lock immediately afterward (`DELETE /api/notes/{id}/lock`)
rather than waiting for it to expire, so the next editor can pick it up right away. Navigating away or
closing the editor (`CancelAsync`, or `DisposeAsync` on any unmount) releases the lock the same way.

Locking only ever applies to `CanEdit` access — a `ReadOnly` or `Share` grantee's form is already
disabled by the access-level check above, so there's nothing for them to conflict over and no lock is
acquired on their behalf. On `CalendarEventEditor.razor` specifically, the lock gates only the event's
own content fieldset (`_canEditContent`), not the separately-gated Guests section (`_canShare`) — a
`Share`-tier recipient can still add guests while someone else holds the content lock, mirroring how
those two sections are already independent for access-level purposes.

## Tasks

`POST /api/tasks` and `PUT /api/tasks/{id}` both take `{ title, items }`, where each item is
`{ description, dueDateUtc, isCompleted, linkedTaskListId }` (`dueDateUtc` and `linkedTaskListId` are
both optional). `GET /api/tasks` and `GET /api/tasks/{id}` return the same shape back, plus
`isCompleted` on the task list itself — this is derived automatically (a list is complete only once
every item on it is checked off) and can't be set directly. Updating a task list always replaces its
whole checklist rather than patching individual items, since the client always sends the full current
list back.

The domain type behind this is named `TaskList`, not `Task`: `Orbit.Core.Tasks.Task` would collide with
`System.Threading.Tasks.Task`, which every async method in the codebase returns.

An item can instead reference another of the user's task lists via `linkedTaskListId`, rather than
being independently completable. A linked item's `isCompleted` is entirely derived — it follows the
referenced list's own completion (true only once every item on that list is checked off) and is
resolved live on every read (`LinkedTaskCompletionResolver`), the same "never trust the persisted
completion column, always recompute it" approach `TaskList.IsCompleted` already uses, extended
transitively across a chain of linked lists. Because of this, a linked item's completion **cannot be
set manually** — `isCompleted` in the request is ignored for a linked item and it is always stored as
not completed; the only way to complete it is to complete every item on the list it links to.

`linkedTaskListId` is validated on create and update (`TaskListLinkValidator`): it must reference a
task list that exists and is owned by the same user, an item can't link to the list it belongs to, and
a link can't close a cycle between task lists (directly, or transitively through a chain of other
links) — either of the last two would make completion resolution loop forever without this check. A
validation failure throws `ArgumentException`, which is not caught anywhere and surfaces as an
unhandled 500, matching how `CalendarEvent`'s start-before-end validation already behaves in this
codebase. The Blazor client's task editor only excludes linking a list to itself from its dropdown of
linkable lists; it does not check for longer cycles client-side, so building one still relies on the
API's validation and surfaces as a failed save rather than a client-side error message — a known rough
edge, not a silent gap (see [Future Plan](future-plan.md#known-scope-cuts-and-rough-edges)).

In the Blazor client, each item's due date and time are edited separately (`InputDate` plus a native
`<input type="time">`) and combined into one timestamp on save; a date picked without a time is stored
as midnight.

`DELETE /api/tasks/{id}` deletes a task list (and its items, via the `ON DELETE CASCADE` foreign key
from `TaskItemEntity` to its owning list — see `OrbitDbContext`); like the other endpoints, it 404s if
the id doesn't exist or isn't owned by the caller. Deleting a list that another list's item links to via
`linkedTaskListId` leaves that reference dangling rather than blocking the delete or cascading further —
`LinkedTaskCompletionResolver` already treats a link to a missing list as "not completed" instead of
failing, so this is safe, just something to be aware of if a list you expect to still be linkable is
gone. The Blazor client's task list page asks for confirmation before calling this endpoint.

## Inventory

`POST /api/inventory` and `PUT /api/inventory/{id}` both take `{ name, productType, category, quantity,
minimumQuantity, expiryDate, expiryNotificationChannel }` — `productType` and `category` are free text
(no fixed list), `quantity`/`minimumQuantity` are decimal (not integer) so fractional amounts like
"1.5 kg" are representable, and `minimumQuantity`/`expiryDate` are both optional: not every product
needs a restock threshold or an expiry date. `GET /api/inventory` and `GET /api/inventory/{id}` return
the same shape back plus `id`, `isBelowMinimum` and `hasPendingRestockTask` (both derived, computed
server-side so the client never reimplements the comparison), and `createdAtUtc`/`updatedAtUtc`. Each
item is owned by exactly one user — unlike Notes/Tasks/Calendar events, there is no sharing or editing
lock on inventory items, since neither was requested and both would be pure scope creep on top of an
already large feature.

**Low stock creates a real Task, not a separate notification.** Whenever a saved item's `quantity` is at
or below its `minimumQuantity`, `InventoryTaskListCoordinator` appends a `TaskItem` ("Uzupełnij:
{name}") to a single, system-managed `TaskList` titled "Uzupełnij zapasy" — the exact same `TaskList`/
`TaskItem` domain objects Tasks itself uses, so the item shows up on `/tasks` with the same
edit/complete/notification UI as anything the user created by hand. This check runs inline inside the
Create/Update handlers right after saving (`CreateInventoryItemCommandHandler`/
`UpdateInventoryItemCommandHandler`) rather than via a background poll — a stock level only ever changes
because the user just edited it, so there's no "time passing" trigger to poll for the way overdue tasks
or calendar reminders have.

The managed task list is created lazily, once per user, the first time they add *any* inventory item —
independent of whether that first item happens to be low — and comes pre-seeded with one standing item,
"Zaktualizuj stan magazynu", with `RemindDaily` turned on. This is the "recurring reminder to keep stock
updated" the feature calls for: Tasks has no engine for a task that recreates itself after being
completed, but `RemindDaily` already nags daily until checked off, and unchecking it re-arms the daily
nag — treated here as close enough to "recurring" without building a second recurrence engine on top of
Tasks' existing one (Calendar's). Since `TaskList` has no field to mark itself "system-managed", a
separate one-row-per-user table (`InventoryManagedTaskListEntity`) tracks which `TaskListId` Inventory
created, entirely outside the Tasks schema.

**Not re-triggering while a restock task is still open.** Each `InventoryItem` remembers the
`TaskListId`/`TaskItemId` of its own open restock task, if any (`PendingRestockTaskListId`/
`PendingRestockTaskItemId`). Before creating a new one, `PendingRestockTaskResolver` checks whether the
tracked task is still genuinely open: if the user completed it, or deleted the list or item out from
under this tracking, that's treated as "nothing pending" (the fields are cleared, and a low item is free
to get a fresh task next time it's saved) rather than an error — the same philosophy
`LinkedTaskCompletionResolver` already applies to a dangling task-list link elsewhere in Tasks. This
resolution happens lazily on every read and write that touches the item (never a background poll), and
any correction it makes is persisted immediately so it doesn't need re-resolving on the next read. If
quantity rises back above minimum, the pending reference is cleared but the already-created `TaskItem`
itself is left alone — the user checks it off manually — rather than Inventory reaching back into Tasks
to delete something it doesn't own the lifecycle of.

**Expiry warnings** are the one part of this feature that genuinely needs a background poll, since a
date becoming "approaching" is a function of time passing, not a user action.
`InventoryExpiryReminderBackgroundService` mirrors `CalendarEventReminderBackgroundService`/
`OverdueTaskNotificationBackgroundService` exactly: a 1-minute `PeriodicTimer`, a fresh DI scope per
tick, a 100-item cap per poll, and a heartbeat reported to `HostedServiceHealthTracker` (so it shows up
in `/health/ready` as `InventoryExpiryReminders`). A warning goes out on the item's own
`expiryNotificationChannel` a fixed 3 days before `expiryDate` (not configurable per item in this first
version). The delivery-tracking table (`InventoryExpiryNotificationDeliveryEntity`) is unique-indexed on
**`(InventoryItemId, ExpiryDate)`** rather than the item id alone — mirroring `TaskDailyReminderDeliveryEntity`'s
keyed-by-value shape instead of `TaskOverdueNotificationDeliveryEntity`'s fire-once shape — so restocking
an item with a new expiry date is automatically eligible for a fresh warning with no explicit reset
logic anywhere. An item already past its expiry date does not get a second, more urgent background
notification; instead the Blazor inventory list page (`Inventory.razor`) sorts expiring/expired items to
the top and shows a passive "Expires soon"/"Expired" badge, computed client-side from `expiryDate` vs.
today — keeping that half of the feature entirely client-side rather than adding another notification
path.

## Calendar

`POST /api/calendar-events` and `PUT /api/calendar-events/{id}` both take `{ details }`, where `details`
is `{ title, description, location, color, startUtc, endUtc, isAllDay, recurrence, guests,
reminderMinutesBeforeStart }` (`description`, `location`, `color`, and `recurrence` are all optional).
`GET /api/calendar-events` and `GET /api/calendar-events/{id}` return the same shape back, wrapped with
`id`, `createdAtUtc`, and `updatedAtUtc`. The fields are grouped under `details` on the wire, not spread
across the request body, because there are enough of them that flattening them out would be harder to
read — see `CalendarEventDetails` in `Orbit.Core.Calendar` for the same grouping on the domain side.

`endUtc` can't be before `startUtc` (`CalendarEvent.ValidateTimeRange`) — checked both server-side, where
a violation throws `ArgumentException` and surfaces as an unhandled 500, and client-side in
`CalendarEventEditor.razor`, which shows an inline error instead of submitting a request that's bound to
fail.

`location`, when present, is `{ address, latitude, longitude }` — `address` is optional (reverse
geocoding can fail to resolve one), `latitude` and `longitude` are always required and validated to be
within their valid ranges (±90/±180 degrees). Unlike the rest of the form, this isn't free text: the
Blazor client's event editor has a "Pick on map" button that opens an embedded
[Leaflet](https://leafletjs.com) map (OpenStreetMap tiles, loaded from a CDN — no API key needed, see
`wwwroot/index.html` and `wwwroot/js/mapPicker.js`). Clicking a point on the map stores its coordinates
and resolves an address for them via OpenStreetMap's free Nominatim reverse-geocoding endpoint
(`GeocodingApiClient`); typing directly into the address field only relabels an already-picked point; it
doesn't set a location on its own; and the Nominatim call intentionally does not go through
`AuthorizationMessageHandler`, so Orbit's own bearer token is never sent to that third-party host.
Nominatim's usage policy caps this to light, non-commercial traffic — a deployment with real volume
should self-host it instead (see https://operations.osmfoundation.org/policies/nominatim/).

`recurrence`, when present, is `{ frequency, intervalCount, untilUtc }` (`frequency` is `"Daily"`,
`"Weekly"`, or `"Monthly"`; `untilUtc` is optional). A recurring event is stored as a single event
carrying this rule — the API itself never expands it into individual occurrences. The calendar page's
day/month/year grid views do that expansion themselves, client-side, via
`CalendarEventOccurrenceExpander` (`Orbit.Web.Services`): each occurrence that falls inside the grid's
visible date range is generated on demand and placed on its own day/time slot, so a recurring event shows
up on every matching date instead of only on its original `startUtc`. The server-side reminder pipeline
does its own, independent occurrence expansion for a narrow due-or-soon window — see
[Calendar event reminders](#calendar-event-reminders) below.

Guests and reminders (`reminderMinutesBeforeStart`, minutes before the event starts) are edited in the
Blazor client as comma-separated text rather than as an add/remove list like task items are, since
neither needed per-item editing for a first pass.

### Sharing

Adding a contact as a guest in the event editor (see the picker under "Add a guest from contacts") does
two things once the event is saved: it adds them to `guests`, and it offers them a share of the event at
the access level chosen alongside them in the picker (`ReadOnly` by default, `Share`, or `CanEdit`) —
`ShareCalendarEventCommand` creates the offer, and `CalendarEventEditor.razor` notifies the recipient
with an encrypted chat message carrying the share id (`EventShareMessagePayload`). The recipient sees an
"Accept" action on that message in `Chat.razor`; accepting (`AcceptCalendarEventShareCommand`) records
the grant as accepted rather than creating a copy — the recipient reads and, with `CanEdit`, writes the
very same event row as the owner (see
[Notes — Sharing notes and task lists](#sharing-notes-and-task-lists) above for why sharing works this
way). The event editor's content fields and its Guests section are gated independently
(`_canEditContent` vs. `_canShare`): a `Share`-tier recipient can't touch the event's own details but can
still add and invite guests, so the "Save" button only sends a `PUT` for the event's content when the
current user actually has `CanEdit`. See
[Notes — Sharing notes and task lists](#sharing-notes-and-task-lists) above for the full `ShareAccessLevel`
model (the `Share`-tier re-sharing rules, the owner exclusion, and duplicate-offer handling all apply to
calendar events exactly as described there) and [Edit locking](#edit-locking) for how simultaneous
`CanEdit` access to the same event is arbitrated.

`DELETE /api/calendar-events/{id}` deletes an event, 404ing under the same ownership rule as the other
endpoints. Any reminder claims already recorded for it in `EventReminderDeliveries` (see
[Calendar event reminders](#calendar-event-reminders) below) are left in place rather than cleaned up,
since they're not a foreign key relationship and a deleted event simply stops producing new reminders.
The Blazor client's calendar page asks for confirmation before calling this endpoint.

### Calendar event reminders

Two independent notification emails can go to an event's owner (the account that created it — not the
`guests` list, which isn't wired to notifications yet; see
[Future Plan](future-plan.md#known-scope-cuts-and-rough-edges)), each gated by its own checkbox in the
event editor:

- **`notifyOnCreation`**: sent once, immediately, the first time the event is saved. Handled directly in
  `CreateCalendarEventCommandHandler` — not by the polling service below — since it's a one-off reaction
  to a single request rather than something that needs to be discovered later. A failure to send it (e.g.
  SMTP briefly unreachable) is logged but never fails the request: the event is already persisted by that
  point.
- **`notifyBeforeStart`**: sent per `reminderMinutesBeforeStart` entry, once its lead time before the
  event's start is reached. All-day events are anchored to local midnight (see `CalendarEventEditor.razor`),
  so a `0`-minutes entry on one of those fires "at 00:00" — except when the event is all-day *and* was
  created on the same calendar day it starts: that specific reminder is suppressed, since the creation
  email above already told the owner about an event starting the same day.

The "approaching event" side runs entirely inside Orbit.Api as `CalendarEventReminderBackgroundService`, a
`BackgroundService` that polls once a minute: sending real email needs SMTP credentials, and those must
never reach the Blazor WebAssembly client, so this can't live in Orbit.Web despite reminders being a
calendar-page feature. `EventReminderScheduler` (`Orbit.Core.Calendar.Reminders`) holds the actual
"what's due right now" logic (including the all-day/same-day suppression rule above), kept independent of
ASP.NET Core hosting so it's unit-testable on its own.

A reminder is due once `startUtc` minus its lead time has passed, and stays eligible for 5 minutes after
that (`LookBackWindow`) so a reminder isn't lost if a poll is briefly delayed — after that window it's
treated as missed rather than emailed late. A single poll sends at most 100 reminders
(`MaxRemindersPerPoll`), protecting against a burst of simultaneously due reminders overwhelming the SMTP
server; anything past that cap is simply picked up on the next minute's poll.

Each reminder is reserved before it's sent, not just recorded after: `EventReminderRepository.TryClaimAsync`
inserts its row into a dedicated `EventReminderDeliveries` table (unique-indexed on the event/lead-time/
occurrence triple) *before* the email goes out, and only sends if that insert wins the race. A failed
insert means another worker already claimed the same reminder, so this one backs off instead of sending a
duplicate — the unique index is the actual concurrency guard, not the earlier existence check
(`HasBeenSentAsync`), which stays as a cheap pre-filter only. This is what makes it safe to eventually run
more than one instance of this background service at once, without needing a distributed lock or message
queue: whichever instance's insert lands first wins, everyone else backs off (see
[Future Plan](future-plan.md#planned-features) for the multi-instance scaling angle). If sending fails
after the claim succeeds (e.g. a transient SMTP error), `ReleaseClaimAsync` removes the reservation so the
reminder is retried on a later poll instead of being silently lost. A recurring event gets a reminder for
every occurrence, not just its original `startUtc`: `EventReminderScheduler` generates the occurrences
that could plausibly be due right now with `CalendarEventOccurrenceGenerator` (`Orbit.Core.Calendar`) — a
narrow window bounded by the look-back window and the event's reminder lead times, not the whole
recurrence — and tracks each occurrence's claim separately via `OccurrenceStartUtc`, so one occurrence
being sent never suppresses another's reminder.

Email is sent via [MailKit](https://github.com/jstedfast/MailKit) (`SmtpEmailSender`), configured
through the `Smtp` section (`Smtp:Host`, `Smtp:Port`, `Smtp:UserName`, `Smtp:FromAddress`,
`Smtp:FromDisplayName`, `Smtp:UseStartTls`) plus `Smtp:Password` from an environment variable or
user-secrets — never from a committed appsettings file (see
[Testing and Running Locally](testing-and-running-locally.md#configuring-smtp-for-local-development)
for exactly where). Unlike the JWT signing key, SMTP isn't required to start the API: `SmtpEmailSender`
just logs a warning and skips sending when `Smtp:Host`/`Smtp:FromAddress` are unset, so a fresh local
checkout still runs without anyone having set up email delivery. The background service reports a
heartbeat to the existing `HostedServiceHealthTracker` on every poll (success or failure), so a crashed
or stuck reminder loop shows up in the `hosted-services` health check the same way any other background
service would.

## Contacts and encrypted chat

`/contacts` (`Contacts.razor`) searches for another user by their **exact** email address or username
(`GET /api/users/search?query=`, tried as an email first and then as a username — no partial/fuzzy
matching, so it can't be used to enumerate the user base) and lists existing conversations
(`GET /api/chat/contacts`), ordered most-recently-active first. Selecting a search result or an existing
contact opens `/chat/{userId}` (`Chat.razor`). There is deliberately no separate "add contact" step: a
`Contact` row is only created (in both directions at once, via `SendMessageCommandHandler`) the moment
either side sends the first message between them — see `SendMessageCommand`. A network failure while
loading the contact list (e.g. `HttpRequestException` with no status code — a DNS lookup, TLS handshake,
or dropped connection failing before any response comes back) shows an inline error with a "Retry"
button instead of crashing the whole page; only an expired session (a 401 after the automatic
refresh-and-retry also failed) redirects to `/login`.

Messages are genuinely end-to-end encrypted, not just transport-encrypted: encryption and decryption
happen entirely in the browser via the Web Crypto API (`wwwroot/js/e2eeChat.js`), and Orbit.Api only
ever stores and relays `ciphertextBase64`/`nonceBase64` (`ChatMessageEntity`) — it has no way to read a
message's content. Each browser generates a non-extractable ECDH (P-256) key pair on first use
(`ensureOwnPublicKey` in `e2eeChat.js`), persists the private key directly as a `CryptoKey` in
IndexedDB (it never leaves the browser, not even to be exported), and uploads only the public key
(`PUT /api/users/me/public-key`, stored on `User.PublicKeyBase64`) so other users can find it. Sending or
reading a message derives a shared AES-GCM key from the local private key and the other party's public
key (`deriveSharedKey`), and that key encrypts/decrypts the message text with a fresh random nonce per
message. `OwnEncryptionKeyProvider` (Blazor) makes sure this key pair exists and is published before
`Chat.razor` tries to send or receive anything.

### Message forwarding

Any message in a conversation can be forwarded into a different conversation via the "..." menu next to
it in `Chat.razor` (alongside "Edit" for the sender's own messages). Forwarding one of the current user's
own messages just sends its text as an ordinary new message in the target conversation — indistinguishable
from typing the same text again, so there's nothing extra to encode. Forwarding someone *else's* message
wraps the text in `ForwardedMessagePayload` (`OriginalAuthorUserId`, `OriginalAuthorDisplayName`,
`Content`) before encrypting and sending it, the same "structured payload riding as ordinary encrypted
plaintext" trick the three share-notice payloads use — the server only ever sees ciphertext, so it never
needs to know a message is a forward at all. The recipient's `Chat.razor` decrypts it, recognizes the
payload's `Type`, and renders a "Forwarded from {original author}" label above the message content
instead of attributing it to whoever actually forwarded it. Forwarding an already-forwarded message
preserves the *original* author through any number of hops, not the most recent forwarder — `Chat.razor`
tracks each decrypted message's original author locally and carries it forward the same way a note,
task list, or calendar event's re-share chain always resolves back to its one true owner rather than
whoever most recently re-shared it (see
[Notes — Sharing notes and task lists](#sharing-notes-and-task-lists) above). The "Forward to…" picker
excludes the conversation the message is already in, since forwarding a message back into the same chat
it came from is meaningless.

Explicit scope limits for this first version, so they aren't mistaken for oversights (see
[Future Plan](future-plan.md#known-scope-cuts-and-rough-edges) for the fuller list): a single shared key
per user pair rather than Signal's rotating Double Ratchet, so there is **no per-message forward
secrecy** — compromising one derived key exposes the whole conversation with that person, not just one
message. There is also no separate identity-verification step (e.g. comparing key fingerprints out of
band), so the browser trusts whatever public key Orbit.Api currently reports for a user; a malicious or
compromised server could substitute a different key and intercept new messages (it still can't read
already-sent ciphertext without the right private key). Only 1:1 chats are supported, not groups.
Message delivery is polling-based (`Chat.razor` polls `GET /api/chat/messages/{otherUserId}?sinceUtc=`
every 3 seconds while a chat window is open), not push/real-time (no SignalR or WebSockets), and a
message sent to a user who has never opened `/chat` (and so has no `PublicKeyBase64` yet) can't be
encrypted — `Chat.razor` shows an explanatory message and disables sending in that case instead of
silently failing.

### Responsive layout

The conversation list next to the message thread (`.chat-list`) defaults to its collapsed, avatar-only
width (`_isContactListCollapsed = true`) rather than showing full names, so the message thread gets the
extra width by default on a typical visit; the toggle button at the top of the list (the chevron in
`.chat-list-header`) still expands it back to full names on demand, same as before. Below 680px this
list stops being an inline column at all and becomes an off-canvas drawer instead (opened via the
hamburger button in the thread header, closed by tapping the backdrop or picking a contact) — the
drawer always shows full names regardless of the collapsed state, since an icon-only slide-out drawer
would defeat the point of it.

The left navigation sidebar (`MainLayout.razor`) auto-collapses to its icon rail — the same visual state
`ToggleSidebarCollapsed` toggles manually by clicking the logo — once the window narrows past 1024px,
without needing a click: a pure CSS media query (`@media (max-width: 1024px) and (min-width: 681px)`)
applies the icon-rail rules directly, independent of the manual toggle's own state. Below 681px it
instead switches to the fully different mobile layout described above (a horizontal icon bar across the
top, sidebar labels and the nav divider/Options row hidden) rather than staying a narrow vertical rail —
680px is also the calendar's page (`app.css`) and chat's own drawer breakpoint, kept consistent across
the app rather than each surface picking its own.

Collapsing the sidebar hides `.user-meta` on the avatar trigger itself (name/initials button in the
rail), but the popup menu opened by clicking that avatar (`.avatar-dropdown`) always shows the full
name regardless — it's an overlay meant to show full detail, not part of the collapsed rail, even
though it's nested inside the same collapsed `.sidebar` in the DOM. The CSS rule that hides
`.user-meta` when collapsed is scoped to `.avatar-trigger .user-meta` specifically (not a bare
`.user-meta` selector) so it doesn't also catch the dropdown's own copy of that markup.

## Dashboard

`/` (`Dashboard.razor`) is the landing page after signing in, giving a single-page overview of
everything the signed-in user owns, loading notes, task lists, and calendar events concurrently rather
than one after another so the page's load time is the slowest of the three calls rather than their sum.
Each item type gets its own column, but only if it actually has items in it — an empty column (e.g. no
task lists yet) is left out entirely rather than shown with a "nothing here yet" placeholder, since the
point of this page is a quick glance at what exists, not a third copy of each list page's empty state.
Clicking any item navigates straight to its editor (`/notes/{id}`, `/tasks/{id}`, or `/calendar/{id}`),
the same page `Notes`/`Tasks`/`Calendar`'s own "Edit" button opens — the dashboard has no editing of its own.

## Push notifications

Orbit.Web can show real browser push notifications — delivered via a service worker, so they still
arrive while no Orbit.Web tab is open — for three activities: an approaching calendar event, a new chat
message, and a task item becoming overdue. Nothing is sent until the signed-in user explicitly turns
this on with the "Enable push notifications" button in the top bar (`MainLayout.razor`), which asks the
browser for notification permission and, once granted, registers a subscription with Orbit.Api.

**Client side.** `wwwroot/js/pushNotifications.js` wraps the browser's Notification and Push APIs;
`PushNotificationManager` (`Orbit.Web.Services`) drives it from C# and registers the resulting
subscription (its endpoint plus the P-256 `p256dh`/`auth` keys the Push API returns) with Orbit.Api via
`PushNotificationApiClient`. `wwwroot/service-worker.js` is the piece that actually receives a push
event and shows the notification (and reopens/focuses an Orbit.Web tab and navigates it to the relevant
page on click) — registered at the origin root (`/service-worker.js`) so its scope covers every route the
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
hand-roll — the same reasoning as MailKit for SMTP (see
[Calendar event reminders](#calendar-event-reminders) above). Like `SmtpEmailSender`, it just logs a
warning and skips sending when no VAPID key pair is configured, rather than failing startup.

The three triggers:

- **Approaching events** ride along on the existing `CalendarEventReminderBackgroundService` poll (see
  [Calendar event reminders](#calendar-event-reminders) above): whichever recipients of a due reminder
  have push enabled get a push notification alongside their email, reusing that same once-only claim.
- **New chat messages** are pushed from `SendMessageCommandHandler` itself, right after a message is
  stored — to the recipient, naming the sender's display name. The notification never includes the
  message itself: Orbit.Api only ever stores and relays ciphertext (see
  [Contacts and encrypted chat](#contacts-and-encrypted-chat) above), so there is no plaintext to put in
  a push payload even if it wanted to.
- **Overdue tasks** get their own poller, `OverdueTaskNotificationBackgroundService`, mirroring
  `CalendarEventReminderBackgroundService`'s claim-before-send pattern (`TaskOverdueNotificationDeliveries`,
  unique-indexed on the task item) but with no look-back window: "overdue and not yet notified" is a
  durable state rather than a point in time that can be missed by a brief outage, so a task item stays
  eligible for exactly one notification for as long as it remains overdue and unnotified. A task item
  that links to another task list (see [Tasks](#tasks) above) is excluded from this check, since its true
  completion depends on the list it links to, not its own stored (always-false) completion flag.
