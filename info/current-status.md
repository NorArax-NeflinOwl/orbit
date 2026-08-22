# Current Status

Orbit is an early-stage prototype. Accounts, notes, tasks, a basic calendar, and end-to-end-encrypted
1:1 chat are implemented end to end, including the Blazor WebAssembly web client — today the only
client. Location sharing and the .NET MAUI client are not implemented yet.

## Implemented vs. planned

| Area | Status | Where to read more |
| --- | --- | --- |
| Accounts (register / login / refresh / logout) | Implemented | [Functionality — Authentication](functionality.md#authentication) |
| Notes (including sharing with another user) | Implemented | [Functionality — Notes](functionality.md#notes) |
| Tasks (including sharing with another user) | Implemented | [Functionality — Tasks](functionality.md#tasks) |
| Calendar (basic, including event sharing) | Implemented | [Functionality — Calendar](functionality.md#calendar) |
| End-to-end-encrypted 1:1 chat | Implemented | [Functionality — Contacts and encrypted chat](functionality.md#contacts-and-encrypted-chat) |
| Push notifications | Implemented | [Functionality — Push notifications](functionality.md#push-notifications) |
| Blazor WebAssembly web client | Implemented (only client so far) | [Architecture](architecture.md#orbitweb) |
| Location sharing | Not started | [Future Plan](future-plan.md#planned-features) |
| .NET MAUI client (mobile and desktop) | Not started | [Future Plan](future-plan.md#planned-features) |
| `Orbit.GoogleIntegration` (Google Calendar/Contacts sync) | Empty placeholder project | [Future Plan](future-plan.md#planned-features) |

## The signed-in experience today

Registering or logging in happens on `/register`/`/login`; the dashboard, notes, tasks, calendar, and
contacts/chat pages are only reachable once signed in. Signing in lands on the dashboard (`/`), which
lists notes, task lists, and calendar events at a glance. Each of those areas also has its own page
(`/notes`, `/tasks`, `/calendar`) where items can be created, edited, or deleted. `/contacts` searches
for other users and lists existing conversations — see
[Functionality — Contacts and encrypted chat](functionality.md#contacts-and-encrypted-chat).

Once a user approves browser notifications, push notifications fire for approaching calendar events,
new chat messages, and overdue tasks — see
[Functionality — Push notifications](functionality.md#push-notifications).

## Not yet implemented

- **Location sharing** — no work has started on this yet, despite being part of the product's stated
  scope (see the top-level [README](../README.md)).
- **.NET MAUI client** (mobile and desktop) — planned but not started; the Blazor WebAssembly web
  client is the only client today.

See [Future Plan](future-plan.md) for the fuller list of planned work, known scope cuts, and testing
gaps.
