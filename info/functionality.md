# Functionality

This document describes what each implemented feature does and how it behaves, in detail. For where
each piece lives in the codebase, see [Architecture](architecture.md).

## Authentication

`POST /api/auth/register` (`email`, `userName`, `displayName`, `password`) and `POST /api/auth/login`
(`emailOrUserName`, `password`) both return `{ token, refreshToken, userId, email, displayName }` on
success. Login accepts either the account's email address or its username in the same field — both are
unique, so there's no ambiguity. Send the access token on every `/api/notes`-style request as
`Authorization: Bearer <token>`; without it, the API returns 401.

A refused sign-in comes back as a 401 carrying a `LoginRejectionDto` naming which half was wrong:
`NoSuchAccount`, `WrongPassword`, or `NoPasswordSet` (an account made with Google that has never set
one, where reporting a wrong password would send somebody looking for a password that does not exist).
This is a deliberate trade — it makes the endpoint an account-existence oracle — taken because
registration already answers the same question by name, so keeping login silent protected nothing while
leaving a reader to guess which of the two fields to change. What still stands between it and a list of
Orbit's users is the rate limit on the whole auth group. Password reset stays silent for a reason that
does not apply here: it sends mail to an address the caller named, so an answer there would be an oracle
anybody could point at anybody.

That rate limit is said out loud too. A 429 from any of the three auth calls (login, registration,
Google) shows "too many attempts, wait a minute" rather than the generic "an error occurred, try again"
it used to — which was both wrong and the worst possible advice, since trying again is what keeps the
window shut.

**Forgetting the password.** `POST /api/auth/password-reset` (`emailOrUserName`) emails a code to the
address the account was registered with, and `POST /api/auth/password-reset/confirm`
(`emailOrUserName`, `code`, `newPassword`) sets the new one. The phone offers this from its sign-in
screen — "Forgotten your password?" — as a screen of its own: ask for a code, then type the new
password twice, since there is nothing to check it against and a typo would lock the account a second
time with the code already spent. Whatever the account turns out to be, the answer is the same
conditional sentence: the request must not become a way of testing whether somebody has an Orbit
account. Nothing is signed in afterwards, because the chat key is wrapped with the password that is
gone — what replaces it is decided at the chat key gate, with the warning that messages sealed under
the old password stay unreadable. The web offers the same two steps at `/forgot-password`
(`ForgotPassword.razor`), reached from a link under the sign-in form; it also still reaches them from
the chat password gate, which is the same flow for somebody already signed in.

Both sign-in forms listen for `input` as well as `change`, and neither uses `@bind`, which can only be
told about one of the two. A password manager fills a box without anybody typing in it: some raise one
event, some the other, some neither until the field is touched — so a form bound to a single event
showed a filled box with an empty model behind it and sent an empty password to a real account, which
the server answers exactly as it answers a wrong one.

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

**The phone is under the same rule, and broke it in its own way.** `TokenRefreshService` there was
registered with `AddHttpClient<TokenRefreshService>`, which registers the type as **transient** - so
every synchronizer held its own, and the single-flight guard inside it (one in-flight redemption shared
by every caller) guarded nothing across them. Two of them meeting an expired access token at the same
moment each redeemed the same single-use refresh token, and the loser's rejection signed the reader out
mid-use. It shows up in the server's log as a refresh answering 200 and another answering 401 a second
later. The client is now registered as a singleton over a named `HttpClient`.

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

## Permissions

Four things an account can hold (`ApplicationPermission`), and an account starts with none of them:

| Permission | What it opens | Needs |
| --- | --- | --- |
| `Contacts` | Looking anybody up, and being findable at all | — |
| `Chat` | Conversations, one-to-one and group | `Contacts` |
| `Sharing` | Handing a note, list or event to another account | `Contacts` |
| `Location` | Recording your own position and seeing it on the map | — |

`Contacts` is the master of the social half: without it an account can neither look anybody up nor be
turned up by anybody else's search (`UserVisibility`), which is why `GET /api/users/{id}` answers **404**
for an account that has not unlocked it even to somebody who is already a contact - knowing an id is not
meant to be a way around being invisible. `Chat` and `Sharing` are stored when redeemed but stay inert
until `Contacts` is held too (`PermissionPrerequisites`), so granting them in either order works.
`Location` stands apart: recording and sharing your own position needs nothing else, but *seeing* what
others have shared needs `Contacts`, since that is somebody else's account becoming visible.

A permission is unlocked by typing its code (`POST /api/users/me/permissions/redeem`, rate-limited like
the other endpoints that change an account). The codes are rows in `PermissionCodes`, one per permission,
made on the first start that finds one missing and left alone by every start after that:

```sql
SELECT "Permission", "Code" FROM "PermissionCodes";
```

Twelve characters of Crockford base32 without I, L, O and U, so nothing in a code can be misread off a
screen; what somebody types is trimmed and upper-cased before it is compared, and every stored code is
compared even after one matches, so how long the answer takes says nothing about which code a typed one
was close to. A code can be **rotated** - `PermissionCodeStore.RotateAsync`, or the `UPDATE` in the
deployment's own notes - and nothing caches one, so a change takes effect on the next code somebody
types, with no restart.

## Priorities

Every note, task list and calendar event carries a priority - `Low`, `Normal` or `High`
(`ItemPriority`), chosen in its editor and stored by name rather than by number, so the values mean the
same thing on the wire, in the database and in a log line. It sorts the task list page, marks a card with
a badge, and is what the dashboard's per-card filter reads. Rows written before the column existed read
as `Normal`, so nothing has to be revisited.

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
and the Blazor editor pages disable their form (via a `<fieldset disabled>`) for the same grantees. **The
phone does the same as of 2026-09-01, and did not before**: it asked only whether an offline edit was
safe (`OfflineEditPolicy`) and never what the share allowed, so anything shared read-only opened as an
ordinary editable screen the moment the phone was online. The edit was applied locally, queued, refused
by the server, and given up on minutes later - work disappearing with nothing on the way saying why.
`SharedItemAccess` (`Orbit.Mobile.Sync`) is the missing half: the four detail screens open read-only and
say so, the four repositories refuse the write rather than queue it, and a copy-for-editing is not
offered, since a copy of something shared to read could never be kept over the original. Both clients
read the rules from `ShareAccess` in `Orbit.Core` rather than comparing strings - `EditOnly` permits
editing too, and a check written as `== "CanEdit"` calls an editor a reader. Deleting is untouched on
both: for a grantee it means taking the item off their own list, which is theirs to do.

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

**What a shared task list drags along with it.** A group list is a set of headings pointing at other
lists, so handing one over on its own handed the recipient a page on which nothing opened — and the same
was true of the inventory its stock check is read against. `TaskListShareCascade` follows the links all
the way down: every list in the tree, and every warehouse any of them is measured against, is granted
alongside the offer at the same access level, accepted when the offer is accepted, and claimed when a
public link to the list is claimed. Private lists and private warehouses are left out, for the same
reason they cannot be shared directly — their contents are sealed in their owner's browser, so a grant
would only ever hand over ciphertext. Only one grant is offered by name, so there is still one chat
message and one thing to accept; the rest follow it. Re-sharing an already-accepted list re-runs the
cascade, so a list added to the group afterwards is not left as a second thing to agree to.

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

A lock is written on its own, and writes nothing but itself: who holds it and until when
(`ITaskRepository.UpdateLockAsync`, and the same on notes and warehouses). It went through the ordinary
update at first, which replaces everything the thing is made of - so a heartbeat every twenty seconds
deleted and re-inserted a whole checklist with its links and categories, rewrote a note's entire text,
or rewrote every row of a warehouse, to say somebody still had the page open. On task lists that also
crashed: the replacement left the entries' child rows to the database's cascade, which the change
tracker knows nothing about, and the inserts for the new ones could reach the server first - a duplicate
key on a save nobody thought was risky, seen in production on 1 September 2026. Holding something open
is not a change to it, so the lock does not touch `UpdatedAtUtc` either.

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

Groups are not a place of their own: the chat page shows **one conversation list**
(`ConversationList`) holding people and groups together, **sorted by when something last happened** -
people and groups against each other, which is the order somebody scanning for a conversation looks in.
A row says which kind it is with a small mark, one search box filters both, and "New group" sits under
the list rather than in a page header. Looking for "who have I been talking to" is one place, and moving
between a group and a person does not change screens.

That single order needs both kinds to answer the same question, so a group carries
`LastMessageAtUtc` of its own (`ChatGroup`), stamped where the fan-out is written -
the same thing sending to one person does to that contact's row. It is never null:
a group with nothing in it yet answers with the day it was made, so the list is
totally ordered from the moment a group exists rather than needing a second rule for
the quiet ones. Groups used to follow the people in a block of their own, sorted by
name, because there was no such time to sort them by.

The list folds to a strip of initials, and **the folding is done by the stylesheet alone** — the names,
the search box and "New group" always reach the page. That matters because on a narrow screen the list
is not an inline panel at all but a slide-out drawer, where folding means nothing: the drawer is either
open or off-canvas. Markup that dropped the names when folded could not be talked back into showing
them however much CSS asked, so the drawer opened as a wide panel of bare initials with no search and
nobody's name on it. The rules that fold it now sit behind a `min-width` query, so the drawer needs
nothing to undo them, and the fold button is hidden there — the drawer is opened and closed from the
thread header instead.

Groups are not a screen of their own either. `Chat.razor` answers `/chat/groups` and
`/chat/groups/{id}` alongside `/chat/{userId}`, so a group opens in the same shell a person does — same
list down the side, same header, same thread — and `GroupConversation` (a component, not a page) draws
only what is genuinely different about a group: who wrote each message, whether everyone has read it,
and an admin's reach over somebody else's message. Switching between a person and a group is a
parameter change on one page rather than a change of screen, and starting a new group happens where the
conversation would be instead of on a separate form.

