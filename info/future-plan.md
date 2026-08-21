# Future Plan

This document collects the work that is known to be planned or still missing, drawn from what the
rest of the documentation already flags as "not implemented yet," a deliberate first-version scope
cut, or an identified follow-up. It is not a committed roadmap with dates — it is the current honest
picture of what's left.

## Planned features

- **.NET MAUI client (mobile and desktop).** The long-term target architecture is a shared ASP.NET
  Core API backing a .NET MAUI client so every device stays in sync (see the top-level
  [README](../README.md)). Today the Blazor WebAssembly web client (`src/Clients/Orbit.Web`) is the
  only client, and MAUI work has not started — see
  [Architecture — Orbit.Web](architecture.md#orbitweb).
- **Location sharing.** Part of the product's stated scope, not implemented at all yet — no server
  endpoints, data model, or client UI exist for it.
- **Google Calendar / Contacts sync.** `Orbit.GoogleIntegration` (`src/Server`) exists today only as
  an empty placeholder project reserved for this — see
  [Architecture — Orbit.GoogleIntegration](architecture.md#orbitgoogleintegration) and
  [Functionality — Calendar](functionality.md#calendar) for what the calendar feature does without
  it.
- **Running more than one instance of the reminder background services.** The claim-before-send
  design of `CalendarEventReminderBackgroundService` and `OverdueTaskNotificationBackgroundService`
  (a unique-indexed "claim" row inserted before sending, so a losing insert means another instance
  already claimed the same notification) was built specifically so this is safe without a
  distributed lock or message queue once it's needed — see
  [Functionality — Calendar event reminders](functionality.md#calendar-event-reminders). No second
  instance runs today; this is forward-looking groundwork already in place.

## Known scope cuts and rough edges

Explicitly called out in the functionality documentation as deliberate limitations of this first
version, so they aren't mistaken for oversights:

- **Chat has no per-message forward secrecy.** A single shared AES-GCM key is derived per user pair
  instead of a rotating scheme like Signal's Double Ratchet — compromising one derived key exposes
  the whole conversation with that person, not just one message. See
  [Functionality — Contacts and encrypted chat](functionality.md#contacts-and-encrypted-chat).
- **Chat has no identity verification.** There is no out-of-band step (e.g. comparing key
  fingerprints) to confirm a public key really belongs to the person it claims to; the browser
  trusts whatever key Orbit.Api currently reports for a user. A compromised server could substitute
  a key and intercept new messages, though it still couldn't decrypt already-sent ciphertext.
- **Chat is 1:1 only** — no group conversations.
- **Chat delivery is polling-based** (every 3 seconds while a chat window is open), not real-time —
  no SignalR or WebSockets.
- **Calendar guests aren't wired to notifications.** `guests` is stored on an event but only the
  event's owner receives reminder emails and push notifications today — see
  [Functionality — Calendar event reminders](functionality.md#calendar-event-reminders).
- **Task list cycle validation is server-side only.** The Blazor task editor only prevents linking a
  list to itself in its dropdown; it does not detect longer cycles client-side. Building one still
  relies on the API's validation (`TaskListLinkValidator`) and surfaces as a failed save rather than
  an inline client-side error — see [Functionality — Tasks](functionality.md#tasks).

## Testing gaps

Documented in [Testing and Running Locally](testing-and-running-locally.md#what-is-not-covered-by-an-automated-test-today)
as not covered by an automated test today, together with why:

- The `/api/auth/*` rate limiter's exact 429 behavior, and the client's retry-after-refresh path
  end-to-end through a real `HttpClientHandler` pipeline — both would need HTTP-integration test
  infrastructure (e.g. `WebApplicationFactory` on the API side) that this project doesn't have yet.
- Actually sending an email through `SmtpEmailSender` or a push notification through
  `VapidPushNotificationSender` — both need a real or fake server to connect to.
- The `Notes`/`Tasks`/`Calendar`/`Dashboard` pages themselves as bUnit tests against the actual
  pages (unlike `Login`/`Register`, which already have this).
- The `Contacts`/`Chat` pages, `PushNotificationManager`, and the browser-side JavaScript
  (`e2eeChat.js`, `pushNotifications.js`, `service-worker.js`) — the encryption/decryption round
  trip, IndexedDB key persistence, the polling UI, browser notification permission handling, and the
  push subscription/service worker lifecycle have no automated coverage at all. bUnit doesn't
  execute real browser crypto/IndexedDB/Push/Notification APIs, and this project has no
  browser-driven test infrastructure (e.g. Playwright) yet.

## Smaller identified follow-ups

- **A permanent, bind-mounted TLS certificate setup for local development.** The mkcert-based option
  in [`info/instructions.md`](instructions.md) currently requires copying certificate files into the
  running `orbit-web` container by hand after every `docker compose down -v`. Switching the
  `orbit-web-certs` volume to a bind mount pointing at a folder holding the mkcert output would make
  this survive that command — noted in `info/instructions.md` as a small `docker-compose.yml`
  change, not yet made.
- **Self-hosting the Nominatim reverse-geocoding endpoint.** The calendar's map location picker
  currently calls OpenStreetMap's free, public Nominatim instance (see
  [Functionality — Calendar](functionality.md#calendar)), whose usage policy caps it to light,
  non-commercial traffic. A deployment with real usage volume should self-host Nominatim instead.
