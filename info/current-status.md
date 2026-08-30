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
| Recording your own location and seeing it on a map | Implemented | [Functionality](functionality.md#the-map-and-the-location-behind-it) |
| Sharing a location with another user | Implemented | [Functionality](functionality.md#sharing-a-position-with-a-contact) |
| Google Calendar and Maps links (verified/Google accounts) | Implemented | [Functionality](functionality.md#handing-something-off-to-google) |
| Two-way Google Calendar sync | Not started | [Future Plan](future-plan.md#what-real-google-calendar-sync-would-take) |
| Mobile client (`Orbit.Maui`, iOS + Android) | Implemented — Android verified on a device, iOS unverified | [Orbit.Maui — Plan](orbit-maui-plan.md) |
| Push delivery to a phone | Not working on Android yet — see below | [Orbit.Maui — Plan](orbit-maui-plan.md#42-push-notifications-web-push-apns-and-fcm-are-three-different-things) |
| Google Contacts sync | Not started | [Future Plan](future-plan.md#planned-features) |
| Name suggestions and duplicate warnings while typing | Implemented | [Functionality](functionality.md#names-you-have-already-used) |
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
list settles its finished errands when the phone opens it, as a browser does, the names already in the
account are offered under a product's name and an errand's description, and an admin can hand a group's
past to somebody they have just added. Of phase 7, the in-app feed,
notification settings, deep links from a notification, and uploadable diagnostic logs are built; push
*delivery* is not (below). Phase 8 — widgets, Live Activities, accessibility — has not been started.

**Android is the verified head.** It has been driven on an emulator and a device: signing in, syncing
each feature both ways, chatting, and sharing. iOS was verified on a simulator at phase 1 and not
since — phases 2-7 were built and driven on Android, and iOS is now deferred for want of an Apple
developer account and signing key.

## Not yet implemented

- **Push delivered to a phone.** The server sends through Firebase, and the app asks Android for
  permission — but it cannot yet obtain the FCM registration token that says where to deliver, which
  needs the Firebase SDK in the app and a `google-services.json` for this application id. Until both
  exist, `PhonePushNotifications` answers `NotAvailableHere` rather than registering a device the
  server would then count as reachable. Browser push works and is unaffected.
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
