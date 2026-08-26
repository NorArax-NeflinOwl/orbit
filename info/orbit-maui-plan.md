# Orbit.Maui — Plan

**`Orbit.Maui`**: one .NET MAUI project producing both the iOS and Android apps, carrying every
feature the Blazor web client (`src/Clients/Orbit.Web`) has today, **plus offline operation** and two
mechanisms the web client has no need for (§7, §8). **iPhone 15 Pro is the target device**; Android is
the second platform.

This document is the plan, not the work: nothing has been built yet.

It is written against the state of the project at the time of writing — 107 API endpoints across
twelve route groups, all of which the mobile client is expected to consume. See
[Current Status](current-status.md) for what "every feature" currently means.

**Settled so far:** the name (`Orbit.Maui`, reusing the folder already reserved for it), the framework
(§1), that the app works offline (§5), and that offline editing is **restrictive** — only items nobody
else can change (§5.4). Still open: §12.

## 1. Framework: .NET MAUI

The [Future Plan](future-plan.md#planned-features) and the top-level [README](../README.md) both named
**.NET MAUI** as the long-term client target, and `src/Clients/Orbit.Maui` already existed as an empty
folder reserving the name. That reservation is now the plan, and the folder becomes the project.

An earlier draft of this document recommended native Swift instead. That rested on reading the goal as
iOS-only, which was wrong: **Android and iOS are both wanted.** That single fact reverses the answer,
because every argument for native assumed there was no second platform to share with.

- **Both platforms from one codebase.** This is MAUI's entire argument, and here it applies: two apps
  are being built, not one.
- **Direct reuse of the code Orbit already has.** A MAUI project can reference `Orbit.Contracts`
  outright — the same DTOs the API serves and the web client consumes. That removes the largest single
  cost in this plan (re-expressing 107 endpoints' worth of contracts in another language) *and* the
  drift risk that came with it, since a contract change then breaks the mobile build at compile time
  rather than in the field. It also demotes the OpenAPI work in §4.4 from prerequisite to nice-to-have.
- **The encryption maps more cleanly in .NET than in Swift, not less.** This reverses the earlier
  draft's first argument. The interop hazard in §4.1 is that WebCrypto uses the *raw* ECDH shared
  secret with no KDF. .NET exposes exactly that as
  `ECDiffieHellman.DeriveRawSecretAgreement()`, whereas CryptoKit steers hard toward
  `hkdfDerivedSymmetricKey(...)`, which is the wrong answer here and looks right. .NET's default path
  is the correct one; Swift's is the trap. (Still prove it with a test vector — see §4.1.)
- **It survives changing operating system**, which native Swift does not. See §1.1.

**What this costs, honestly:** the iPhone 15 Pro features in §9 — Dynamic Island / Live Activities,
the Action Button, widgets, Face ID gating — are native iOS APIs with no MAUI abstraction over them.
They need platform-specific code under `Platforms/iOS`, and their Android counterparts are different
again. That work does not disappear; it just sits inside a shared project instead of a separate one.
Budget for it explicitly rather than assuming "cross-platform" covers it.

**Naming, settled.** One MAUI project yields both apps, so a platform-specific name would describe
only half of it. The project is `Orbit.Maui`, taking over the folder already reserved under
`src/Clients` — which also removes the "two competing reservations" problem the empty folder created.

### 1.1 Can this be developed from Windows?

Short answer: **yes for Android, never entirely for iOS — but MAUI degrades gracefully where native
Swift stops dead.**

The constraint is Apple's, not the framework's, and no toolchain choice removes it:

> Compiling and code-signing an iOS app requires Apple's toolchain, which runs only on macOS.

What that means for each option:

| | On Windows | Mac still needed for iOS? |
| --- | --- | --- |
| **Native Swift** | Impossible — Xcode is macOS-only | Yes, for everything |
| **MAUI — Android target** | Fully supported, no Mac involved | No |
| **MAUI — Windows target** | Fully supported | No |
| **MAUI — iOS target** | Build/debug via a networked Mac build host ("Pair to Mac") | Yes, but it can be remote |

So switching to Windows would **not** block the project under MAUI. Android development continues
untouched; iOS work needs a Mac reachable over the network, which can be:

- the current Mac, kept on the LAN purely as a build host;
- a cloud Mac (MacStadium, AWS EC2 Mac, Scaleway) rented per hour;
- **a macOS runner in GitHub Actions** — worth calling out because this repo already deploys through
  GitHub Actions (`.github/workflows/main_orbit.yml`), so CI-built iOS artifacts are an incremental
  change rather than new infrastructure.

Two caveats worth verifying at the time rather than trusting this document:

- MAUI has a "Hot Restart" mode that deploys to a *physical* iPhone from Windows without a Mac for
  day-to-day iteration, but with real limitations (no simulator, needs a paid Apple Developer account)
  and it does **not** cover release builds or App Store submission. Treat it as an iteration
  convenience, not as "no Mac needed".
- App Store submission itself is macOS-tooling territory. A CI runner satisfies it; a Windows machine
  alone does not.

**Practical conclusion:** under MAUI, a Mac is a *build resource* that can be borrowed, rented, or run
in CI. Under native Swift it is the daily development machine, and moving to Windows ends iOS work.
If moving to Windows is genuinely on the table, that alone settles §1 in MAUI's favour.

## 2. Target device

"iPhone 15 Pro exactly" is read here as **the reference device**: it defines the baseline capabilities
the app may assume, the screen it is designed against, and the only device tested during development.

| | |
| --- | --- |
| Display | 6.1", 2556×1179 at 460 ppi, Super Retina XDR |
| Refresh | ProMotion, adaptive 1–120 Hz, plus Always-On display |
| Chip | A17 Pro |
| Distinctive hardware | Dynamic Island, Action Button, Face ID, USB-C |
| Shipped OS | iOS 17 |

**Deployment target: iOS 17 as the floor.** That is what the device shipped with, and it is what
Live Activities, interactive widgets, and App Intents in their current form need. Set the actual
deployment target to the current major minus one when work starts.

Worth being explicit about two consequences, because "exactly one device" is not a thing the App
Store really has:

- The app will still *install* on any iPhone meeting the deployment target. Designing only for the
  15 Pro means smaller screens (SE, mini) and non-Pro devices without an Action Button or Dynamic
  Island are untested, not blocked. Either accept that and say so, or add a device-family check.
- Anything built on the Dynamic Island or Action Button needs a defined fallback on hardware that has
  neither, even if that fallback is only "the feature is absent". Decide this per feature in §9, not
  at the end.

## 3. Scope: what "all implemented features" means

Everything below is implemented in Orbit.Web today and therefore in scope. Grouped by the API surface
each maps onto, since that is what Orbit.Maui actually consumes.

| Area | Endpoints | Notes for iOS |
| --- | --- | --- |
| Auth: register, login, refresh, logout, password reset | `/api/auth/*` | JWT 15 min, refresh token 30 days |
| Google sign-in | `POST /api/auth/google`, `POST/DELETE /api/users/me/google` | Needs a separate iOS client id — see §4.3 |
| Account: profile, email verification, password set/change, deletion | `/api/users/me/*` | |
| Notes, incl. checklist lines, sharing, private notes | `/api/notes/*` | Private notes are client-encrypted |
| Tasks, incl. group lists, pinning, item moves, sharing | `/api/tasks/*` | |
| Calendar, incl. recurrence, reminders, sharing, edit locks | `/api/calendar-events/*` | |
| Inventory: warehouses, items, sharing, locks | `/api/warehouses/*` | |
| Chat 1:1: send, edit, delete, read receipts, approval | `/api/chat/*` | End-to-end encrypted — see §4.1 |
| Group chat: create, members, roles, messages | `/api/chat/groups/*` | One ciphertext copy per member |
| Contacts and user search | `/api/chat/contacts`, `/api/users/search` | |
| Location: record own, share with contacts, view shared | `/api/users/me/location*` | |
| Push notifications | `/api/push/*` | **Web Push only today** — see §4.2 |
| In-app notification feed, settings, read state | `/api/notifications/*` | |
| Public share links | `/api/share-links/*`, `/api/public/*` | |
| Export / import | `/api/transfer/*` | |
| Client feature flags | `/api/config/client-flags` | |

Beyond the API, Orbit.Web relies on twelve browser-side scripts (`wwwroot/js/`). Each is a capability
Orbit.Maui must supply natively rather than port:

| Web | iOS equivalent |
| --- | --- |
| `e2eeChat.js` (WebCrypto + IndexedDB) | `System.Security.Cryptography` + `SecureStorage` — **§4.1** |
| `pushNotifications.js` + `service-worker.js` (Web Push) | APNs on iOS, FCM on Android — **§4.2** |
| `googleSignIn.js` | Platform Google Sign-In, or `WebAuthenticator` — **§4.3** |
| `locationMap.js`, `mapPicker.js` (Leaflet) | `Microsoft.Maui.Controls.Maps` (MapKit / Google Maps underneath) |
| `geolocation.js` | `Geolocation` (`Microsoft.Maui.Devices.Sensors`) |
| `theme.js` | `AppThemeBinding` / system light-dark |
| `checklistTextEditor.js`, `chatScroll.js`, `viewport.js` | Native `CollectionView` behaviour |
| `fileDownload.js` | `Share` / `FileSaver` |
| `clientLogging.js` | `ILogger` — the same abstraction the web client already logs through |

## 4. The four hard problems

Everything else in this plan is ordinary app work. These four are where it can actually go wrong, and
three of them require **server changes** — Orbit.Maui is not a client-only project.

### 4.1 End-to-end encryption interop

Orbit.Maui must interoperate byte-for-byte with ciphertext produced by browsers, in both directions,
against the same stored keys. The spec is fixed by `wwwroot/js/e2eeChat.js` and cannot be renegotiated
without breaking every existing conversation:

- **Key agreement:** ECDH on **P-256**. Public keys are exchanged as WebCrypto `raw` format — the
  uncompressed EC point, 65 bytes (`0x04` ‖ X ‖ Y), base64.
- **Message key:** WebCrypto's `deriveKey(ECDH → AES-GCM, length 256)`. **This is the raw ECDH shared
  secret used directly as the AES key — there is no KDF, no HKDF, no hashing.**
  In .NET this is `ECDiffieHellman.DeriveRawSecretAgreement()`, which returns exactly that value. Note
  that the *other* `Derive*` overloads (`DeriveKeyFromHash`, `DeriveKeyFromHmac`) all apply a KDF and
  are therefore wrong here — as is CryptoKit's `hkdfDerivedSymmetricKey(...)` had this gone native.
  Getting it wrong produces code that encrypts and decrypts happily against itself and cannot read a
  single message from the web client. **Pin it with a cross-platform test vector before building
  anything on top of it.**
- **Messages:** AES-GCM, 12-byte random nonce, nonce stored and transmitted alongside the ciphertext,
  both base64. .NET's `AesGcm` takes nonce, ciphertext, and tag as separate buffers, while WebCrypto
  appends the 16-byte tag to the ciphertext — so the tag must be split off on decrypt and appended on
  encrypt to match what the browser sends.
- **Private key backup:** the private key is exported as **JWK**, JSON-serialised, then AES-GCM
  encrypted under a key from **PBKDF2-HMAC-SHA256, 600,000 iterations**, with the salt and the
  iteration count stored per backup (so the count can be raised later without invalidating old
  backups). This backup is how a phone gets the user's existing chat identity at all — see below.
  JWK is not a format .NET exports natively for EC keys, so this needs explicit mapping between JWK's
  base64url `d`/`x`/`y` and `ECParameters` — a small, well-defined piece of work, and another good
  test-vector target.

**Key storage: the platform keystore, not a hardware-backed key.** `SecureStorage` maps to the iOS
Keychain and the Android Keystore, which is the right home. What to avoid on both platforms is a
*hardware-backed, non-exportable* key (Secure Enclave / StrongBox): those are the reflexive answer and
the wrong one here, because Orbit's password-change flow requires exporting the private key to re-wrap
it under the new password (`OwnEncryptionKeyProvider.RewrapAsync`). A non-exportable key would make
changing the password silently destroy chat history. Keep the key exportable in the keystore and gate
access behind biometrics at the app level instead.

**Onboarding consequence worth designing for:** a fresh phone has no private key. It gets one by
restoring the password-wrapped backup at sign-in — which means **sign-in must capture the password**,
and a Google-only account (no password set) therefore cannot read chat on a new device until it sets
one. Orbit.Web already handles this exact case; the mobile client must too, not discover it late.

**Built, following Orbit.Web's shape deliberately.** `OwnEncryptionKeyProvider` mirrors the web client's
split: `EnsurePublicKeyAsync`/`OpenAsync` never create or restore anything, and only
`UnlockOrCreateAsync` — called right after signing in or registering, while the plaintext password
exists — may. A password change re-wraps the backup through `RewrapAsync`, without which the backup
stays readable only under the old password and the next device silently starts a fresh key, losing every
earlier message.

**Two deliberate departures from the web client**, both the same principle: never replace a key unless
the server has confirmed there is nothing to replace.

1. Orbit.Web treats a *failed* backup lookup as "no backup exists" and generates a fresh key, so a
   browser is never locked out of chat. On a phone, losing the network mid-sign-in is ordinary rather
   than rare, and the same rule would discard the user's real key for the length of a tunnel. The API
   already distinguishes the two — it answers 204 for "no backup", deliberately, rather than 404 — so
   the mobile client acts only on that answer, and a lookup it could not make leaves chat locked.
   Locked is recoverable; generated is not.
2. Likewise when a backup exists but the password does not open it, which means it was wrapped under an
   older password. The key inside is still the account's real one.

**The gate is built too.** `ChatKeyGatePage` mirrors the web's `ChatPasswordGate`, keeping the same
three situations in one place because they differ only in which secret unlocks the key: a Google account
with no password sets one, a known password on a new device restores the backup, and a forgotten
password resets by email code.

The reset path is where the two departures above have to be answered rather than merely stated. Orbit.Web
gets a working reset for free, because it generates a fresh key whenever a backup will not open; the
mobile provider refuses that by default, so a reset would otherwise leave chat locked forever - the old
backup can never be opened by anyone again, including its owner. So the gate calls
`ReplaceAfterPasswordResetAsync` explicitly. That keeps the rule intact and names it precisely: not
"never replace a key", but "never replace one without being asked".

### 4.2 Push notifications: Web Push, APNs, and FCM are three different things

This is the largest server-side change the mobile client forces, and going cross-platform makes it
three transports rather than two.

What exists: `IPushNotificationSender` is properly transport-agnostic (`Orbit.Core.Notifications`), so
the domain layer is ready for more implementations. Good.

What does not: the stored subscription is Web-Push-shaped all the way down. `PushSubscriptionEntity`
holds `Endpoint`, `P256dhBase64`, `AuthBase64` — a browser endpoint URL and its two encryption
parameters. An APNs registration is a **device token plus a topic**, and an FCM one is a registration
token; neither fits those columns in any honest way.

Required work, server-side:

1. Add a platform discriminator to the push subscription (domain type, entity, migration) and make
   the token/endpoint fields shaped per platform rather than assuming Web Push.
2. Add an `ApnsPushNotificationSender` (APNs auth key `.p8`, key id, team id, bundle id) and, for
   Android, an FCM sender — each implementing `IPushNotificationSender`, following the existing
   `VapidSettings` pattern, and staying silent-but-warning when unconfigured exactly as
   `VapidPushNotificationSender` does today.
3. Teach `PushNotificationDispatcher` to route each subscription to the sender for its platform, and
   keep the existing expired-subscription pruning working for each transport's own "gone" response
   (APNs `Unregistered`, FCM `UNREGISTERED`).
4. Decide how `PushNotificationPayload` (`{title, body, url}` today) maps onto each envelope,
   including what `url` means when the target is an app route rather than a web path.

None of this is exotic, but it is a schema change plus two integrations, and it should be scoped and
merged **before** the mobile client needs it rather than alongside.

### 4.3 Google sign-in accepts exactly one audience

`GoogleAuthSettings` holds a single `ClientId`, and `GoogleIdentityVerifier` validates ID tokens with
`ValidationSettings { Audience = [ClientId] }`. A mobile app has its **own** OAuth client id per
platform, so tokens it obtains will fail that check.

Small, contained server change: allow a set of accepted audiences (web, iOS, and later Android)
rather than one. Worth doing carefully — the comment on that line correctly notes that the audience
check is the security-critical part, so widening it must stay an explicit allowlist and never become
"accept any audience".

**Configuration.** Client ids are not secrets — the existing comment says so, and a mobile client id
ships inside the app binary regardless. Even so, this repo deliberately keeps the *value* out of
tracked files and only commits placeholders: `appsettings.Development.json.example` carries an empty
`GoogleAuth:ClientId`, the real value lives in the gitignored `appsettings.Development.json`, and
deployment reads `GOOGLE_CLIENT_ID` from a gitignored `.env` (see `docker-compose.yml`). Whatever
shape the allowlist takes should follow the same convention — placeholders tracked, values not — and
the iOS client id is recorded outside this document for that reason.

### 4.4 The API contract — mostly solved by choosing MAUI

This was the fourth hard problem while the plan assumed Swift: 107 endpoints whose request and
response shapes live only in C# (`Orbit.Contracts`) and in `functionality.md` prose, with no OpenAPI
document to generate a client from. Hand-writing that in another language is a large cost and a
permanent drift risk.

**Choosing MAUI removes it.** The mobile project references `Orbit.Contracts` directly, exactly as
`Orbit.Web` does, so there is one definition of every DTO and a contract change breaks the mobile
build at compile time. Nothing to generate, nothing to keep in sync.

What remains is smaller: the *calls* themselves still have to be written, since `Orbit.Web`'s typed
API clients (`NotesApiClient`, `ChatApiClient`, …) live in the web project rather than in a shared
one. Two options, worth deciding early:

- **Move the API clients into a shared project** both `Orbit.Web` and the mobile app reference. They
  are already thin wrappers over `HttpClient` returning `Orbit.Contracts` types, so most should move
  as-is. This is the higher-value option and shrinks the mobile client's scope considerably.
- **Write mobile-specific clients**, accepting the duplication.

Adding OpenAPI to `Orbit.Api` is still worth doing — for documentation, for testing, and for any
future non-.NET client — but under MAUI it is an improvement rather than a prerequisite, and it drops
out of the blocking phase in §10.

## 5. Offline operation and synchronisation

**Decided: the app works offline**, backed by a local database, with background synchronisation when a
connection returns. This is the single largest departure from the web client, which is online-only and
polls, and it reaches into almost every feature — so the consequences are worth being explicit about
rather than discovering one at a time.

### 5.1 The local store

**SQLite via EF Core.** The team already writes EF Core against this domain on the server, the entity
shapes will look familiar, and migrations are a solved problem. The lighter `sqlite-net` is the usual
MAUI default and would also work; the argument for EF Core here is familiarity with an existing
codebase rather than raw startup time.

The local schema mirrors `Orbit.Contracts` DTOs plus per-row sync bookkeeping (`LastSyncedAtUtc`, a
dirty flag, and the local-vs-server id distinction for records created offline).

**The local database holds decrypted content, and that deserves attention.** Private notes, task
lists, and warehouses are client-encrypted precisely so the server can never read them; caching them
in a plaintext SQLite file weakens exactly the property that feature exists to provide. At minimum the
file belongs in app-private storage relying on platform disk encryption; encrypting the database
itself (SQLCipher, key in the platform keystore) is the stronger option and should be decided
deliberately, not defaulted.

### 5.2 What can work offline, and what cannot

| Works offline | Read-only offline | Online-only |
| --- | --- | --- |
| Notes, tasks, calendar, inventory — read and edit | Chat history | User search (`/api/users/search`) |
| Recording your own location | Notification feed | Share links, export/import |
| Composing chat messages (queued, see §5.5) | Contacts | Viewing others' shared locations |
| | | Sign-in, Google linking |
| | | **Registering an account** |
| | | **Changing the username, email address, or password** |
| | | **Deleting the account** |

Anything requiring a fresh server decision — a share offer, a lock, an account change — stays online.

**Identity is online-only, and the app says so rather than queueing it.** Registering, and changing the
username, email address, or password, all go straight to the server and are refused up front when there
is no connection - `AccountClient` has no queued outcome to return. Each needs a verdict only the server
can give (is this username free, is this email address already registered, is this the current
password), and each changes how the user signs in *everywhere*, not only on this phone. A queued
password change is the clearest failure: it would tell someone their password had changed while the old
one still worked, possibly for days. Registration goes further - the account is created on the server
before anything is written locally, so a local account the server has never heard of cannot exist.

Notes can wait in an outbox because nothing outside the phone depends on when they land. An identity
cannot.

**Deleting an account is online-only, and the app should say so rather than queue it.** It is the one
action where an outbox would actively mislead: the request is irreversible, it needs the password
checked against the server, and it has effects the phone cannot carry out on its own — on the server
side it also takes the account out of its chat groups, promoting a new admin where it was the last one.
An offline "delete my account" that sits in a queue would leave someone believing their account was
gone while it was still live, possibly for days. Grey the action out while offline and explain why.

### 5.3 Pulling changes: delta and tombstones (built)

Two gaps stood in the way of real sync, both server-side:

1. **No `since` parameter.** Every collection endpoint returns everything it has; only
   `GET /api/chat/messages/{otherUserId}` accepts `sinceUtc`. The main DTOs already carry
   `UpdatedAtUtc` (`NoteDto`, `TaskDto`, `CalendarEventDto`, `WarehouseDto`, `InventoryItemDto`), so
   adding a `since` filter is straightforward — the data is there, the parameter isn't.
2. **No way to learn about deletions.** A full pull detects a delete by absence; a *delta* pull cannot
   see one at all, so a note deleted on the web would live forever on the phone. This needs either
   soft-delete tombstones server-side, or a periodic full reconciliation pull to catch what the delta
   missed. Tombstones are the cleaner answer and the larger change.

**Both now exist.** `GET /api/{notes,tasks,calendar-events,warehouses}/changes?since=` returns what
changed and what was deleted, and deletions are recorded as tombstones in one table covering every
entity type — see `Orbit.Core.Sync.SyncTombstone`. The cursor comes back as an ISO-8601 UTC string
ending in `Z`, safe to drop straight into the next URL, and `since` is inclusive so a change landing
mid-request is re-sent rather than lost.

### 5.4 Pushing changes, and conflicts (built for notes)

Local mutations go into an **outbox** and replay in order when connectivity returns. The conflict
question is where Orbit's existing design bites.

**Edit locks are the sharp edge.** Notes, task lists, calendar events, and warehouses are protected by
server-held, time-limited edit locks with a heartbeat (`LockedByUserId`, `LockExpiresAtUtc`, and
`IsLockedByAnotherUser`) — the mechanism that stops two people editing the same shared item at once.
**An offline client cannot hold a lock.** It can only find out at replay time that someone else was
editing, by which point the user has already done the work.

**Decided: restrictive.** Offline editing is allowed only for items **nobody else can change**;
anything shared, in either direction, is read-only until connectivity returns. The alternative —
edit anything and resolve on replay — needs a conflict UI and delivers "your change was rejected
because someone else was editing" long after the user did the work. Refusing up front is honest and
surprises nobody.

Last-write-wins on `UpdatedAtUtc` then covers what remains, and is defensible there: the only writer
who can lose anything is the same person on another device. For a shared item it would not be, because
it silently discards someone else's work — the exact outcome the locks were added to prevent.

**Sharing is not a copy, which is why this matters.** Accepting a share does not duplicate the item:
`NoteAccessResolver` (and its task, calendar, and warehouse equivalents) loads the *owner's* row and
stamps the caller's access level onto it. Two people with `CanEdit` are editing one row, which is what
the locks exist for and what makes offline editing of a shared item genuinely unsafe.

**One prerequisite the API doesn't meet yet.** A client can see when an item was shared *with* it —
`IsShared` on the DTO — but nothing tells an **owner** that they shared an item *out*. So the owner's
copy of a note that someone else can edit looks, to the client, exactly like a private one. Applying
this policy needs the server to say so: a flag on the owner's view meaning "somebody else has an
accepted grant on this". Worth doing as its own change, since deriving it per item is a query per item
unless it is batched.

### 5.5 Offline and end-to-end encryption (the one-to-one half is built)

Two concrete rules, both consequences of §4.1:

- **Encrypting offline needs the recipient's public key cached.** A message to a contact whose key was
  never fetched cannot be composed offline at all. Cache public keys alongside contacts.
- **Encrypt group messages at send time, never at compose time.** A group message is one ciphertext
  copy per member, and the server validates *exactly one copy per current member* — no more, no fewer.
  A message encrypted at compose time and sent an hour later carries a stale membership list and will
  be rejected, correctly. The outbox must therefore store the plaintext (locally, protected per §5.1)
  and perform the fan-out at the moment of sending.

  **Built, and followed even where it isn't needed yet.** `EncryptedChatMessageSender` encrypts at send
  time for one-to-one messages too, where nothing would notice the difference — precisely so group chat
  is a fan-out added to a working outbox rather than a rewrite of one. The queue therefore holds
  plaintext, which is the app's only plaintext at rest and the sharpest argument for answering §5.1.
  Received messages are cached as ciphertext and opened per screenful, so the local database stays no
  more revealing than the server.

### 5.6 Background sync

- **iOS:** `BGAppRefreshTask` — opportunistic, scheduled at the system's discretion, with no timing
  guarantee. It is not a substitute for syncing on foreground.
- **Android:** `WorkManager`, with constraints on connectivity.
- **Silent push as a trigger** ties this to §4.2 and is the closest thing to timely sync on iOS.

Both are platform-specific code under `Platforms/`, not shared MAUI surface. Foreground sync on app
resume is the reliable path; background sync is an optimisation on top of it.

## 6. Proposed architecture

```
src/Clients/Orbit.Mobile/          one MAUI project, two apps
  App/              entry point, shell/routing, session
  Features/         one folder per area, mirroring the web pages
    Dashboard, Notes, Tasks, Calendar, Inventory,
    Chat, Contacts, Map, Notifications, Options
  Core/
    Api/            typed clients + auth handler        (§4.4)
    Crypto/         E2EE matching e2eeChat.js           (§4.1)
    Data/           SQLite (EF Core), local schema      (§5.1)
    Sync/           outbox, delta pull, conflict policy (§5.3-5.5)
    Storage/        SecureStorage for keys and tokens
    Push/           APNs + FCM registration             (§4.2)
    Update/         version gate                        (§7)
    Diagnostics/    file logging + upload               (§8)
  Platforms/
    iOS/            Live Activities, Action Button, widgets, Face ID  (§9)
    Android/        the Android counterparts of the above
```

References `Orbit.Contracts` (and, if §4.4's first option is taken, a shared API-client project)
rather than redefining anything the server already declares.

- **UI:** MAUI with XAML, MVVM, and `CommunityToolkit.Mvvm` for the boilerplate. Handlers rather than
  custom renderers where platform behaviour has to differ.
- **Networking:** the typed clients (§4.4) behind a `DelegatingHandler` that attaches the access token
  and refreshes on 401 — the same shape as `Orbit.Web`'s `AuthorizationMessageHandler`, and reusable
  if the clients move to a shared project. **Refresh must be single-flight** — the server rotates
  refresh tokens and invalidates the old one, so two concurrent refreshes race and log the user out
  mid-use. Orbit.Web hit exactly this bug and fixed it in `TokenRefreshService`; the mobile client
  should be built with the fix, not rediscover it.
- **Tokens:** `SecureStorage` (Keychain / Android Keystore), never `Preferences`.
- **Reads go through the local database, never straight to the API.** Screens bind to what SQLite
  holds and the sync layer keeps it current (§5). This is what makes offline work, and it is a
  structural decision rather than a feature: retrofitting it later means rewriting every screen.
- **Localization:** the web client keys its strings by the English text itself (`Translations` /
  `PolishTranslations`) and Polish is being added now. The same dictionary could move to a shared
  project and serve both clients, which is worth more than adopting `.resx` on mobile and translating
  everything twice.

## 7. Forced update

A mobile app cannot be rolled back the way a web deploy can: old versions stay on devices until their
owners update. When a release changes something the server can no longer support — a sync contract, an
encryption detail, a breaking API change — the app has to be able to refuse to run.

**Behaviour:** on startup, before the splash screen releases, the app asks the server what it should
do with its own version. Three outcomes:

| Verdict | Behaviour |
| --- | --- |
| `Supported` | Continue normally |
| `UpdateAvailable` | Continue, offer a dismissible prompt |
| `UpdateRequired` | Stop on the splash screen with a store link and no way past it |

**Versioning.** Semantic versioning, surfaced through MAUI's `ApplicationDisplayVersion` (the
human-readable `1.4.2`) and `ApplicationVersion` (the monotonic build number stores order by). The
server compares against a configured **minimum supported version** and a **latest version**, both
per-platform, since iOS and Android release independently and will drift apart.

**Where it lives server-side:** `GET /api/config/client-flags` already exists as the unauthenticated,
fetched-once-at-startup endpoint for exactly this kind of question, and is the natural home — either
extended, or joined by a sibling under `/api/config`. It must stay unauthenticated: a client too old
to sign in still needs to be told to update.

**The interaction with offline is the part worth getting right.** These two requirements pull against
each other: the version gate wants the server, and offline mode means the server is often unreachable.
A gate that blocks whenever it cannot reach the server would brick the app on a train — the precise
situation offline support exists for.

> **Rule: block only on a *known* verdict, never on a missing one.** Cache the last verdict with the
> version it applied to. Offline with no cached verdict, or a cached verdict for an older version:
> allow through. Only a cached-or-fresh `UpdateRequired` for *this* version stops the app.

The failure mode this accepts — an app that should have been blocked runs offline until it next
reaches the server — is strictly better than the alternative, and the server rejects whatever it
cannot support anyway.

## 8. Diagnostic logs

**Behaviour:** the app writes its own logs to a file on the device; the user can send that file to the
server, which parses it and stores the entries in a dedicated table alongside information about the
device they came from.

There is precedent to follow rather than invent: `wwwroot/js/clientLogging.js` already keeps the last
N warning/error lines in the browser's `localStorage`, and `Options.razor` exposes them behind a
"Show exceptions" switch with a per-entry Copy button, gated by a server-side environment flag. The
mobile version is the same idea with a file and an upload instead of a clipboard.

**Client side:**

- A rolling file sink (`Microsoft.Extensions.Logging` with a file provider, or Serilog) capped by size
  and count, so logs never grow without bound on a user's phone.
- Warning-and-above by default, with a switch to raise verbosity when actually chasing something.
- Sending is **explicit and user-initiated** from the options screen. Not automatic, not silent.

**Device information to attach:** app version and build, platform and OS version, device model,
locale, and whether the failure happened offline. That last one matters more here than in most apps,
because §5 means "it broke" and "it broke while offline" are genuinely different bugs.

**Server side:** a new endpoint (`POST /api/diagnostics/logs`, authenticated, rate-limited, with a
request-size cap), parsing into a table of `{ UserId, ReceivedAtUtc, AppVersion, Platform, OsVersion,
DeviceModel, Level, Timestamp, Message, Exception }`. Two constraints that are easy to miss:

- **Nothing decryptable may reach the log.** This is an end-to-end-encrypted app whose entire promise
  is that the server cannot read message content. A log that captures message plaintext, private note
  content, or key material hands the server exactly what the design spent effort withholding — and
  does it through a channel nobody thinks of as data. The client must scrub at the point of logging,
  not at the point of upload, and this belongs in review for any log statement added later.
- **The new table must be wired into account deletion.**
  `AccountDeletionRepository.DeleteAllDataForUserAsync` enumerates every table explicitly, one
  `ExecuteDeleteAsync` per table — a new table keyed by `UserId` will simply be missed unless it is
  added there. Leaving a deleted user's logs behind would quietly break the guarantee that deleting an
  account deletes everything it owns.

**Retention** should be finite and stated: diagnostic logs are the kind of data that accumulates
forever by default and is only ever read for a week after it arrives.

## 9. What iPhone 15 Pro actually buys

These are the reasons to build a real app rather than wrap the web one. Each maps onto a feature Orbit
already has.

- **Live Activity / Dynamic Island for an active location share.** Orbit's live location sharing has a
  real "this is running right now, and you can stop it" state — the exact thing the Dynamic Island
  exists for, and a genuine safety improvement over a share the user forgot about. Best fit on the
  device.
- **Face ID for private notes, task lists, and warehouses.** The `IsPrivate` feature already means
  "only the owner can read this, and the server never can". A biometric gate in front of those is the
  natural physical counterpart.
- **Action Button for quick capture.** One press to a new note or task, the most frequent action in
  the app.
- **Widgets and Live Activities for today's tasks and the next event.** The dashboard's most valuable
  content, without opening the app.
- **App Intents / Siri** for "add a task to …", reusing the same capture path.
- **ProMotion** — free, but it means list and chat scrolling must actually hold 120 Hz, which is a
  reason to keep heavy work off the main actor rather than an afterthought.

## 10. Phasing

Each phase should end somewhere installable on a real device rather than half-integrated. iOS leads
throughout — it is the harder platform and the one with a named target device; Android follows each
phase behind it, mostly for free apart from the platform-specific work.

| Phase | Contains | Done when |
| --- | --- | --- |
| **0. Server prerequisites** (built) | Version-gate endpoint (§7), diagnostic-log endpoint and table (§8), push transports (§4.2), multi-audience Google (§4.3), delta + tombstones for sync (§5.3), optionally the shared API-client project (§4.4) | Merged into `main`, web client unaffected |
| **1. Walking skeleton** (built) | `Orbit.Maui` project, `Orbit.Contracts` referenced, auth + `SecureStorage` + single-flight refresh, **version gate on startup (§7)**, sign in/out, one real screen | A real account signs in on a device and an out-of-date build is stopped on the splash screen |
| **2. Local store and sync spine** (built) | SQLite schema, repositories, outbox, delta pull, reconciliation, conflict policy — proven on Notes alone before anything else uses it | A note edited offline on the phone appears on the web after reconnect, and vice versa |
| **3. Crypto spine** (built) | E2EE against cross-platform test vectors, key restore from backup, 1:1 chat, offline outbox for messages | A message sent from the web decrypts on the phone and vice versa |
| **4. The content features** (built) | Tasks, Calendar, Inventory on the sync spine — CRUD, sharing, edit locks, private items behind biometrics | Feature parity with the web for everything non-chat |
| **5. The rest of chat** (built) | Group chat (send-time fan-out, §5.5), roles, edit/delete, read receipts, forwarding, contacts | Chat parity |
| **6. Location and maps** | Geolocation, maps, recording, sharing, viewing shared | Location parity |
| **7. Notifications and diagnostics** | APNs/FCM registration, notification settings, in-app feed, deep links, **file logging and upload (§8)** | A push taps through to the right screen; a user can send a log |
| **8. Platform polish** | Live Activities and the Dynamic Island location share, Action Button, widgets, accessibility, localisation | Ready for review |

Two things changed shape once offline became a requirement:

- **The sync spine is now phase 2, ahead of crypto and ahead of every feature.** Every screen reads
  from the local database (§6), so building features first and adding sync later means rewriting them.
  Proving it end-to-end on Notes alone — the simplest entity — is much cheaper than discovering the
  design is wrong across five features.
- **The version gate lands in phase 1, not at the end.** It is the mechanism that lets every later
  phase change the sync or crypto contract without stranding installed builds. Shipping it late means
  the builds that most need retiring are the ones that cannot be told to update.

Phase 0 is genuinely blocking for 2, 3 and 7, and should start immediately; phase 1 can run alongside
it. The iPhone-15-Pro-specific work in §9 deliberately lands last — it needs the Mac most and benefits
least from being started early.

## 11. Risks

- **Offline sync is now the largest risk in the plan, ahead of crypto.** It touches every screen, has
  no equivalent anywhere in the existing codebase to copy from, and its hardest part — conflicts
  against a server that uses edit locks (§5.4) — has no obviously correct answer. Mitigate by proving
  the whole spine on Notes alone in phase 2 and being willing to change the conflict policy before
  four more features depend on it.

  **Downgraded, not retired.** Task lists joined the spine as its second entity type, which is what the
  mitigation was for: the parts that turned out not to be about notes — replaying a queue in order,
  classifying which failures are worth retrying, remembering a cursor — were extracted rather than
  copied, and `NoteSynchronizer` shrank from 344 lines to 217 with its tests unchanged and still
  passing. What each feature still owns is small and visible: which requests its create, update and
  delete are, and how its DTO becomes a local row. The conflict policy did not need changing. Calendar
  events and warehouses are now expected to be additions rather than discoveries.
- ~~**Crypto interop is the other schedule risk.**~~ **Retired.** The mitigation was carried out as
  written: vectors generated *from the browser* running Orbit.Web's own `e2eeChat.js`, checked into
  `tests/Orbit.Mobile.Tests/Crypto`, and asserted against. The no-KDF detail in §4.1 is now pinned in
  two independent ways - the browser proved at generation time that `deriveKey` and the raw
  `deriveBits` secret are the same key, and .NET decrypts browser ciphertext using
  `DeriveRawSecretAgreement`. Both directions are verified: browser ciphertext opens in .NET, and a
  browser opens .NET ciphertext, including a JWK private-key backup written by .NET.

  What is *not* done is the rest of phase 3 - key storage on the device, restore at sign-in, and chat
  itself. The risk this bullet described was that the spec would be discovered wrong late; that part is
  settled.
- **A local database of decrypted content weakens what private items promise** (§5.1). Private notes
  exist so the server cannot read them; caching them in plaintext on the device moves the exposure
  rather than removing it. Decide on database encryption deliberately.
- **Diagnostic logs are a new way to leak plaintext** (§8) out of an app whose whole design avoids it.
  Scrubbing has to happen at the logging call, and stay a review concern afterwards.
- **Chat history is not portable to a device that never had the key.** This is by design, but it will
  read as a bug to users. Needs deliberate onboarding copy, not an error state. The key gate is where
  that copy lives now; it is written as an explanation rather than a failure.
- **The Mac is a dependency, not a preference** (§1.1). Under MAUI the project survives moving to
  Windows, but every iOS release build and store submission still needs macOS somewhere. Decide early
  whether that is a machine kept on the LAN or a CI runner, because discovering it at submission time
  is the expensive moment.
- **Polling on a phone is not free.** The web client polls chat once a second and the dashboard every
  three. Reproducing that literally on a phone will cost battery and get throttled in the background;
  push-driven refresh plus polling only while foregrounded is the minimum adjustment.

  **The minimum adjustment is in place for chat:** a conversation polls every five seconds and only
  while its screen is actually in front of someone, started and stopped by the page's own lifecycle.
  Silent push (§4.2) is still what would make this timely without a timer at all, and nothing else polls
  yet - the notes screen syncs on open and on pull-to-refresh only.
- **"Cross-platform" does not cover the interesting part.** The §9 features are per-platform code, so
  is background sync (§5.6), and Android needs its own answers to each. The shared-code win is real
  for the other 80%, not for these.
- **Scope.** This is full parity with a web client that has grown a lot and is still growing weekly,
  *plus* offline, *plus* two mechanisms the web client never needed — the feature list in §3 is a
  snapshot, not a fixed target. Whether Orbit.Web keeps moving during the build (it does) changes the
  plan's shape, and §12 asks about it.

## 12. Open questions

These change the plan materially and are worth answering before the phase they land in:

1. ~~**Offline conflict policy** (§5.4)~~ — **settled and built: restrictive**, and the owner-side gap
   is closed for all four shareable types. `IsSharedWithOthers` tells an owner that somebody holds
   accepted access, so the client can tell a private item from one another person may be editing.
2. **Is the local database encrypted?** (§5.1) ~~Needed before phase 2~~ — **still open, and now
   load-bearing.** Phase 2 shipped plain SQLite in app-private storage, relying on platform disk
   encryption. That is a deliberate deferral rather than an answer: private notes are client-encrypted
   so the server cannot read them, and the phone now caches them decrypted. Everything needed to change
   it is in `Orbit.Maui/Platform/LocalDatabase.cs` and one provider registration, so switching to
   SQLCipher stays cheap - but it does not get cheaper by waiting, and more entity types arriving makes
   the exposure wider rather than the change harder.
3. **Does Orbit.Web keep evolving during this build?** Full parity with a moving target is a very
   different project from parity with a frozen one. Right now the answer looks like yes, which argues
   for §4.4's shared-API-client option so parity work happens once rather than twice.
4. **Distribution** — App Store and Play Store, TestFlight/internal only, or personal? This decides
   how much review-facing work (privacy manifest, data-safety disclosure, in-app account deletion —
   already supported server-side, and a store requirement) lands in scope, and how urgently the Mac
   question in §1.1 needs answering. It also decides how the forced update in §7 sends people to
   update: a store link, or something else entirely.
5. **Where does the Mac live?** Kept as the development machine, kept on the LAN as a build host, or
   replaced by a CI runner. Only matters if Windows is actually on the cards, but it changes the
   day-to-day workflow completely.
6. **Diagnostic log retention** (§8) — how long uploaded logs are kept before deletion.

## 13. What exists so far

Phases 0 and 1 are built. `src/Clients/` now holds two mobile projects rather than one:

- **`Orbit.Mobile`** (`net10.0`) — everything decided without a device: the version gate, the session
  store, single-flight refresh, the authorization handler. In `Orbit.sln`, so `dotnet test` covers it.
- **`Orbit.Maui`** (`net10.0-ios`, `net10.0-android`) — the two app heads. Deliberately *not* in
  `Orbit.sln`: CI runs on `ubuntu-latest`, which can build neither head.

That split was not in the architecture sketch in §6, and is worth stating plainly: a MAUI head cannot
be referenced by an ordinary test project, so anything left inside it can only be checked by running
the app. With the sync spine named as the largest risk in the plan (§11), it needs to be somewhere a
test can reach it. The view models are the part still on the wrong side of that line — they hold real
behaviour and currently depend on MAUI's `Launcher` and page navigation. Worth moving before phase 4
adds five features' worth of them.

**Verified on a simulator, not merely compiled:** an account signs in, the session survives relaunch
from the Keychain, notes load through the token handler, and an out-of-date build stops on the splash
screen — including with the server switched off entirely, from the cached verdict, which is the rule
in §7 that matters most.
