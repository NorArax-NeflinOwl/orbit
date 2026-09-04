# Future Plan

This document collects the work that is known to be planned or still missing, drawn from what the
rest of the documentation already flags as "not implemented yet," a deliberate first-version scope
cut, or an identified follow-up. It is not a committed roadmap with dates — it is the current honest
picture of what's left.

**Last checked against the code on 2026-09-04.** A plan is only worth reading if it describes the
present. Anything below that says "not started" or "no coverage" was checked against the repository on
that date rather than carried forward on trust.

Since the last pass: every table and column was renamed to the Orbit convention and a storage is an
*inventory* everywhere, which is what stops a 0.2.x Android build (see
[Current Status](current-status.md#the-mobile-client)); the assistant's first model round trip exists on
the server, and a measured answer to whether a small local model could do the language half is in
[Local model measurements](ai-assistant-local-model-measurements.md); two of the four things this
document held back for one migration have shipped, so [that section](#what-the-ui-pass-still-needs-a-migration-for)
is down to two; the six objects each have a page that reads and a form one press further in, so the
unevenness recorded under [Smaller identified follow-ups](#smaller-identified-follow-ups) is gone and
only the inconsistency beside it is left; and a task entry's own field is one line that offers names
from everywhere they get typed. What this pass found and did **not** fix is in
[Known scope cuts and rough edges](#known-scope-cuts-and-rough-edges) below.

## Planned features

- **.NET MAUI client (mobile and desktop).** The long-term target architecture is a shared ASP.NET
  Core API backing a .NET MAUI client so every device stays in sync (see the top-level
  [README](../README.md)). **The mobile half is built** — see
  [Current Status](current-status.md#the-mobile-client) for exactly how far, and
  [Orbit.Maui — Plan](orbit-maui-plan.md) for the design it was built to. Android is the verified
  head; iOS has not been run since phase 1, and desktop has not been started at all.

  What is left of it: the iOS head beyond phase 1 (deferred — no Apple developer account or signing
  key, which also blocks push there) and phase 8's iOS half — Live Activities, the Dynamic Island, the
  Action Button. Phase 8 is done on Android: every switch, picker, date or time picker and checkbox
  names itself to a screen reader and a test fails on one that does not, and the home screen widget is
  built and driven on a device (see
  [Functionality](functionality.md#the-home-screen-widget-android)).
  A push
  arriving while the app is in front of somebody now shows a banner on the navigation bar, which is
  where the browser shows its own; it honours `AllowMobileBanner` and the two settings that pace it,
  all three of which existed for this and had no reader on the phone — and, since 2026-09-01, no way to
  be set from it either: the phone's own banner was configured only from a browser. Push to an Android phone is delivered as of
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
- **An AI assistant for inventories and task lists.** Two steps of it stand; the useful part does not.
  It is meant to suggest and correct what the user is typing, find duplicate items, explain what Orbit
  can do, and propose calendar events linked to the right task lists. It is deliberately shut out of
  private items and out of chat entirely - it is not a party to any conversation, and the messages are
  sealed so there would be nothing to give it. The whole design, the model and hosting decision, and the
  order to build it in are in [Orbit Assistant — Plan](ai-assistant-plan.md); the file-by-file version of
  steps 3 onwards is in [Orbit Assistant — Build Plan](ai-assistant-build-plan.md).

  **What is built.** Step 1, the half that needs no model: names the reader already has, offered as they
  type, with a warning when what is being typed is a name they already use (see
  [Functionality](functionality.md#names-you-have-already-used)). And step 3's first round trip:
  `POST /api/assistant/messages` answers one question through `Microsoft.Extensions.AI`, against Ollama
  on a laptop (`docker compose up -d ollama`) or a hosted model in production, and says "not configured"
  where neither is set.

  **What is not.** Everything that would make that round trip worth having: no context is assembled, so
  the model is told none of the reader's data and is instructed to say so rather than invent; no tools,
  and so no proposals to apply; nothing remembered between questions; and no surface in either client -
  the web and the phone contain no assistant code at all. Step 2, merging the duplicates step 1 already
  finds, is not started either.

  Two things are worth knowing without opening the plan. **Half of what was asked for is not a language
  model's job**: typeahead and duplicate detection are trigram similarity searches over the user's own
  data, which PostgreSQL answers in milliseconds for nothing and more correctly than a model could. And
  **the model should not be self-hosted**, which is now measured rather than argued - see
  [Local model measurements](ai-assistant-local-model-measurements.md). A 3B model on a CPU-only laptop
  corrected at most one of eight real Polish spelling errors while changing names that were already
  correct, and its latency had no floor when the machine was busy (181 s for a 39-token reply under
  load). A small hosted model in Azure AI Foundry costs cents a month at this size. Ollama stays, for
  local development only.

## What real Google Calendar sync would take

**Waiting on infrastructure, deliberately, as of 2 September 2026.** Google's review of a sensitive scope
asks for authorised domains the applicant owns, and for a privacy policy and terms of service hosted on
one of them. Orbit is served from a hostname Microsoft owns
(`orbit-web.…azurecontainerapps.io`), which cannot be verified as Orbit's, so nothing here can start
until three things happen in this order:

1. a domain is bought;
2. a **production environment of its own** is carved out in Azure, separate from what is running now;
3. DNS points the domain at it, and the OAuth client's authorised origins and redirect URIs are
   re-issued against that name.

Only then is there anything to submit. The order matters and none of it is code: the review is measured
in weeks *after* the domain exists, so buying it is what actually starts the clock. Everything in
[google-calendar-api-plan.md](google-calendar-api-plan.md) - what maps onto what, which of Orbit's
expectations the API narrows rather than meets, and the task-by-task breakdown - stands as written and is
ready to start the day that infrastructure is there.

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
The task-by-task version of it - what maps onto what, which of Orbit's expectations the API narrows
rather than meets, and what has to be decided before any of it starts - is in
[google-calendar-api-plan.md](google-calendar-api-plan.md).

## Known scope cuts and rough edges

Explicitly called out in the functionality documentation as deliberate limitations of this first
version, so they aren't mistaken for oversights:

- ~~**`pg_trgm` may not be allowed on the deployed database.**~~ It was allowed: the deploy on
  2026-08-31 applied the migration and `orbit-api` came up healthy, with `azure.extensions` empty. The
  warning was over-stated - the allowlist is not the absolute gate it is usually described as, at least
  not for this extension on this server. What remains true, and is kept in
  [Azure setup](azure-setup.md#3-allow-the-pgtrgm-extension), is the shape of the failure if a different
  server ever does refuse: migrations run at startup, so the API simply would not start, and CI would not
  see it first because its smoke test uses a plain `postgres:18-alpine`.
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
- ~~**`PushNotificationManager`, `pushNotifications.js` and `service-worker.js` have no coverage.**~~
  Closed the same way, by `ci/verify-push-notifications.mjs`. It registers the real worker, grants the
  permission, and delivers real push events through Chrome DevTools' `ServiceWorker.deliverPushMessage`,
  which turned out to be the piece that made this reachable at all: what a headless browser cannot be
  made to do is receive a push from a push service, and this side-steps that entirely by handing the
  worker the payload directly. Ten checks, covering what is shown for a good payload and for the three
  bad ones a push service can still deliver. The C# half is `PushNotificationManagerTests`. Two things
  are still out of reach and are named in the script: `notificationclick`, since nothing outside the
  operating system can click a system notification, and subscribing for real, which needs a push service.
- **The chat thread still has no coverage.** It is a polling component whose interesting behaviour is
  timing.
- ~~**Nothing runs on a pull request.**~~ Put back, cheaply. The trigger was removed because every
  billed minute counted and a day of ordinary work exhausted the allowance; what changed is that a run
  now costs a fraction of what it did. The android job looks before it builds and does nothing when
  nothing it builds from changed, a pull request run is cancelled by the next push to the same branch,
  and documentation-only branches are skipped outright. The deploy job stays out of it either way -
  guarded on the event as well as gated on the suite.
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
  [Azure Container Apps setup](azure-setup.md). The public URL is at least written down in production
  now: `WebClientBaseUrl` on `orbit-api` holds `orbit-web`'s own address, set 2026-09-04 so a
  shared-item email carries a link. What is still open is recording it somewhere a reader of this
  repository can find, rather than having to ask Azure for it.
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

## What the live connection still leaves undone

The web client now hears about chat, notifications and presence instead of asking - see
[Functionality — Live updates](functionality.md#live-updates). Three things were deliberately left out
of that change rather than missed.

- ~~**The phone still polls.**~~ Done, in its own change as this said it should be. The phone holds the
  same connection and only while the app is in front, started and stopped with the window: a socket held
  open behind a locked screen is one Android drops in Doze anyway, and what it would have carried is what
  push already delivers. Its chat polls slow to thirty seconds while it is up and snap back when it
  drops, the unread badge and the feed hear about changes instead of waiting for the next screen, and the
  presence heartbeat goes over the connection when there is one. It does not listen for
  `PresenceChanged`: the phone shows nobody else's presence yet.
- ~~**Edits and deletions are only found by the slow poll.**~~ Done, along with the rest of what was
  left announcing nothing: editing and deleting a message in a conversation or a group, making a group,
  adding or removing a member, changing a role, sharing history with a new member, and reading or
  clearing a notification - the last of which reaches this account's *other* devices, so a badge
  cleared on a phone does not stay lit on the laptop.
- **Somebody going *away* can never be announced.** It happens by time passing, with nothing calling
  anything, so there is no moment at which the server could say so (see `UserPresence.StatusAt`). Making
  it instant would mean the server tracking timers per connected account and announcing on expiry - real
  work, for a transition nobody is usually watching. The slow poll resolves it, and that is the reason
  the slow poll exists.

Scaling `orbit-api` past one replica needs a backplane before any of this survives it - see
[Azure setup](azure-setup.md#5-confirm-ingress).

## The calendar that shrinks as you scroll - Android, not the web

Decided 2026-09-01, while the web calendar was being reshaped. **The web keeps what it has**: side by
side on a wide screen, and stacked - calendar above, list below - once there is no room for that. It
does not shrink as the page scrolls, and it is not meant to.

**The phone does, as of 2026-09-02.** On Android the calendar stays pinned while the list under it is
read, and minimises to a single row as soon as the reader scrolls past it:

| view | what is left when it is minimised |
|---|---|
| Day | one hour row |
| Month | one week row |
| Year | the month's name, and nothing else |

Why there and not here: a phone has one column and a thumb, so the calendar is either taking the
screen or getting out of the way, and the row that survives is the one the reader is standing on.
A desktop window has room for both at once, so nothing has to move - and a grid that resized itself
while somebody scrolled a list beside it would be motion answering a question nobody asked.

Not attempted on the web deliberately. It is scroll-and-viewport behaviour, which no test in this
project can cover, and the web has no problem for it to solve.

What is testable was kept out of the page: which row survives is a rule (`MinimisedCalendar`,
`CalendarViewModel.IsMinimised`, `HoursOnShow`) and is covered; the page owns only the scroll offset
that turns it on and the redraw that follows. The hour rule is the one worth restating: today keeps the
hour it is now, held inside the stretch there is to draw, and any other day keeps the hour its first
thing starts in - an empty row above everything the day holds would be the wrong answer.

## What the UI pass still needs a migration for

The web redesign was done except for four things of the same shape: each needed somewhere to store
something the database had no column for, so they were held together deliberately - one migration, one
deploy and one APK release rather than four of each. **All four have since shipped**, one at a time -
which is the answer to why the bundle was worth breaking up.

- ~~**Archiving a conversation.**~~ Done: `IsArchived` on a contact and on a group membership, with the
  fourth tab on the contacts page appearing only when it holds something. Archiving is a command like
  any other, so it holds across devices - which is what ruled out doing it in the browser alone.
- ~~**A description under a name.**~~ Done, and on all three: a note, a task list and an inventory each
  take a title and a description as one control (`TitledDescription` in `TaskEditor.razor` and
  `InventoryEditor.razor`), first line the title and the rest the description. See
  [[task-list-description-deferred]] in the session memory for the phone's half and the two migrations
  it took.
- ~~**"Needed" on a shelf item.**~~ Done: `InventoryItem.IsCheckedRegularly`, and the restock list asks
  on `BelongsOnTheRestockList => IsBelowMinimum || IsCheckedRegularly` - so a thing checked every week
  is asked for whether or not it has fallen under a minimum.
- ~~**Sharing an inventory from its own editor**, with the Inventories card on the dashboard.~~ Done,
  and it needed no column after all - which is why it was the last of the four left standing. The panel
  that was written inline on the inventory list card is now `ShareInventoryPanel`, shown from both
  places a shelf is met; the dashboard gained an Inventory card beside the others, opening
  `/inventory/{id}` the way every other card on that page opens what it names.

Everything else on the pass - the top bar, the shared card and its footer, the calendar, the task and
inventory lists, the contacts tabs, the chat menus - is built and needs no schema change.

## Noticed while working

Written down rather than fixed on the spot, per rule 14 in `.claude/CLAUDE.md`: work that turns up
beside a task belongs here, not in that task's diff. A defect is the exception and is fixed when found.

- **`setup-dotnet@v4`, `setup-java@v4` and `upload-artifact@v4`** carry the same Node 20 deprecation
  `actions/checkout` did. `dependency-submission.yml` already pins `setup-dotnet@v5`, so the bump is
  available whenever somebody wants it.
- **The `android` job in `main_orbit.yml` starts a runner even when nothing mobile changed.** It
  detects that in its first step and exits in seconds, but a started job is billed a whole minute -
  about 29 of the 346 minutes measured. Moving the phone-head compile into its own workflow with a
  `paths:` filter would skip the runner entirely on the pull requests that do not touch it.
- **Nothing enforces that work reaches `main` only through `Coding`.** `guard-main.yml` closes stray
  pull requests, but a direct push to `main` deploys before any workflow can run. Real branch
  protection needs GitHub Pro on a private repository.

## Smaller identified follow-ups

- **Done, kept here as the map of it.** Orbit has two depths for the same thing: a shallow view for
  reading and doing, and a full form for changing what it is. Every object that can have both now does,
  and the pattern is the same one each time - land on what the thing is, with the fields a named press
  further in, and whatever light doing belongs to that thing offered where it is read. Which is which:

  | Object | Shallow view | Full form |
  | --- | --- | --- |
  | Task list | `/tasks/{id}` - the checklist: tick items, see the tree of lists it stands for, measure it against a storage | `/tasks/{id}/edit` |
  | Task entry | `/tasks/{listId}/items/{itemId}` - `TaskItemSummary`: when, where, what the appointment is about, who is coming, and a map | the entry's own row in the list's editor |
  | Note | `/notes/{id}` - `NoteSummary`: the note read, with the checklist lines in it tickable | `/notes/{id}/edit` |
  | Calendar event | `/calendar/{id}` - `CalendarEventSummary`: when, where, what it is about, who is coming, its reminders, and the place on a map | `/calendar/{id}/edit` |
  | Storage | `/inventory/{id}` - the shelf read rather than edited, one row per batch: what it is, how much, when it arrived, how long it keeps | `/inventory/{id}/edit` |
  | Contact / group | `/contacts/{userId}`, `/chat/groups/{id}/info` - read-only cards about who somebody is | no form; membership is edited on the roster |

  The unevenness this used to record is gone: when it was written a note, an event and a storage had
  nothing between a card and a whole form, and each of the three has had its own reading page since
  (`NoteSummary`, `CalendarEventSummary`, the shelf at `/inventory/{id}`), all built to the same shape
  as part of the screen-ladder pass. A contact and a group are the deliberate exception - they are read
  and never edited as objects, so there is no second depth to give them.

  One thing is still wrong with it, and it is the smaller half: the shallow view and the full form are
  reached inconsistently. `OnBodySelected` opens the *full* editor for a note and the *shallow* view for
  an entry, which is the same gesture meaning two different things. Worth settling what a card's body is
  for across all of them before adding a sixth answer.


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

- ~~**The phone has not caught up with the browser's last few passes.**~~ Done. The Google link and the
  verified-address filter are the same builder as the browser's with tests on both sides
  (`Orbit.Mobile/Google/GoogleCalendarEventLink.cs`); the calendar's list leaves out what is over; a task
  entry's own screen now says what the appointment is about and who is coming, above the map, as the
  browser's page does; and a press on an entry that stands for another list names that list and offers to
  open it rather than ticking off something the server will only overwrite.

  What is left is a difference in shape rather than a gap: the browser split each object into a page that
  reads and a form one press further in, where the phone keeps one screen that does both - a note's lines
  are ticked where they are written, and a shelf is counted up and down on the same screen that edits it.
  That is the right answer for a phone, so it is recorded here rather than queued.

- **The phone cannot flatten a tree of lists.** The browser's checklist reads a group list either as the
  stack of cards it is or as one run of items labelled with the list each came from, and remembers which
  (`ChecklistView`). The phone now matches everything else in that menu - the three orders, the stock
  panel's folding and its four orders - but has no flat view, and its checklist draws one list at a time
  rather than the tree. Worth doing after the tree itself is drawn there; flattening a view that does not
  nest would change nothing.

- **A switch's thumb cannot be coloured on Android.** Orbit's style asks for an accent thumb; Android
  paints it from the Material theme instead, and saying it again through `SwitchHandler.Mapper` does not
  change that (tried on a device: the track follows the accent, the thumb stays grey). The accent now
  goes on the track, which is where it lands and where the browser fills its own toggles in. Worth
  revisiting only if MAUI's Android switch handler grows a thumb tint that sticks.

- ~~**A permanent, bind-mounted TLS certificate setup for local development.**~~ Done, without
  touching the committed `docker-compose.yml`: the certificate lives in a folder of the developer's
  own and a gitignored `docker-compose.override.yml` mounts it over `/etc/nginx/certs`, which Compose
  reads automatically. `docker compose down -v` no longer costs the certificate. The steps are in
  [`info/instructions.md`](instructions.md), along with what an untrusted certificate looks like from
  inside the app — every `.wasm` and every `/api` call failing with `TypeError: Failed to fetch`,
  which reads as a broken build rather than as the certificate it is.
- **Self-hosting the Nominatim reverse-geocoding endpoint.** The calendar's map location picker
  currently calls OpenStreetMap's free, public Nominatim instance (see
  [Functionality — Calendar](functionality.md#calendar)), whose usage policy caps it to light,
  non-commercial traffic. A deployment with real usage volume should self-host Nominatim instead.
