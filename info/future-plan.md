# Future Plan

This document collects the work that is known to be planned or still missing, drawn from what the
rest of the documentation already flags as "not implemented yet," a deliberate first-version scope
cut, or an identified follow-up. It is not a committed roadmap with dates — it is the current honest
picture of what's left.

**Last checked against the code on 2026-09-04, and in part again on 2026-09-07.** A plan is only worth
reading if it describes the present. Anything below that says "not started" or "no coverage" was checked
against the repository on that date rather than carried forward on trust.

The 2026-09-07 pass was partial and it is worth knowing which parts, so the rest is not read as freshly
verified: [Testing gaps](#testing-gaps), [What the footer could grow into](#what-the-footer-could-grow-into)
and the entry about a card's body under [Smaller identified follow-ups](#smaller-identified-follow-ups).
Two of the three had drifted - the footer section described a footer two changes old and called a page
"not yet written" that has been serving since, and the card entry named a note behaving in a way it had
stopped behaving in `7e1504f5`. **Both were stale in the direction that costs a session**: each named
work that was already done, and a session choosing what to do next reads this file first. Everything
outside those three sections still carries its 2026-09-04 date.

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

## What the invitation page still owes

Done on 2026-09-07, as asked for on 2026-09-06: a share's notification leads to **what was shared**
rather than to the conversation - see [In-app notifications](functionality.md#in-app-notifications) and
`ShareInvitation.razor`. Two things about it are worth knowing:

- ~~**It does not name the thing.**~~ Done the same day: `GET /api/shares/{kind}/{shareId}` answers with
  the offer - the item, its name and whether it has been taken up - so the page says which note, and
  accepting lands on the thing itself rather than on the list it appears in. One endpoint for all four
  kinds; accepting stayed on each section's own, where that kind's rules are.
- ~~**The phone still has no invitation screen.**~~ Done 2026-09-10: `InvitationViewModel` and
  `InvitationPage` read the same path and the same `GET /api/shares/{kind}/{shareId}`, and accept
  through the one place that already knew which endpoint each kind uses (`SharedItemAcceptance`). Two
  differences from the browser, both because the phone stores things under its own ids: what is accepted
  appears when the section next syncs, so "open where it landed" is the section rather than the thing. A
  shared position, and a kind newer than the build, still open the conversation - which is what every
  kind did before, kept for the cases with no offer to show.

## What a real advertising network would take

The slots exist and are filled by Orbit itself - see [Advertising](functionality.md#advertising). Putting
somebody else's adverts in them is a bigger decision than swapping the source, and these are the parts
of it:

- **Consent, first.** Orbit withholds the map's tiles from a reader who has said not to share their
  information (`mapTiles.js`), and that is one request to one host that is told nothing but a tile
  coordinate. An advertising script is told who is looking, from where, and on which page, and it runs
  in the reader's browser. It belongs behind the same gate at least, which means `DoNotShareDialog` and
  the account-level flag behind it grow a third answer, and a slot that draws nothing when the answer is
  no - not a slot that quietly draws a house advert instead, which would make the two indistinguishable.
- **A content security policy.** Orbit serves its own scripts and nothing else today. Loading one from a
  network means naming that host in nginx's CSP (`nginx-app-locations.conf`), and every host it in turn
  loads from - which for most networks is a list nobody can enumerate in advance.
- **An account, and keys.** A publisher id is configuration, so it follows the rule every secret here
  follows: an environment variable or a Container Apps secret, never a tracked file, and
  `.env.example` updated with it.
- **The phone is a separate integration.** The web's script does nothing in a MAUI app; that is a
  platform SDK, an Android permission review and a second account.
- **What the slots would then be worth measuring.** Nothing here counts an impression or a press. That
  is fine while Orbit is advertising itself, and it is the first thing a network asks for.

One smaller thing is owed even without a network: there is **no interrupting advert on the phone** at
all, only the bar.

**The phone's bar is shown to an account holding Debugger** (noticed 2026-09-11). The browser keeps every
advert away from such an account until "Allow ads" is switched on under Options → Debug
(`AdAudience`, `DevicePreferences.AllowAdsForDebugger`); the phone's `AdBanner` draws for everybody, and
its own Debug tab (`AccountViewModel.IsShowingDebug`) has no such switch. It needs the same rule: a
Preferences key the Debug tab flips, off by default, and the bar asking one place - the phone's
permission state and that key together - rather than each screen's banner deciding for itself. Not
built with the web's, because the phone's switch is a screen of its own and the bar is on every screen.

~~The Android bar is **not tappable**.~~ Done 2026-09-10, and the reason it was not had stopped being
true: the app is told the API's address and never the web client's (`OrbitApiSettings`), but the
*server* tells it the web client's, and has since public share links needed exactly that
(`ClientFlagsDto.WebAddress`). So a press builds the page's address out of that answer and opens the
browser - see `HouseAdLink`, which is also what says there is nowhere to go, for a deployment that has
not set a web address and for a phone that could not reach the server to ask. It is asked at the press
rather than when the bar is drawn: every screen carries one, and that would be a request per screen for
something nobody may ever press.

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

## Proving it is you before an account without a password is deleted

**Decided by the user on 2026-09-11: ask Google again. Steps one and two are built; step three waits.**
The server accepts `DeleteAccountRequest(Password, GoogleIdToken)` and, when a token is sent, deletes
only if it is a genuine sign-in for this account's `GoogleSubjectId` issued within ten minutes
(`GoogleIdentity.IssuedAtUtc`, `DeleteAccountCommandHandler.FreshGoogleSignIn`) - a token that proves
nothing is a refusal, and a Google-linked account whose password is forgotten may confirm with Google
instead. The web's Options shows Google's button to a passwordless linked account where Google is
configured, and keeps the typed address only where it is not. The phone does the same since the same
day (`AccountViewModel.ConfirmsWithGoogle`, `GoogleAccountLink.SignInAgainAsync`) - but only once the
rebuilt APK is installed. **Still to do:** once those builds are the ones in use, step three - refusing a
passwordless account that sends no token, with the emailed-code fallback below for a deployment without
Google. Until then the empty password is still accepted from
such an account, exactly as installed phones send it. What was written before building it:

Since 2026-09-11 an account with no password - made with Google and never given one - types its email
address or login before Options deletes it. That makes the press deliberate and proves nothing: the
server still accepts `DeleteAccountRequest("")` from it (`DeleteAccountCommandHandler`), and whoever holds
the session can read the address off the same page. The stronger answer is to ask Google again.

- **What it would be.** The client runs Google sign-in once more (the web's `GoogleSignInButton`, the
  phone's `GoogleSignIn`) and sends the fresh ID token with the request -
  `DeleteAccountRequest(string Password, string? GoogleIdToken = null)`. The server checks it with the
  `IGoogleIdentityVerifier` sign-in already uses, and deletes only when its subject is this account's
  `GoogleSubjectId` and it was issued moments ago, so a token kept from an earlier sign-in is no use -
  which means the verified identity has to carry the token's issue time, and today it does not.
- **What it buys.** A stolen session - a browser left signed in, a leaked refresh token - can no longer
  end the account, which today it can for exactly the accounts that have nothing else to prove
  themselves with. If the rule becomes "the password, or Google", it also lets a Google-linked account
  whose password is forgotten delete itself without a reset first.
- **The contract, and the order it has to change in.** Installed phone builds send `{ "password": "" }`
  and nothing else. The new field has to be optional with a default so their request still binds (see
  `RequestBindingTests`), and the server has to keep accepting the empty password until those builds are
  gone - requiring the token at once would make deletion fail on every phone already installed, and store
  review expects deletion to work inside the app (see [the mobile plan](orbit-maui-plan.md)). So: accept
  the token when it is sent; ship both clients sending it; only then require it. Every deployment in
  between is no weaker than today.
- **The cost.** Google's prompt can be declined, blocked by the browser, or missing - a deployment whose
  `GoogleClientId` is unset cannot show it at all, and an account made with Google there would have no way
  left to delete itself. That needs an answer before the token is required; an emailed code to the
  verified address, which password reset already knows how to send (`VerificationCodePurpose`), is the
  obvious fallback. It is also one more round trip to Google in the middle of the one flow nobody wants to
  fail.
- **What it does not change.** An account with a password keeps proving itself with the password.

## Known scope cuts and rough edges

Explicitly called out in the functionality documentation as deliberate limitations of this first
version, so they aren't mistaken for oversights:

- **Calendar events are not filed in folders, and will not be.** Decided by the user on 2026-09-09,
  when folders were given a page of their own (`FolderScope`). A folder holds notes and task lists;
  an event is found by when it happens, which is what the calendar is. Written down because it looks
  like an omission from the outside - the Finished tab's own wording used to say it "concerns tasks and
  events" - and because the change is not a small one: `OP_EVENTS` has no folder column, so this would
  be a migration, a third `FolderScope`, and a field on the event form.
- **The month and year calendar views stay filtered to what is still to come.** Also confirmed by the
  user on 2026-09-09, alongside making the week account for everything the way a day does (see
  `Calendar.ShowsEverythingInThisView`). They are read to find something rather than to account for a
  stretch, so a month drawn full of struck-through appointments is the thing being avoided rather than
  a gap. "Show → Everything, including what is over" still reaches it on both.

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
- ~~**One test was removed because it could not be made to fail on demand.**~~ Restored 2026-09-06, and
  the parallelism question turned out to have been answered the day after the removal by somebody
  fixing it for another reason. `NoteDetailScreenTests.Turning_private_off_puts_the_words_back_where_the_server_can_read_them`
  was taken out on 2026-08-31 for failing about one full-suite run in ten and never on its own; on
  2026-09-01, `afd0f7e2` "Give every local-store context a connection of its own" gave each test store a
  database of its own, and says in its own words that sharing one connection made SQLite refuse EF's
  user-function registration while a statement was open, so the failure "arrived only under load, on CI,
  in a test about something else entirely". That is the shape this one had. Nobody came back for it.

  The lesson worth keeping: a test parked for flakiness needs somebody to own going back, or the fix
  lands a day later and the coverage stays lost. This one was lost for a week.
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
- ~~**"Read" means "the chat was open", not "somebody looked at it".**~~ Done on both clients
  (2026-09-11): the mark-read routes take an optional `readUpToUtc` - the newest message actually seen -
  and mark nothing past it, while absent still marks everything for installed phone builds. The web marks
  only with the tab visible and the window focused, up to the newest message in view; the phone only
  with the page showing and the app in the foreground, up to the last line the thread shows. See
  [Functionality — What counts as read](functionality.md#what-counts-as-read).

  What this said before: A message is marked read by the thread that is polling for it (`Chat.razor`),
  which is a stand-in for the other party actually seeing it. Narrowing the poll so it stops while the
  tab is behind others made the stand-in closer to the truth than it was, but not equal to it: a thread
  open in a visible window nobody is sitting at still reports everything as read. A real signal - tab
  focus and scroll position, pushed to the server rather than inferred from a poll - is still open, and
  is worth having before read receipts are shown to the *sender* as a promise rather than kept as an
  unread count for the reader.
- **The phone's "seen" rests on `CollectionView.Scrolled`, which is not device-verified.** The Android
  head builds and the decision is covered by view-model tests, but nobody has yet watched a mark leave a
  real phone. Android's `RecyclerView` reports a scroll after every layout that changes what is visible,
  so a thread that opens at its newest line should report it; iOS's `UICollectionView` reports only
  actual offset changes, so a short thread that fits the screen without scrolling may never say what is
  on it and so never be marked read there. Check on a device before relying on it; if iOS is silent, the
  page needs a second source for the visible range once it has laid out.
- **Notifications about a web conversation are still cleared by the open window.**
  `Chat.razor`'s `ClearNotificationsForThisConversationAsync` marks the conversation's entries in the
  notification feed read on load and on every poll, whether or not the new message has been seen. The
  feed is "tidying, not reading" (the unread badge comes from the conversation, not from the feed), so it
  is not wrong in the way read receipts were, but a notification can disappear for a message nobody has
  looked at yet. The same seen signal (`ChatReadState`) could drive it.
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
- ~~**The chat thread still has no coverage.**~~ Done, and the reason it was open turned out to be the
  reason to do it: what a polling component decides is invisible from the screen either way. A poll that
  stops honouring the tab's visibility costs money and battery and looks identical; a poll that reads the
  whole roster on every tick was two thirds of this page's traffic and looked identical too.
  `ChatThreadTests` pins both, plus the two ways the loop has to stop and the `Chat` page's own
  explanation for an account the API will not resolve - which was the *other* entry on the not-covered
  list, held open by the cost of standing this page up under bUnit at all. Each was checked by removing
  the behaviour and watching its own test go red.

  The technique is what is worth keeping, and it is written up under
  [The chat thread](testing-and-running-locally.md#the-chat-thread): the tests wait for the loop's own
  ticks rather than for the clock, counted off the visibility question it asks before deciding anything
  else - because "nothing was polled" is also true of a loop that never started. bUnit's
  `WaitForAssertion` is no use for it: it re-checks on a render, and a tick behind a hidden tab renders
  nothing.

  Still out of reach, and named in the class: `OnChatAnnounced`, since `LiveUpdatesConnection` raises
  its events from inside itself and nothing outside can, so the live-connection half of the pace
  (`ConnectedPollInterval`) is reasoned about rather than driven.
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
- **A production environment beside the test one.** Agreed with the user on 2026-09-04: the
  environment running today becomes *test* and keeps its
  auto-deploy, a new resource group with a custom domain becomes *production*, and production installs
  the same `sha-<commit>` image that ran on test, from a release queue (a `workflow_dispatch` that
  writes the tag and the time to `deploy/production-schedule.json`, and a cron that applies it when
  due, reusing the health gate and rollback). Waiting on three decisions before any resource is made:
  which domain, the production Postgres SKU (B1ms proposed), and whether production starts empty.
  **What did not have to wait is done**: `orbit-web` no longer carries the test environment's
  `orbit-api` name in its image - `ORBIT_API_HOST` names it at startup (2026-09-10,
  `point-nginx-at-the-api.sh`), so the second environment runs the same image with one variable set.
  The domain is also what [Google Calendar sync](#what-real-google-calendar-sync-would-take) waits on.
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

**Re-read against the code on 2026-09-07, and most of this section had already happened.** What it
described - a footer carrying the copyright year, the version and a link to the licence - is two
changes out of date. The numbers moved into a dialog (`AboutDialog`: this build, the *server's* build
beside it, and the licence), and the footer kept words instead: About, Privacy, Security, Docs, Status,
the licence, Manage cookies, and Do not share my personal information. `OrbitRelease` holds the
copyright and the licence name, `OrbitVersion` the build - see
[Functionality](functionality.md#which-build-this-is).

What it is still missing, roughly in the order it would be worth adding:

- ~~**The build, not just the version.**~~ Done: the footer reads `ver:0.1.17+gitHash:51536f3`, and
  pressing it grows the rest of the hash - see
  [Functionality](functionality.md#which-build-this-is). The number is no longer maintained by hand
  either; it is counted from the history, one per day on which a commit touched that project.
- **When it was deployed.** The year is a constant maintained by hand, which is honest but coarse: it
  answers "roughly when was this written", not "is what I am looking at the thing that was merged this
  morning". A build timestamp answers the second, and the second is the question people actually ask -
  though the commit hash now answers most of what it was wanted for, and the About dialog answers the
  rest of it from the other side by showing the **server's** build beside the client's, which is what
  catches a browser holding a cached client. What is left for a timestamp to add is small enough that
  it is worth weighing against where it would have to come from: the stamp is made by
  `ci/compute-version.sh` at build time, so this is a change to the build rather than to a page.
- **A link to what changed.** The version means nothing to somebody who has not been reading the
  commits. A release-notes page, or simply a link to the repository's releases, is what makes a version
  number worth showing at all. One thing settles which of those two it has to be: **the repository is
  private**, so a link to its releases is a 404 for everybody but its owner - and there are no releases
  cut there anyway, since a deploy is a merge of the integration pull request. A page Orbit serves
  itself is the only version of this that works, the same reasoning that put the licence on `/license`
  rather than linking the file on GitHub.
- ~~**A health or status link.**~~ Done, and it needed more than a link: nothing on the web origin
  reached the report. nginx forwards `/api/` to the API *under* `/api/`, so `/api/health` arrived there
  as `/api/health`, which is not where health lives. There is now an exact-match `= /health` location on
  both nginx configs, and the footer's **Status** opens it in a new tab - which is also what stops the
  Blazor router claiming the address. Publishing the report was a decision taken deliberately; what it
  does and does not say is written down beside both the location and the writer.
- ~~**Privacy and data handling.**~~ Written: `/privacy`, linked from the footer and deliberately
  carrying no `[Authorize]` - what a service does with what you give it is a question somebody is
  entitled to an answer to *before* handing anything over, so the sign-in page's footer reaches it too.
  It says the unusual part plainly, which was the point: most of Orbit's content is sealed in the
  browser, so a large part of the answer is that the server cannot read it. `/security` and `/docs`
  went the same way. The deadline this entry carried - a store will ask for one - is met.
- ~~**Making it reachable rather than only visible.**~~ Answered, and not the way this predicted. It has
  grown well past three items, and rather than becoming an About *page* the numbers became an About
  *dialog*: what build this is has no address worth sharing and is read in the middle of doing
  something else. The footer kept the words, which is the shape the phone's own About row already had.

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

## The calendar that shrank as you scrolled - withdrawn 2026-09-09

Decided 2026-09-01, shipped 2026-09-02, taken out again on 2026-09-09. On Android the calendar stayed
pinned while the list under it was read and minimised to a single row as soon as the reader scrolled
past it - one hour of the day, one week of the month, one month of the year - and came back whole at
the top. The web never did this and was never going to.

**Why it is gone.** The week is one of four views the reader now asks for by name (Day, Week, Month,
Year, in a row across the top of the page), so a grid that shrank on its own was a second and silent
answer to the same question - one nobody could ask for, and one nobody could refuse. `MinimisedCalendar`
became `CalendarWeek`, which still picks the week out of the month grid that was already built; the
year's month and the day's hour went with the gesture that caused them.

What is worth keeping from the reasoning: a phone has one column and a thumb, so the row worth reading
is the one the reader is standing on. That is what the week view is for. Nothing about scrolling.

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

- ~~**Only the web's members page asks who takes over a group.**~~ Fixed 2026-09-11: the roster's
  question is `GroupLeaveConfirmation`, which the archive's "Leave and delete chat history" opens too, and
  the phone asks the same question (`GroupLeaveQuestion`, `GroupLeaveDialog`) from the group's own screen
  and from the group list, sending the choice as `successorUserId` (needs the rebuilt APK). As noticed:
  since 2026-09-11 a group's last admin may leave, naming a successor (`successorUserId` on
  `DELETE /api/chat/groups/{id}/membership`) or letting the server promote the longest-standing member
  (`ChatGroup.Leave`). The roster offered the picker; the archive (`Contacts.razor`) and the phone (group
  detail and group list) left without asking, so they always got the automatic choice.

- ~~**The phone's old leave sentence is a dead translation, and four pages still call the obsolete action
  sheet.**~~ Fixed 2026-09-11: the dictionary entry is gone and the seven calls are
  `DisplayActionSheetAsync`, with the same arguments; the Android head's CS0618s went with them. As
  noticed: since the phone asks the web's leave question (2026-09-11), "You stop receiving what is posted,
  and the group sees you go." in `PolishTranslations.cs` is no longer asked for anywhere; nothing fails
  on an unused key, so it will sit there until somebody removes it. And the Android head builds with
  CS0618 on `Page.DisplayActionSheet` in `TaskListDetailPage` (four calls), `CalendarEventDetailPage`
  (two) and `InventoryDetailPage` (one) - MAUI 10 wants `DisplayActionSheetAsync`, which
  `GroupLeaveDialog` already uses. Only warnings, and the head is not in `Orbit.CI.slnf`, so no gate
  catches them. What it would take: deleting the one dictionary entry, and renaming the seven calls.

- ~~**The phone's group detail screen leaves through the wrong route.**~~ Fixed 2026-09-11: the self row
  now calls `ChatClient.LeaveGroupAsync` (needs the rebuilt APK). As noticed: `GroupDetailViewModel.RemoveAsync`
  leaves by removing itself (`DELETE .../members/{ownId}`, `RemoveChatGroupMemberCommandHandler`), which
  takes the account out but leaves its copies of the group's messages in `OP_CHATS` for nobody
  to read - the leave route (`LeaveChatGroupCommandHandler`, which the phone's group list and both web
  pages use) deletes them. Harmless to anyone but untidy, and it grows with every leave. What it would
  take: calling `ChatClient.LeaveGroupAsync` for the self row, and a rebuilt APK; the server keeps
  accepting the old route for installed builds either way.

- ~~**Page and section descriptions are folded on the web, not yet on the phone.**~~ Fixed 2026-09-11:
  a screen whose name is in the bar hands its sentence to `NavigationBar.Description`, which draws the
  same "?" (`Controls/HintMark.xaml`, now shared with `FieldHint`) beside the name; the two sign-in
  neighbours fold theirs into a `FieldHint` beside their own heading (`LabelStyle`). What was folded and
  what was left in view is in `info/functionality.md`, "What a field is for"; `FoldedDescriptionTests`
  pins where each sentence lives now. The account screen was a separate pass.

- ~~**Two sentences under a place field are a "!" on the web and still a line on the phone.**~~ Fixed
  2026-09-11: each is a `FieldHint Warns="True"` labelled "Location", shown under the same condition, with
  the plain label standing in otherwise; the calendar one uses the web's key, and the phone's own wording
  left the Polish dictionary with it (needs the rebuilt APK). As noticed while folding the page descriptions: "Pick it on the map - a place with no point cannot be drawn on
  one." (`PlaceDetailPage.xaml`, shown while `NeedsAPoint`) and "The name is yours to write - the point
  is kept either way." (`CalendarEventDetailPage.xaml`) are `FieldHint Warns="true"` on the browser
  (`PlaceForm.razor`, `EventFields.razor`). They are under a field rather than under a title, which is why
  that pass left them. What it would take: a `FieldHint Warns="True"` whose `Label` is the field's own
  ("Location"), with the same `IsVisible` - and the calendar sentence is worded differently on the two
  clients ("the pin keeps its exact position"), so one of them moves to the other's key.

- ~~**`PageHeader.Subtitle` on the phone has no user left.**~~ Removed 2026-09-11, with the
  `PageSubtitle` style; `PageHeader` keeps its leading action and its far end. As noticed: after the page
  descriptions moved into the
  bar, `AccountPage.xaml` was the last to set it, and since 2026-09-11 its sentence is behind the bar's
  "?" too (`NavigationBar.Description`). The property and the `PageSubtitle` style draw nothing now and
  can go; `PageHeader` itself stays, for the Groups page's leading "+".

- ~~**A few sentences under a control, rather than under a title, were left in view on the web.**~~ Folded
  2026-09-11 at the user's request. Each is now a `FieldHint` beside the control it follows: a "?" on
  the restock switches (a "!" on the first one while it is off, since that sentence says what turning it
  off costs), the claim buttons and the Android download. A "!" on the share link's note, on
  `FeatureLocked`, and on the chat password gate's two warnings. `ForgotPassword.razor` still carries
  its own copy of the reset warning in view, because it was not on this list. As noticed: The
  restock switches' three `<p class="field-hint">` lines in `InventoryEditor.razor` and the one in
  `GenerateInventoryOverlay.razor` change with the switch they follow. They had no CSS rule at all and
  rendered as body-size paragraphs until 2026-09-11, when `p.field-hint` was given the small print's style. Also kept: the two lines under the
  claim buttons on `SharedItemPage.razor`, `ShareLinkButton`'s note once a link exists, the unknown-sources
  note on `Download.razor`, `FeatureLocked`'s explanation and `ChatPasswordGate`'s. Each either describes
  a live state or is the only thing on its screen; folding them is a judgement a later pass may still
  want to make, with a `Warns` "!" for the ones about a state.

- ~~**The phone's export does not offer places.**~~ Fixed 2026-09-11: a Places switch that starts off,
  the browser's warning beside it while it is on, and `TransferClient.OpenPlacesAsync` opening each sealed
  place before the file is written; one it cannot open goes out empty and is counted on screen, and the
  import message names five counts (needs the rebuilt APK). As noticed: The browser gained a Places box on 2026-09-11 that writes
  every place out opened, behind a warning (see [functionality](functionality.md#taking-places-out-in-a-file)).
  The phone's `ExportChoice.Narrow` empties places instead, because passing the server's rows through
  would write sealed places nobody could read, and its import message still names four counts, not five.
  What it would take: an `IncludesPlaces` switch that starts off, the same warning beside it in
  `AccountPage.xaml`, and opening each sealed place with the key `LocalPlaceRepository` already uses
  (`SealedContentSerializerContext.Default.SealedPlace`) before `TransferClient.Write`. Import already
  sends places closed, so that half is done.

- ~~**An exported place loses its link to a private list.**~~ Fixed 2026-09-11, on the server alone: a
  link to a private list travels as the nonce of that list's sealed half (`ArchivedPlace.SealedTaskLists`,
  `ArchivedTaskItem.LinkedSealedTaskLists`), which the list carries in the same file, and import finds
  it by that. Doing it this way also fixed a defect nobody had noticed. An entry linking to a private
  list used to be written with the empty title, and import sent that link to whichever private list it
  made first. A private list is no longer found by title at all. As noticed: A private task list's title is empty on the
  server, and the archive carries links by title, so `ExportArchiveQueryHandler` drops such a link rather
  than writing a title that would match whichever untitled list came first. A task entry linking to a
  private list has always had the same gap. What it would take: the browser opening the private lists'
  titles while it opens the places, and writing those in - which only helps when lists are exported too.

- ~~**A wrong password on a signed-in endpoint is retried as if the session had expired**~~ Fixed the
  same day (2026-09-11): both clients' `AuthorizationMessageHandler` return a 401 from these two requests
  untouched unless it carries a bearer challenge, which only an expired token's refusal does. As first
  noticed, while looking at account deletion: `DELETE /api/users/me` and `PUT /api/users/me/password`
  answer 401 for a wrong password, and `AuthorizationMessageHandler` reads every 401 outside the sign-in
  paths as an expired access token: it spends the refresh token, rotates the pair and sends the request
  again. The answer is still right, but each wrong try costs two of the five a minute the `Auth` rate
  limit allows, so the third wrong try in a minute comes back 429 - which Options reports as the generic
  "Couldn't delete your account. Try again." (or "Couldn't change your password") rather than as a wrong
  password, and trying again is what keeps the window shut. What it would take: those endpoints
  answering a wrong password with something other than 401 (403, or 400 with a reason, as login's
  `LoginRejectionDto` does), and every client that branches on the 401 changed in the same breath -
  both web pages, `AccountClient` on the phone and `FakeUsersServer` in its tests.

- ~~**The phone's delete-account form has the dead end the web's had**~~ Fixed 2026-09-11: a
  Google-linked account is told which password is meant, every account asked for one gets "Forgot your
  password?" to the reset screen, a passwordless one types its address or login before the platform
  prompt appears (`AccountViewModel.IsReadyToDelete`), nothing is sent before the account has loaded, and
  the refusals are said inside the danger card rather than at the top of the screen (needs the rebuilt
  APK). As noticed (2026-09-11): For an
  account with a password it shows an Entry whose placeholder is "Password" and nothing else: no word
  about which password a Google account holds, and no way to the forgotten-password screen, which the
  phone only offers from sign-in. The fix is the phone's own (`AccountPage.xaml`, `AccountViewModel`),
  not shared with the web's; the same hint and a way to the reset screen would do it. Nor does it ask an
  account **without** a password to type its address or login before deleting, as the web has since
  2026-09-11 - it deletes on the platform prompt alone. Also the phone's own to build: a field bound in
  `AccountViewModel` beside `RequiresPasswordToDelete`, checked against the account it loaded.

- ~~**The phone account screen's other forms answer at the top**~~ Fixed 2026-09-11: each form says what
  became of it under its own button (`AccountViewModel.UserNameMessage`, `EmailMessage`,
  `PasswordMessage`, each a `FormMessage`), in the accent colour or the danger colour on a refusal, and the
  shared `Message` line is gone (needs the rebuilt APK). As noticed (2026-09-11, while folding that
  screen's section sentences). Its own subtitle, the other leftover noticed then, is behind the bar's "?"
  since the same day (`NavigationBar.Description` in `AccountPage.xaml`). The
  username, email and password forms still report into the one `Message` line at the top of the screen,
  which the deletion no longer uses for the reason it stopped: the reader is further down when they
  press. What it would take: a message line per form, as `DeletionMessage` is.

- ~~**Account deletion leaves the account's own rows keyed on anything but `UserId`.**~~ Fixed 2026-09-11:
  `AccountDeletionRepository.DeleteWhatTheAccountHandedOutAsync`, and the sweep test now finds entities by
  `OwnerUserId` and `SharerUserId` as well. As noticed: the sweep test
  finds entities by a property called `UserId` (`AccountDeletionSweepTests.Every_entity_owning_a_user_is_covered_by_this_test`),
  and `AccountDeletionRepository` deletes by it. Rows the account owns under another name stay: the
  shares it granted (`OP_*_SHARED.*_OWNERUSERID`), its contact list (`OL_CONTACTS` owner), its public
  links (`OL_PUBLIC_SHARES` owner) and the positions it shared (`OP_LOCATIONS` sharer).
  `IAccountDeletionRepository` says leaving references in *other* people's data is deliberate; these are
  the deleted account's own, and a deletion somebody asked for should take them. None of them blocks a
  deletion - nothing in the schema has a foreign key to `OS_USERS` - so this is tidiness and privacy, not
  a failure.

- ~~**A heartbeat that races an account deletion answers 500.**~~ Fixed 2026-09-11 with
  `IUserRepository.TryUpdateAsync`. As noticed: `PresenceHeartbeatCommandHandler` reads
  the user, then saves it with `UserRepository.UpdateAsync`; if the deletion commits between the two,
  the update affects no row and EF throws. The account is gone either way and the browser signs out
  right after, so all it costs is an error in the log - but an update of a row that may have gone should
  be a conditional update, not a read and a blind write.

- **Handing an appointment to a contact still happens in its form - decided to stay so** (the user,
  2026-09-11: guests stay in the form, and only the share link is in the panel's menu). The note, task list and inventory
  forms moved Share and Share link into the panel's menu on 2026-09-11; the calendar event's form moved
  only its link, because giving an appointment to somebody is adding them as a guest (`EventFields`), and
  that is part of what Save writes - the invitation goes out once the event is saved
  (`CalendarEventEditor.ShareWithNewlyAddedContactsAsync`). A Share entry for an event needs its own
  answer to whether a guest added from the panel is saved on the spot or waits for Save; neither was
  obviously right, so the guests stayed where they are.

- ~~**Moving between notes in the editor's column carries the first note's way back.**~~ Fixed 2026-09-11
  with `ReturnTo.PastThePageOf`. As noticed: opening another note
  from the column replaces the form (`NavigationTrail`) and keeps the returnTo the first one was opened
  with. When that was the first note's own page, finishing the second note ends on the first note's
  page. What it would take: when `ComeBackTo` names the page of the note being left, hand the next form
  that page's own returnTo instead - it is on the address being replaced, so nothing has to be remembered.

- ~~**A task entry opened from the page of lists names no way back.**~~ Fixed 2026-09-11: the entry and
  the checklist are both opened naming `/tasks`. As noticed: `Tasks.razor` opened an entry's page
  as `/tasks/{list}/items/{item}` with no returnTo, so the entry's Back falls back to its list and
  replaces the entry's page with the checklist, rather than stepping back to `/tasks`. Naming itself -
  `ReturnTo.Link(..., "/tasks")`, as every other page that opens something now does - would make it step
  back.

- **Orbit.Web's pages read the machine's clock directly** - `DateTime.Today` and `DateTime.Now`, in
  eighteen places across the pages and components, with no `TimeProvider` injected anywhere in that
  client. It is why `DashboardTests.An_appointment_that_has_ended_counts_as_one_that_is_behind_the_reader`
  failed for the last three hours of every day until 2026-09-10 (an event "three hours from now" is
  tomorrow's after nine in the evening); the test was anchored to the ends of today instead, which is a
  patch on one test rather than an answer. What it would take: registering a `TimeProvider` in
  `Program.cs` and injecting it where a page asks what day it is - the phone has done this all along
  (`FakeTimeProvider` in every screen test), and it is the only way a page whose answer changes at
  midnight can be tested at all.

  **Measured on 2026-09-11, and held back on purpose.** It is 28 reads across 13 files, wider than the
  pages: `EventFormModel` reads the clock in its constructor and is made with `new()` from `TaskEditor`
  and `CalendarEventEditor`, `Calendar.razor`'s `ReferenceDate` lives on a nested state class, and
  `OrbitAuthenticationStateProvider` is constructed by hand in `Program.cs` and in `OrbitTestContext`
  (which is also where bUnit tests would get the `TimeProvider`). Seven of those files - `Program.cs`,
  `MainLayout`, `Dashboard`, `MapPage`, `Notes`, `Options`, and the two editors through the form model -
  are also changed by PR #279, so it waits for that to merge and is then done whole on a fresh branch,
  rather than half now and half after with a conflict in the middle. The session behind #279 was told.

- ~~**A response the phone cannot parse escapes the sync's own catch.**~~ Fixed the same day it was
  found (2026-09-10): `EverythingSynchronizer.TryAsync` catches `JsonException` too, and answers it the
  way it answers a server it could not reach - "couldn't sync", with everything still queued - rather
  than throwing out of a method every screen calls on a timer and on resume. It reads as unreachable
  rather than refused: nothing about a body this build cannot parse says the reader may not have what
  they asked for.

- ~~**A create the outbox has given up on leaves a row that never syncs.**~~ Done on 2026-09-11, with
  the first of the two designs this entry named. When a queued create is dropped - after five answered
  refusals (`SyncFailure.StaysInTheOutbox`) - the local row stays with no `ServerId`, and every later
  edit used to queue an update that is `Abandoned` quietly on a row the server has never seen. Now the
  next edit of such a row - a save, a filing, a folder rename, a review's "keep mine", a calendar link
  resolved onto a list - queues the *create* again instead, and the create carries what the edit
  changed (`LostCreates`, used by all six repositories and `PendingCalendarLinkResolver`). A copy
  awaiting review is left alone: it has no create on purpose.
  - **Not done, and chosen not to be:** the other design, a mark on the row the list can draw with
    "send again" under its menu. The retry is silent, so a row nobody edits again stays on the phone
    alone, and a server that keeps refusing it says so in the feed once per five tries - worded as
    "Kept on this phone only", and naming editing (renaming, for a folder) as the way to try again.

- ~~**Linking a list to a storage places nothing by itself**~~ Fixed 2026-09-11: the link handler places
  the list's product entries and settles the restock list, as a save does. As noticed (2026-09-11, with
  `ProductEntryPlacement`). `LinkTaskListToInventoryCommandHandler` only sets the link, so product
  entries already on the list reach the shelf on the list's next save rather than at the moment it is
  linked. What it would take: calling the placement from the link handler, and settling the restock list
  after it, the way the list's save does.

- ~~**Orbit.Web still writes a new product onto the shelf itself**~~ Removed 2026-09-11. As noticed: (`TaskEditor.SaveTheShelfAsync`, the
  `ShelfPicker.NewProductsIn` branch). Since 2026-09-11 the server has placed it before this runs, so the
  fresh shelf read finds it and the branch does nothing; a save the server declines is one the
  inventory request is refused for too. The branch can go.

- ~~**An entry matched to an existing row keeps nothing of what it described.**~~ Said before it happens
  since 2026-09-11: the web's entry form tells an entry whose name a row on the list's shelf already has
  that it will be matched and the row left as it is - and one whose name two rows share that it will be
  matched to neither (`TaskEditor.WhereANewProductGoes`). As noticed: matching leaves the row as
  it is on purpose (see `ProductEntryPlacement`), so a minimum typed on the entry is dropped with its
  description. A line on the entry's form saying "already on the shelf in X" before it is saved would
  keep that from being a surprise.

- ~~**Two lists pointing entries at one shelf row each ask for its whole minimum**~~ Fixed 2026-09-11: one
  set of counted shelf rows is shared across every list in the split
  (`StockRequirementCounter.DemandOf`'s `shelfItemsAlreadyCounted`). As noticed: in the shared-shelf
  split (`GetTaskListStockCheckQueryHandler.AskedForByTheOtherLists`), because a row's minimum is counted
  once per list (`StockRequirementCounter.RequiredBy`). Rare - generation points only its own list's
  entries at the rows it builds - but a hand-made errand on a second list makes both look short.

- ~~**The phone reopens an entry saved offline on a blank product form**~~ Fixed 2026-09-11:
  `ShelfForSomethingNew` hands the entry's own product to `TaskItemShelfProduct.ForSomethingNotOnTheShelfYet`,
  which fills the form from it (needs the rebuilt APK). As noticed: until the server has placed it,
  `TaskListDetailViewModel.ShelfForSomethingNew` does not fill the form from `TaskItemDto.Product`, which
  such an entry now carries. Not new - the form was blank before too - but the answer is on the entry now.

- ~~**The phone's `FakeTasksServer` does not place product entries**~~ Fixed 2026-09-11: given the
  screen tests' `FakeInventoryServer`, it places them on a list's save and on its link
  (`PlaceProductEntries`, mirroring `ProductEntryPlacement` - matched by name, a new row counted as
  `StockRequirementCounter` counts, the entry's own product dropped, declined for a private list or a
  shelf that may not be changed), and
  `An_errand_for_something_not_on_the_shelf_yet_comes_back_standing_for_its_row` is the screen test it
  allowed. A fake built without an inventory server still places nothing. As noticed: it did not, the way the real server does since
  2026-09-11, so a screen test can only assert what was sent. A fake that placed them would need to know
  the fake inventory server; until then the placement itself is covered by `ProductEntryPlacementTests`.
  Left unmodelled on purpose (decided 2026-09-11): the restock list the server settles after a placement
  (`SettleTheRestockListAsync`). No phone screen or test leans on that errand appearing - the restock
  list reaches the phone as an ordinary task list on its next pull - and the fakes rebuild no restock
  list even for the explicit `restock-list/refresh`, which answers a canned count; modelling the settle
  alone would mean a managed list per inventory, its settings and its errand naming in the fakes, for no
  screen test that needs them.

- ~~**The Android head still builds with twelve warnings**~~ Fixed 2026-09-11: it builds with none. The
  swatch's `AutomationProperties.Name` went, `AvatarCircle`/`PersonRow.Id` became `SubjectId`, the four
  `Frame` elements became `AvatarFrame`, `ButtonFrame`, `CardFrame` and `RowFrame`, `ItemCard`'s cleared
  shadows are `null!`, `MainActivity` checks the insets controller for null, and the two crefs name
  `Orbit.Mobile.Screens.Startup.StartupViewModel` and `Platform.WebSignInBrowser`. As noticed: (noticed 2026-09-11, after the seven
  `DisplayActionSheet` CS0618s went). None fails anything, and the head is not in `Orbit.CI.slnf`, so
  nothing gates them: one more CS0618, `AutomationProperties.Name` on the accent swatches in
  `AccountPage.xaml` (MAUI wants `SemanticProperties.Description`, which the swatch's button already
  carries); five CS0108 where `AvatarCircle`/`PersonRow` declare `Id` and four controls name an element
  `Frame`, hiding the base members; two CS8625 in `ItemCard.xaml.cs`, a CS8602 in `MainActivity.cs`, and
  two CS1574 crefs (`StartupViewModel` in `App.xaml.cs`, `WebSignInBrowser` in
  `WebAuthenticationCallbackActivity.cs`) that no longer resolve. What it would take: the swatch attribute
  dropped, the two `Id`s and four `Frame`s renamed, and the rest a line each.

- ~~**Options still calls an inventory a "storage".**~~ Done on 2026-09-10, and it was wider than the
  export section: eleven English strings across both clients still said storage - the task editor's
  picker and its two refusals, the checklist's, the shared-link page's kind label, the account screen's
  export tick-box and its two result lines, and the locked-feature message. Renamed with their Polish
  keys in the same change, since a key renamed on one side alone falls back to English on a Polish
  screen. The two that talk about a *browser's* local storage were left alone: that is what they mean.

- **A Location entry says where in words, and cannot be drawn.** `TaskItemKind.Location` was added on
  2026-09-10 so an entry can say where without saying when, and what it carries is `TaskItem.Location` -
  a line of text, the same one every other kind has had. A point lives on `Place` instead, which is its
  own row with its own sharing and, since the same day, its own encryption. So a Location entry cannot
  become a pin: the map draws places and things in the calendar, and an entry that only names a street
  is not either. This is written down rather than decided because the obvious fix is the wrong shape -
  a latitude and a longitude on every task item would put a point in a second place, against the rule
  that a place is stored once, and would make two rows that disagree about where something is a thing
  Orbit can hold. The alternatives worth weighing when somebody wants this: an optional link from an
  entry to a `Place` (one point, one owner, and the entry borrows it), or leaving Location as prose and
  letting the entry offer "keep this as a place" once. Needs a decision before any of it is built.

- **Why the map's Start and Share do nothing on a phone: two of the three causes are ruled out.** Both
  are hidden below 680px as of 2026-09-09 (`.map-panel-start`, `.map-panel-share`), on a report that
  pressing them achieves nothing there, and the page says so instead - since 2026-09-11 in a "!" beside
  its name rather than a line under the button. That is a cover, not
  a fix.

  **Measured on 2026-09-10** in a browser emulating 375×812, with the hiding rule lifted from the live
  page: the button is **not covered and not disabled** - `document.elementFromPoint` at its own centre
  answers the button itself - a press runs the handler, and the page answers *"Orbit isn't allowed to
  use your location. Turn it on in Options first."* 72px below it, both on screen without scrolling.
  So neither "the press never lands" nor "the failure is silent" is what happens: the third cause on
  the original list, **the Options switch never turned on for that device**, is what this reproduces,
  and the page does say so. `DevicePreferences.AllowLocation` is per device and per browser, so a
  laptop with it on says nothing about the phone.

  What is left to rule out needs the phone itself, and is one press now rather than an investigation:
  turn **Options → Location** on there, then press Start and read the line under it. If it says the
  location could not be read, the remaining cause is the certificate - a phone reaching
  `https://<LAN IP>:8443` does not trust this machine's mkcert CA, proceeds past an interstitial, and
  Chrome refuses geolocation to an origin with a certificate error. The cover comes off the moment that
  answer is in.

- ~~**The phone cannot answer whether a list is finished.**~~ Done on 2026-09-10, the way this said:
  **Completed** in the list's menu (`TaskListDetailViewModel.IsFinished`, ticking itself once every
  entry is, recording the reader's own answer when pressed), `LocalTaskList.Completion` with a local
  migration, and the field on every save from the phone - which `UpdateTaskRequest.Completion`'s
  null-means-not-provided rule was holding the door open for. `TaskListView.Describe` still says "Not
  finished" for the status it produces, and that status is still not among `TaskListView.Statuses`, so
  no chip finds one; it is reachable under "all", the same as on the web.

- ~~**The invitation page treats "any other kind" as an inventory.**~~ Fixed on 2026-09-10, the way this
  said: `ShareInvitation.CanBeTakenUpHere` names the five kinds the page can act on and answers null for
  anything else, so a kind with no branch of its own goes down the "something this version of Orbit
  doesn't know about" path instead of posting a share id to the *inventory* accept endpoint and being
  told it is no longer there. `location` is the case that used to be wrong and is covered by a test.

- ~~**The checklist matches entries by position when it no longer has to.**~~ Done on 2026-09-10, and it
  turned out to be a defect rather than the tidy-up this entry called it. `TaskItemCompletion` found the
  entry with `IndexOf`, which compares a record by every field - so it worked until the caller held a
  copy that was no longer equal to the stored one, which a second reader's save is enough to produce.
  `IndexOf` then answers -1, nothing matches, and the list is saved back **with nothing ticked at all**,
  reporting success. It matches on the id now, and by position only for an entry that has none.

Written down rather than fixed on the spot, per rule 14 in `.claude/CLAUDE.md`: work that turns up
beside a task belongs here, not in that task's diff. A defect is the exception and is fixed when found.

- ~~**A two-person group's messages leaked into that pair's one-to-one conversation.**~~ Found on
  2026-09-07 while walking the phone's chat screens, and fixed the same day — a defect, so fixed rather
  than only recorded. A group message is sealed pairwise, one copy per member (`ChatMessage.CreateForGroup`),
  so in a group of two the copy carries the same sender/recipient pair as an ordinary one-to-one message
  between them. The server's `ChatMessageRepository.GetConversationAsync` matched only on that pair, so it
  handed those copies back on the one-to-one endpoint too, and both clients drew them in the private
  thread — the phone made it visible because it also stamps each such row with `OtherUserId`. Fixed at the
  source with a `GroupId == null` clause on the one-to-one query (a group is read by `GroupId`), mirrored
  in `InMemoryChatMessageRepository` so the fake refuses what the server refuses, and defended on the
  phone in `ChatRepository.GetConversationAsync` and `LatestMessageAtAsync` — which already held that
  invariant in `DeleteConversationAsync` and had simply not carried it to the read and the cursor.
  Covered by `GetConversationQueryHandlerTests` and `GroupChatTests` on both sides.

- ~~**The web client went into a loop once, on 2026-09-05 at 11:06Z, right after approving a
  conversation.**~~ **Cause found and fixed on 2026-09-07**, and it was not a render loop: it was two
  open chat windows announcing at each other. Marking a conversation read published a live "chat
  changed" to the other party **whether or not anything had actually been read**; the other window
  answers an announcement by polling, a poll marks the conversation read, and that announced back. Two
  windows therefore ran the same four calls - `conversations/{id}/access`, `messages/{id}`,
  `messages/{id}/read`, `messages/{id}/read-receipt` - at network speed for as long as both were open,
  which is exactly the shape of what was measured. Approving is what started it because it is the moment
  both sides open the same conversation at once. Both handlers now publish only when a row actually
  changed (`MarkConversationAsReadCommandHandler`, `MarkGroupConversationAsReadCommandHandler`, and the
  repository methods that now answer whether they marked anything), so the exchange dies after one
  round: a read receipt still travels, a read that did not happen says nothing. In a group it was worse
  by the size of the group, since the announcement goes to every other member.

  The original measurement, kept because it is what made the cause findable: In one minute it made the same four chat calls - `conversations/{id}/access`,
  `messages/{id}`, `messages/{id}/read`, `messages/{id}/read-receipt` - about 987 times each against
  only **two** ids, plus `chat/contacts` 201 times and `chat/groups` 109 times: 4,332 requests from one
  caller, sixteen a second, and the busiest minute in the month by fifteen times. A render-and-refetch
  loop, it was read at the time - and reading it that way is what kept it unexplained, since nothing in
  the approve flow re-renders anything. `FloodStopPerCaller` (600 a minute) would have cut it off after
  ten seconds had it existed then, and a burst of 429s on those four paths is still how a recurrence
  would announce itself.
- ~~**`setup-dotnet@v4`, `setup-java@v4` and `upload-artifact@v4`** carry the same Node 20 deprecation
  `actions/checkout` did.~~ Bumped on 2026-09-10 to the current major of each - `setup-dotnet@v6`,
  `setup-java@v6`, `upload-artifact@v7` - which is the rule `checkout@v7` already followed. Every input
  these steps pass was checked against the new major's own `action.yml` first; none of them moved.
  **Unrun**: nothing here starts a workflow, so the first proof is the next push to `main` and the next
  Android release.
- ~~**`info/azure-setup.md` and `info/architecture.md` still call the subscription an Azure Free Trial**~~
  Done. `azure-setup.md` had already stopped saying it by the time this was looked at - only
  `architecture.md` still did, in the step explaining why the pipeline builds images on the runner. It
  now says what the `ci-pipeline` skill says: ACR Tasks were blocked on the free trial, are not blocked
  now, and the runner build stays because it is the verified path rather than because anything forbids
  the alternative.
- ~~**Whether the Container Apps ingress sets `X-Forwarded-For` at all is still unmeasured.**~~
  **Measured on 2026-09-06, and it does.** A plain request to the deployment logged
  `xff="46.205.200.84"` - the caller's own address. A request carrying a forged header logged
  `xff="203.0.113.250,46.205.200.84"`: the forgery is on the left and **the ingress appends the real
  address on the right**, which is exactly the shape `real_ip_recursive` is built for, since it walks
  from the right and stops at the first untrusted entry.

  So the per-caller limits are real and a client cannot choose its own bucket. What that measurement
  also caught is that they were not working at all until `real_ip_header X-Forwarded-For` was added -
  the module defaults to `X-Real-IP`, nothing sends that inbound, and `nginx -t` accepts the
  configuration either way.

  **Measured on both paths on 2026-09-07, once the request log carried `from <network>`.** The phone's
  path is right: a direct request logs the caller's own network, and one carrying a forged
  `X-Forwarded-For` logs the same - the forgery is never reached. The browser's path was **wrong**:
  every request through nginx logged `from 20.215.81.0`, the environment's egress NAT, because nginx had
  been pointed at the API's *public* name and the request left the environment and came back in. Fixed
  by returning nginx to `orbit-api.internal` and letting the API walk two hops (`ForwardedCaller`); the
  chains, and that failure, are pinned in `ForwardedCallerTests`. The last dependency - that a freshly
  started nginx resolves the internal name with the API's ingress external - was confirmed from inside a
  newly created `orbit-web` replica the same day; `info/azure-setup.md` keeps the one-line check.

  **`orbit-web` now really scales to zero, and the first request after idle takes over fifteen
  seconds.** Measured by accident: a probe answered `000` while the container was being created
  (`ContainerCreated` fourteen seconds after the request). Until the phone was pointed at the API
  directly its syncs kept the web container warm; that was the cost being saved, and this is what it
  bought. Whether a cold start of that length is acceptable for the first browser visit of the day, or
  worth `min-replicas 1` on `orbit-web` (a recurring cost), is a decision, not a defect.

  ~~The ceilings in `RateLimiterPolicies` were sized for a forgeable address and can come down.~~ Done
  on 2026-09-07, from thirty days of the request log (2,464 minutes with traffic): anonymous sign-ins
  peaked at 4 a minute, public share links were opened 0 times outside a probe, the busiest ordinary
  minute overall was 287. Now 30 / 150 / 3,000 a minute and 50 a second at the edge - each with the
  measurement and the growth rule written beside the number.
- **17 of 147 endpoints carry a *named* rate limit.** The rest are now covered by
  `RateLimiterPolicies.FloodStop`, a coarse per-caller limit over everything, kept in memory rather than
  in the shared PostgreSQL window because that one costs a round trip per permitted request and this one
  runs on every request in Orbit. It is a flood stop, not a per-account budget: nothing yet bounds what
  one account, or one leaked token, can ask for over an hour.
- **Kestrel's default 30 MB request body applies everywhere** except the diagnostic upload, which is the
  one endpoint with an explicit `RequestSizeLimit`, on a container with 0.5 GiB of memory.
- **The phone talks to `orbit-api` directly, so nginx's edge limits never see it.** That was the
  deliberate trade for letting `orbit-web` scale to zero again; `FloodStop` is what stands in front of
  that path instead. A WAF in front of both is what would make the two surfaces equal, and is the
  expensive half below.
- **There is still no autoscaling and no WAF.** Both apps are `max-replicas 1` with no scale rules at
  0.25 vCPU and 0.5 GiB, and nothing sits in front of `orbit-web`. The edge limits refuse a flood rather
  than absorbing it, which is the cheap half of the problem; the expensive half is unchanged.
- **`max-replicas` is still 1 on both Container Apps.** Nothing in the code assumes otherwise any more -
  the live update hub, the privacy choice cache and the rate limiter each count across instances now -
  but raising it is a deliberate act and a cost decision, and it has not been taken. Two things to know
  before it is: `orbit-web` currently scales to zero when idle and will stop doing so once anybody holds
  a live update connection open, and nothing above has ever run on more than one replica, so the first
  time it does is the first real test of all three.
- **Nothing enforces that work reaches `main` only through `Coding`.** `guard-main.yml` closes stray
  pull requests, but a direct push to `main` deploys before any workflow can run. Real branch
  protection needs GitHub Pro on a private repository.

## Redrawing the rest of the phone

**Superseded 2026-09-08.** The section below was about pulling the Android head to `app.css`; the
phone now has a look of its own - the Classical system, designed in Claude Design and handed over as a
prototype of nineteen screens. See [`android-ui-parity.md`](android-ui-parity.md), which is now the map
of what the two clients still share (the roles, the copy, the behaviour) and what the phone decides for
itself (the type, the ground, the shapes, the shell).

Every screen the design covers has now been redrawn. The passes were:

1. ~~Foundations - fonts, palette, styles, the Android colour resources.~~
2. ~~The shell - the bar, the drawer, the title menu, the floating button, the navigation stack.~~
3. ~~Dashboard, notes list, note, tasks list.~~
4. ~~Tasks detail and a task entry~~, plus `CheckCircle`, which both they and the note screen tick with.
5. ~~Calendar and an event.~~ The month grid is open now; the event screen saves from a floating button.
6. ~~Inventory and a shelf~~, whose rows carry the design's stepper.
7. ~~Chat~~ - the bubbles are outlined, the person rows are hairline rows, the avatar is a ring in the
   person's own hue, and the two bare rails are gone. `EditorRail` itself is deleted: nothing drew one.
8. ~~The notification feed~~ (its three actions moved under the title), ~~sign-in~~ and ~~the account
   screen's accent swatches~~.

9. ~~The five screens the design never drew~~ - copies (the review and history screens), diagnostics,
   the update screen, the shared-link page and the place picker. The prototype covers none of them, so
   they were redrawn by applying its rules rather than by copying a picture: no page heading where the
   bar already says the name, one quiet line of context where the screen needs one, hairline rows with
   the rule above rather than below, the accent outline on the one thing a screen is for and the danger
   outline on what cannot be undone, and a bordered group where three answers are one choice.

   Six leftover "Back" buttons went with them. They were right while screens replaced each other; the
   navigation stack gave every detail screen an arrow in the bar, and drawing a second way out under
   the content had become a duplicate that also contradicted its own comment.

10. ~~The written spec, 2026-09-09~~ - the design was rejected as built, and the answer was a
    screen-by-screen description in the user's own words. What it changed: the bar lost its back arrow
    and gained an optional pair of arrows beside a screen's name; About became a screen; every list
    screen's settings moved under its name and left the page to its rows; the note editor became one
    surface; the calendar gained a week view and lost the grid that shrank on its own; the map took the
    whole screen. What it did **not** cover, and is still owed a description: see the list at the foot
    of [`android-ui-parity.md`](android-ui-parity.md).

11. **The design read again, 2026-09-09** - not as a style guide this time but as a specification of
    composition, which is what it always was. It turns out to draw six of the screens the written spec
    never reached, and to correct a dozen things about the screens built from the spec. The whole of it
    is in [`android-design-deltas.md`](android-design-deltas.md), screen by screen, with the six
    disagreements between design and spec listed first and settled in the spec's favour. Nothing there
    is a defect; it is the list of what is still owed if the design is taken as the specification for
    the rest. The one structural piece everything else waits on: a title menu is *groups* with headings
    and counts, and `ScreenMenu` can only draw a flat list with one heading.

One thing the design asks for that belongs to **both clients** rather than to the phone:

- **A person's row says nothing about how recent the conversation is.** The design's contact row carries
  the last message under the name and when it was on the right; `PersonRow` - the phone's and the
  browser's, which are the same shape on purpose - carries the name and a subtitle, and the subtitle is
  the username. So a list ordered by recency has nothing on it that says so.

  Not done on the phone alone, deliberately: the looks follow the design, but *what a row says about a
  person* is a feature, and a feature the browser has not got would make the two clients answer
  different questions. The preview is the harder half - a message is sealed, so drawing a list of
  twenty contacts would mean opening twenty messages - and "when" on its own, over a subtitle that is
  still a username, is half a row. If it is wanted, it is wanted on `Orbit.Web/Components/PersonRow.razor`
  and `Orbit.Maui/Controls/PersonRow.xaml` together, with `LocalContact.LastMessageAtUtc` (already
  synced and already what the list is ordered by) carrying the easy half.

One thing the design showed up that is not fixed:

- ~~**The tick in a menu is a character, not a drawing.**~~ **Went on 2026-09-09 without being fixed.**
  `ScreenMenuEntry.Mark` is still the character `"✓"`, but the faces are IBM Plex Sans over Space
  Grotesk again and IBM Plex Sans carries that glyph, so nothing is substituted any more. It is worth
  remembering that the string is still a glyph and not a drawing: a third change of face would bring it
  back. The box glyphs `☐ ☑` are carried by neither face, which is why `CheckCircle` draws its circle.

### The pass this replaced

The phone was walked against `app.css` on 2026-09-06 and shared its type scale, its button roles and
its shared controls. What that pass left, all of it now overtaken:

- ~~**A card has no overflow menu.**~~ Done: Notes, Tasks and Inventory cards each carry one, with the
  actions lifted onto the list view models and the same refusals the browser applies - somebody else's
  note is removed from your own list rather than deleted, a shared list or inventory is not yours to
  delete, and a private or never-synced inventory is handed to nobody. Two things the browser has that
  the phone deliberately does not: an "Edit" entry (its press and the card's press open the same screen
  here) and the second question a group task list asks before deleting what it gathers (the local store
  deletes one list at a time). See [`android-ui-parity.md`](android-ui-parity.md).
- ~~**Chat and the calendar's grids have not had the pass.**~~ Done: the month grid's cells are
  `.calendar-month-grid-day`'s (the lifted surface, a hairline, today tinted, the days either side
  quiet), a calendar card carries its event's colour along its edge and its own Delete menu, and both
  conversation screens draw messages as `.chat-bubble` does - the reader's own at the right in the
  accent, everybody else's at the left, at most 70% of the thread's width. The four platform action
  sheets in chat and on the calendar are Orbit's own panel now. What the grid deliberately does not
  copy is the browser's 5.5rem cell full of event chips - see `android-ui-parity.md`.
- ~~**The map and the account screen have not had the pass.**~~ Done: the map's groups are
  `.map-panel-section` cards under `.map-panel-heading`s with the map itself in a rounded, hairline
  frame, and the account screen is `.options-card`s of `.options-row`s - a title, what it does
  underneath, the control at the far edge - with the tabs underlined in the accent and the delete
  section in its own red frame. The map stays where the browser hides it below 680px; see
  `android-ui-parity.md` for why that one rule is not copied.
- ~~**The chat screens are built but were not walked on a device.**~~ Walked on 2026-09-07, on the
  Windows emulator, against the docker API — two throwaway accounts (`chatweb` in Orbit.Web, `chatphone`
  on the phone) each with a real published key, so a real E2EE conversation could be had rather than
  staged. Both conversation screens hold: the one-to-one and the group each decrypt, draw the reader's
  own bubbles at the right in the accent and everybody else's at the left, label a group message with
  who wrote it, and open Orbit's own panel — not a platform action sheet — for the message menu (own:
  Edit/Delete/Forward/Reply one-to-one, and *Who has read this* in place of Forward in a group; other
  people's: Forward/Reply) and the conversation menu (Info → the read-only contact card; a group's
  member roster with roles and Leave). The contact row's incoming-request Accept, the avatar's
  top-right presence dot and the row's unseen mark were all seen on the way in. **A defect came out of
  the walk** and is recorded under [Noticed while working](#noticed-while-working): a two-person group's
  messages leaked into that pair's one-to-one thread.
- ~~**The dashboard was never given the pass.**~~ Done on 2026-09-07, and it was the last screen
  still speaking the old vocabulary: it now opens with `PageHeader`, both of its "⋯" are the shared
  `OverflowMenu` filling the screen's one `ScreenMenu` (the page's parts menu stays open as
  Orbit.Web's does, a card's filter closes after one pick), its rows are `Row`s, and every card and
  every row that a notification can name carries the mark - `.item-card-unseen` on the card and
  `.list-row.row-unseen` on the row, which is what stops a card saying "something happened here" over
  six rows and leaving the reader to open all six. The strip of today's counts is the way to the
  calendar, as it is there, and drops its chat-request line at nought. Two defects came out of it: a
  card narrowed to nothing vanished and took its own filter menu with it, so the choice could not be
  undone (the bug Orbit.Web had already fixed), and the coloured dot beside an event had never once
  been drawn - see `android-ui-parity.md` for why.
- ~~**The dashboard's rows carry no avatar, and there is no Inventory card.**~~ Done on 2026-09-07.
  The circle came out of `PersonRow` into `Controls/AvatarCircle.xaml` on the way, so the chat list,
  the contact list and the dashboard draw one avatar rather than three - and the presence dot moved to
  the top-right edge, which is where app.css has always put it. `DashboardCardKind.Inventories` sits
  between what is coming up and who is around, as Orbit.Web orders them; a shelf says how much is on it
  and whether it is private or shared, a private one is hidden while private things are locked, and the
  card carries the news because a shelf about to go off names no shelf. Two smaller things came with
  it: putting every part away now says so instead of telling a full account to add a note, and pressing
  a shelf opens that shelf.
- ~~**A conversation still shows no count of what is waiting.**~~ Done on 2026-09-11, and it needed no
  read mark of the phone's own after all: the server keeps one per conversation and already sends the
  count on every contact (`ContactDto.UnreadCount`) - the phone simply dropped it on the way into its
  store. `LocalContact.UnreadCount` keeps it (local migration `KeepHowManyMessagesAreWaiting`), so it
  survives a restart and reads offline; `ChatRepository.MarkReadAsync` takes it to nought the moment the
  server has been told a conversation was read; and `AvatarCircle` draws it where `UnreadBadge` sits on
  the web - bottom left, "9+" above nine, nothing at nought. The row's own mark now lights for unread
  messages as well as for a request to answer, as Orbit.Web's does. A group has no count on either
  client, and the dashboard's rows still draw no face to put one on (see android-design-deltas.md).
- ~~**`ContactsPage.xaml` declares a `PresenceColor` converter it never uses.**~~ Gone: the row that
  needed it became `AvatarCircle`, which holds the converter itself, and the declaration went with the
  markup it belonged to.
- ~~**A card's footnote says the whole timestamp.**~~ Done: `LastChanged` gives the four answers
  `Notes.razor`'s `WhenLastChanged` gives - today, yesterday, the weekday within the week, a date past
  it - against the injected clock rather than the machine's, so a test about the wording is a test
  about the wording.
- ~~**The `.item-card-unseen` pulse is a colour, not an animation.**~~ Done, and with it the thing
  that made it visible at all: nothing on the phone had ever set `HasUnseenAction`, so neither the mark
  nor the edge could appear. A task list card now asks whether anything unread points at it, matching
  on the `/tasks/{id}` a notification carries exactly as Orbit.Web's card does, and the edge breathes
  the halo `card-with-news` draws - called off where the reader has asked the phone for less motion,
  which is the platform's answer to `prefers-reduced-motion`.
- ~~**No screen hands `EditorRail` an `Extras` view yet.**~~ Done: the note's and the task list's
  read-only reasons are in it, where they stay in view however far the screen is scrolled. The task
  list gained the rail itself in the same change - its menu moved off the title and onto the bar, as
  Orbit.Web's checklist keeps it, taking Delete and History with it, so the row of words under the last
  entry is gone. The arrow now follows what it hides rather than merely whether a slot was filled: a
  screen hands over a label that hides itself, and an arrow that opens an empty line is a control that
  does nothing.

## Smaller identified follow-ups

- **The phone's wait does not look like the web's yet.** Since 2026-09-11 the web waits with Orbit's icon
  inside a turning ring (`Components/Loading.razor`, and the boot screen in `wwwroot/index.html`); the
  phone still draws MAUI's own `ActivityIndicator` wherever it waits (the startup screen, sign-in,
  register, password reset, the map, the notification feed, an inventory, an invitation, a shared link,
  Diagnostics - and its style in `Resources/Styles/Styles.xaml`). A matching control would be one
  `ContentView` holding the app icon with a rotating arc round it, slowed rather than stopped when the
  system asks for less animation, as the web's is.

- ~~**The phone does not yet describe a product before the shelf exists, and does not ask what to build.**~~
  Both halves done on 2026-09-10, as this said. An Inventory entry on a list with no storage behind it
  now shows the product form bound to the entry's own `TaskItemDto.Product`
  (`TaskItemEditor.ProductWanted`, `InventoryItemEditor.ForSomethingAListWillAskFor`), with the entry's
  words as the name and its categories box as what the product is filed under; and "Generate inventory"
  asks the six questions `GenerateInventoryOverlay` asks before it builds anything
  (`GenerateInventoryForm`, unfolded in the stock-check card). Two fakes were made to answer like the
  server on the way: `FakeTasksServer` dropped an entry's product on every save and now keeps it,
  null-means-not-provided included.

- ~~**The pin on a shared list or note stays on the browser that set it.**~~ Done on 2026-09-07, the way
  this predicted except for the DTO: a per-recipient flag on the share row
  (`NoteShare.IsPinnedByRecipient`, `TaskListShare.IsPinnedByRecipient`, migration
  `PinASharedThingForItsRecipient`), and **no new field anywhere**. The existing `IsPinned` already meant
  "sorts this above the others on every client that shows a list of them", so a shared item is simply
  handed over carrying the recipient's answer in it - stamped by the access resolvers, the way the
  access context beside it already was. The endpoint did not change either: `PUT .../pinned` picks
  between the two rows by which one the caller has. `SharedItemPins` is gone; `DevicePins` stays, since
  `ConversationPins` still uses it.

  **It also closed a defect nothing had recorded.** The browser overrode the owner's flag locally, so
  nobody saw it there - but the phone sorts straight by `IsPinned` and had no second answer to prefer,
  so a note or list *its owner* had pinned arrived at the top of the recipient's list. Both halves are
  pinned down in `GetNotesQueryHandlerTests` and `GetTaskListsQueryHandlerTests`, including that the
  stamp does not touch `UpdatedAtUtc` - a value depending on who is asking must not make the row look
  changed, because that timestamp is what the phone syncs against.

  **The phone's half went in the same day.** Its pin control for a shared item had been left out
  (`TaskListRow.CanBePinned`, `NoteListItem.CanBePinned`) while the server refused a recipient outright;
  the rest of that path already existed and already went through the server, so lifting the gate was the
  whole change. A sealed row nobody has unlocked still offers no pin, which is what the gate is left for.

- ~~**A task list's own description is written and never shown.**~~ Done. It goes under the name on the
  list's own page (`TaskListChecklist`), which is where a storage's goes, so the two read the same way -
  and it takes the place of the sentence that used to sit there, which was a signpost about the page
  rather than anything about that list. A list nobody described still gets the signpost. Addresses in it
  are pressable like every other description.

- ~~**Only the task pages carry "come back where you came from".**~~ Done: the note, the event and the
  storage forms read `ReturnTo` too, their summaries pass it on, and the dashboard names itself so an
  edit begun there ends there. **Notifications name themselves as well since 2026-09-10**: the bell
  panel names the page it was pressed on - it is on every page, so that page is where the reader was
  interrupted - and the notifications page names itself, since somebody working down a list of them is
  still on that list afterwards. Fixing `ReturnTo.Link` was part of it: it assumed the path it was given
  had no query, and a notification about a shared place carries `/map?place={id}`, so the place id would
  have arrived as `{id}?returnTo=…` and opened the map on no pin.

  ~~**Chat opening a shared thing is the one left, and it is not a call site.**~~ Done 2026-09-11, with no
  contract change. After "Accepted - added to your account." the chat offers "Open it". It asks the
  offer endpoint the invitation page already uses (`GET /api/shares/{kind}/{shareId}`, which answers
  with the item's id) for where the thing is, through `SharesApiClient.WhereItLandsAsync`. An offer
  accepted in an earlier visit links to its section, because only its status was read then. The phone is
  unchanged: what it accepts lands under its own ids at the next sync. As noticed: A share notice in a
  conversation carries the *share's* id and nothing else (`NoteShareMessagePayload` and its four
  siblings), and accepting answers `bool` - so after "Accepted - added to your account." the chat has no
  address to offer. Giving it one means the five accept endpoints answering with the item's id, which is
  a server change and a contract change rather than one line.

- ~~**Two of the five screens that send a share notice are covered; three are not.**~~ Done, all five.
  Sharing something is two halves: the server records the share and raises a notification, and the
  sharer's *browser* posts an encrypted chat message carrying the share's id, which is the only thing a
  recipient can press "Accept" on (see Chat.razor's `TryParseShare`). The server cannot send that half -
  it holds no key to seal it with - so a screen that forgets it shares something nobody can accept,
  which is exactly what the guest invitation on a task entry's event did for as long as it did (fixed
  2026-09-06, covered 2026-09-07).

  The five, and where each is pinned down: the guest invitation on a task entry's event and the task
  list's own sharing block (`TaskEditorItemFormTests`), the storage panel (`ShareInventoryPanelTests`),
  the note (`NoteEditorTests`) and the calendar event's guests (`CalendarEventEditorTests`). Each was
  checked against the mechanism rather than only run: removing the `SendAsync` call turns its own test
  red. Each also has the negative beside it - sharing with nobody chosen sends nothing - which is what
  keeps the positive from passing on a page that posts to everybody.

  What unblocked them: the sealed payload's shape was a `private record` inside
  `EncryptedChatMessageSender`, so a bUnit test could not plan the JavaScript result; it is `public` now
  and nested there on purpose - `InternalsVisibleTo` was the alternative and would have opened the whole
  assembly for one type. The recipe, if a sixth screen ever sends one: a signed-in token,
  `./js/e2eeChat.js` answering `hasOwnPrivateKey` / `ensureOwnPublicKey` / `encryptMessage`, and a stub
  answering `/api/users/{id}` with a contact who has a public key. `ShareInventoryPanelTests` is the
  smallest of the five to copy.

- **The phone shows links in nearly all of what it draws** (2026-09-09, extended 2026-09-10). The
  splitter moved to `Orbit.Core.Text.LinksInText`, so both clients share one rule about what counts as
  an address, and `LinkedLabel` is the phone's half of `TextWithLinks` - a `Label` that writes
  `FormattedText`, since a Span is the only thing in MAUI that can carry a gesture of its own. It is
  drawn in **chat messages**, in both a conversation and a group, on **a task entry's appointment
  description**, and - since 2026-09-10 - on every description the reader cannot write in: a list's and
  a shelf's, which share `TitledDescription` (`ReadsAsWords`), and an appointment's on the event screen.
  A box is drawn instead wherever it can be typed into, which is the whole rule: **a text box cannot
  hold a link at all**, so this is about read-only screens rather than about labels.

  What is left is a note's own lines, and they are the case that cannot be swapped: every line is a
  field being written in, on a screen whose whole point is that it is one writing surface. An address in
  a note is still selectable text and nothing more.

- ~~**The phone has no box for an entry's description.**~~ Done on 2026-09-10, as this said:
  `TaskItemEditor.Notes`, a box on the entry's sheet under its categories, and the calendar rule - the
  entry's description is its appointment's, written onto the event at save, and the event's own box on
  the phone is gone. An entry never opened on the phone still passes through what the server sent.

- ~~**The phone still asks twice what a shelf entry is filed under.**~~ Done on 2026-09-10, as this
  said: `InventoryItemEditor.ShowsCategories` hides the product's row on a task entry
  (`AskedFromATaskEntry`, set by both `TaskItemShelfProduct` factories), and the entry's `Categories`
  are written onto a product being described when the entry is saved. A product already on the shelf
  keeps its own, which is what Orbit.Web's `ProductAsked` does for a linked entry too.

- **Done, kept here as the map of it.** Orbit has two depths for the same thing: a shallow view for
  reading and doing, and a full form for changing what it is. Every object that can have both now does,
  and the pattern is the same one each time - land on what the thing is, with the fields a named press
  further in, and whatever light doing belongs to that thing offered where it is read. Which is which:

  | Object | Shallow view | Full form |
  | --- | --- | --- |
  | Task list | `/tasks/{id}` - the checklist: tick items, see the tree of lists it stands for, measure it against a storage | `/tasks/{id}/edit` |
  | Task entry | `/tasks/{listId}/items/{itemId}` - `TaskItemSummary`: when, where, what the appointment is about, who is coming, a map, and a Done box that crosses it off | the entry's own row in the list's editor |
  | Note | `/notes/{id}` - `NoteSummary`: the note read, with the checklist lines in it tickable | `/notes/{id}/edit` |
  | Calendar event | `/calendar/{id}` - `CalendarEventSummary`: when, where, what it is about, who is coming, its reminders, and the place on a map | `/calendar/{id}/edit` |
  | Storage | `/inventory/{id}` - the shelf read rather than edited, one row per batch: what it is, how much, when it arrived, how long it keeps | `/inventory/{id}/edit` |
  | Contact / group | `/contacts/{userId}`, `/chat/groups/{id}/info` - read-only cards about who somebody is | no form; membership is edited on the roster |

  The unevenness this used to record is gone: when it was written a note, an event and a storage had
  nothing between a card and a whole form, and each of the three has had its own reading page since
  (`NoteSummary`, `CalendarEventSummary`, the shelf at `/inventory/{id}`), all built to the same shape
  as part of the screen-ladder pass. A contact and a group are the deliberate exception - they are read
  and never edited as objects, so there is no second depth to give them.

  ~~One thing is still wrong with it, and it is the smaller half: the shallow view and the full form are
  reached inconsistently.~~ Settled on 2026-09-07. The note half of it had already gone by the time this
  was read again - `OnBodySelected` on `/notes` has opened `/notes/{id}` since `7e1504f5`, so what was
  left was the **task entry**, which four pages answered three different ways:

  - the checklist opened the list's own **form** with that entry unfolded, skipping the entry's page;
  - the calendar forked on whether the entry had a place - one that did opened as itself, one that did
    not opened as the **list** it sits on, which is a different object, decided by a field no card
    mentions (`DueTaskDto.HasPlace`, now gone with the fork, along with `Calendar.razor`'s
    `GoToTaskList` and the walk up the tree of group lists it used);
  - the dashboard's Upcoming named an entry, "Shopping: Milk", and opened Shopping;
  - only `/tasks` opened the entry itself.

  All four open `/tasks/{listId}/items/{itemId}` now, and the rule they were settled into is written
  down under [Two editing levels](functionality.md#two-editing-levels): **a press opens the thing that
  was pressed, at its reading depth; the form is a named press further in.** Nothing was lost - ticking
  an entry off is the checkbox's job, which sits on the row beside the words, and the entry's page leads
  back to the list. The flat reading of a checklist was folded in on the way: its rows were `<label>`s,
  so pressing what an entry said crossed it off there while the same words on the grouped view opened it
  (`CheckRow.OnTitlePressed`).

  The entry's page ticks it off itself since 2026-09-09, on both clients - the light doing that belongs
  to the thing it reads, which it was the one shallow view without. Both web screens tick through
  `TaskItemCompletion`; the phone's `TaskItemSummaryViewModel` writes it to the phone and queues it.
  What is left of the old note above still holds: the checkbox on the checklist row is where an entry is
  crossed off *while reading the list*, and the entry's page is where it is crossed off while reading
  the entry.

  ~~**The phone still forks the way the calendar used to**~~ (`CalendarViewModel.OpenDeadline`,
  `CalendarDeadline.IsSomewhere`). Gone on 2026-09-10, exactly as this said: the `if` dropped, and
  `IsSomewhere` and `IsSomewhereAsWellAsAtSomeTime` with it. Every deadline on the phone's calendar
  opens the entry itself now, and nowhere on either client does pressing an entry open something else.


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