The thread header carries **one menu in its corner** for the conversation itself. For a person it
offers **Info**, which opens their card (`/contacts/{userId}`, `ContactInfo.razor` — the same page the
contact list's "Info" button and the dashboard's contacts card open). For a group it offers **Members**
(`/chat/groups/{id}/members`, `GroupMembers.razor` — the roster, with the add/remove/promote controls
an admin gets and the "Leave group" button everybody gets) and **Info** (`/chat/groups/{id}/info`, name,
size, when it started, and this reader's own role). The roster is a page rather than a panel folded into
the thread: one row per person with two buttons each for an admin, above the messages, left the
conversation itself below the fold on every visit.

**A card for somebody who has gone unfindable says so, and says what it cannot know.** An account that
has not unlocked `Contacts` is invisible in both directions, and a lookup for it answers exactly as a
lookup for nobody does — "found, but hidden" would be finding them (`UserVisibility`). So the card
genuinely cannot tell "they closed their door" from "no such id", and it says that rather than picking
one. It can tell whether there is a conversation, though: the contact entry is read whether or not the
profile resolves, because a conversation's names are resolved without that visibility check
(`GetUsersByIdsQueryHandler`), which is what keeps an existing chat readable. So somebody you talk to is
named from the conversation, told the two possible reasons, and told plainly that the messages are
unaffected — while an id that means nothing gets "there is nothing to show" and no offer to open a chat
that does not exist.

**The phone opens the same card** (`ContactInfoViewModel`), from the contact list's row menu, from
beside somebody just found by search, and from the conversation's own header - the three places a name
is already on screen. It answers from the row this phone holds before it asks the server anything, which
is why it says who somebody is on a train: the contact sync now stores the address as well as the name,
so the whole card reads offline. What only the account can say - a name changed since the last sync -
overwrites it when the lookup answers, and nothing is claimed when it cannot be reached: being offline
is not an answer about somebody.

A chat with more than one other person, under the same end-to-end encryption one-to-one chats already
have. There is no group key: the sender's browser encrypts the same text **once per other member**,
under the pairwise key it already shares with each of them, and posts the copies together
(`POST /api/chat/groups/{id}/messages`). Each copy is an ordinary `ChatMessage` row tagged with the
group and with a `GroupMessageId` the copies share.

That choice buys a lot and costs two things worth knowing:

- Nothing new to distribute or rotate when membership changes, and the server still can't read
  anything — it never had a key and doesn't get one now.
- **A new member cannot read anything sent before they joined unless somebody hands it to them**, since
  no copy was ever encrypted for them — see [Letting a new member read the history](#letting-a-new-member-read-the-history)
  below.
- A message costs N rows instead of one.

The server checks the fan-out rather than trusting it: exactly one copy per current member, no more and
no fewer. A missing copy would silently cut someone out of a conversation they are in; an extra one
would deliver into a group its recipient has no part in. Reading a group returns only the copies the
caller can actually decrypt — the ones addressed to them plus the ones they sent.

### Letting a new member read the history

Adding somebody to a group offers a checkbox: **"Also give them what was said before they joined"**
(`GroupMembers.razor`). It is off unless asked for. Everything said in the group so far was said to the
people who were in it, and handing it on is a decision somebody makes rather than what happens if they
do not look.

The work is the **adder's own device's**, because there is nowhere else it could happen. The server has
never held a key to any of this and cannot make a copy for the newcomer, so `GroupHistorySharing` reads
the conversation the adder can already read, decrypts each message on their device, seals each one again
under the pairwise key they share with the new member, and posts the results
(`POST /api/chat/groups/{id}/history`). A message this device cannot open — one sealed under a key pair
since replaced — is left behind rather than passed on as ciphertext the newcomer would stare at, and the
screen says how many actually went across.

**Both clients can do it**, with a class of that name each: the browser's runs the crypto through
`e2eeChat.js`, the phone's through `ChatIdentity` in process, and they hand the same thing to the same
endpoint. Being able to add somebody to a group but not give them its past — which is what a phone
could do until now — made the switch a thing you had to go and find a browser for.

What the server will accept on their behalf is narrow, since it cannot read what it is being handed:

- **Only an admin may share.** Deciding what a newcomer sees on arrival is the same act as deciding they
  arrive at all (`ChatGroup.EnsureHistoryCanBeSharedWith`). Nothing stops an ordinary member replaying a
  conversation by hand; what the rule buys is that the group's own history is not handed over as the
  group's doing by somebody never trusted with its membership.
- **Only into a membership.** The recipient has to already be in the group, and nobody shares with
  themselves.
- **Only what the sharer actually holds.** Each copy names a `GroupMessageId`, and the server looks up
  the sharer's own copy of it to take the sender, the instant and the edited state from — never from the
  request. Who wrote a message and when are facts about it, and re-sharing is not the place they get to
  be restated. A posting the sharer holds no readable copy of is dropped rather than stored under an
  invented attribution.
- **Never twice.** A message the recipient already has is skipped, so a retry after a half-finished
  share, or a button pressed twice, does not leave them reading everything in duplicate.

A backfilled copy is marked (`ChatMessage.IsSharedHistory`) and that mark does two things. It keeps the
copy out of the original's **delivery receipts** — a receipt says whether a message reached the people it
was posted to, and counting a copy made afterwards would take a sender's "Read" away for a delivery that
had already happened. And it sorts last when the conversation picks which copy stands for a message, so
a reader who already had one keeps reading the one they have been reading, and only the newcomer — who
has nothing else — is given the new one.

### The line that says somebody joined

Being added to a group used to be visible only to the person it happened to: the group turned up in
their list, and everybody else saw a roster that had quietly changed. Now the conversation itself carries
a line for it — *"Anna joined Weekend trip"* — drawn where it happened rather than pinned anywhere
(`ChatGroupAnnouncement`, rendered by `GroupConversation.razor` and, on the phone, by
`GroupAnnouncementLine` in the same thread and the same words).

When the history was shared, the same line says so too: *"Anna joined Weekend trip · Piotr shared the
conversation so far"*. The two halves are recorded separately on purpose. The join is known the moment it
happens; the history is sealed in the sharer's browser and arrives afterwards, so the line gains its
second half only once the copies actually land. An admin who ticked the box and whose browser could open
nothing has shared nothing, and the line says only what happened.

These lines hold no ciphertext, and that is not an oversight: everything in one — who joined, who added
them, when — the server already knows from the membership table. There is nothing here to seal. They are
read from a route of their own (`GET /api/chat/groups/{id}/announcements`) rather than folded into the
messages, so the conversation endpoint keeps answering exactly what every already-installed client
expects of it; the browser merges the two by time.

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
read it, and Orbit's servers can't. Ticking the box in the editor makes the client seal the title and
the content before saving, so what the server stores is `IsPrivate` plus a base64 ciphertext and nonce
(`EncryptedPayload`) — the readable columns go **empty**, not merely unread. `Note.Update` and
`TaskList.Update` enforce that pairing rather than trusting callers: claiming privacy without sealed
content is refused, and turning privacy off drops the ciphertext.

The key is the one chat already uses, agreed with the owner's own public key on both sides of an ECDH
exchange (`e2eeChat.js`'s `encryptForSelf`, and `ChatIdentity.EncryptForSelf` on the phone). That means
no second key to generate, back up, or restore: a device that can read your chat can read your private
items, one that can't asks for your password the same way, and **a password reset replaces the key
pair, so private content is lost with the chat history** — the editor says so next to the checkbox.

Sealing and opening happen in `NotesApiClient`/`TasksApiClient`, not in the pages, so the overview, the
dashboard, the checklist view and the calendar all receive a readable DTO without knowing any of this
happened. Content that can no longer be opened renders with an "Unreadable — encrypted with an older
key" title rather than throwing, so one lost item doesn't take a whole list down.

**Both clients do all of this**, and to the same bytes: what goes inside the ciphertext is JSON, so the
payload shapes (`SealedNote`, `SealedTaskList`, `SealedWarehouse`) live in `Orbit.Contracts` and are
serialized with the same property names on either side — `SealedContentTests` pins the phone's
source-generated output against the browser's reflection-based one, because a mismatch there produces
notes that round-trip perfectly on one client and cannot be opened on the other.

Two things are the phone's own. Its local database holds the **sealed** payload rather than the opened
words, so a handset that is picked up says no more about a private note than the server does; the
words are opened for the screen that shows them and never written back (`LocalNoteRepository`,
`PrivateContentSealer`). And a private item is additionally kept behind the **device lock** — a face
or a passcode — on the notes, tasks and inventory screens, which is the physical counterpart of a
promise that otherwise only holds against the server (`PrivateItemGate`). A device holding no key says
which of the two situations it is in — no key here, or a key pair since replaced — rather than showing
an empty editor.

What private costs:

- **It can't be shared.** `ShareNoteCommandHandler`/`ShareTaskListCommandHandler` refuse it, and an
  existing share stops resolving the moment the item becomes private — the grant row is left in place
  and simply no longer grants, so turning privacy back off restores it.
- **A private task list gets no reminders.** Overdue and daily reminders are scheduled server-side from
  due dates the server can no longer read.
- **Items can't be moved into or out of one** (`MoveTaskItemCommandHandler` refuses): a private list
  keeps no readable items, so the move would take the item off the source and then drop it.
- **Completion is recomputed on the client.** The server derives `IsCompleted` from items it can't see,
  so what it sends for a private list means nothing; `TasksApiClient` and `LocalTaskListRepository`
  work it out after opening.
- **A private list's entries get their identity from the client.** The server mints entry ids and never
  sees a private list's entries, so the phone mints one for any entry that has none before sealing —
  without it every entry on the list would share the empty id.

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

### Planning something at a place

The map is where people already go to point at somewhere, so it is also where pointing at somewhere and
making something of it belongs. **Plan something here** opens the same `LocationPickerOverlay` the task
editor uses - a pin, or an address search - and confirming one asks a single question: is this an event,
or a task list?

The question is asked rather than guessed. An appointment and an errand at the same address are
different things, and only the person pointing at it knows which they meant. Answering takes them to the
form they chose with the place already filled in:

- **An event in the calendar** opens `/calendar/new` with the address and its pin set.
- **A task list starting here** opens `/tasks/new` with one entry already standing at that place - a
  calendar entry, because it is the only kind that has anywhere to be, and open, because an entry whose
  place is filled in and whose day is not is not finished.

Either way the **pin** travels, not only the address: the calendar keeps places as coordinates with a
label (see [`EventLocation`](#the-place-is-stored-once)), so an address on its own could not be shown on
a map or turned into a Google Maps link.

The place travels in a scoped `ChosenPlace` rather than in the address bar. `/calendar/new?lat=52.2&lon=21.0`
would write where somebody is going into their browser history and into anything that later reads a URL,
and a place is exactly the kind of thing that should not be sitting in a link somebody might paste.
Nothing about it needs to survive a reload - it is a handover between two screens, a second apart - and
it is **taken** rather than read, so coming back to a new event or a new list later starts empty instead
of at somewhere the reader looked at once and has no memory of choosing.

## Handing something off to Google

An account that has **confirmed its email address or connected Google** is offered links that carry
something across to Google (`GoogleIntegrationAccess` decides; both routes mean the same thing here -
somebody stood behind the account rather than typing an address nobody has read):

- **Add to Google Calendar**, on a calendar event and on a task item that has a due date. Google Calendar
  opens with everything filled in and the user saves it wherever they want.
- **An address as a link**, on a calendar event's location and on the Map page - the street name itself
  opens the place in Google Maps.
- **Directions** to an event's location or to a position someone shared.

### These are links, not an API integration

Nothing here calls a Google API, and Orbit asks for no access to anyone's calendar. `GoogleCalendarEventLink`
builds a Google Calendar *template* URL; `GoogleMapsLink` builds Maps URLs. Both are plain links - no
API key, no quota, no OAuth scope, no stored token - and the user stays in control of what actually
lands in their calendar, because they press save themselves.

What that means in practice:

- It works the moment someone clicks it, on any deployment, with no Google Cloud setup beyond the sign-in
  client id Orbit already needs.
- It is **one-way and one-shot**. Editing the event in Orbit afterwards does not change the copy in Google
  Calendar, and deleting it there does not tell Orbit. There is no sync.
- Orbit cannot *read* anyone's Google Calendar, so it cannot show Google events beside Orbit's own.

Turning that into real two-way sync is a different kind of change - see
[Future Plan](future-plan.md#what-real-google-calendar-sync-would-take).

Details worth knowing about the links themselves, all of them things that silently break a link when got
wrong (and each pinned by a test):

- Google reads `dates` in UTC in one exact shape, so a local time is converted rather than relabelled.
- An all-day range's end is **exclusive**: one day is written `20260901/20260902`.
- A task has no Google equivalent a template link can create, so a deadline becomes a short event ending
  at it.
- Coordinates are formatted invariantly - under `pl-PL` a decimal comma would split the pair into two
  parameters and send the reader somewhere else.
- Directions carry **no origin**, so Google routes from where the reader actually is. Passing Orbit's
  recorded position would look more precise and be worse: it is whatever they last recorded on purpose,
  possibly another city days ago.
- Every link opens with `target="_blank" rel="noopener"`.

### Two depths, everywhere

Every object that can have both now has both: land on what the thing is, with the fields one named press
further in, and whatever light doing belongs to it offered where it is read.

| Object | Read | Change |
| --- | --- | --- |
| Task list | `/tasks/{id}` - tick items, see the tree it stands for, measure it against a storage | `/tasks/{id}/edit` |
| Task entry | `/tasks/{listId}/items/{itemId}` - when, where, what the appointment is about, who is coming, and a map | its own row in the list's editor |
| Note | `/notes/{id}` - the note read, with the checklist lines in it tickable | `/notes/{id}/edit` |
| Calendar event | `/calendar/{id}` - when, where, what it is about, who is coming, its reminders, and a map | `/calendar/{id}/edit` |
| Storage | `/inventory/{id}` - one row per batch, counted up and down in place | `/inventory/{id}/edit` |

What "light doing" means differs by object and is the point of the split: a list is ticked, a note's
checklist lines are ticked, a shelf is counted up and down. An appointment has none - there is nothing
about it to do without changing what it is - so its page has no Save, which is honest rather than
missing. Nothing is written until Save on any of them: these are pages people scroll through.

### A shelf, read rather than edited

`/inventory/{id}` is what opening a warehouse lands on: one row per batch, saying what it is, how much
there is, when it arrived and how long it keeps. A row is a batch rather than a product - two rows can
carry the same name, which is what two deliveries of one thing are, and the check that measures work
against a shelf adds them up by name (`StockRequirementCounter`). A row an errand pointed at is marked,
the way the editor already marked one.

Each row also carries the two things somebody standing in front of a shelf actually does: **one off, one
back on**, before the name, where the eye starts. Nothing is written until Save - the same tick and cross
every editor in Orbit carries - and saving refreshes the restock list, because a count that has just
crossed a minimum either raises an errand or settles one. Counting below nothing is refused rather than
stored: minus one of something is a number nobody can act on.

Everything else is behind the menu beside them: all warehouses, the editor, and deleting. Changing the
fields themselves is a named press further in (`/inventory/{id}/edit`) - the same two depths a task list
has, for the same reason: opening a warehouse to see what is in it is a different thing from opening it
to change it, and a page of editable fields is the wrong answer to "what have we got".

### What the calendar's list leaves out

The list beside the grid answers "what is coming", so it leaves out what is over: a deadline already
ticked off, and an event that has already ended. An overdue deadline that is still not done **stays** -
it is the one thing on the page that most needs saying, and hiding it would hide the work.

The grid never hides anything. A day with something in it should say so whether or not it has been, and
a month drawn with holes in it would be a month that had not happened.

**The phone draws the same line**, from the same menu the order is chosen in and kept beside it on the
device (`CalendarListReading`). Its grid keeps everything too.

**Show → "Everything, including what is over"** in the page's menu puts them back, and is remembered by
the device the way the list's order is (`CalendarListOrder`, localStorage - it describes one page for
one reader on one screen).

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

An item can instead reference other task lists of the user's - `linkedTaskListIds`, with
`linkedTaskListId` repeating the first of them for a client that has not learned about the plural yet -
rather than being independently completable. **The phone offers all of them as of 2026-09-01**: the
picker says what to add next rather than what is already there, each list it stands for is listed with a
way off, and a save sends the whole set. It carried them from the first sync but showed only the first,
so the rest were lost to whichever phone touched the entry next. A linked item's `isCompleted` is entirely derived — it follows the
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
see [Refusing a request](#refusing-a-request).

**The editor asks the same question before it offers the link.** Its "link to list" dropdown leaves out
every list that links back to the one being edited, however long the chain (`TaskListLinkCycle`, which
walks the saved lists exactly as the server does) - so a link that would be refused is never offered in
the first place, which is how every other rule the server enforces is handled on this side. What it
replaced was a failed save naming a rule nothing on screen had mentioned, and the deeper the chain the
less obvious what had gone wrong: A links to B, B to C, and the row offering C a link back to A looked
like any other. The "move to list" dropdown is not narrowed this way - moving a row is not linking, and
carries none of linking's rules.

Each **item** also says what it is: `kind` is `Checklist` (the default) or `Calendar`. A calendar entry
is somewhere to be rather than something to fetch, so it also carries a `location`, and can name the
`linkedCalendarEventId` of the calendar event it is the same appointment as. The kind sits on the item
rather than on the list because a list is rarely all one or all the other — a day's plan holds two
errands and an appointment, and asking somebody to keep those on separate lists is asking them to keep
the list that matches their day in two places.

**The place is stored once.** An entry tied to an event keeps no location of its own: the event already
holds one, and a second copy is how the two come to disagree. Every other kind of entry has nowhere to
be and stores nothing for it, including one changed back from a calendar entry. The link itself is not
validated — an event deleted afterwards leaves it pointing at nothing, which reads as "no event", the
same way a link to a deleted task list reads as "not completed".

An entry's own location can be typed or pointed at: **Show map** opens `LocationPickerOverlay` over the
page — a map (Leaflet, via the same `mapPicker.js` the calendar's event editor drives) that takes a pin
and reverse-geocodes it into an address. Over the page because a map needs room and an editor row has
none. Nothing is written back until the pin is confirmed: the overlay asks "Use this place?" with the
address it found, and only a yes replaces what the box held — a stray click on a map must not silently
rewrite an address somebody typed. The map opens where the box already points, when that address can be
found (`GeocodingApiClient.FindPlaceAsync`).

**A confirmed pin keeps its position, not only its name.** The overlay hands back a `PickedPlace` -
where it is as well as what it is called - and the editor keeps both. The words are still the reader's
to choose (confirming a pin fills an empty box and leaves a name they wrote alone), but the position is
what the calendar needs: a `EventLocation` is coordinates with an optional label, so an address on its
own cannot be shown on a map or turned into a Google Maps link.

This used to be the other way round - the overlay reverse-geocoded the pin and then discarded where it
was - and the consequences reached further than the pin. A calendar entry's event was created with no
place at all, and worse, an entry that *already had* an event sent "no place" back on every save, so
editing a task list's colour or its day erased a location somebody had set in the calendar. The web
editor now reads the linked event's location into the form along with every other field it already read
back, and the phone - which has no map to offer and so cannot set a place - carries the one it loaded
through untouched rather than writing a blank over it (`TaskItemEventForm.PlaceItAlreadyHad`).

**A name nobody pointed at stays a name.** Typing "the back entrance" and never opening the map leaves
the entry with a label and the event with no place, and the editor says so under the box rather than
letting it go nowhere quietly. Orbit does not look coordinates up for it: `SearchPlacesAsync` exists
precisely because "Długa 4" is a real address in a dozen towns, and silently taking the first match
would put the appointment in the wrong one. The calendar's own event editor refuses in the same way.

**A place can be named as well as pointed at.** The overlay carries an address search
(`GeocodingApiClient.SearchPlacesAsync`), which is the way that works when somebody knows the address
but not where it is on a map. It offers every match rather than the best one — street names repeat, and
"Długa 4" is a real address in a dozen towns, so quietly taking the first would drop a pin in whichever
of them Nominatim happened to rank first. Picking one moves the pin there (`mapPicker.js`'s
`moveMarker`) and asks the same "Use this place?" question a clicked pin asks, so there is one way to
save and not two. A single match is taken straight away: confirming the only answer twice — once as a
row, once as the question — is asking the same thing twice. The address saved is the one that was
picked, not a second lookup of it, which could answer differently.

In the Blazor client, each item's due date and time are edited separately (`DateField` plus `TimeField`)
and combined into one timestamp on save; a date picked without a time is stored as midnight. Both are
Orbit's own boxes rather than the browser's `<input type="date">`/`<input type="time">`, which draw
themselves in the *browser's* locale — so Orbit read in Polish on an English-language browser asked for
times in AM/PM and opened a Sunday-first month, unlike every other calendar in the app. A time is
entered and shown as `HH:mm`, a date as `dd.MM.yyyy`, and the date box opens a Monday-first month of its
own. Neither guesses at what it cannot read: an unparseable entry snaps the box back to what it held
rather than turning half a deadline into some other one.

`DELETE /api/tasks/{id}` deletes a task list (and its items, via the `ON DELETE CASCADE` foreign key
from `TaskItemEntity` to its owning list — see `OrbitDbContext`); like the other endpoints, it 404s if
the id doesn't exist or isn't owned by the caller. Deleting a list that another list's item links to via
`linkedTaskListId` leaves that reference dangling rather than blocking the delete or cascading further —
`LinkedTaskCompletionResolver` already treats a link to a missing list as "not completed" instead of
failing, so this is safe, just something to be aware of if a list you expect to still be linkable is
gone. The Blazor client's task list page asks for confirmation before calling this endpoint.

### How much of each list the page shows

The tasks page's menu carries a **Card view** with three answers, remembered by the browser like the
sort order beside it (`TaskListArrangement`):

- **Minimal** folds every card to its heading and the one line worth having - what is still to be done.
  Each card's own control reads "Expand" while it is on, because this is the same state as folding them
  all by hand.
- **Normal**, the default, shows up to five items per card.
- **Full** shows as much as a card can carry before it stops being a card: **twenty items** on an
  ordinary list, and **four member lists** on a group one. Counted differently on purpose - a group
  list's rows are not items but other lists, and each one brings five lines with it: the row naming the
  member, three of its items, and a fifth that is either "and N more…" or, when there are exactly four,
  the fourth item itself. Four members is therefore already the twenty lines an ordinary list gets.

Minimal deliberately writes nothing into the per-card folded set, so **leaving it puts back exactly the
cards that were folded before**. And **expanding a card while it is on leaves the view** rather than
unfolding one card the view says is folded - it goes back to whatever the page was before it was folded
away, not to the default, which may be a choice nobody made in months.

### The page of lists

`/tasks` is one card per list, showing enough to recognise it: its badges, how far through it is, and a
few of its rows. A row that only points at another list is followed — the first few items of the list it
points at are drawn under it — so a group list's card says something about the work rather than being a
stack of titles.

A list one row over the preview limit is drawn in full: "and 1 more…" takes exactly the room the row it
stands for would have taken, so hiding that row saves nothing and costs the reader the one thing it was
hiding (`RowsToShow`). Two over, and the summary line starts earning its place.

A card can be folded down to its heading, one row and its buttons. That one row is what is still to be
done, and a row that only points at another list is not work itself — so what it stands for is looked up
on the list it points at, and shown with that list's name beside it. A group's card is nothing but such
rows, and used to fold down to "Nothing left to do." with every one of its members' errands still open.

Chips narrow the page to a status, or to **Shared**, which is about where a list came from rather than
how far along it is; "All" is a chip like the rest, so there is always exactly one answer to what is on
screen. The orders live behind the page's menu rather than in a control taking up the top of every
visit: most and least important first, newest and oldest, A to Z and Z to A, and **the way I arranged
them** — the one order the reader sets by hand. Only under that one do the cards carry a drag handle;
under any other, moving a card by hand would not survive the next redraw. Both the chosen order and the
dragged arrangement are kept on the device (`TaskListArrangement`, localStorage, the same category as the
dashboard's own layout). A list made or shared since the last drag sits after the ones that have been
placed, rather than pushing the arrangement about.

Pinned lists lead every order except that one, which already says where every card goes.

### Finding one entry among every list

Above the chips sit the two questions about what is *on* the lists rather than about the lists
themselves: a search box, and a row of categories.

Every entry can be filed under as many categories as apply — free text, typed on one line and separated
by commas, the way a shelf item's category is written, with every category already in use offered
underneath it (`TaskItem.Categories`, `CategoryText`). One errand is often two subjects at once, so
being made to pick the single truest one is how a category stops being written at all. Every kind of
entry carries them: an appointment is about something the same way an errand is.

The search matches a word anywhere in an entry's own words. The chips are built from what entries are
actually filed under, each with how many carry it. Several can be chosen: **any of them** by default,
because that is usually what picking a second one means, and a checkbox appears once a second is chosen
for the reader who means an entry that is both at once. The two narrow independently — a search and a
category both set means both must hold (`TaskItemFilter`).

What they narrow is which lists are worth showing: a list stays if one entry on it still matches. The
card then previews **what matched** rather than its first few rows, and every row says what it is filed
under with the chosen category marked — a list shown for a match nobody can see reads as a bug. The
checklist says it too, since that is where the work is actually done.

**The phone files an entry the same way**: the same box on its item form, and the same rule behind both
(`CategoryText`, which lives in Orbit.Core precisely so the browser and the phone cannot come to
disagree about what "shopping, Shopping" means).

A save that says nothing about categories leaves them alone. That is what lets a client written before
they existed — an older tab — go on saving lists without unfiling every entry on them, the same rule a
list's description already follows (`UpdateTaskListCommand.EntriesKeepingTheirCategories`).

**On the phone, an entry that stands for other lists is the way into them.** The browser stacks the
whole tree as cards on one page; a phone has room for one list at a time, so the entry carries a chip
per list it stands for and each one opens that list - the same chips an inventory errand uses to reach
its shelf. A group list is nothing but such entries, and its screen used to be a dead end: the work it
gathers was one tap away in the browser and unreachable here. A list this phone does not hold offers no
chip, because a chip that leads nowhere is worse than none.

**The phone files entries the same way and looks for them the same way.** The entry's form carries the
same comma-separated box, each row on a list shows what it is filed under, and the tasks screen carries
the search and the category chips above its status chips - narrowing to the lists that still hold a
match, with the card saying what matched rather than what is next. Its sync sends the categories on
every save rather than leaving them out: "not provided" is what an older client says, and a phone that
said it could never clear a category somebody had removed.

### Two editing levels

A task list can be opened at either of two depths, both reachable from the task list page:

- **Shallow** (`/tasks/{id}`, `TaskListChecklist.razor`) — the whole list as nothing but tickable rows.
  The only thing it can change is whether an item is checked off, which is what lets it show the entire
  list at once. It deliberately takes **no** edit lock: ticking items off is not an editing session, and
  two people doing it at the same time is normal rather than a conflict. It still goes through the same
  `PUT /api/tasks/{id}`, so it does respect someone else's lock — a save during another user's deep edit
  comes back 409 and the checkbox snaps back to what the server holds.
- **Deep** (`/tasks/{id}/edit`, `TaskEditor.razor`) — the full editor: title, grouping, every
  item's text, due date, link, notification settings, adding and removing items. This is the level that
  takes the edit lock described under [Edit locking](#edit-locking).

**The shallow level is what opening a list means.** It owns the plain `/tasks/{id}` route, so every way
into a list lands there whether or not the code that sent you thought about it: the card on the task
list page, a deadline clicked on the calendar, a row on the dashboard, an overdue-task or daily-reminder
notification, a bookmark, a push notification already sitting on somebody's phone. Ticking something off
is what somebody opening a list nearly always came to do; reworking the list itself is a named click
from there. `/tasks/{id}/checklist` is kept as a second route on the same page, so links written before
that was true still work.

Rows that can't be ticked by hand render as disabled checkboxes: items whose completion follows a
linked list (see above), and any list reached through a read-only share.

There is a third depth, reached only from the calendar: **the summary of a single entry**
(`/tasks/{taskListId}/items/{itemId}`, `TaskItemSummary.razor`). An entry that has both a due date and a
place is an appointment rather than something to tick off, so clicking it on the calendar opens that one
entry — its name, the list it is on, when it is, where it is, and a Leaflet map with a pin — instead of
the whole checklist. Two buttons lead back out: **Back to Calendar** and **Show Tasks**, the latter to
the shallow level of the list. A deadline with no place still opens the checklist, since there would be
nothing on such a page the list does not already show. `Calendar.razor`'s `GoToDueTask` makes that
choice, from the `HasPlace` flag `DueTaskDto` carries.

**An entry tied to an event is not drawn twice on the day that event is on.** It *is* that event, so a
deadline row beside it is the same appointment written out a second time, one line under the other. The
grid leaves it off whenever the event it names is on the same day — asked of the occurrence rather than
of the date the event is stored under, so a repeat takes its entry off every day it lands on
(`CalendarGridBuilder.DueTasksOnDate`). It stays on any other day, and it stays when the event is one
this reader cannot see (deleted, or somebody else's), where nothing on the day stands for it.

**The list beside the grid holds both kinds, in one list.** Appointments and deadlines answer the same
question - what is happening in this period - and two lists side by side made the reader merge them by
eye, in a period where they interleave by definition. Each row says which kind it is, and the list is
read in one of three orders from the page's own menu: by when (soonest first, which is what a calendar
is asked for), by type (appointments first, still by when within each), or by name. The order is kept on
the device, like the dashboard's own layout (`CalendarListOrder` in the browser,
`ICalendarListOrderStore` on the phone). **The phone draws the same one list**, with the same three
orders behind the heading's menu - it used to stack "Tasks with a due date" underneath the events, which
is the shape the browser moved away from. The one difference between them is the entry tied to an event:
the browser's list carries both rows, and the phone leaves the deadline off the day its event is on,
since in a single list the two would sit one under the other.

The pin comes from whichever source holds the address. An entry tied to a calendar event takes the
event's stored coordinates directly — the link exists so the address lives in one place. An entry with
only its own typed address has no coordinates, so it is looked up once through
`GeocodingApiClient.FindPlaceAsync` (Nominatim's forward search, the mirror of the reverse lookup the
event editor's map picker uses). An address nobody can find leaves the words on the page and draws no
map, rather than dropping a pin in the wrong country.

### Group lists

Setting `isGroup` marks a list as one that gathers other lists. It changes nothing about completion —
the flag is purely about how the list is presented — but in the shallow checklist view a group list is
rendered together with **every list its own items link to** via `linkedTaskListId`, each as its own
card with its items tickable in place. Ticking an item there saves that member list, not the group,
and the group's own linked row then follows it automatically through the usual completion resolution:
check off the last item on a member list and the group's row for it ticks itself.

Expansion goes all the way down. A member that is itself a group list unfolds too, and so does a member
of that, so the checklist shows the whole tree rather than stopping one level in - work a click away on
the screen meant for ticking through is work that gets missed. Each list is drawn once however many
places link to it, and a list that links back to one of its own ancestors stops at the repeat rather
than unfolding forever (`LinkedTaskListTree`).

### Reading a nested list flat, and keeping how it reads

A tree two levels deep reads as a stack of cards, which is right for seeing how the work is organised
and wrong for working through it. **Show single items** folds the whole tree into one run of items, each
labelled with the list it came from, leaving out the rows that only point at another list. It is offered
only where there is something to flatten.

**Sort** chooses between the list's own order, A to Z, and what is left to do first - which puts the
undone at the top and the done at the bottom, each alphabetically, so a half-finished list reads as what
is left of it. Flattened, A to Z runs across the whole tree -
sorting list by list would look random once the headings are gone. **Save view** keeps both the view and
the order as the way that list opens next time, on that device (`ChecklistViewPreference`, localStorage,
the same category as the dashboard's own layout).

All of it lives behind the screen's three-dot menu, along with Edit and the two inventory actions:
none of it is what somebody came to this screen to do. The menu stays open while the settings are being
tried and closes behind the entries that act.

**The phone offers the same three orders**, behind the list's own three-dot menu, with the one in force
marked - a menu of three with no answer among them leaves the reader guessing what they are looking at.
The menu is offered on every list, not only on the ones priced against a shelf: a single long list is
exactly where reading it off alphabetically helps. Moving an entry up or down disappears from its menu
while the list is read in any other order, since "up" would move it in an arrangement nobody can see.
The order is kept per list on the device (`ChecklistReading`, the phone's preferences, the same category
as the dashboard's pins) and never reaches what is saved: what goes back to the server is always the list
as it was arranged. Flattening a tree is still the browser's alone - see
[the follow-ups](future-plan.md#smaller-identified-follow-ups).

### Arranging a list by hand

In the deep editor each item carries a drag handle, and dropping one where another sits puts it there.
The list is saved in the order its rows are written in - `TaskRepository` numbers them by position - so
what is arranged here is what the checklist reads back under "in list order". Only the handle is
draggable, so a row full of text boxes does not start a drag whenever somebody selects what they typed.

**Beside the handle are a move-up and a move-down button** (`ReorderControls`), which make the same move
one place at a time. They exist because HTML5 dragging is mouse and trackpad only - a handle you can
only drag is a handle only a mouse can use, and a keyboard had no way to arrange anything at all. A move
that would fall off either end greys its button out rather than removing it: a control appearing and
vanishing as a row travels up a list is harder to follow than one that dims at the top. Both ways end in
`RowArrangement`, which matches rows by reference, so a list naming the same thing twice still moves the
row that was asked about rather than the first match.

**Below 680px the whole control is hidden.** Browsers raise no drag events for a finger, so the handle
sat there doing nothing when pressed - and nothing on screen said whether it was broken or the press had
missed. Arranging by hand is a wide-screen affordance; an arrangement made there is still read on a
phone, which is the half that always worked.

### Can this list be done?

A group list can be pointed at a warehouse (`PUT /api/tasks/{id}/warehouse`), and
`GET /api/tasks/{id}/stock-check` then answers what the work costs against it. The counting rule is that
**repetition is quantity**: a tree naming "Makaron świderki" in three recipes needs three
(`StockRequirementCounter`). That is what makes a checklist a bill of materials without asking anybody
to type a number beside every line. A line with a due date in the future is not counted - that work has
not come round, and counting it would raise a restock errand early. `POST /api/tasks/{id}/stock-check/shortfalls`
puts what is short onto the warehouse's standing restock list, where the daily reminder brings it up;
names already waiting are left alone. The panel carries a menu of its own: whether it is in the way at
all, and what order it lists things in - its own order, A to Z, Z to A, or shortfalls first, which is
the only part of the table anybody has to act on. Both are remembered as they are set rather than
waiting for "Save view", since a panel somebody puts away every visit has already been answered about.

**The phone folds the same panel and offers the same four orders**: its heading and its chevron both put
it away, and its own menu holds the orders. The warehouse it is measured against stays at the card's foot
rather than inside the fold - it is how the panel gets linked in the first place, so unreachable while
folded would mean unreachable before it has anything to fold.

"Recalculate against the inventory", on the three-dot menu, re-reads the check and says what it found -
everything covered, or how many things are short. It writes nothing by itself; putting what is short
onto the restock list is the separate press above.

There used to be a third endpoint here, `POST /api/tasks/{id}/stock-check/reconciliation`, which brought
a list and its warehouse back into step in both directions at once - crossing off what the shelf covered
and writing onto the list whatever the shelf held that no list mentioned. Nothing called it any more:
the web now recalculates by reading, and the phone by the same two presses. It was deleted rather than
left reachable, since an endpoint nothing asks for is an endpoint nobody notices going wrong.

Two things are defaulted rather than asked for, and the same way in both directions: the unit is
**pieces**, and **how many times a name is written is how little is too little** - one entry asks for one
of the thing, the same entry twice asks for two. Nothing on a task entry says an amount, so repetition is
what says it, and pieces is what something nobody counted otherwise is counted in.

**An entry on a list that already has a storage describes a product for that shelf.** It shows the
product's fields - how much, how little is too little, the unit, what it is, how long it keeps - and
everything except the name, because the entry's own words are the name. That is the same rule the
generation above follows and the same one the check matches by, so the two cannot come to disagree about
which product an errand is about. Saving the list puts it on the shelf; a shelf already holding
something by that name is what the entry was asking for, so nothing is added twice.

**And says when each batch arrived**, which is the fourth thing a shelf answers and the one the phone
left out. The date comes down with the items and is kept beside them (`LocalWarehouse.ItemArrivals`)
rather than on them: the item shape is what a save sends back, and when something arrived is the
server's answer rather than the phone's. A row this phone has queued and nothing has accepted yet says
nothing about it.

**The phone's shelf opens on the row somebody was sent to.** A warehouse reached from an errand naming
it, or from the search across every shelf, marks that product and scrolls to it - the accent bar and
tint the browser gives the row its `?highlight=` names (`WarehouseItemRow.IsPointedAt`,
`IScreenNavigator.ShowWarehouse`). The row says so in words as well, because a colour is nothing to
somebody who cannot see it. The mark is not lifted to the top and does not outlive the screen: a shelf
read in one order should not rearrange itself around where somebody came from, and narrowing the shelf
and clearing the filter again finds the row still marked.

**The phone describes one the same way, one entry at a time.** An Inventory entry on a list measured
against a storage opens the product's fields with no name box (`ShelfProductFor`,
`WarehouseItemEditor.ForSomethingNotOnTheShelfYet`), says above them which shelf it will go on, and
saving the entry puts it there - a shelf already holding something by that name is what the entry was
asking for, so nothing is added twice (`ShelfCorrection.ApplyAsync`). The same fields correct a product
the entry is already linked to, which is all the phone could do before: the difference is whether the
form is filling in something that has an id yet, and the entry's own words are the name either way. It
happens on the entry's save rather than the list's, because that is the moment this screen has. The
fields arrive with the choice: picking Inventory on an open form shows them there and then, and picking
something else takes them away again - waiting for a save and a reopen made the feature unreachable
without knowing it was there.

Which storage a list is measured against is set in its editor, under **About this list**, for any list
rather than only a group one - an entry describing a product has to be able to say which shelf it goes
on. The picker leaves out storages another list already measures (one list per storage, see
`LinkTaskListToWarehouseCommandHandler`), and "Generate inventory" is refused to a list that already has
one: it would build a second and quietly move the list onto it, leaving the first with nothing pointing
at it.

`POST /api/tasks/{id}/inventory` goes the other way: it builds the shelf the work needs - one entry per
distinct thing, **each carrying how many the job needs as its minimum**, and starting with whatever the
list has already crossed off, since a ticked line is something somebody has fetched - and points the list
at the result. Everything the tree names is included, including lines dated in the
future: the shelf holds what the whole job will need, while the check counts only what is due. Both are
reached from the three-dot menu on the checklist and the deep editor, where "recalculate" is offered
greyed until a warehouse is chosen rather than hidden.

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
minimumQuantity, unit, expiryDate, expiryNotificationChannel }` — `productType` and `category` are free
text (no fixed list), `quantity`/`minimumQuantity` are decimal (not integer) so fractional amounts like
"1.5 kg" are representable, and `minimumQuantity`/`expiryDate` are both optional: not every product
needs a restock threshold or an expiry date. `GET /api/inventory` and `GET /api/inventory/{id}` return
the same shape back plus `id`, `isBelowMinimum` and `hasPendingRestockTask` (both derived, computed
server-side so the client never reimplements the comparison), and `createdAtUtc`/`updatedAtUtc`.

`unit` says what the two amounts are counted in, and unlike the type and the category it **is** a fixed
list (`InventoryUnit`): `Piece`, `Kilogram`, `Milligram`, `Litre`, `Millilitre`, `Pack`. Fixed because
`quantity` and `minimumQuantity` are compared as bare numbers, so both have to mean the same thing —
"szt." typed three ways would leave a shelf that looks stocked and a restock task nobody understands.
An item that says nothing is counted in pieces — every item stocked before units existed became one, and
a save that omits the field is read the same way rather than refused, so a client built before units
existed can still save a warehouse (`InventoryEndpoints.UnitOf`). A unit that is *named* but not
recognised is still refused: that is a typo, not a silence. The client applies the same rule when it
opens a private warehouse sealed before units existed, whose items carry none
(`InventoryUnitOption.For`). The editor writes the short form beside the amount (`kg`, `ml`, `pcs`) and keeps the
full name in each option's tooltip, and a restock errand carries it too - "Restock: Flour (5 kg)"
(`RestockTaskNaming.EntryFor`). Pieces are left off there, since "(5)" of a thing already means five of
them, and an errand raised from a checklist carries no unit at all: repetition is the quantity on a
checklist (`StockRequirementCounter`), so its number counts lines rather than an amount of anything.
The short forms live in Core (`InventoryUnitShortForm`) because both sides need the same list - the
server writes them into an errand, and the client reads them back to say them in the reader's language
(`OrbitWrittenNames`), which only touches a trailing "(number unit)" whose unit is one Orbit itself
wrote.

**A full shelf can be narrowed down.** The warehouse editor offers a product-type and a category filter,
each listing only values something is actually filed under, so neither can be set to a dead end —
neither is offered at all where nothing is filed under it. Beside them is a **search by name**, which
has no such condition: a name is typed rather than picked, and every item has one, so the box is there
for any shelf with anything on it. It matches anywhere in the name and ignores case, because a shelf
holds "Flour, wheat" and "Wholemeal flour" and somebody typing "flour" means both. All three narrow
together (`ItemFilter.Matches`), so a search inside a category is a search inside that category. The
phone offers the same three on its warehouse screen, matching the same way
(`WarehouseItemFilter.Matches`).

This is a view and nothing more: `WarehouseFormModel.ToRequest` reads the whole item list, so a save
made while the shelf is narrowed keeps the rows that were hidden — the editor says so on screen
(`Showing 1 of 2 items. Saving keeps all of them.`) rather than leaving it to be discovered. Adding a
row clears all of it, since a new row is filed under nothing and has no name yet, and would otherwise
be added and hidden in the same click.

**One level up, the inventory page answers "which warehouse is this in".** `/inventory` lists shelves
rather than what is on them, so `Warehouses.razor` carries a search that reads every warehouse and
returns each match with the warehouse holding it; a result opens that warehouse. Two things about it
are deliberate:

- **It searches client-side**, one warehouse at a time, rather than through an endpoint of its own. A
  private warehouse keeps no item rows on the server at all — its stock is sealed and only the owner's
  browser holds the key (see [Private warehouses](#private-warehouses)) — so a server-side search would
  leave those shelves out and report "nowhere". The reader would have no way to tell that apart from
  "not there".
- **A warehouse that cannot be read is named**, not skipped: the summary under the results says which
  ones could not be opened and that nothing in them was searched. The same reasoning — an incomplete
  answer that looks complete is worse than an honest partial one.

The whole account's stock is read once, on the first search, and reused while the page stays open: the
results narrow as the reader types, and fetching every shelf per keystroke would be a request storm.
Opening `/inventory` without searching costs nothing extra.

The phone's inventory screen answers the same question (`InventoryViewModel.ShowMatchingItems`), and has
less to do about it: every warehouse's items came down with the warehouse, so there is nothing to fetch
and nothing to cache. A private warehouse is searched like any other once this device can open it and
private things are unlocked; one it cannot look inside — no key for it, or the device lock still on — is
**counted** in the same summary for the same reason, rather than named, since the name is one of the
things being kept back.

A shelf is read back in the order somebody arranged it (`InventoryItem.Position`, set from the order the
warehouse editor's rows arrive in, where they are dragged into place by their handles), then by name -
which is the whole order for a warehouse nobody has arranged, since everything in one sits at position
zero. A shelf generated from a task list keeps the order the work asks for things rather than the
alphabet.

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

### The restock list

One per warehouse, pinned, named after it - "Restock supplies - Pantry" - and renamed with it, unless
the reader renamed the list themselves. It holds one standing "Update stock levels" reminder that comes
back daily, plus an errand per product that has gone low.

An errand says how many to bring: "Restock: Flour (5)" is the level the shelf is meant to hold when a
product goes low, and how many are short when a task list comes up short. Entries are matched on the
product rather than the whole line, so raising one for eight after one for five does not put a second
copy on the list.

**An errand is a kind, not a sentence.** A restock entry is a `TaskItem` whose `Kind` is `Inventory` and
which carries `LinkedInventoryItemId` - the shelf item it is about. Orbit used to recognise these entries
by reading their own description back, parsing "Restock: " and a product name out of it, which meant
renaming a product broke the connection and two products differing only by punctuation were the same
errand. The description is now what a person reads; the link is what Orbit acts on. Entries written
before the link existed are still matched by the product name in their description, so nothing has to be
migrated by hand.

**Crossing an errand off settles it: the shelf comes up to the level it is meant to hold, and the errand
leaves the list.** Both halves matter. Only ever upwards - somebody who stocked more than the minimum
keeps it, because finishing an errand is not a claim about how much is there beyond it. And it leaves,
because it is no longer something missing: a list that accumulates permanently crossed-off lines is a
list that stops being read. The shelf item's pointer to the entry is cleared at the same time, or the
next time that product went low Orbit would look up an entry that no longer exists.

**The two halves no longer happen in the same breath.** The shelf fills on the save
(`TopUpFinishedAsync`); the errand leaves on a refresh a few minutes later, which the checklist asks
for five minutes after the last tick. Removing it immediately meant a row answered a tap by vanishing,
and a tap on the wrong row could not be undone by untapping it - there was nothing left to untap. The
delay measures from the *last* tick rather than each one, because somebody working down a list ticks
six things in a minute and does not need six refreshes; it is also when they have stopped, and might
look back.

`RestockCompletion.ReconcileAsync` does all of it, and is safe to run twice - which is what lets it run
in two places. It runs on that refresh, and again **when the list is
opened** (`POST /api/tasks/{id}/restocking/reconcile`, asked for by `TaskListChecklist.razor` and by the
phone's `TaskListDetailViewModel`), which is what clears errands that were ticked off before Orbit did
any of this. Both clients ask, and on opening rather than on ticking: a list that settled itself in a
browser and quietly did not on a phone would behave differently depending on which client last looked
at it, with the shelf left un-topped-up in between. An errand whose product has since
been deleted still leaves the list: there is nothing left to bring back.

**What the list asks for is the warehouse's choice.** Two settings at the bottom of the warehouse editor
(`GET`/`PUT /api/warehouses/{id}/restock-list/settings`):

- **What goes on it.** By default the list answers "what is running out": everything on the shelf below
  its own minimum. Ticked, it answers a different question - "what do I need before Thursday" - and holds
  only products some task with a **due date** is waiting on. A product below its minimum that nothing is
  waiting on is left off, and so is one something wants with no date, because without a date there is
  nothing to be early or late for.
- **When it comes round.** Nine in the morning was a constant; it is now the default. Changing it moves
  the standing reminder, since a field that changed nothing would look like a field that does nothing.

Changing either rebuilds the list to match (`RestockListRefresh`), and **Refresh**
(`POST /api/warehouses/{id}/restock-list/refresh`) does the same rebuild against settings that have not
changed - what somebody presses when the world moved rather than the settings. It replaced a button that
used to sit on the checklist's menu, "Recalculate against the inventory", which did half of something
else and did not answer the question somebody has in front of a restock list.

**The phone offers the same one**, in the stock check beside the list's warehouse picker, and stopped
offering the button it replaced. Until it did, the two clients disagreed about what the menu over a
restock list even contained.

**An errand says where it came from and where else it is being asked for.** Under each one the checklist
draws up to two links (`GET /api/tasks/{id}/inventory-references`): the warehouse the product sits in,
and - when a second list carries an errand about the same product - that list. Following either opens the
target with `?highlight={id}`, and the row it names is drawn highlighted, so arriving on a page of fifty
rows lands on the one that was linked rather than at the top. Neither link is stored on the task list:
the shelf item lives in a warehouse, and the other lists are a fact about the whole account, so both are
looked up when the screen asks rather than carried on every read of every list.

Crossing off "Update stock levels" while errands are still open asks whether the whole round is done.
Yes (`POST /api/tasks/{id}/restocking/finished`) finishes the list and brings every item in the warehouse
up to its minimum; the errands then leave it as they would one at a time, the reminder is finished with
them, and `RemindDaily` brings the reminder back tomorrow.

### Editing the shelf from the list

A restock errand carries the whole shelf item it names - amount, minimum, unit, product type, category,
expiry and its notification channel - in the task editor, behind the entry's own toggle. That is what
the kind and the link were for: the row already knows which product it means, so correcting the amount
should not mean opening the warehouse in another tab and finding it again.

Saving the list writes the change back to the warehouse and then rebuilds that warehouse's restock list,
because a corrected amount can settle an errand or raise one. The list is saved first and the shelf
second: if the shelf write fails the list is still saved, and the screen says so.

**Expiry is asked as how long something keeps**, not as the day it stops keeping - "2 weeks", not the
14th (`ExpiresInField` in the browser, the same two boxes on the phone's shelf editor). A date is still
what gets stored, because the expiry reminder needs one and "in two weeks" is not something a background
service can compare against; what changed is only the asking. An item that already has a date says how
long is left, in the coarsest unit that lands on it exactly.

The rule turning one into the other is `ExpiryPeriod` in `Orbit.Core`, shared rather than written twice:
a phone reading "14 days" where a browser reads "2 weeks" would be two clients disagreeing about one
row. Months and years are asked of the calendar rather than divided out of a day count - three months
from the 30th of August is 92 days, which divides by neither 30 nor 7, so a length set in months used to
read back as "92 days".

### What an entry's form offers

The row itself reports rather than sets: what the entry says, whether it is done, when it is due, and -
for anything that is not an ordinary checklist entry - what kind it is. Everything editable waits behind
the toggle, because a list of thirty items was thirty rows of boxes.

Behind it, four fields every entry has - **type, due date, due time, move to list** - and then fields
that depend on the type:

| Kind | What it also carries |
| --- | --- |
| Checklist | Link to list, overdue notification, remind daily and its channel and hour |
| Inventory | The shelf item itself - see above |
| Calendar | The event's own form - see below |

**A Calendar entry is the appointment, not a pointer at one.** It carries the event's own fields -
description, start and end, all-day, repeats, a reminder, a colour - and **saving the list makes the
event**; nothing has to be linked by hand. The events are written before the list itself, so an entry
carries its event's id when the list is saved rather than in a second write, and a failure there stops
the save instead of leaving entries pointing at appointments that were never made. Opening a list reads
each linked event back into its entry, so saving again keeps them in step rather than overwriting them
with an empty form.

**An entry that already has an event cannot quietly stop being one.** Changing its type is refused, with
the entry named. Orbit cannot settle that on its own: deleting the event would throw away something that
may since have been edited in the calendar, and keeping it leaves an appointment nothing points at. So
the save stops and hands the choice back - **Detach from the event** stops the entry being that event
without destroying it, and the type is free to change afterwards.

The place named on a calendar entry stays on the entry. The calendar's own location is coordinates first
(`EventLocationRequest`) and the map overlay deliberately hands back an address rather than a pin, so
there is nothing to build one from here; the screen says so rather than dropping it quietly.

**A daily reminder needs an hour.** Saving refuses without one rather than sending it at midnight - an
hour nobody chose is worse than being asked for one. An entry loaded at exactly 00:00 reads as one with
no hour set: the wire carries a plain `TimeOnly` and cannot say "none". **Both clients read it that
way.** The phone needed one thing the browser did not: its picker cannot be empty, so an hour shown
from the start would be one somebody accepts by not touching it - which records nothing and leaves the
refusal standing. There the picker appears only once an hour exists, and a button puts one there.

### Naming a place in your own words

The address box beside a map pin is the reader's to write. It always was, and behaved as though it was
not: every confirmed pin overwrote whatever was in it with a street address, so correcting
"ul. Krucza 16/22" to "the back entrance" lasted until the next click on the map.

The pin always moves; the name only follows it when it is still the one the map gave. **The coordinates
are untouched by anything typed there** - they are what the Google Maps link and the directions are built
from, and the name is only what the place is called. The event editor offers the map's own address back
for somebody who renamed a place and then wanted the street after all.

**The phone had already landed here**, by a different route. It has no map pin to move: a place there is
the phone's own position, taken on purpose, and its calendar screen has always filled the name only when
the box was empty. Its task entries carry a name and no point at all, so the map's answer has nowhere
else to go and must land in the name - which is why the picker asks before answering rather than writing
on every tap. What was missing was only saying so, which the screen now does.

### Which build this is

The footer says `ver:0.1.17+gitHash:51536f3`, and pressing it grows the rest of the hash - the short form
is what anybody reads, the whole one is what a `git checkout` takes, and asking for it should not mean
going somewhere else. The phone's **About** row says the same thing and behaves the same way when tapped.

**The hash goes to the accounts holding `Debug`, and to nobody else.** Every build reads its own commit
off itself whatever configuration it was made in - a deployed footer that cannot answer "which code is
this" is a footer with no reason to carry a hash at all - and the *showing* is what is gated. Somebody
without the permission sees `ver:0.1.17` and nothing to press: the number is what a bug report needs and
what the update gate compares, while which commit it was cut from is detail about Orbit's own insides.
All three ends apply the one rule: the server leaves the hash out of its answer rather than sending it
to be hidden (`ConfigEndpoints`), the browser's footer drops it with `OrbitVersion.WithoutTheCommit()`,
and the phone's About row does the same - it was the half still showing it to everybody.

**Both versions are shown, the client's and the server's**, because they can differ:

- The pipeline deploys `orbit-api` and `orbit-web` from one commit but **rolls each back on its own**, so
  an API that fails its health check leaves the web client new and the server old.
- A browser holding a cached Blazor client is the same drift by another route.
- The phone is released separately and updated whenever its owner chooses - which is the whole reason the
  version gate exists.

So the footer and the About row carry a second entry, `api ver:0.1.17+gitHash:…`, read from
`GET /api/config/version`. A released **server** sends no hash at all rather than sending one the client
then hides: what is not sent cannot be read off the wire. When the server cannot be reached the entry is
simply absent - an offline footer knows nothing about it and should not guess.

**The number reads `version.patch.build`, and the three parts answer three different questions.**

- **version** - the move to production. Orbit runs on a test environment today: publicly reachable, but a
  test one. The day it moves to its own address this becomes `1`, and nothing else raises it.
- **patch** - a milestone, raised by hand when somebody decides one has been reached. Deliberately not
  derivable from anything: "far enough to matter" is a judgement, not a count.
- **build** - nobody maintains this one. It is the count of distinct days on which a commit landed **on
  main** touching that project (`ci/compute-version.sh`), since either number above was last changed. A
  day with five commits counts once, a day whose commits went nowhere near the project does not count at
  all, and the same commit always numbers itself the same.

The first two live in `version.props` - a file that holds nothing else, and that is the point. The build
count restarts at whatever commit last changed that file, so the file has to mean "a version bump" and
nothing more. It used to live in `Directory.Build.props` alongside the warnings settings, and turning
warnings into errors silently restarted the count, which is not a version bump by any reading.

Counting against **main** rather than against whatever is checked out is what makes the number a claim
about what has shipped. Commits on a branch have not shipped, so a build from a branch reports the number
main would - which is what lets a local build be compared against a released one at all.

It is counted **per project**, which is the point of counting it at all: a day that changed the phone and
not the web client raises one and not the other. Each client counts the shared projects it compiles as
its own, since a change to `Orbit.Core` is a change to every app built from it.

The number is stamped into the assembly at build time as its informational version and read back at
runtime (`OrbitVersion`), each client reading **its own** assembly. A build nobody stamped - a local
`dotnet run`, a local `docker compose build` - says `0.0.0-dev` rather than inventing a number that looks
real, because this is the string somebody pastes into a bug report.

**An unstamped build still knows which commit it is.** The SDK writes its own `1.0.0` default next to
the real `HEAD`, so a local build reads as `0.0.0-dev` and keeps the hash. That matters because the
hash is the whole point of the line while debugging: nobody compares `0.0.0-dev` against anything,
they are asking which code is running. Discarding the commit along with the number left a Debug build
showing no hash and a footer that could not be opened - the one case the hash exists for.

The Android release carries it twice over: `-p:InformationalVersion` for the About row, and the file name
itself - **`orbit-android-0_1_32v.apk`**, so a download says which build it is without anybody opening
it. It is also published under the fixed `orbit-android.apk` the download page links to, so that one
address keeps working without the page being edited every release.

### Names you have already used

The four fields where the same thing gets typed twenty ways - a task list's title, a task item, a
warehouse's name, a product - offer what the reader already has as they type
(`GET /api/suggestions/names?kind=…`, `NameSuggestions.razor`). Picking one fills the field.

**The phone offers the same thing under two of the four** (`Orbit.Mobile`'s `NameSuggestions`, drawn by
`NameSuggestionChips`): a product's name and an errand's description, which are the two the feature
exists for. It offers them under the box a new one is typed into as well as in the editor, because on a
phone that box is where names are actually written - the editor is mostly for changing one that exists.
A list's title and a warehouse's name are not offered there yet. When what is
being typed is close enough to an existing name to be the same thing spelled differently, the control
says so out loud - "You already have «Mleko 2%»" - because the moment a duplicate is about to be created
is the only cheap moment to avoid it.

This is a **similarity search over the reader's own rows**, not a language model: PostgreSQL's `pg_trgm`
answers it in milliseconds for nothing, and answers it better, since a model does not know what is in
this warehouse and would invent plausible names instead of offering real ones. Four GIN indexes make it
cheap enough to run while somebody types. Nothing is asked for under two characters, and nothing is asked
for when a screen merely opens holding saved values - suggestions are about what is being typed. Private
notes and private task lists are left out entirely: their names are sealed client-side, so there is
nothing readable to suggest from. See [Orbit Assistant — Plan](ai-assistant-plan.md), where this is
step 1 and the reasoning behind it is written down.

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

#### The restock list

One per warehouse, pinned, named after it - "Restock supplies - Pantry" - and renamed with it, unless
the reader renamed the list themselves. It holds one standing "Update stock levels" reminder that comes
back daily, plus an errand per product that has gone low.

An errand says how many to bring: "Restock: Flour (5)" is the level the shelf is meant to hold when a
product goes low, and how many are short when a task list comes up short. Entries are matched on the
product rather than the whole line, so raising one for eight after one for five does not put a second
copy on the list.

Crossing an errand off brings its item up to that level - saying it once, on the list, rather than twice.
Only ever upwards: somebody who stocked more than the minimum keeps it, because finishing an errand is
not a claim about how much is there beyond it.

Crossing off "Update stock levels" while errands are still open asks whether the whole round is done.
Yes (`POST /api/tasks/{id}/restocking/finished`) finishes the list and brings every item in the warehouse
up to its minimum; the reminder is finished with it, and `RemindDaily` brings it back tomorrow.

## Calendar event reminders

Two independent notification emails can go to the event's owner and to every guest who has accepted a
share of it (see `ResolveRecipientsAsync`) — the `guests` list on the event itself is the editor's
record of who was invited, while the accepted share is what makes someone a recipient. Each is gated by
its own checkbox in the event editor:

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
Message delivery is polling-based (`GET /api/chat/messages/{otherUserId}?sinceUtc=` once a second while
a conversation is open), not push/real-time (no SignalR or WebSockets), and a message sent to a user who
has never opened `/chat` (and so has no `PublicKeyBase64` yet) can't be encrypted — `Chat.razor` shows an
explanatory message and disables sending in that case instead of silently failing.

Three things about that poll are worth knowing:

- **A group conversation polls too** (`GroupConversation`). It did not, which made a group a dead
  screen: your own messages appeared because you had just sent them, and everybody else's arrived only
  if you left and came back. It redraws only when the conversation actually changed, so a quiet thread
  is not re-rendered once a second for nothing.
- **Nothing is polled while the tab is behind others** (`PageVisibility`, asking the same
  `presence.js` the heartbeat asks). Nobody is reading there. A tab that cannot be asked counts as
  visible: a poll that stops because the question failed is a chat that silently goes quiet, and quiet
  is indistinguishable from nobody writing.
- **The conversation list is read every tenth tick, not every one.** A message wants the second it
  takes to arrive; who is on the list changes on the scale of days, and asking for the whole roster and
  every group once a second was two thirds of the loop's traffic spent on an unchanged answer. Ten
  seconds matches what `MainLayout` already refreshes its notification feed on.

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
top, sidebar labels and the rows below the nav divider hidden) rather than staying a narrow vertical
rail — 680px is also the calendar's page (`app.css`) and chat's own drawer breakpoint, kept consistent
across the app rather than each surface picking its own.

**The rows below the divider move into the avatar menu rather than disappearing.** "Get the app"
(`/download`) and "Options" are not sections to work in — they are somewhere to go once — so they sit
under the divider on a wide screen, and on a narrow one the icon bar has no room for them. Each carries
`.nav-item-overflows` in the rail and appears again as `.overflowed-nav-item` in the avatar menu, and
each class hides where the other shows: exactly one copy is ever on screen. The menu reads **Status,
Notifications, Get the app, Options, Log out** — what somebody uses most, first — which on a wide screen
is the three that are always there, with the two rail rows folded out of it.

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
Clicking any item navigates straight to it (`/notes/{id}`, `/tasks/{id}`, or `/calendar/{id}`) — the
dashboard has no editing of its own. For a task list that is its checklist, not its settings: see
[Two editing levels](#two-editing-levels) for why the shallow level is what opening a list means.

### Deciding what the page shows

Not everybody's dashboard is everybody's. The menu in the page's top right lists every part of it - the
day's strip and each card - with a tick beside the ones being shown, so anything unwanted can be put away
and brought back. A page with everything put away says so, rather than looking like one that failed to
load.

Each card that has something to filter by carries its own menu in its top right: everything, what is
pinned, or one priority. The count beside a card's title counts what the card is showing rather than
what it holds, so a filtered card cannot look like one that lost something. A calendar event offers no
"pinned" - it has a priority but nothing to pin it to.

Both live on the device (`DashboardCardPreferences`, localStorage), like the pins beside them: they
describe one page for one reader and say nothing about what the cards hold. What is stored is what is
*hidden*, so a card added to the dashboard later shows up for everybody rather than staying invisible to
whoever saved a layout before it existed.

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

## Putting a conversation away

`PUT /api/chat/conversations/{otherUserId}/archived` and `PUT /api/chat/groups/{groupId}/archived`,
both taking `{ "isArchived": true }`.

**Archiving is one-sided, and that is the whole point.** Nothing is deleted, nobody leaves, and the
other party is told nothing - their list has its own row and its own answer. A group's flag lives on
the *membership* rather than the group, so an admin tidying their own list cannot take the group off
anybody else's, and archiving needs no rank at all: deciding to stop looking at something is not a
moderation act.

A member who archives a group is still in it and still receives what is posted. Leaving is the other
thing, and this is not it.

Both answer 404 when the caller has no such conversation - which for a group covers both "no such
group" and "you are not in it", because from the caller's side those are the same fact.

The change is announced to that account's **other devices** only (see
[Live updates](#live-updates)): a conversation put away on a phone should not still be in the way on
the laptop, and nobody else's screen changed.

**The phone offers all three as of 2026-09-01.** Each list - people, groups - carries its own switch to
what has been put away, shown only once something is there, and each row's menu offers putting it away,
bringing it back, and the thing that is not reversible: emptying a conversation, or leaving a group,
each behind a question first. Emptying also drops what the phone had cached, because a pull only ever
adds and the server has nothing left to send that would take those words away.

**And the other half of arranging a list: pinning.** A conversation kept at the top stays there whatever
the last message said, on the device that pinned it and nowhere else - the browser has kept its own
since it had a list to keep, and the phone had no way to. People and groups share one set of pins
(`ConversationPins`, `IConversationPinStore`): an id means one conversation whichever kind it is. Pinned
rows are lifted rather than taken out, so unpinning finds the row back where the sort would have put it,
and the archive is left in the order it has - keeping something at the top of the day is the opposite of
what putting it away said. The row says it is pinned in a word as well as a mark, since a mark is
nothing to somebody who cannot see it.

## Saying nothing about a field

Descriptions on a task list and a warehouse, and a shelf item's regular-check flag, are all optional on
the way in: **null means "not provided" and keeps what is stored; an empty string, or false, means the
caller really said so.**

This exists because a save replaces what it touches wholesale and the two clients learn about a field at
different times - the browser deploys with the server, the phone whenever somebody installs it. Without
the distinction, an older phone returning a row it does not fully understand would erase a description
written on the web, which is the shape of bug this codebase has already had three times (a calendar
entry's place, the phone's event place, and every entry's kind when a checklist box was ticked).

The cost is one distinction to keep in mind. What it buys is that the two clients never have to ship in
lockstep for a new field to be safe.

On the way **out** these fields are always sent, so a reader never has to guess.

## Live updates

The web client holds one WebSocket open to the API and is told when something changed, instead of
asking. It replaced polling that ran **four requests a second per open chat** (a message read, a
conversation's approval state, a read receipt, and every tenth tick the whole contact roster), a
notification poll every ten seconds, and a presence heartbeat every twenty.

**What travels over it is an announcement, never the thing that changed.** The server says "your chat
changed" and the client answers with the same API call its timer used to make. This is the design, not
a shortcut:

- Chat messages are end-to-end encrypted. A connection that carried content would need a plaintext the
  server does not have, so the announcement carries none and the server stays exactly as ignorant as it
  was.
- Nothing new is readable. The client fetches over the endpoints it already used, behind the guards
  those already have — the hub adds no read path of its own.
- A dropped announcement costs a delay, never a message. The answer to every announcement is "read
  again from the cursor you already hold", so hearing it late, twice, or not at all is harmless.

**The polls are still there, and deliberately so.** They slow down while the connection is up (chat 1s →
20s, notifications 10s → 60s) and snap back the moment it drops. Announcements are best-effort, and a few
things genuinely have no moment to announce — most notably somebody going *away*, which happens by time
passing with nothing calling anything (see `UserPresence.StatusAt`). A chat that silently stopped
updating because one announcement was lost would be a far worse bug than one that takes twenty seconds
in a rare case.

| | Announced | Still only found by the slow poll |
|---|---|---|
| Chat | a message sent or edited or deleted, in a conversation or a group; a read receipt; a conversation approved; a group made, joined, left, or a role changed in one; history shared with a new member | nothing that changes what is on screen |
| Notifications | anything recorded in the feed, from any trigger; and anything read or cleared, so this account's other devices hear it | a change to the notification settings themselves |
| Presence | somebody arriving, somebody choosing "do not disturb" | somebody ageing to away or offline |

Who each one goes to is the part worth stating, because getting it wrong is invisible: an announcement
sent to the wrong account raises nothing anywhere, and the client that needed it simply falls back to
its slow poll. Two are easy to get backwards. A **read receipt** is the *other* party's news - the
reader already knows they read it. A **removal from a group** goes to the person removed as well as to
the people left, because otherwise the group stays in their list and they will write to it.

Presence keeps its old rule exactly: the beat stops while the tab is in the background, because a tab
left open behind thirty others is not somebody there to answer. The connection staying open does **not**
on its own keep an account looking available — the client reports being at the keyboard, and declining to
report is what lets the account age.

**The phone holds the same connection, and only while it is in front.** Started and stopped with the
window, the way its presence heartbeat already was: a socket held open behind a locked screen is one
Android drops in Doze anyway, and what it would have carried is exactly what push already delivers. So
the connection speeds up the app somebody is looking at, and push covers the app they are not. Its chat
polls slow from 5s and 10s to 30s while it is up and snap back when it drops; the unread badge and the
notification feed hear about the feed changing instead of waiting for the next screen to be opened; and
the presence heartbeat goes over the connection when there is one — a frame instead of a handshake and a
round trip every twenty seconds — falling back to the request when there is not.

The phone does not listen for `PresenceChanged`: it shows nobody else's presence yet, so there would be
nothing to redraw.

### How it is put together

| Piece | Where | What it is for |
|---|---|---|
| `ILiveUpdatePublisher` | `Orbit.Core.LiveUpdates` | What the domain calls. Knows nothing about WebSockets — the same separation `IPushNotificationSender` gives push. |
| `SilentLiveUpdatePublisher` | `Orbit.Core.LiveUpdates` | The default, so every call site can announce unconditionally. |
| `LiveUpdatesHub` | `Orbit.Api.LiveUpdates` | The connection. Almost empty: the only thing coming *up* it is presence. |
| `SignalRLiveUpdatePublisher` | `Orbit.Api.LiveUpdates` | Delivers announcements, swallowing its own failures — a sent message must not fail because a socket was reconnecting. |
| `SubjectClaimUserIdProvider` | `Orbit.Api.LiveUpdates` | Which account a connection belongs to. |
| `LiveUpdatesConnection` | `Orbit.Web.Services` | One connection for the whole app; pages subscribe while they are on screen. |

**Two things here fail silently, and both have tests of their own.**

The first is the claim a connection is keyed on. SignalR looks for `ClaimTypes.NameIdentifier`; Orbit's
tokens carry the account in `sub` and keep it there (`MapInboundClaims = false`). Read the wrong one and
every announcement is addressed to nobody — no exception, no log, just an app that quietly polls exactly
as it did before.

The second is nginx. A WebSocket handshake is an HTTP/1.1 `Upgrade`, and nginx proxies as HTTP/1.0
unless told otherwise, which strips it. SignalR survives that by falling back to long polling, so
leaving `proxy_http_version 1.1` out does not break anything visible: the live connection simply never
happens. Both configs carry it, and both go through the same `/api/` location the phone uses.

### The access token in the URL

A browser cannot put an `Authorization` header on a WebSocket handshake — the WebSocket API has no way
to set one — so the token goes in the query string, which is what SignalR does and what `OnMessageReceived`
reads. It is accepted **only** on the hub's own path; nowhere else in the API takes a credential that
way.

A token in a URL is a token in access logs, so nginx stops logging the query string for that one path
(`map $uri $orbit_logged_request`). Everything else still logs what it asked for.

### Before this scales past one replica

`orbit-api` runs at `max-replicas 1`, and the hub's delivery depends on it. SignalR keeps its connection
registry in the process's own memory, so with two replicas an announcement raised on one reaches only the
clients connected to that one — the rest hear nothing and fall back to their slow poll. Nothing errors.

Raising `max-replicas` therefore needs a backplane (Azure SignalR Service, or Redis) added at the same
time. There is also a cost consequence worth knowing: `orbit-web` is set to scale to zero when idle, and
a client holding a connection open is not idle, so it will stop scaling to zero once this is in use.

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
`AllowMobileBanner` is on, it shows the newest entry as a toast fixed to the top of the viewport. The
phone reads the same three settings for its own banner (see
[the foreground banner](orbit-maui-plan.md)) and, as of 2026-09-01, offers them: its Settings screen
edits `AllowMobileBanner`, both banner timings and `RetentionDays` alongside the channel switches it
already had. `ShowExceptionDetails` stays browser-only, since it governs what Orbit.Web prints on the
page and nothing on the phone reads it.

**A person with a message waiting is marked wherever that person appears** — the chat page's own
conversation list, the contact list, and the dashboard's "Recent chats" card — all through one
`UnreadBadge` on the avatar, with the row's name in bold behind it. That count comes from the
conversation itself (`ContactDto.UnreadCount`), not from the notification panel, so clearing the panel
does not clear it: tidying is not reading. It was on the chat page alone, which made every other screen
say "nobody waiting" while one of them said otherwise — and the dashboard, the first thing a visit looks
at, was among the silent ones. Nothing at all is drawn when nothing is waiting: an empty badge is a
mark, and a mark means something.

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

### The bell, and what it counts

Notifications are their own button in the bar on both clients as of 2026-09-01, left of the avatar and
carrying the unread count - 0 draws nothing, 1 to 9 draw themselves, anything above draws "9+". It was
a badge on the avatar before, and a menu entry behind it: **a count on a face says "you", not "unread"**,
and reaching the panel meant opening a menu first, which is two steps for the one thing people check
most. What is left in the avatar menu are the places somebody goes once - status, the language, the
app's own settings, signing out.

### A link opened on a phone

A public link is an ordinary web address (`https://<host>/s/<token>`), and on a phone with Orbit
installed Android hands it to the app instead of a browser: the app declares an intent filter for that
path on the deployment's own host, and the same screen the browser would have shown appears inside
Orbit - what was shared, who shared it, and one button to keep a read-only copy. It is the same
destination pipeline a tapped notification travels (`NotificationDestination`), so there is one way into
the app from outside it rather than two.

The host is fixed when the app is built, from the deployment address it is already given
(`OrbitShareLinkHost`, defaulting to the host of `OrbitApiBaseAddress`) - an intent filter is an
attribute and takes compile-time constants, and a filter with no host would offer Orbit for every link
on the phone. A build told no address gets a name that can never resolve.

**Two halves are needed for a link to route on its own.** The app declares the filter with
`autoVerify`; the deployment has to serve `https://<host>/.well-known/assetlinks.json` naming the app's
package and the certificate the installed build was signed with. `orbit-web` writes that file at
startup from `ANDROID_APP_SHA256` (see `write-android-app-links.sh`) and writes nothing when it is
unset, which is the ordinary state for a local stack. Without it, Android 12 and later open the link in
a browser and the reader has to allow the app by hand under Settings > Apps > Orbit > Open by default -
the app half is complete either way, and was checked on an emulator with the domain approved.

The fingerprint to set it to is the release keystore's, printed by the command the keystore's own notes
already document for the Maps key, reading `SHA256` where that one reads `SHA1`. It is not a secret:
Android hands it to every device that installs the app.

Following a link, like following a notification, waits for an account: the app holds the destination
and opens it once somebody is signed in, rather than showing a stranger's shared item over a signed-out
app. Signing in now goes on to whatever was waiting instead of always landing on the dashboard.

## The home screen widget (Android)

A 3 × 2 widget showing the day and the few things still ahead in it: today's appointments that have
not finished, and what falls due today and is not done, in the order they happen. Four lines fit;
anything past that is counted rather than dropped ("2 more"). Tapping a line opens Orbit on it - an
appointment on the calendar, an errand on the list it is ticked off on - through the same paths a
tapped notification travels (`NotificationDestination`), so there is one way into the app from
outside it rather than two.

What it shows is `TodayAtAGlance` (`Orbit.Mobile.Widgets`), which is where the rules live and is
covered by tests; `OrbitTodayWidget` (`Orbit.Maui/Platforms/Android`) is the drawing. Two rules are
about the home screen specifically rather than copied from any screen:

- **Nothing private is ever named.** A widget is on show to whoever is holding the phone, and on most
  Androids to whoever can see the lock screen, with no unlocking in between. The gate that guards
  private items inside the app (see [Private notes and task lists](#private-notes-and-task-lists)) has
  no equivalent out there, so private lists are left off rather than hidden behind it.
- **A phone nobody is signed in on shows no day at all**, only "Open Orbit to see your day". Signing
  out clears the session but leaves the local database, so a widget reading it would go on showing the
  previous account's day to the next person holding the phone.

None of the app is running when the widget is drawn: the launcher asks for it in a broadcast that can
arrive with no MAUI application, no service container and no session in memory. It reads the local
database itself, through the secure store and the database file - the two things that outlive the app
being closed - rather than a snapshot the app left behind, because "today" has a different answer every
midnight and a snapshot taken at nine in the evening is wrong by morning. Android redraws it every half
hour, which is what makes it right after midnight without the app being opened at all, and Orbit asks
for a redraw itself whenever it is put down, which is the update carrying whatever just changed.

It follows the system's light or dark mode rather than the theme chosen inside Orbit: a widget is drawn
in the launcher's process, and the app's own choice is not something it can see.

There is no iOS counterpart yet - see [Orbit.Maui — Plan](orbit-maui-plan.md), phase 8.
