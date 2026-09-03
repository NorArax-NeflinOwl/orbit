# Putting Orbit's events into Google Calendar for real

What this is: the work needed to replace the "add to Google Calendar" **link** with calls to the Google
Calendar API, so that an event lands in Google saying everything Orbit knows about it.

Why it is needed is written into the link itself - see
[GoogleCalendarEventLink](../src/Clients/Orbit.Web/Services/GoogleCalendarEventLink.cs). A template URL
takes a title, dates, a description, a place, a rule and guests, and refuses everything else. Three
things the app is expected to carry cannot travel that way at all:

- **the event's colour** - the URL has no parameter for it;
- **the reminders** - none either, so Orbit's lead times are written into the description as words;
- **the guests' access levels** - a template link has no per-guest role, so everyone arrives as an
  ordinary guest.

And three more that only a real integration can answer: whether the event actually landed, what happens
when it is edited in Orbit afterwards, and what happens when it is deleted.

The high-level shape of this (verification, the OAuth flow, token storage, one-way versus two-way) is in
[future-plan.md](future-plan.md#what-real-google-calendar-sync-would-take). This document is the task
list underneath it.

## What Google can and cannot be told

Worth reading before estimating anything: the API is not a superset of what Orbit stores, and two of the
three gaps above only *narrow* rather than close.

| Orbit | Google Calendar API | Notes |
| --- | --- | --- |
| `Title` | `summary` | One line. The rule the link already uses stays: first line is the summary, the rest joins the description. |
| Task list behind the event | `summary` prefix | `[list] - [event]`, as the link does today - see `CalendarEventDestination` for how the two are related. |
| `Description` | `description` | Multi-line is fine here, unlike the summary. |
| `Location` | `location` | Address when there is one, `lat,lng` otherwise. |
| `Color` (any hex) | `colorId` (one of 11) | **Narrows.** Google's event palette is a fixed set of ids, not a colour. The nearest one has to be computed - and from what `colors.get` returns for this account rather than from hardcoded hexes. |
| `StartUtc`/`EndUtc`, `IsAllDay` | `start`/`end`, `dateTime`+`timeZone` or `date` | All-day already has its exclusive-end rule and its "the reader's day, not UTC's" rule in the link builder; both carry over unchanged. |
| `Recurrence` | `recurrence: ["RRULE:…"]` | The RRULE builder in the link is reusable as it stands. |
| `ReminderMinutesBeforeStart`, `NotifyAtStart` | `reminders.overrides[].minutes` | **Closes.** `NotifyAtStart` is minutes 0. Google allows at most 5 overrides and at most 40320 minutes. |
| `ReminderNotificationChannel` | `reminders.overrides[].method` | `Push` → `popup`, `Email` → `email`, `Both` → one override of each **per lead time**, which halves how many lead times fit under the cap of 5. `None` → `useDefault: false` and no overrides. |
| `Guests` (user ids) | `attendees[].email` | Only guests whose address Google itself has verified - the same test `ContactDto.HasGoogleVerifiedEmail` makes on the client, made server-side from `User.GoogleSubjectId`. |
| Guest `ShareAccessLevel` | `guestsCanModify` (per **event**) | **Narrows, and this is the awkward one.** Google has no per-guest role. `CanEdit` for every guest → `guestsCanModify: true`; anything else, including `EditOnly`, → `false`. Being wrong in the narrower direction is the safe way to be wrong. |
| `Priority` | nothing | No field. Either dropped or said in the description; a decision, not a mapping. |

Two more that have no Orbit counterpart yet and need deciding: `sendUpdates` (whether Google emails the
guests - `all`, `externalOnly` or `none`) and `guestsCanInviteOthers`/`guestsCanSeeOtherGuests`.

## Tasks

Each task says what it is, where it lands, and what makes it done. They are in dependency order; T1-T3
are the cost of admission and produce nothing a user can see.

### T1 - Get the scope approved in Google Cloud Console (outside this repo, L)

`https://www.googleapis.com/auth/calendar.events` is a scope Google classes as **sensitive**: an app may
ask for it in testing straight away, but not from anybody outside its own test-user list until the
consent screen has been through verification. That is a review with a person on the other end, measured
in weeks, so it starts before any of the code below.

Sensitive is not the worst tier - a **restricted** scope (Gmail, full Drive) additionally needs an
annual third-party security assessment, and this one does not. Google's console shows the current
requirements for the scopes actually selected, and that checklist is the authority; what follows is what
to have ready before opening it.

**Status, 2 September 2026: waiting on the domain, on purpose.** A domain has to be bought, a production
environment of its own carved out in Azure, and DNS pointed at it before any of this can be submitted -
see [future-plan.md](future-plan.md#what-real-google-calendar-sync-would-take).

**The prerequisite that is not in Google's console at all: a domain.** Verification asks for authorised
domains that the applicant owns and has verified in Google Search Console, and for a privacy policy and
terms of service hosted on one of them. Orbit is served from
`orbit-web.victorioustree-36ad82ca.polandcentral.azurecontainerapps.io` - a domain Microsoft owns, which
cannot be verified as Orbit's. **A custom domain is therefore step zero**, and it is also the longest
pole: registering it, pointing it at the Container App, and re-issuing the OAuth client's authorised
JavaScript origins and redirect URIs against it.

In order:

1. **A domain, and the two documents on it.** Register it, put it in front of `orbit-web` (Container
   Apps custom domain plus its managed certificate), verify it in Google Search Console under the same
   Google account that owns the Cloud project. Publish a privacy policy and terms of service on it -
   they have to say what Google user data Orbit reads, what it stores, who else sees it, and how someone
   deletes it. For this integration the honest answer is short: calendar events Orbit itself created,
   written on the reader's instruction, never read back.
2. **Enable the Google Calendar API** on the project that already issues Orbit's sign-in client ids
   (APIs & Services → Library). Verification is per project, and Orbit has one.
3. **Fill in the consent screen** (Branding / Audience): app name, support email, developer contact,
   the authorised domain from step 1, and links to both documents. A logo is optional and not free -
   uploading one adds a brand review on top of the scope review.
4. **Add the scope** and write its justification: what Orbit does with it (creates and updates events
   the reader asked for, in their own calendar), and why nothing narrower does - there is no
   write-only-what-I-created scope.
5. **Record the demo video.** Unlisted on YouTube is fine. It has to show the whole flow end to end: the
   browser's address bar with the client id visible on the consent screen, somebody granting consent,
   and then what the app actually does with the access - creating the event and it appearing in Google
   Calendar. A video that only shows the consent screen comes back with questions.
6. **Switch the app from Testing to In production and submit.** Then answer whatever comes back;
   expect at least one round of questions.

**One trap while still in Testing**: for an app in that state, Google expires refresh tokens after
**seven days**. T3's "does a token survive a restart" is therefore testable, and "does it still work next
week" is not, until verification passes. Worth knowing before someone spends a day chasing a token that
was revoked by policy rather than by a bug.

**Done when** an account that is neither a project member nor a listed test user can complete the consent
flow and stay connected for longer than a week.

### T2 - The authorization-code flow and a client secret (M)

Today the browser gets an ID token and `GoogleIdentityVerifier` checks it; that is the whole of Google in
this codebase. Writing to a calendar needs an access token for the scope above and a refresh token to
keep working tomorrow, which means an authorization-code exchange with `access_type=offline` and
`prompt=consent`.

- `Orbit.GoogleIntegration`: a `GoogleCalendarAuthorizer` that exchanges a code, refreshes an access
  token, and revokes a grant.
- `GoogleAuthSettings` gains `ClientSecret` - and its class comment, which currently states there is no
  secret to keep, has to stop being true carefully: the secret is an environment variable fed from a
  Container App secret, never a committed file (see [azure-setup.md](azure-setup.md), and the
  `.env.example` rule in `.claude/CLAUDE.md`).
- An endpoint pair in `Orbit.Api`: start the consent flow, and take the redirect back.

**Done when** an account can connect its Google Calendar from Options and disconnect it again, and
disconnecting revokes the grant at Google rather than only forgetting it locally.

### T3 - Somewhere to keep the refresh token (M)

A refresh token is a long-lived credential to somebody's calendar. It needs its own table, encrypted at
rest, and a revocation path that works from both ends - Orbit's own disconnect, and the user revoking
Orbit from their Google account, which Orbit only discovers by getting a refusal on the next call.

- New entity `GoogleCalendarAuthorization` (user id, ciphertext, scope, granted at, revoked at) with an
  EF Core migration in `Orbit.Data/Migrations`.
- Decide the encryption: ASP.NET Data Protection with a persisted key ring, or Azure Key Vault. Not the
  chat's end-to-end scheme - that one deliberately keeps the server unable to read anything, and here the
  server is the party that has to use the secret.

**Done when** a token survives a restart, a revoked grant is detected on use and cleared rather than
retried forever, and no test or fixture writes a real one.

### T4 - The mapping, as its own testable thing (M)

A `GoogleCalendarEventMapper` in `Orbit.GoogleIntegration` turning `CalendarEventDetails` into the API's
event resource, implementing the table above. Pure, no HTTP, no I/O - the whole point is that the awkward
parts (colour narrowing, the reminder cap, the guest-role collapse) are unit-testable without touching
Google.

Two pieces worth calling out:

- **Colour.** Fetch the palette from `colors.get`, cache it, and pick the nearest id by distance in a
  perceptual space (CIELAB, not raw RGB - "nearest" in RGB puts things in visibly wrong buckets). Hard-
  coding Google's hexes would be quicker and would rot.
- **Idempotency.** Google accepts a client-supplied event `id` on insert, matching `[a-v0-9]{5,1024}`.
  An Orbit event id written as `Guid.ToString("n")` is 32 characters of `0-9a-f`, which fits - so an
  insert can be made repeatable rather than duplicating on a retry. Confirm the character rule against
  the current API reference before relying on it.

**Done when** the mapper has tests covering: a multi-line title, an all-day event spanning days, every
reminder channel, more lead times than Google's cap allows, a guest without a Google address, and a
mixed set of access levels.

### T5 - Push on create, update and delete (L)

The event's own lifecycle, one way, Orbit → Google.

- `CalendarEvent` gains a `GoogleSyncState` value object (calendar id, remote event id, last pushed at,
  state, last error) rather than four loose fields, plus its migration.
- `CreateCalendarEventCommandHandler`, the update handler and the delete handler each ask an
  `IGoogleCalendarWriter` afterwards - best effort, out of the request's own transaction, the way
  `SharedItemNotifier` is best effort about announcing a share.
- Whose calendar: the **owner's** `primary`. An event shared into an Orbit account does not go into that
  recipient's Google calendar; if that is ever wanted it is a separate feature with its own consent.

**Done when** creating, editing and deleting an event in Orbit is visible in Google within seconds, an
edit updates rather than duplicating, and a failure leaves the Orbit event saved and correct.

### T6 - Guests, invitations and the roles Google does not have (M)

Attendees, `sendUpdates`, and the `guestsCanModify` collapse from the table above. Note this overlaps
with what Orbit already does on its own: adding a guest sends an Orbit notification and, since the fix
in this branch, an e-mail through `SharedItemNotifier`. Two invitations for one appointment is a
decision to make deliberately, not something to discover in production.

**Done when** a guest with a Google address receives Google's own invitation exactly as often as
intended, and one without is silently left out of the Google copy while keeping their Orbit invitation.

### T7 - Somewhere for a failure to be seen (M)

API calls fail, get rate-limited and time out in ways a link never does. This is the difference between
"it works" and "it can be trusted": retries with backoff, and a per-event state the user can actually
see - the card saying this one did not reach Google, with a way to try again.

**Done when** an event that failed to sync says so on the calendar, retrying is one press, and nothing
retries in a tight loop.

### T8 - The link stays (S)

Whatever else changes, the template link remains for accounts that have not connected Google, and
`DevicePreferences.AllowGoogleExtras` still turns the whole thing off. The API path is what an account
gets *after* connecting, not a replacement for the thing that works with no setup at all.

**Done when** an account with no Google connection sees exactly today's behaviour.

### T9 - Two-way, if it is ever wanted (XL - not now)

Reading Google back needs watch channels or polling, a rule for when both sides changed the same event,
and a story for one deleted on one side. Everything above is worth having without it; this is a separate
decision with its own failure modes.

## What must not regress

- An account that has not connected Google keeps the link, unchanged.
- `Orbit.Mobile` has its own twin of the link builder. It is not touched by any of this - the phone is
  another thread's work - but the mapping rules above are exactly the ones it will eventually need, so
  keep them in `Orbit.GoogleIntegration` and out of `Orbit.Web`.
- No secret in a committed file, and `.env.example` gains a placeholder for the client secret.

## Testing

- The mapper: unit tests, no network (T4's list).
- The handlers: a fake `IGoogleCalendarWriter` recording what it was asked to do, the way
  `RecordingSharedItemNotifier` and `RecordingEmailSender` already work in `Orbit.Api.Tests`.
- The token store: encrypt/decrypt round-trip, revocation, and a refusal from Google clearing the grant.
- Nothing in CI ever calls Google.

## Decisions the owner has to make first

1. **Who sends the invitation** - Orbit, Google, or both (T6).
2. **What happens to `Priority`** - dropped, or a line in the description (T4).
3. **Opt-in per event or all of them** - every Orbit event goes to Google once connected, or only the
   ones a reader asks for. Changes the UI, not the plumbing.
4. **Where the encryption key lives** - Data Protection key ring or Key Vault (T3).
5. **Which domain Orbit lives at** - forced by T1 rather than chosen for this feature, but it is the
   first thing to buy and the slowest to take effect.
