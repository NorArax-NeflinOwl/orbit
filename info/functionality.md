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

**A locally-expired token never means "signed out" on its own.** `OrbitAuthenticationStateProvider.GetAuthenticationStateAsync()` —
the single method every one of the above paths, `AuthorizeRouteView`'s route gate, and `MainLayout`'s own
initial "am I signed in" read all ultimately call — tries a silent refresh itself before falling back to
an anonymous state, whenever the locally-stored access token is missing or its `exp` claim has already
passed. This is what stops a page load that happens to land exactly when the access token has lapsed
(a cold boot, reopening a backgrounded PWA tab, or a full browser reload) from forcing a real sign-out
while the refresh token could still have kept the session alive — and since it's the same method the
cascading authentication state is built from, the sidebar and route gating never end up disagreeing with
each other the way they would if only some callers knew to retry. `TokenRefreshService.TryRefreshAsync`
returns quickly without any network call when there's no refresh token stored, so calling it
unconditionally here costs nothing on a page that was never signed in at all (e.g. `/login` itself).
Any page that needs the signed-in user's id on its own initial load (rather than through an API call that
already goes through `AuthorizationMessageHandler`) calls `OrbitAuthenticationStateProvider.TryGetCurrentUserIdAsync()`
instead of reading the `sub` claim directly, which also calls `NotifyAuthenticationStateChanged()` before
returning so the sidebar reacts immediately rather than waiting for the next heartbeat tick.

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

`POST /api/notes` and `PUT /api/notes/{id}` both take `{ title, content }`, where `content` is an
ordered list of lines, each `{ text, isChecklistItem, isChecked }` — a note is plain text and checklist
items in one body, not two separate features. A checklist item's checked state is a real field rather
than `"[ ]"`/`"[x]"` text every client would have to parse back out, and it is persisted as JSON (see
`NoteEntity.ContentJson`). `GET /api/notes` and `GET /api/notes/{id}` return the same shape back, plus
`id`, `createdAtUtc`, and `updatedAtUtc`. `DELETE /api/notes/{id}` deletes a note, 404ing under the same
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

## Group chats

A chat with more than one other person, under the same end-to-end encryption one-to-one chats already
have. There is no group key: the sender's browser encrypts the same text **once per other member**,
under the pairwise key it already shares with each of them, and posts the copies together
(`POST /api/chat/groups/{id}/messages`). Each copy is an ordinary `ChatMessage` row tagged with the
group and with a `GroupMessageId` the copies share.

That choice buys a lot and costs two things worth knowing:

- Nothing new to distribute or rotate when membership changes, and the server still can't read
  anything — it never had a key and doesn't get one now.
- **A new member can't read anything sent before they joined**, since no copy was ever encrypted for
  them. The group view says so rather than showing empty space.
- A message costs N rows instead of one.

The server checks the fan-out rather than trusting it: exactly one copy per current member, no more and
no fewer. A missing copy would silently cut someone out of a conversation they are in; an extra one
would deliver into a group its recipient has no part in. Reading a group returns only the copies the
caller can actually decrypt — the ones addressed to them plus the ones they sent.

### Roles

Two roles, `Member` and `Admin` — every capability this feature needs falls on one side of the same
line, and a permission matrix nobody varies is machinery to keep correct for nothing. All of it lives on
`ChatGroup`, which takes the id of whoever is asking and refuses if they aren't entitled:

| | Member | Admin |
|---|---|---|
| Read and post | yes | yes |
| Delete own messages | yes | yes |
| Delete anyone's messages | no | yes |
| Add and remove members | no | yes |
| Promote and demote | no | yes |
| Rename the group | no | yes |

The creator is the first admin. **The last admin can't be removed or demoted** — that would leave a
group nobody can manage and no way to fix it from inside; an admin can step down once someone else can
take over. Adding someone requires an existing one-to-one chat with them, so a group can't be used to
reach a stranger who never agreed to hear from you; re-adding an existing member skips that check, being
a no-op.

Deleting a message removes it **for everyone**, not just for the person asking: there is one row per
recipient, and removing only your own copy would leave the message standing for everybody else. The same
endpoint covers one-to-one messages, where only the sender may delete — being sent something doesn't
give you the right to erase it from the sender's own history.
## Private notes and task lists

