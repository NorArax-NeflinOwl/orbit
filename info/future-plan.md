# Future Plan

This document collects the work that is known to be planned or still missing, drawn from what the
rest of the documentation already flags as "not implemented yet," a deliberate first-version scope
cut, or an identified follow-up. It is not a committed roadmap with dates — it is the current honest
picture of what's left.

**Last checked against the code on 2026-08-31.** A plan is only worth reading if it describes the
present. Anything below that says "not started" or "no coverage" was checked against the repository on
that date rather than carried forward on trust.

Since the last pass: the version is counted from the history rather than maintained by hand, the tasks
page reads three ways, a restock errand edits the shelf it names, a calendar entry is the appointment
rather than a pointer at one, and the build refuses to finish on a warning. What that pass found and did
**not** fix is in [Known scope cuts and rough edges](#known-scope-cuts-and-rough-edges) below.

## Planned features

- **.NET MAUI client (mobile and desktop).** The long-term target architecture is a shared ASP.NET
  Core API backing a .NET MAUI client so every device stays in sync (see the top-level
  [README](../README.md)). **The mobile half is built** — see
  [Current Status](current-status.md#the-mobile-client) for exactly how far, and
  [Orbit.Maui — Plan](orbit-maui-plan.md) for the design it was built to. Android is the verified
  head; iOS has not been run since phase 1, and desktop has not been started at all.

  What is left of it: the iOS head beyond phase 1 (deferred — no Apple developer account or signing
  key, which also blocks push there), a push that arrives while the app is in front of somebody, and
  phase 8 — widgets, Live Activities, accessibility. Push to an Android phone is delivered as of
  2026-08-31 and no longer on this list. Remaining design decisions are in
  [§12](orbit-maui-plan.md#12-open-questions); the local database staying unencrypted and iOS being
  deferred are both settled there.
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
- **Google Contacts sync.** Not started, and named here because
  [Current Status](current-status.md#implemented-vs-planned) lists it and links to this section. Like
  calendar sync it needs an authorization-code flow and a sensitive scope
  (`https://www.googleapis.com/auth/contacts.readonly`) through Google's verification - see
  [What real Google Calendar sync would take](#what-real-google-calendar-sync-would-take), which is the
  same shape of work and should be done once for both rather than twice. It also needs a decision this
  document cannot make for it: Orbit's own contacts are people who hold an Orbit account and have agreed
  to a conversation, and a Google contact is a name and an email address. Whether an imported contact is
  a third kind of row, or only a way to find somebody already on Orbit, changes the feature entirely.
- **An AI assistant for inventories and task lists.** No work started. It suggests and corrects what the
  user is typing, finds duplicate items, explains what Orbit can do, and proposes calendar events linked
  to the right task lists. It is deliberately shut out of private items and out of chat entirely - it is
  not a party to any conversation, and the messages are sealed so there would be nothing to give it.
  The whole design, the model and hosting decision, and the order to build it in are in
  [Orbit Assistant — Plan](ai-assistant-plan.md).

  Two things there are worth knowing without opening it. **Half of what was asked for is not a language
  model's job**: typeahead and duplicate detection are trigram similarity searches over the user's own
  data, which PostgreSQL answers in milliseconds for nothing and more correctly than a model could. And
  **the model should not be self-hosted**: an earlier version of this entry proposed running Ollama
  beside `orbit-api`, and measuring that is what killed it - a model small enough to serve on the CPU
  Orbit deploys on is too weak for Polish, and one good enough at Polish is too slow to sit behind a chat
  window. A small hosted model in Azure AI Foundry costs cents a month at this size. Ollama stays, for
  local development only.

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

- **`pg_trgm` may not be allowed on the deployed database, and nothing would find out until it is too
  late.** The name-suggestion migration runs `CREATE EXTENSION pg_trgm`. Azure Database for PostgreSQL
  Flexible Server only permits extensions listed in its `azure.extensions` server parameter, and
  `Program.cs` applies migrations at startup without catching anything - so a server that has not been
  told to allow it would fail to start, the revision would never turn healthy, and the deploy would roll
  back. **CI cannot catch this**: the smoke test runs against a plain `postgres:18-alpine`, where the
  extension needs no permission. Check with
  `az postgres flexible-server parameter show --resource-group Orbit --server-name <name> --name azure.extensions`
  before the next deploy, and record the answer in [Azure setup](azure-setup.md).
- ~~**The forced-update gate has nothing to compare against.**~~ Done: the Android release now sets
  `MobileVersion__Android__LatestVersion` on `orbit-api` to whatever it just published, so the app's
  update row can light up. It needs one repository variable naming the resource group - see
  [Azure setup](azure-setup.md#6-let-a-release-record-itself-as-the-newest-build) - and skips silently
  without it. `MinimumSupportedVersion` is the one that **blocks** an app and stays empty while this is
  a prototype.
- **One test was removed because it could not be made to fail on demand.**
  `NoteDetailScreenTests.Turning_private_off_puts_the_words_back_where_the_server_can_read_them` failed
  about one full-suite run in ten and was never reproduced on its own - roughly fifty targeted runs,
  including under load from a second test host, all passed. It needs the whole suite in flight, which
  points at something about running the three assemblies together rather than at the unsealing it
  covers.

  Taken out rather than left red or "fixed" by guessing: a change that cannot be shown to address the
  failure only hides it, and a test that fails one run in ten teaches everybody to re-run the build
  instead of reading it. What is no longer asserted is the way *back* from private - that clearing the
  switch puts the title and lines where the server can read them and drops the sealed payload. Turning
  privacy on, and the refusal when the device holds no key, are still covered. Worth restoring once the
  parallelism question is answered.
- **A timestamp is only as fine as the clock.** `NotificationChangeFeedTests` took its cursor from
  `DateTimeOffset.UtcNow` a moment before recording, and on a fast machine both reads land on the same
  tick - fixed by stamping its records at a fixed point in the past, which is the technique the other
  tests of this shape already use (`PretendItWasLastChanged`, `AMinuteAgo`).

  Not only a test problem, and this is the part worth keeping in view: the change feed gates on
  `UpdatedAtUtc > since`, so two changes inside one tick are genuinely indistinguishable to a syncing
  client and the second is never delivered.

  **Decided on 2026-08-31: not worth fixing at this scale.** Two changes to the same row inside one tick
  needs either two people editing the same thing in the same instant or a script; with one person and a
  handful of accounts it is theoretical. Recorded rather than dropped because the answer depends entirely
  on that scale - the day Orbit has concurrent editors or a bulk import, it stops being theoretical, and
  whoever hits it should find this rather than rediscover it. The fix, when it is wanted, is a stamp that
  cannot go backwards or sideways: keep the last one issued per row and step forward a tick when the
  clock has not moved.

- **Chat has no per-message forward secrecy.** A single shared AES-GCM key is derived per user pair
  instead of a rotating scheme like Signal's Double Ratchet — compromising one derived key exposes
  the whole conversation with that person, not just one message. See
  [Functionality — Contacts and encrypted chat](functionality.md#contacts-and-encrypted-chat).
- **Chat has no identity verification.** There is no out-of-band step (e.g. comparing key
  fingerprints) to confirm a public key really belongs to the person it claims to; the browser
  trusts whatever key Orbit.Api currently reports for a user. A compromised server could substitute
  a key and intercept new messages, though it still couldn't decrypt already-sent ciphertext.
- ~~**A new group member can't read anything sent before they joined.**~~ Answered without giving up the
  design: there is still no group key, and the server still holds no key to anything. What changed is
  that the admin adding somebody can tick a box to hand over what was said before, and their browser does
  the work - decrypting what it can already read and sealing each message again under the pairwise key it
  shares with the newcomer. The conversation gains a line saying both halves of what happened. What the
  server will accept is narrow, because it cannot read what it is being handed: an admin only, into a
  membership only, only postings the sharer demonstrably holds, and never twice. A backfilled copy is
  marked so it stays out of the original's delivery receipts. See
  [Functionality — Letting a new member read the history](functionality.md#letting-a-new-member-read-the-history).

  Still true, and now the deliberate part rather than the whole story: a group message costs one stored
  row per member, and sharing history adds one more row per message per newcomer. Nobody who was never
  given the history can read it, which is the point - this is a member's decision to make, not something
  joining a group grants.
- **Chat delivery is polling-based** (once a second while a conversation is open), not real-time - no
  SignalR or WebSockets. The polling itself has since been made to cost what it should: a group
  conversation polls at all, nothing is polled while the tab is behind others, and the conversation list
  is read every tenth tick rather than every one. Replacing it with a push transport is still open.
- **"Read" means "the chat was open", not "somebody looked at it".** A message is marked read by the
  thread that is polling for it (`Chat.razor`), which is a stand-in for the other party actually seeing
  it. Narrowing the poll so it stops while the tab is behind others made the stand-in closer to the
  truth than it was, but not equal to it: a thread open in a visible window nobody is sitting at still
  reports everything as read. A real signal - tab focus and scroll position, pushed to the server rather
  than inferred from a poll - is still open, and is worth having before read receipts are shown to the
  *sender* as a promise rather than kept as an unread count for the reader.
- ~~**Task list cycle validation is server-side only.**~~ Done: the editor's "link to list" dropdown now
  leaves out every list that links back to the one being edited, however long the chain
  (`TaskListLinkCycle`), so a link the save would refuse is never offered. `TaskListLinkValidator` stays
  the authority — this only stops the editor asking for something it already knows the answer to.

## Testing gaps

Documented in [Testing and Running Locally](testing-and-running-locally.md#what-is-not-covered-by-an-automated-test-today)
as not covered by an automated test today, together with why. Most of what used to be listed here has
since been closed; what is left is recorded below with the same honesty about why.

- ~~**The `/api/auth/*` rate limiter's exact 429 behavior.**~~ Done. It needed no
  `WebApplicationFactory` in the end - what stood in the way was that the policies were written inline
  in `Program.cs`, reachable only by running the whole application. They now live in
  `RateLimiterPolicies.AddOrbitPolicies`, which `Program.cs` calls and `AuthRateLimiterTests` calls too,
  so the test cannot pass against a copy that has drifted. It covers the sixth attempt in a window being
  refused rather than queued, and the partitioning that matters most: one signed-in caller running out
  of attempts does not lock anybody else out, which is the whole reason the partition key is the user id
  rather than an address every request shares behind an ingress proxy.
- ~~**Actually sending an email or a push notification.**~~ Done, both against a stand-in rather than a
  real service. `SmtpEmailSenderTests` drives MailKit against `FakeSmtpServer`, a loopback listener
  speaking just enough of RFC 5321 - the only seam available, since `SmtpEmailSender` constructs its own
  client. `VapidPushNotificationSenderTests` hands `WebPushClient` a stub transport instead. Neither
  test is about the protocol libraries; both are about Orbit's own decisions around them: that an
  unconfigured deployment stays quiet and says so, that half-configured credentials count as no
  configuration rather than as something to try, and that a 404/410 from a push service comes back as
  `PushSubscriptionExpiredException` while a 503 does not - pruning on the latter would throw away a
  working subscription because the push service was briefly down.
- ~~**The browser-side encryption (`e2eeChat.js`).**~~ Done, and it is the gap that mattered most: every
  line of that file is Web Crypto and IndexedDB, bUnit executes neither, and the entire chat's
  confidentiality rests on it. `ci/verify-browser-crypto.mjs` runs the module itself in headless
  Chromium - serving `wwwroot` directly rather than booting Blazor, since the module has no dependency
  on it and `127.0.0.1` is a secure context. Fourteen checks: the round trip, a per-message nonce, a
  tampered message refusing to open, a stranger's key not opening it, two accounts in one browser not
  sharing a key, the password-wrapped backup and its restore, and the key surviving a reload. It runs in
  the `test` job on every pull request, not only on a deploy - a change that quietly weakens the
  encryption is not something to find out about afterwards.
- **The chat thread, `PushNotificationManager`, `pushNotifications.js` and `service-worker.js` still
  have no coverage.** These are the parts of the same entry that the browser harness does not reach:
  notification permission prompts, the push subscription lifecycle and the service worker's own
  activation are all things a headless browser can be made to do, but each needs a permission grant and
  a registered worker rather than a module import, which is a different and larger harness than the one
  now in place. The chat thread itself is a polling component whose interesting behaviour is timing.
- **Nothing runs on a pull request.** `main_orbit.yml` is triggered by a push to `main`, deliberately -
  its own header weighs billed runner minutes against a branch going unchecked until it lands, and
  production stays covered because the deploy job needs the test job. It is still worth naming here: a
  branch is tested by whoever remembers to run `dotnet test` on it. If the minutes ever stop being the
  binding constraint, a `pull_request` trigger on the `test` job alone is the cheapest thing to add
  back.
- **What Google actually does with an "Add to Google Calendar" link.** The URL is built and pinned by
  `GoogleLinkTests` - the shape of the dates, the RRULE, what is escaped - but whether Google renders
  a pre-filled event form from it has only ever been checked by reading its documentation. Opening
  the link in a browser that is not signed in to a Google account lands on a marketing page, which is
  Google's own path for an anonymous visitor and tells us nothing either way. Checking the real thing
  needs a signed-in Google session, which no automated test here has.

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
  (SKU, storage, backup retention - see [Azure setup](azure-setup.md#4-confirm-database-backups)).
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

## What the footer could grow into

The footer at the bottom of every page - and the phone's About row, which says the same three things -
currently carries the copyright year, the version, and a link to the licence
(`OrbitRelease` for the copyright and the licence, `OrbitVersion` for the build - see
[Functionality](functionality.md#which-build-this-is)). What it is missing, roughly in the order it would be
worth adding:

- ~~**The build, not just the version.**~~ Done: the footer reads `ver:0.1.17+gitHash:51536f3`, and
  pressing it grows the rest of the hash - see
  [Functionality](functionality.md#which-build-this-is). The number is no longer maintained by hand
  either; it is counted from the history, one per day on which a commit touched that project.
- **When it was deployed.** The year is a constant maintained by hand, which is honest but coarse: it
  answers "roughly when was this written", not "is what I am looking at the thing that was merged this
  morning". A build timestamp answers the second, and the second is the question people actually ask -
  though the commit hash now answers most of what it was wanted for.
- **A link to what changed.** The version means nothing to somebody who has not been reading the
  commits. A release-notes page, or simply a link to the repository's releases, is what makes a version
  number worth showing at all.
- **A health or status link.** Orbit already exposes `/health`, `/health/ready` and `/health/live`
  (see [Architecture](architecture.md)). A footer is where people look when something is wrong, and a
  link that answers "is it me or is it the server" belongs there rather than in a document.
- **Privacy and data handling.** Not yet written, and it is the one entry here with a deadline attached
  to it: an application that ends up in a store needs one, and the store is the place that will ask.
  What it would have to describe is unusual and worth saying plainly - most of Orbit's content is sealed
  client-side, so a large part of the answer is "the server cannot read it".
- **Making it reachable rather than only visible.** The footer sits at the end of the scrolling content,
  which is right for something read once. If it grows past three items it stops being a footer and
  becomes an About page, and the honest move at that point is to give it one and leave a single link
  behind - the phone has already made that choice, since it has no footer to put anything in.

Deliberately not there: a language switch (it is in the avatar menu, where the rest of the account's
settings are), and anything that has to be fetched. A footer that waits on a request is a footer that
sometimes is not there.

## Smaller identified follow-ups

- ~~**Reordering by hand needs a mouse.**~~ Done: each handle now carries a pair of move-up/move-down
  buttons (`ReorderControls`, `RowArrangement.Move`), which a keyboard can use as well - a handle you can
  only drag is a handle only a mouse can use. Below the 680px breakpoint the whole control is hidden
  rather than left there doing nothing when pressed: arranging by hand is a wide-screen affordance, and
  an arrangement made there is still read on a phone. True dragging by finger (pointer events, with the
  hit-testing and autoscroll that needs) was weighed against this and not taken - it cannot be covered by
  any test this project can run, while the buttons are covered end to end.
- ~~**A group has no "last message" time.**~~ Done: `ChatGroup` carries `LastMessageAtUtc`, stamped where
  the message fan-out is written, so the one conversation list sorts people and groups against each other
  by when something last happened. A group nobody has written in yet answers with the day it was made,
  which keeps the order total without a second rule.
- ~~**An established contact can disappear.**~~ Settled the second way: the gate stays as it is - an
  account that has not unlocked `Contacts` is unfindable, and a lookup for it answers exactly as a lookup
  for nobody does, because "found, but hidden" would be finding them. What changed is that `ContactInfo`
  now says so. It reads the contact entry whether or not the profile resolves, which is what lets it tell
  somebody you talk to from an id that means nothing: for the former it names them from the conversation,
  says the two possible reasons and that Orbit cannot tell them apart from here, and states plainly that
  the messages are unaffected; for the latter it says there is nothing to show and why it cannot be more
  specific. It used to say "Orbit can't reach that account right now" to both, which read as a fault
  Orbit was having.

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
