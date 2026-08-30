# Current Status

Orbit is a working prototype. Accounts (including Google sign-in and full self-service account
management), notes, tasks, calendar, an inventory planner, and end-to-end-encrypted chat — one-to-one
and group — are implemented end to end in the Blazor WebAssembly web client. Locations can now be
recorded, seen on a map, and shared with a contact under the same encryption chat uses. The mobile
client is no longer a plan: `Orbit.Mobile` and `Orbit.Maui` build an Android app that CI signs on
every change to it and that `/download` links to, with the phasing in
[Orbit.Maui — Plan](orbit-maui-plan.md#10-phasing) marking phases 0 through 6 built. A real two-way
Google Calendar sync, as opposed to the hand-off links Orbit builds today, is what remains unbuilt of
the product's stated scope.

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
| Mobile client (`Orbit.Mobile` + `Orbit.Maui`) | Android released; iOS head unreleased (needs a Mac) | [Orbit.Maui — Plan](orbit-maui-plan.md) |
| Google Contacts sync | Not started | [Future Plan](future-plan.md#planned-features) |
| Password manager and password generator | Not started | [Future Plan](future-plan.md#planned-features) |

`Orbit.GoogleIntegration` (`src/Server`) is no longer the empty placeholder it was: it holds the
Google ID-token verification behind Google sign-in, and nothing else - the calendar and maps features
are links the browser builds, needing no API. The Calendar/Contacts sync it was originally
reserved for still hasn't been started — see [Future Plan](future-plan.md#planned-features).

## The signed-in experience today

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

## Not yet implemented

- **The iOS half of the mobile client.** `Orbit.Maui` targets `net10.0-ios` as well as
  `net10.0-android`, but nothing builds or releases it: Apple's toolchain is macOS-only and every
  runner here is Linux or Windows, so the iOS head has never run on a device. What ships today is the
  Android app. See [Orbit.Maui — Plan §1.1](orbit-maui-plan.md#11-can-this-be-developed-from-windows).
- **Two-way Google Calendar sync** — writing an event onto a recipient's real Google Calendar. What
  ships today is link-based hand-off, which needs no Google API at all — see
  [Functionality](functionality.md#these-are-links-not-an-api-integration).
- **Google Contacts sync** — not started.
- **Password manager and password generator** — not started.

See [Future Plan](future-plan.md) for the fuller list of planned work, known scope cuts, and testing
gaps.