A note or task list can be marked **private**, which means exactly one thing: only its creator can ever
read it, and Orbit's servers can't. Ticking the box in the editor makes the browser seal the title and
the content before saving, so what the server stores is `IsPrivate` plus a base64 ciphertext and nonce
(`EncryptedPayload`) — the readable columns go **empty**, not merely unread. `Note.Update` and
`TaskList.Update` enforce that pairing rather than trusting callers: claiming privacy without sealed
content is refused, and turning privacy off drops the ciphertext.

The key is the one chat already uses, agreed with the owner's own public key on both sides of an ECDH
exchange (`e2eeChat.js`'s `encryptForSelf`). That means no second key to generate, back up, or restore:
a browser that can read your chat can read your private items, one that can't asks for your password
the same way, and **a password reset replaces the key pair, so private content is lost with the chat
history** — the editor says so next to the checkbox.

Sealing and opening happen in `NotesApiClient`/`TasksApiClient`, not in the pages, so the overview, the
dashboard, the checklist view and the calendar all receive a readable DTO without knowing any of this
happened. Content that can no longer be opened renders with an "Unreadable — encrypted with an older
key" title rather than throwing, so one lost item doesn't take a whole list down.

What private costs:

- **It can't be shared.** `ShareNoteCommandHandler`/`ShareTaskListCommandHandler` refuse it, and an
  existing share stops resolving the moment the item becomes private — the grant row is left in place
  and simply no longer grants, so turning privacy back off restores it.
- **A private task list gets no reminders.** Overdue and daily reminders are scheduled server-side from
  due dates the server can no longer read.
- **Items can't be moved into or out of one** (`MoveTaskItemCommandHandler` refuses): a private list
  keeps no readable items, so the move would take the item off the source and then drop it.
- **Completion is recomputed in the browser.** The server derives `IsCompleted` from items it can't see,
  so what it sends for a private list means nothing; `TasksApiClient` works it out after opening.

### Private warehouses

A warehouse can be marked private too, on the same key and the same rules — with one difference worth
stating plainly. A note's lines and a task list's items live inside their parent row, so sealing the
parent seals them. **A warehouse's items are rows of their own**, so making one private *deletes* those
rows: the sealed payload carries the name and every item, and `UpdateWarehouseCommandHandler` removes
whatever item rows were there before. "The server can't read this warehouse" is therefore literally
true — there is nothing left for it to read.

That is also why a private warehouse **raises no restock tasks and sends no expiry reminders**: both are
worked out from item rows that no longer exist. `IsBelowMinimum` is recomputed in the browser after
opening the payload, the same way a private task list's completion is.


## The map, and the location behind it

A user can record **one** location for themselves — coordinates, the address reverse geocoding resolved
for them if it managed to, and when it was taken (`UserLocation`). The Map page (`/map`) shows it on a
Leaflet map, the same library and tile source the calendar's location picker already uses.

Recording is always something the user does on purpose: pressing the button asks the browser for a
position, the browser asks the user's permission, and nothing is read until both happen. Refusing is an
ordinary answer — it comes back as a sentence on the page, not an error. Nothing in Orbit reads a
position on its own, and there is no background tracking.

**One point, no history.** Recording again replaces what was there; "Forget it" removes it and leaves
nothing behind. `PUT /api/users/me/location` and `DELETE /api/users/me/location` are the only ways to
write one, and both act on the caller's own account — there is no endpoint for writing anyone else's,
and none for reading one either. A location leaves the API only through the caller's own `GET
/api/users/me`, which is to say: **a location is currently visible to nobody but the person who
recorded it.**

Coordinates are validated the same way a calendar event's are (±90 / ±180), and a point off the globe is
refused with a message rather than stored — see [Refusing a request](#refusing-a-request). The address is
best-effort: a point Nominatim has nothing for is still worth keeping.

The map waits for its container to have a height before Leaflet measures it. Blazor adds the element in
the same render pass that draws into it, so measuring immediately measures a box the browser hasn't laid
out yet — which leaves the tiles covering one corner and the marker outside them. Correcting afterwards
doesn't work: `invalidateSize` fixes the size Leaflet believes in, but a `setView` back to the same
centre and zoom is a no-op, so the tile grid keeps its stale origin.

### Sharing a position with a contact

A position can be shared with one contact at a time, sealed for **them specifically** under the pairwise
key the two already use for chat (`SharedLocationSender`). Orbit relays a point it cannot read, exactly
as it relays a message; the recipient's browser opens it with the same key.

Two shapes, one row:

- **Send once** — a fixed point. It stays until the sharer ends it.
- **Keep sharing** — the same row, marked live and refreshed **every minute while the Map page is open**.
  The refresh is tied to the page on purpose: sharing a position is something someone is doing
  deliberately, and a timer that outlived the page would keep broadcasting after they had moved on.

**There is exactly one row per (sharer, recipient) pair, overwritten in place** — enforced by a unique
index as well as by the handler, so a refresh racing itself can't leave two points behind. That is the
whole of "no history": an hour of live sharing leaves one row, not sixty that together say where someone
has been. A client is free to keep its own local history; nothing server-side does.

Ending a share **deletes the row** (`DELETE /api/users/me/location/shares/{recipientUserId}`, or without
the id to end all of them at once), so stopping means the position is gone rather than stale. Stopping
something never started is not an error — the end state asked for is already true.

Sharing requires an existing one-to-one chat with the recipient, the same rule adding someone to a group
follows: a position is not something to be able to push at a stranger who never agreed to hear from you.

The Map page shows the viewer's own position and everyone sharing with them on **one** map, framed to fit
them all.

## Refusing a request

Anything that refuses what a caller asked for — domain validation (an event ending before it starts, a
task list link that would close a cycle) or an endpoint reading a field by name (`accessLevel`, any
`*NotificationChannel`, `frequency`) — throws `InvalidRequestException` (`Orbit.Core.Abstractions`).
`InvalidRequestExceptionHandler`, registered once in `Program.cs`, turns every one of them into
**`400` with `{ "message": "..." }`** and logs it at information level, since a refused request is the
API working rather than failing.

It is deliberately its own type rather than plain `ArgumentException`: the framework raises that one too
when Orbit's own code passes something impossible, and those are faults that have to keep surfacing as
500s instead of being reported to the caller as their mistake. Nothing else is mapped, so an unexpected
exception still produces the empty 500 it always did.

Enum-valued fields go through `RequestEnum.Parse`, which refuses a missing, misspelled, or
numeric-but-undeclared value with a message naming the field and the values it accepts — `Enum.Parse`
on its own turned a missing field into a null-argument fault and an unknown one into a message written
for a programmer.

## Tasks

`POST /api/tasks` and `PUT /api/tasks/{id}` both take `{ title, items, isGroup }`, where each item is
`{ description, dueDateUtc, isCompleted, linkedTaskListId }` (`dueDateUtc` and `linkedTaskListId` are
both optional, and `isGroup` defaults to false — see [Group lists](#group-lists)). `GET /api/tasks` and
`GET /api/tasks/{id}` return the same shape back, plus `isCompleted` on the task list itself — this is derived automatically (a list is complete only once
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
validation failure throws `InvalidRequestException` and comes back as a **400 carrying the reason** —
see [Refusing a request](#refusing-a-request). The Blazor client's task editor only excludes linking a list to itself from its dropdown of
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

### Two editing levels

A task list can be opened at either of two depths, both reachable from the task list page:

- **Deep** (`/tasks/{id}`, `TaskEditor.razor`) — the full editor: title, grouping, every item's text,
  due date, link, notification settings, adding and removing items. This is the level that takes the
  edit lock described under [Edit locking](#edit-locking).
- **Shallow** (`/tasks/{id}/checklist`, `TaskListChecklist.razor`) — the whole list as nothing but
  tickable rows. The only thing it can change is whether an item is checked off, which is what lets it
  show the entire list at once. It deliberately takes **no** edit lock: ticking items off is not an
  editing session, and two people doing it at the same time is normal rather than a conflict. It still
  goes through the same `PUT /api/tasks/{id}`, so it does respect someone else's lock — a save during
  another user's deep edit comes back 409 and the checkbox snaps back to what the server holds.

Rows that can't be ticked by hand render as disabled checkboxes: items whose completion follows a
linked list (see above), and any list reached through a read-only share.

### Group lists

Setting `isGroup` marks a list as one that gathers other lists. It changes nothing about completion —
the flag is purely about how the list is presented — but in the shallow checklist view a group list is
rendered together with **every list its own items link to** via `linkedTaskListId`, each as its own
card with its items tickable in place. Ticking an item there saves that member list, not the group,
and the group's own linked row then follows it automatically through the usual completion resolution:
check off the last item on a member list and the group's row for it ticks itself.

Expansion goes exactly one level deep. A member that is itself a group list stays a single row rather
than unfolding further, so one screen can't turn into an unbounded tree.

### Moving an item to another task list

`POST /api/tasks/{sourceListId}/items/{itemId}/move` (`{ targetTaskListId }`) moves a single item out of
one task list and into another of the caller's own lists — a separate operation from `linkedTaskListId`
above, which mirrors another list's completion state without the item ever changing which list it
belongs to. Both lists must resolve to `CanEdit` access for the caller and share the same owner; the
item, its due date, notification settings, etc. are otherwise unchanged, just relocated.
`MoveTaskItemCommandHandler` persists both lists in a single `ITaskRepository.UpdateManyAsync` call so a
mid-operation failure can't duplicate or drop the item across the two lists. In the Blazor client, the
task editor's "Move to list" dropdown (next to the existing "Link to list" one, on each already-saved
item) triggers the move immediately rather than waiting for the form's own Save, since it reaches beyond
the one task list this editor page otherwise touches; a freshly added, not-yet-saved item has no dropdown
since there's nothing persisted yet to move.

## Inventory

`POST /api/inventory` and `PUT /api/inventory/{id}` both take `{ name, productType, category, quantity,
minimumQuantity, expiryDate, expiryNotificationChannel }` — `productType` and `category` are free text
(no fixed list), `quantity`/`minimumQuantity` are decimal (not integer) so fractional amounts like
"1.5 kg" are representable, and `minimumQuantity`/`expiryDate` are both optional: not every product
needs a restock threshold or an expiry date. `GET /api/inventory` and `GET /api/inventory/{id}` return
the same shape back plus `id`, `isBelowMinimum` and `hasPendingRestockTask` (both derived, computed
server-side so the client never reimplements the comparison), and `createdAtUtc`/`updatedAtUtc`.

**Items live in warehouses, and the warehouse is what sharing understands.** An `InventoryItem` carries
a `WarehouseId` rather than an owner of its own, so "may this caller read/change this item" is answered
entirely by `WarehouseAccessResolver` — the same owner-or-accepted-grant lookup `NoteAccessResolver`
does, with `ResolveForEditAsync` on top for the write paths so a read-only grantee can list a shared
warehouse's stock without being able to touch it. `Warehouse`/`WarehouseShare` mirror `Note`/`NoteShare`
including the re-share rules (who may re-share, and never above their own level), and a share is offered
over chat exactly like every other kind: `WarehouseShareMessagePayload` carries the share id inside an
ordinary end-to-end encrypted message, and Chat renders it with the same "Accept" action.

**Editing works the way a task list does.** A warehouse and its whole item list are edited in one form
and saved in one request (`UpdateWarehouseCommand`), so items have no routes of their own — exactly as
task items only exist through their task list. Items missing from a save are deleted, which makes the
request the full intended contents rather than a patch.

Items are *reconciled* by id rather than replaced wholesale: a row arriving without an `Id` is new, one
with an `Id` updates in place. That matters because an inventory item carries state the editor never sees
— its open restock task — so a delete-and-reinsert would drop it and re-raise a restock task the user
already has. The restock rule itself is unchanged, just applied per item during the save: an item that
just went below its minimum raises a task, one that recovered has its reference cleared.

Because the whole warehouse is now saved in one go, it carries the same **edit lock** Note/TaskList/
CalendarEvent do (`POST`/`DELETE /api/warehouses/{id}/lock`, a 60-second lease refreshed by a 20-second
heartbeat from the editor) — without it two people editing at once would silently overwrite each other.
A save attempted while someone else holds the lock comes back `409` with their name.

**Only the owner may delete** a warehouse — not even a `CanEdit` grantee, since that would let a
recipient destroy the owner's data wholesale rather than just edit it; its items go with it, because
nothing could reach them afterwards. Accepted shares of a deleted warehouse are left as dangling grants,
which the resolver already reads as "not found".

Every route names the warehouse (`/api/warehouses/{warehouseId}/...`) — there is deliberately no route
that reaches an item without it, since the warehouse is what authorizes the request. On the client,
`/inventory` is the warehouse list (`Warehouses.razor`, where sharing lives) and `/inventory/{id}` is the
editor (`WarehouseEditor.razor`).

**Low stock creates a real Task, not a separate notification.** Whenever a saved item's `quantity` drops
strictly below its `minimumQuantity` (exactly at the minimum still counts as fine - the minimum is the
level to keep, not one that already needs restocking), `InventoryTaskListCoordinator` appends a `TaskItem` ("Restock:
{name}") to a single, system-managed `TaskList` titled "Restock supplies" — the exact same `TaskList`/
`TaskItem` domain objects Tasks itself uses, so the item shows up on `/tasks` with the same
edit/complete/notification UI as anything the user created by hand. This check runs inline inside the
Create/Update handlers right after saving (`CreateInventoryItemCommandHandler`/
`UpdateInventoryItemCommandHandler`) rather than via a background poll — a stock level only ever changes
because the user just edited it, so there's no "time passing" trigger to poll for the way overdue tasks
or calendar reminders have.

The managed task list is created lazily, once per **warehouse**, the first time any item lands in it —
independent of whether that first item happens to be low — and comes pre-seeded with one standing item,
"Update stock levels", with `RemindDaily` turned on. This is the "recurring reminder to keep stock
updated" the feature calls for: Tasks has no engine for a task that recreates itself after being
completed, but `RemindDaily` already nags daily until checked off, and unchecking it re-arms the daily
nag — treated here as close enough to "recurring" without building a second recurrence engine on top of
Tasks' existing one (Calendar's). Since `TaskList` has no field to mark itself "system-managed", a
separate one-row-per-warehouse table (`InventoryManagedTaskListEntity`) tracks which `TaskListId`
Inventory created, entirely outside the Tasks schema. The list itself belongs to the warehouse's *owner*,
never to whoever happens to be looking — otherwise a share recipient's own `/tasks` would fill up with
someone else's restock chores. Expiry warnings go to the owner for the same reason.

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
a violation comes back as a 400 naming the rule (see [Refusing a request](#refusing-a-request)), and
client-side in `CalendarEventEditor.razor`, which shows an inline error instead of submitting a request
that's bound to fail.

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

## In-app notifications

Every push/email trigger above (the three under [Push notifications](#push-notifications), plus a
calendar event's own "on creation" notification) also records an entry in a per-user in-app feed,
independent of whether push or email delivery is actually enabled — this is what backs the sidebar's
unread badge, the "Notifications" panel, and the toast banner described below.

**`NotificationSettings`** (`Orbit.Core.Notifications`) is a per-user row (created lazily on first read,
not at registration, so no migration ever has to touch existing accounts) with five switches:
`AllowNotifications` (master — off suppresses everything below, including new feed entries),
`AllowPush`, `AllowEmail`, `AllowMobileBanner`, and `ShowExceptionDetails` (see below). `Update` forces
the three delivery/display switches off whenever the master switch is off, so nothing downstream needs
to check the master switch itself. `GET`/`PUT /api/notifications/settings` expose this to the client;
Options.razor's "Notifications" section renders the master switch and three child switches (the
children visually greyed out via the `disabled` attribute, not hidden, when the master is off).

**`NotificationRecorder`** (`Orbit.Core.Notifications`) is the single place every trigger goes through
instead of calling `PushNotificationDispatcher`/`IEmailSender` directly: given a user id, the per-item
`NotificationChannel` that item was configured with, and the notification's title/body/url,
`RecordAndFilterAsync` looks up that user's settings once and returns the per-item channel with any
globally-disabled channel stripped out (the global switch overrides the per-item choice, not the other
way around), plus whether a `NotificationEntry` was recorded (true whenever the master switch is on,
regardless of whether the specific delivery channels are). Each background service's own claim-before-send
idempotency guard (see [Push notifications](#push-notifications) above) treats a recorded entry the same
as a successful channel send, so a notification with both delivery channels globally disabled doesn't
look "unclaimed" and get retried every poll.

Existing per-item `NotificationChannelOption` dropdowns (on a calendar event, task item, or inventory
item) grey out the "Push"/"Email"/"Email and push" options (via `NotificationChannelOption.IsDisabledBy`)
when the corresponding global switch is off — the value can still be picked and stored, it just won't
actually deliver on a channel the account has turned off.

**The feed itself.** `NotificationEntry` (`Id, UserId, Kind, Title, Body, Url?, CreatedAtUtc, ReadAtUtc?`)
is a flat, reverse-chronological list per user — `GET /api/notifications` returns the most recent 30,
`GET /api/notifications/unread` the unread ones (which is what the per-source badges are computed from),
`POST /api/notifications/read` marks everything read at once, `DELETE /api/notifications` empties the feed (there's no per-entry read state exposed anywhere, matching "opening the panel clears the
badge" rather than tracking which individual entries were seen). `Kind` is `PushReminder` or
`ChatMessage`, mostly for the client to render slightly differently later; `Url` is the same in-app deep
link (`/tasks/{id}`, `/calendar/{id}`, `/chat/{userId}`, ...) the corresponding push notification's own
payload already carries.

**Client (`MainLayout.razor`).** The avatar gets a small unread-count badge (`FormatUnreadCount`: hidden
at 0, the number at 1–9, "9+" above that) and a new "Notifications" entry next to "Log out" in the
dropdown. Opening it loads the recent feed, calls `POST /api/notifications/read`, and zeros the badge
immediately rather than waiting for the next poll tick. Clicking a feed row that carries a `Url`
navigates there, so the panel reaches the same destination the corresponding push notification would.

**Desktop opens a popup; a phone opens a page.** A 320px panel anchored to the mobile top bar leaves
almost nothing readable, so on that breakpoint the entry navigates to `/notifications`
(`Notifications.razor`) instead — the same decision the logo makes when it becomes a Dashboard shortcut
(`OrbitViewport.isMobile`, the one place both CSS and Blazor read the breakpoint). Both forms render the
same `NotificationList` component and offer the same **Clear**, which empties the server feed *and* this
browser's captured errors, because the panel presents them as one list and clearing half would look
broken. Clear discards rather than marks read — it is about getting rid of the list, not the badge.

**Badges mark where a notification came from, not just that one arrived.** The 10-second poll fetches the
unread *entries* (`GET /api/notifications/unread`) rather than a bare count and puts them in
`NotificationFeedState`, a scoped service everything badges off: the avatar (total), each nav section,
and each chat contact's avatar. A section's count is simply how many unread entries have a `Url` under
its prefix (`/tasks`, `/calendar`, `/inventory`, `/chat`), so a reminder shows up on the thing it is
about. Chat subscribes to the same state instead of polling again. The poll also refreshes settings, so a
change made on Options takes effect within one interval; when the unread count has just gone up and
`AllowMobileBanner` is on, it shows the newest entry as a toast fixed to the top of the viewport.

How long that toast stays up, and the minimum quiet gap before the next one, are per-user settings
(`BannerTiming`, defaulting to 5 seconds each) editable from Options — the poll interval only bounds how
quickly a new entry is *noticed*, not banner pacing. `BannerTiming` clamps rather than rejects
out-of-range input (1–30 seconds visible, 1–300 seconds gap), since a settings form shouldn't hard-fail
over a typo in a number field.

**Frontend exceptions stay client-local**, extending the existing `PersistentLoggerProvider`/
`orbit.clientLogs` localStorage mechanism ([Authentication](#authentication) above touches the same
mechanism for session-expiry diagnostics) rather than round-tripping raw client errors to a new server
endpoint — they're per-browser debug info, not something worth centralizing or risking as a spam vector.
The Notifications panel reads them via a new `window.OrbitClientLogging.getEntries()` (added alongside
the existing `copyLogsToClipboard`), filters to Error-level entries, and gives each one its own "Copy"
button (`navigator.clipboard.writeText`, invoked directly from C# via `IJSRuntime` — no new JS needed for
that part). This section only renders when **both** `GET /api/config/client-flags`'s
`ExceptionDetailsAllowed` (reflecting `IWebHostEnvironment.IsDevelopment()` — an unauthenticated,
environment-driven flag, the same shape as the existing VAPID public-key endpoint) and this user's own
`ShowExceptionDetails` setting agree — the server-side flag is a hard gate a Production deployment can
never be talked out of via a stored per-account preference. Options.razor's own "Diagnostics" section
(the "Show exceptions" switch) is likewise only rendered at all when the server reports it's not running
in Production.
