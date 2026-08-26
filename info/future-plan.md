# Future Plan

This document collects the work that is known to be planned or still missing, drawn from what the
rest of the documentation already flags as "not implemented yet," a deliberate first-version scope
cut, or an identified follow-up. It is not a committed roadmap with dates — it is the current honest
picture of what's left.

## Planned features

- **.NET MAUI client (mobile and desktop).** The long-term target architecture is a shared ASP.NET
  Core API backing a .NET MAUI client so every device stays in sync (see the top-level
  [README](../README.md)). Today the Blazor WebAssembly web client (`src/Clients/Orbit.Web`) is the
  only client, and MAUI work has not started — `src/Clients/Orbit.Maui` exists as an empty folder that
  isn't part of `Orbit.sln`, so it builds nothing and is a reservation rather than a stub. See
  [Architecture — Orbit.Web](architecture.md#orbitweb).

  **This is now planned in detail:** [Orbit.Maui — Plan](orbit-maui-plan.md). One MAUI project builds
  both apps, referencing `Orbit.Contracts` directly; iPhone 15 Pro is the target device and Android
  the second platform. Beyond web parity it adds **offline operation** with a local SQLite database
  and background sync, a **forced-update version gate**, and **uploadable diagnostic logs**. It also
  names the server work that has to land first: push transports beyond Web Push, a Google audience
  allowlist, and delta/tombstone support for sync. Remaining decisions are in
  [§12](orbit-maui-plan.md#12-open-questions).
- **Writing to a real Google Calendar.** `Orbit.GoogleIntegration` (`src/Server`) holds the ID-token
  verification behind Google sign-in (`GoogleIdentityVerifier`, `GoogleAuthSettings`) — that is
  authentication only, and no calendar data is read or written. What ships today is the link-based
  half: a verified or Google-linked account can hand an event or task to Google Calendar and turn a
  location into directions, both as deep links needing no API credentials (see
  [Functionality — Handing something off to Google](functionality.md#handing-something-off-to-google)).
  Making Orbit actually write to someone's calendar — so an edit updates the copy rather than
  duplicating it — is a different kind of change, and most of the work is outside this repository:
  see [What real Google Calendar sync would take](#what-real-google-calendar-sync-would-take) below.
- **Running more than one instance of the reminder background services.** The claim-before-send
  design of `CalendarEventReminderBackgroundService` and `OverdueTaskNotificationBackgroundService`
  (a unique-indexed "claim" row inserted before sending, so a losing insert means another instance
  already claimed the same notification) was built specifically so this is safe without a
  distributed lock or message queue once it's needed — see
  [Functionality — Calendar event reminders](functionality.md#calendar-event-reminders). No second
  instance runs today; this is forward-looking groundwork already in place.
- **A local AI model on the server, as groundwork for a future chat bot.** No work started; explicitly
  scoped on the backlog as infrastructure to land before the chat bot feature itself. **Proposed
  approach:** self-hosting something like Ollama alongside `orbit-api` in `docker-compose.yml` (a new
  service, similar in shape to the existing `aspire-dashboard` one) keeps this from depending on a
  paid third-party LLM API, at the cost of needing real CPU/GPU/RAM sized for whatever model is chosen
  - worth prototyping with a small model before committing to it as the target architecture.

## What real Google Calendar sync would take

Orbit currently hands events to Google as **links** (see
[Functionality](functionality.md#handing-something-off-to-google)). That needs nothing beyond the sign-in
client id, works immediately, and keeps the user in control - but it is one-way and one-shot: Orbit
cannot read a Google calendar, cannot update what it already put there, and learns nothing when the copy
in Google changes.

Making it a real integration is a different kind of change, and most of the work is outside this
repository. In rough order:

**1. In Google Cloud Console.** Enable the Google Calendar API on the project. Add the
`https://www.googleapis.com/auth/calendar.events` scope to the OAuth consent screen. That scope is one
Google classes as **sensitive**, which means the consent screen has to go through Google's verification
before anyone outside the project's own test users can grant it - a review that asks for a privacy
policy, a recorded demonstration of the flow, and a justification of why the scope is needed. Budget
weeks, not hours, and expect back-and-forth.

**2. A different OAuth flow.** Today the browser gets an ID token and Orbit verifies it - that is all.
Writing to a calendar needs an **access token** for the scope above, plus a **refresh token** so it keeps
working tomorrow, which means an authorization-code flow with `access_type=offline`. That introduces a
**client secret**, which the current design deliberately does not have (see `GoogleAuthSettings`). The
secret has to live where the API's other secrets do - an environment variable fed from a Container App
secret, never a committed file.

**3. Somewhere to keep the tokens.** A refresh token is a long-lived credential to someone's calendar. It
belongs encrypted at rest, with a clear path for revoking it - both when the user disconnects Google in
Orbit and when they revoke Orbit from their Google account, which Orbit only finds out about by getting a
refusal on the next call and having to handle it gracefully.

**4. Deciding what "sync" means.** One-way (Orbit → Google, keeping the Google event id so an edit
updates rather than duplicates) is a substantially smaller job than two-way, which needs Google's watch
channels or polling, a rule for what happens when both sides changed the same event, and a story for an
event deleted on one side. One-way is the sensible first step.

**5. Quota and failure handling.** API calls fail, get rate-limited, and time out in ways a link never
does: retries, backoff, and somewhere for the user to see that a sync did not go through.

None of this is required for what Orbit does today, which is why it is here rather than in the code.

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
- **A new group member can't read anything sent before they joined.** Group messages are encrypted
  once per member under the existing pairwise keys rather than under a group key, so no copy exists
  for someone who wasn't a member at the time. The group view says so rather than showing empty
  space, and the trade-off is deliberate — there is no group key to distribute or rotate when
  membership changes. The other side of the same choice is that a group message costs one stored row
  per member instead of one. See [Functionality — Group chats](functionality.md#group-chats).
- **Chat delivery is polling-based** (once a second while a chat window is open), not real-time —
  no SignalR or WebSockets.
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
- The `Contacts`/`Chat` pages, `PushNotificationManager`, and the browser-side JavaScript
  (`e2eeChat.js`, `pushNotifications.js`, `service-worker.js`) — the encryption/decryption round
  trip, IndexedDB key persistence, the polling UI, browser notification permission handling, and the
  push subscription/service worker lifecycle have no automated coverage at all. bUnit doesn't
  execute real browser crypto/IndexedDB/Push/Notification APIs, and this project has no
  browser-driven test infrastructure (e.g. Playwright) yet.

## Deployment

- **A public, reachable address to test against, instead of only local Docker.** Mostly done:
  [`.github/workflows/main_orbit.yml`](../.github/workflows/main_orbit.yml) builds both `orbit-api`
  and `orbit-web` images on every push to `main` and deploys them to two Azure Container Apps via
  OIDC login (no stored client secret), and the first-time setup — Container App secrets for
  `JWT_SIGNING_KEY`, SMTP and VAPID, the database, backups — is written down in
  [Azure Container Apps setup](azure-setup.md). What is still open is the public URL itself being
  recorded somewhere findable, rather than read off the workflow file.
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
