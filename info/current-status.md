# Current Status

Orbit is a working prototype. Accounts (including Google sign-in and full self-service account
management), notes, tasks, calendar, an inventory planner, and end-to-end-encrypted chat — one-to-one
and group — are implemented end to end. Locations can now be recorded, seen on a map, and shared with a
contact under the same encryption chat uses. There are two clients: the Blazor WebAssembly web client,
and a .NET MAUI mobile client whose Android head runs on a real device. A real two-way Google Calendar
sync, as opposed to the hand-off links Orbit builds today, is what remains unbuilt of the product's
stated scope.

## Implemented vs. planned

| Area | Status | Where to read more |
| --- | --- | --- |
| Accounts (register / login / refresh / logout) | Implemented | [Functionality — Authentication](functionality.md#authentication) |
| Account management (email verification, password reset and change, account deletion) | Implemented | [Functionality — Authentication](functionality.md#authentication) |
| Google sign-in and account linking | Implemented | [Functionality — Authentication](functionality.md#authentication) |
| Notes (sharing, private notes, checklist lines) | Implemented | [Functionality — Notes](functionality.md#notes) |
| Tasks (sharing, private lists, group lists, list links, daily and overdue reminders) | Implemented | [Functionality — Tasks](functionality.md#tasks) |
| Calendar (sharing, recurrence, reminders, map location picker) | Implemented | [Functionality — Calendar](functionality.md#calendar) |
| Inventory planner (warehouses, sharing, restock tasks, expiry warnings) | Implemented | [Functionality — Inventory](functionality.md#inventory) |
| End-to-end-encrypted 1:1 chat (including editing a sent message) | Implemented | [Functionality — Contacts and encrypted chat](functionality.md#contacts-and-encrypted-chat) |
| Group chats, with admin and member roles | Implemented | [Functionality — Group chats](functionality.md#group-chats) |
| Giving a new group member the history, re-encrypted for them | Implemented | [Functionality](functionality.md#letting-a-new-member-read-the-history) |
| Permissions (Contacts, Chat, Sharing, Location) and their unlock codes | Implemented | [Functionality — Permissions](functionality.md#permissions) |
| Counting a task list's work against a warehouse, and generating one from it | Implemented | [Functionality — Tasks](functionality.md#can-this-list-be-done) |
| Priorities on notes, task lists and events, and the dashboard filters that read them | Implemented | [Functionality — Priorities](functionality.md#priorities) |
| Push notifications | Implemented | [Functionality — Push notifications](functionality.md#push-notifications) |
| In-app notification feed, badge, and banner | Implemented | [Functionality — In-app notifications](functionality.md#in-app-notifications) |
| Blazor WebAssembly web client | Implemented | [Architecture](architecture.md#orbitweb) |
| Live updates over a WebSocket (chat, notifications, presence) | Implemented on both clients; the phone holds the connection only while it is in front, and push covers the rest | [Functionality — Live updates](functionality.md#live-updates) |
| Recording your own location and seeing it on a map | Implemented | [Functionality](functionality.md#the-map-and-the-location-behind-it) |
| Sharing a location with another user | Implemented | [Functionality](functionality.md#sharing-a-position-with-a-contact) |
| Google Calendar and Maps links (verified/Google accounts) | Implemented | [Functionality](functionality.md#handing-something-off-to-google) |
| Two-way Google Calendar sync | Not started | [Future Plan](future-plan.md#what-real-google-calendar-sync-would-take) |
| Mobile client (`Orbit.Maui`, iOS + Android) | Implemented — Android verified on a device, iOS unverified | [Orbit.Maui — Plan](orbit-maui-plan.md) |
| Push delivery to a phone | Implemented on Android, in the tray and in front of you; not on iOS — see below | [Orbit.Maui — Plan](orbit-maui-plan.md#42-push-notifications-web-push-apns-and-fcm-are-three-different-things) |
| Home screen widget | Implemented on Android and driven on a device; nothing on iOS | [Functionality](functionality.md#the-home-screen-widget-android) |
| Google Contacts sync | Not started | [Future Plan](future-plan.md#planned-features) |
| Name suggestions and duplicate warnings while typing | Implemented | [Functionality](functionality.md#names-you-have-already-used) |
| Choosing what a warehouse's restock list asks for, and when | Implemented | [Functionality](functionality.md#the-restock-list) |
| Editing a shelf item from the restock list | Implemented | [Functionality](functionality.md#editing-the-shelf-from-the-list) |
| A task entry that is a calendar appointment, and makes one | Implemented | [Functionality](functionality.md#what-an-entrys-form-offers) |
| AI assistant for inventories and task lists | Step 1 built, the model half not started | [Orbit Assistant — Plan](ai-assistant-plan.md) |

`Orbit.GoogleIntegration` (`src/Server`) is no longer the empty placeholder it was: it holds the
Google ID-token verification behind Google sign-in, and nothing else - the calendar and maps features
are links the browser builds, needing no API. The Calendar/Contacts sync it was originally
reserved for still hasn't been started — see [Future Plan](future-plan.md#planned-features).

## The signed-in experience today (web)

Registering or logging in happens on `/register`/`/login`, either with an email address and password
or with Google. Everything else is behind authentication. Signing in lands on the dashboard (`/`),
which summarizes notes, task lists, calendar events, and contacts, and where each row opens the thing
it names.

Each area also has its own page: `/notes`, `/tasks`, `/calendar`, `/inventory` (warehouses and their
contents), `/contacts` (user search and existing conversations), `/map` (the one location you've
recorded for yourself), and `/options`. Notes, task lists, calendar events, and warehouses can each be
shared with another user through an offer/accept flow carried over encrypted chat, or marked private
so they can't be shared at all.

`/options` covers the account itself — display name, username, email address and its verification,
password, connecting or disconnecting Google, and deleting the account outright — alongside the theme
picker and notification preferences.

Notifications arrive three ways: browser push (even while Orbit is closed), an in-app feed with an
unread badge, and a short banner while the app is open. All three are configurable per account, and
the push half needs the user to approve browser notifications first — see
[Functionality — Push notifications](functionality.md#push-notifications) and
[In-app notifications](functionality.md#in-app-notifications).

## The mobile client

`src/Clients/` holds two projects rather than one, and the split matters when reading the tests:

- **`Orbit.Mobile`** (`net10.0`) — every screen's view model, the local store, the sync spine, the
  crypto, the outbox. In `Orbit.sln`, so `dotnet test` covers it.
- **`Orbit.Maui`** (`net10.0-android`, `net10.0-ios`) — the two app heads: XAML pages, platform
  services, resources. Deliberately *not* in `Orbit.sln`, because CI runs on `ubuntu-latest`, which
  can build neither head. Nothing left in here can be reached by a test, which is why so little is.

Phases 0-6 of the [plan](orbit-maui-plan.md#10-phasing) are built: the version gate, offline SQLite
with an outbox and delta pull, end-to-end-encrypted chat against the same test vectors the browser
uses, tasks, calendar, inventory, group chat, and location sharing. Private notes, task lists and
warehouses are read and written here too, sealed under the account's own key and kept sealed in the
local store as well — see [Functionality](functionality.md#private-notes-and-task-lists). A restock
list settles its finished errands when the phone opens it, as a browser does, and an admin can hand a
group's past to somebody they have just added. The names already in the account are offered under all
four fields the browser offers them under — a product's name, an errand's description, a list's title
and a warehouse's name — each field with its own set, since a title and the box below it are on screen
together.

Two more things the browser had and the phone did not: a warehouse's **restock list settings** - the rule
deciding what that list asks for, and the hour its reminder comes round - and the calendar's **day view**,
so "just today" no longer means finding today in a month grid. The names Orbit writes for itself are also
read in the reader's language here now, as they always were in the browser: a restock list on a Polish
screen said "Restock supplies - Kuchnia" until this.

Five more the browser had and the phone did not. **Who somebody is**: the same card the browser opens
at `/contacts/{id}` - name, login, address, where they are, when they last wrote, and whether they have
set up encryption yet - reached from the contact row's menu, from beside somebody just found by search,
and from the conversation's own header, and answered from what the phone already holds so it reads with
no connection. **One calendar list**: appointments and deadlines together rather than stacked in two,
each saying which kind it is, read by when, by type or by name. **What an entry is about**: the same
comma-separated categories box, shown on every row, with a search across the entries on every list and
a chip per category above the status chips. **A way into what a group gathers**: an entry standing for
other lists carries a chip per list, and each one opens it - the browser stacks the whole tree, and a
phone has room for one list at a time. And **how one checklist is read**: its three orders, the stock
panel folded away or open, and that panel's own four orders, each kept for that list on that device.

The browser's rebuilt item controls are all here now: the expiry asked as a length, the daily
reminder's missing hour, and the Inventory kind, which round-trips rather than silently rewriting to
Checklist and cutting a restock errand loose from its product. So are the two entries that carry
something bigger than themselves. A **Calendar** entry is the appointment rather than a pointer at one:
it carries the event's own form, and saving the entry is what puts the event in the calendar — the one
thing on that screen which needs a connection, since the entry has to carry an id the server issued,
and which says so rather than saving a link to nothing. An **Inventory** errand opens the product it is
about, through the same form the warehouse screen shows, and saving writes the correction back to the
shelf and rebuilds that warehouse's restock list; it also says which shelf it is about and which other
list is asking for the same product, both as something to tap. Unlike the calendar half this one works
offline, because the product already exists and is only being corrected.

That errand no longer has to be about something the shelf already holds. On a list measured against a
storage, an Inventory entry describes a product **for** that shelf - the same fields with no name box,
since the entry's own words are the name - and saving the entry puts it there, skipping a shelf that
already holds that name. It is the last of the browser's 2026-09-02 item form that the phone was
missing.

The Google links can be turned off here too, on this phone rather than for the account: the switch sits
on the account screen where the browser keeps its own, is offered only where the account may use the
extras at all, and turning it off leaves a connected Google account connected.

An export is the reader's to choose, as it is in the browser: four switches for notes, task lists,
events and storages, all on to begin with, and the file says how much of each it ended up carrying.
What is left out is emptied rather than dropped from the file's shape, so an older Orbit still reads it.

Conversations can be pinned here now, as they always could in the browser: people and groups out of one
set, kept on the device that pinned them, lifted to the top of the list without being taken out of its
order and without touching the archive.

On Android the calendar now gets out of the way as the list under it is read: it minimises to the week
the reader is standing on, the month they are reading in the year view, or one hour of the day, and
comes back whole at the top of the list. Decided for the phone and not for the browser - a desktop
window has room for the grid and the list at once, and a phone has one column and a thumb.

The shelf itself answers two more questions. A warehouse opened from an errand naming a product, or from
the search across every shelf, marks that row and scrolls to it rather than landing on a list with no
sign of which one was meant - and says so in words as well as in colour. And every row says when its
batch arrived, which is what tells two rows of one name apart: they are two deliveries of the thing.

Being offline no longer only refuses. Anything shared that cannot be edited without a connection - see
[the conflict policy](orbit-maui-plan.md#54-pushing-changes-and-conflicts-built-for-notes) - now offers
a copy to write in instead, for all four kinds: notes, task lists, appointments and warehouses. The copy
belongs to the phone, is shared with nobody, and stays off the wire until it has been decided on; back
online, one review window shows every outstanding copy against what it came from, each diffed from what
that said when the copy was taken, and offers three answers: keep mine, keep theirs, or keep both. The
last leaves the copy as a thing of its own, tagged `copy` in its list and still pointing at what it came
from, which is what that thing's own History window lists - opened from the thing itself, because a
history is a fact about a note or a list rather than about the account. The review window hangs off the
avatar's menu, badged there the way notifications are, because a copy can be of any of the four kinds
and no single list is the right place to wait for one. When what a copy came from has been deleted the
review stops offering a choice that no longer has two sides and asks the one thing left: keep your copy?

The outbox no longer deletes work for being out of range. Every retryable failure used to count towards
its give-up limit, and "there is no network" is retryable - so five launches without signal dropped a
queued change for good. Only an answer from the server counts now, and when a change really is given up
on, the phone writes that into its own notification feed rather than only into a log. That feed is now
something the phone can write to at all: it also carries a copy waiting to be reviewed, named and by
kind, and takes the notice away once the review is answered.

Of phase 7, the in-app feed, notification settings, deep links from a notification, and uploadable
diagnostic logs are built — and **push is delivered on Android**: the app obtains an FCM registration
token, registers it on every sign-in, and a notification the server raises arrives in the tray and taps
through to the screen it names. A push arriving while the app is in front of somebody still shows
nothing. Phase 8 — widgets, Live Activities, accessibility — has not been started.

**Android is the verified head.** It has been driven on an emulator and a device: signing in, syncing
each feature both ways, chatting, and sharing. iOS was verified on a simulator at phase 1 and not
since — phases 2-7 were built and driven on Android, and iOS is now deferred for want of an Apple
developer account and signing key.

## Not yet implemented

- **Push delivered to an iPhone.** Android delivers (above). iOS cannot: FCM reaches it through APNs,
  which needs an auth key uploaded to the Firebase console, and `PhonePushNotifications` there still
  answers `NotAvailableHere` rather than registering a device the server would then count as
  reachable. Browser push works and is unaffected, and on Android a push now shows whether the app is
  in front of somebody or not.
- **Google sign-in and maps depend on configuration that is not in the repository.** Each mobile head
  needs its own OAuth client id set on the server (`GOOGLE_ANDROID_CLIENT_ID`, `GOOGLE_IOS_CLIENT_ID`)
  or its Google button is hidden, and the Android map needs a Maps SDK key merged into the manifest or
  the map screen says it has none. Both are deployment values kept out of git — see `secrets/README.md`
  for where they live and which Google project each belongs to.
- **iOS beyond phase 1 — deferred.** The head is written and everything in `Orbit.Mobile` is shared
  with it, but nothing built since phase 1 has been run there. It is blocked on an Apple developer
  account and a signing key rather than on the work: without them the head cannot be produced at all.
  What that leaves unknown is the head's own platform services, not the features.
- **Two-way Google Calendar sync** — writing an event onto a recipient's real Google Calendar. What
  ships today is link-based hand-off, which needs no Google API at all — see
  [Functionality](functionality.md#these-are-links-not-an-api-integration).
- **Google Contacts sync** — not started. What it would take, and the design question that has to be
  answered first, is in [Future Plan](future-plan.md#planned-features).
- **The AI assistant** — step 1 of [its plan](ai-assistant-plan.md) is built and is the half that needs
  no model: names the reader already has, offered as they type, and a warning when what they are typing
  is a name they already use. Everything from step 3 on - the model, the overlay window, the
  proposals - is not started.

See [Future Plan](future-plan.md) for the fuller list of planned work, known scope cuts, and testing
gaps.
