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
  endpoints, data model, or client UI exist for it. The fuller intended shape, as captured on the
  backlog:
  - Capture and store the user's own device location.
  - Share it with another user through chat, either as a one-off snapshot or a "live" share that
    keeps refreshing — a client polling for the target's last known location, with the sharer's own
    client pushing an updated position roughly once a minute while online.
  - **Proposed approach:** a `UserLocations` table (`UserId`, `Latitude`, `Longitude`, `RecordedAtUtc`)
    updated via an upsert endpoint the client calls on an interval (mirroring the chat page's existing
    3-second polling loop — see
    [Known scope cuts — Chat delivery is polling-based](#known-scope-cuts-and-rough-edges) — a
    once-a-minute interval for location is far cheaper). A share would work like calendar event
    sharing already does (`CalendarApiClient.ShareCalendarEventAsync` /
    `EncryptedChatMessageSender`): an encrypted chat message carrying a reference the recipient's
    client resolves by polling `UserLocations` for as long as the share is marked active, with an
    explicit "stop sharing" action to end the live share.
- **Google Calendar / Contacts sync.** `Orbit.GoogleIntegration` (`src/Server`) exists today only as
  an empty placeholder project reserved for this — see
  [Architecture — Orbit.GoogleIntegration](architecture.md#orbitgoogleintegration) and
  [Functionality — Calendar](functionality.md#calendar) for what the calendar feature does without
  it. The backlog frames this as two separate integrations, not one:
  - Sharing a calendar event or task and writing a copy of it onto the recipient's actual Google
    Calendar, on top of Orbit's own sharing model.
  - Turning a saved location into driving/walking directions via Google Maps (sending the address
    onward to pick a route to it) — a client-side deep link (`https://www.google.com/maps/dir/?api=1&destination=...`)
    covers this without any Google API credentials or server work, unlike the Calendar-writing half
    above, which does need OAuth and the Google Calendar API.
- **Running more than one instance of the reminder background services.** The claim-before-send
  design of `CalendarEventReminderBackgroundService` and `OverdueTaskNotificationBackgroundService`
  (a unique-indexed "claim" row inserted before sending, so a losing insert means another instance
  already claimed the same notification) was built specifically so this is safe without a
  distributed lock or message queue once it's needed — see
  [Functionality — Calendar event reminders](functionality.md#calendar-event-reminders). No second
  instance runs today; this is forward-looking groundwork already in place.
- **Checklist items inside a note.** Today a note is a single title/body pair - no way to attach a
  list of `{ checkbox, text }` items to it. **Proposed approach:** a `NoteChecklistItem` child table
  (`NoteId`, `Text`, `IsChecked`, `SortOrder`) rather than cramming it into the note's existing body
  text, so items can be toggled with their own small PATCH endpoint instead of re-saving the whole
  note body on every checkbox click.
- **Chat groups, with per-role permissions.** Chat is explicitly 1:1 only today - see
  [Known scope cuts — Chat is 1:1 only](#known-scope-cuts-and-rough-edges). The backlog wants real
  group conversations, with two roles: an admin who can remove any message or any member, and a
  regular member who can only remove their own messages. **Proposed approach:** a `ChatGroup` +
  `ChatGroupMember` (with a `Role` column) pair sitting alongside the existing 1:1 `ChatMessage` table
  - group messages would need their own encryption story, though, since the current design derives one
    AES-GCM key per *pair* of users (see
  [Known scope cuts — Chat has no per-message forward secrecy](#known-scope-cuts-and-rough-edges));
  a group needs a key every member can decrypt, which is a materially bigger design change than the
  schema addition, and should be scoped as its own follow-up before implementation starts.
- **Editing an already-sent chat message.** No edit path exists today - see
  `SendMessageCommand`/`ChatApiClient.SendMessageAsync`. **Proposed approach:** an
  `EditMessageCommand` mirroring the existing send path (re-encrypt the new text client-side, PUT the
  ciphertext to `/api/chat/messages/{id}`), plus an `EditedAtUtc` column so the UI can show an "edited"
  marker the way most chat apps do.
- **Shopping/inventory planner.** A stock-management feature: products with a name, type, category, an
  on-hand quantity, an optional minimum quantity, and an expiry date. When a product's quantity drops to
  or below its minimum, the system should create a task for the user; a recurring reminder task should
  separately prompt the user to keep the recorded quantity up to date, and the expiry date should
  surface its own approaching-expiry warning. **Proposed approach:** this reuses more of the existing
  Tasks feature than it first looks like it needs - a background service in the same family as
  `CalendarEventReminderBackgroundService`/`OverdueTaskNotificationBackgroundService` (see
  [Functionality — Calendar event reminders](functionality.md#calendar-event-reminders)) can create a
  regular `TaskList` item automatically when a new `Product`'s quantity crosses its minimum, rather than
  inventory needing its own separate notification pipeline.
- **Password manager and strong-password generator.** Not scoped in detail yet on the backlog beyond
  the idea itself. **Proposed approach:** worth treating as an extension of the existing E2EE chat
  design rather than a new subsystem - encrypted credential entries could reuse the same per-user key
  material `OwnEncryptionKeyProvider` already manages, so the server only ever stores ciphertext it
  can't read, matching the chat's own trust model (see
  [Functionality — Contacts and encrypted chat](functionality.md#contacts-and-encrypted-chat)). The
  generator itself is a pure client-side algorithm (length/character-set rules), no server involvement
  needed at all.
- **A local AI model on the server, as groundwork for a future chat bot.** No work started; explicitly
  scoped on the backlog as infrastructure to land before the chat bot feature itself. **Proposed
  approach:** self-hosting something like Ollama alongside `orbit-api` in `docker-compose.yml` (a new
  service, similar in shape to the existing `aspire-dashboard` one) keeps this from depending on a
  paid third-party LLM API, at the cost of needing real CPU/GPU/RAM sized for whatever model is chosen
  - worth prototyping with a small model before committing to it as the target architecture.

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

## Deployment

- **A public, reachable address to test against, instead of only local Docker.** Partially done
  already, not just an open idea: [`.github/workflows/main_orbit.yml`](../.github/workflows/main_orbit.yml)
  builds both `orbit-api` and `orbit-web` images on every push to `main` and deploys them to two Azure
  Container Apps via OIDC login (no stored client secret) - this is real, working infrastructure, not
  a stub. What's still open is verifying and documenting that the deployed result is actually reachable
  and functional end-to-end (health checks, the LAN-IP-style TLS/certificate concerns from
  [`info/instructions.md`](instructions.md) don't apply the same way behind Azure's own ingress
  TLS termination - see `nginx.azure.conf` vs `nginx.conf`), and writing down the public URL and any
  first-time setup (e.g. seeding `JWT_SIGNING_KEY`/VAPID keys as Container App secrets rather than a
  local `.env`) somewhere a person can follow without archaeology through the workflow file. Now
  mostly written down in [Azure Container Apps setup](azure-setup.md).
- **Manage the Azure infrastructure itself as code (Bicep or Terraform), instead of one-off `az cli`
  commands typed into Cloud Shell.** Not started. Every Azure resource this project depends on today
  - `orbit-api`/`orbit-web` Container Apps, `orbit-environment`, the container registry, the
  PostgreSQL Flexible Server, Application Insights - was created and configured by hand, one `az`
  command at a time, across several sessions. That's exactly why a whole day of incidents in
  2026-08-23 consisted of *rediscovering* what was and wasn't configured (missing env vars, an
  unmounted volume, a firewall rule that may or may not exist) rather than reading it off a file.
  **Proposed approach:** a Bicep template (native to Azure, no separate state file to manage, unlike
  Terraform) under a new `infra/` folder, covering at minimum the two Container Apps' full
  configuration (env vars referencing Key Vault secrets rather than plain Container App secrets,
  ingress settings, scaling), the Container Apps Environment, and the PostgreSQL Flexible Server
  (SKU, storage, backup retention - see [Azure setup](azure-setup.md#3-confirm-database-backups)).
  `.github/workflows/main_orbit.yml` would gain an `az deployment group create` step using that
  template, so an infrastructure change goes through the same PR review as a code change instead of
  being invisible until someone happens to `az containerapp show` and finds it. This is a genuinely
  large undertaking - importing *already-running* resources into a Bicep template without disrupting
  them takes real care (`az bicep decompile` / `az deployment group what-if` as a starting point, not
  a one-shot conversion) - and should be scoped as its own project, not bundled into an unrelated
  feature change.
- **A deploy-approval gate done correctly.** Attempted once on 2026-08-23 by adding
  `environment: production` to the `build-and-deploy` job in `main_orbit.yml`, intending to require a
  human to click "approve" before every push to `main` goes live. It broke `azure/login` outright:
  targeting a GitHub Environment changes the OIDC token's subject claim from
  `repo:<org>/<repo>:ref:refs/heads/main` to `repo:<org>/<repo>:environment:<name>`, which the
  federated identity credential configured on the `identity-orbit` Azure AD app registration didn't
  trust - every deploy failed at the login step until the change was reverted. **Proposed approach:**
  before touching the workflow file again, add a *second* federated credential on `identity-orbit` in
  Entra ID (App registrations > identity-orbit > Certificates & secrets > Federated credentials) with
  subject `repo:NorArax-NeflinOwl/orbit:environment:production` (or whatever the org/environment name
  ends up being - the exact string matters), audience `api://AzureADTokenExchange`, issuer
  `https://token.actions.githubusercontent.com` - the same three values the existing branch-based
  credential uses, just with an environment-shaped subject instead of a ref-shaped one. Once that
  credential exists, `environment: production` can be added back to the job and a required reviewer
  configured under the repo's Settings > Environments > production, this time without breaking OIDC.
  Worth doing alongside (or after) the CI smoke test and health-gated rollback added in
  `ci/deploy-safety-gates`, since those two mean a bad deploy self-heals automatically - a manual
  approval gate is then about *deliberateness* (did a human mean to ship this now) rather than being
  the only thing standing between a bug and production.

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
