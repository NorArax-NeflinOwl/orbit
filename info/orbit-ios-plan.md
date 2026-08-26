# Orbit.iOS — Plan

A native iOS client for Orbit, targeting iPhone 15 Pro, carrying every feature the Blazor web client
(`src/Clients/Orbit.Web`) has today. This document is the plan, not the work: nothing has been built,
and the decisions below should be settled before it is.

It is written against the state of the project at the time of writing — 107 API endpoints across
twelve route groups, all of which Orbit.iOS is expected to consume. See
[Current Status](current-status.md) for what "every feature" currently means.

## 1. The decision that comes first: native Swift, or MAUI?

The [Future Plan](future-plan.md#planned-features) and the top-level [README](../README.md) both name
**.NET MAUI** as the long-term client target, and `src/Clients/Orbit.Maui` exists as an empty,
unreferenced folder reserving that name. A native iOS app is a different answer to the same question,
and the two should not both be half-built.

**Recommendation: native Swift + SwiftUI, and retire the MAUI reservation.** The reasons are specific
to this app rather than general preference:

- **Encryption.** Orbit's chat is end-to-end encrypted with primitives the platform has to provide
  exactly (see §4). Apple's CryptoKit covers all of them first-class; under MAUI the same code would
  run through .NET's `System.Security.Cryptography`, which is fine in principle but adds a second
  interop surface to prove correct against the browser's WebCrypto behaviour, on top of the one
  Orbit.iOS already has to prove.
- **The iPhone 15 Pro features that make this worth doing at all** — Dynamic Island / Live Activities,
  the Action Button, interactive widgets, Face ID gating — are native APIs. Reaching them from MAUI
  means platform-specific Swift or binding layers anyway, i.e. writing the interesting half twice.
- **One platform is the stated goal.** MAUI's whole argument is code shared across iOS, Android,
  Windows, and macOS. If only iOS is being built, that argument buys nothing and costs an abstraction
  layer.

**What this costs, honestly:** no code reuse if an Android client is ever wanted, and the C#-shaped
contracts in `Orbit.Contracts` have to be re-expressed as Swift types rather than referenced. §5
proposes making that cheap instead of manual.

This is a decision for the project owner, not for this document — but the rest of the plan assumes
native Swift. If MAUI wins instead, §§4–7 still apply almost unchanged; only §8's phasing shifts.

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
  neither, even if that fallback is only "the feature is absent". Decide this per feature in §7, not
  at the end.

## 3. Scope: what "all implemented features" means

Everything below is implemented in Orbit.Web today and therefore in scope. Grouped by the API surface
each maps onto, since that is what Orbit.iOS actually consumes.

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
Orbit.iOS must supply natively rather than port:

| Web | iOS equivalent |
| --- | --- |
| `e2eeChat.js` (WebCrypto + IndexedDB) | CryptoKit + Keychain — **§4.1** |
| `pushNotifications.js` + `service-worker.js` (Web Push) | APNs + `UNUserNotificationCenter` — **§4.2** |
| `googleSignIn.js` | GoogleSignIn SDK or `ASWebAuthenticationSession` — **§4.3** |
| `locationMap.js`, `mapPicker.js` (Leaflet) | MapKit / `Map` in SwiftUI |
| `geolocation.js` | CoreLocation |
| `theme.js` | System light/dark, no custom work |
| `checklistTextEditor.js`, `chatScroll.js`, `viewport.js` | Native SwiftUI list/scroll behaviour |
| `fileDownload.js` | `ShareLink` / Files |
| `clientLogging.js` | `OSLog` |

## 4. The four hard problems

Everything else in this plan is ordinary app work. These four are where it can actually go wrong, and
three of them require **server changes** — Orbit.iOS is not a client-only project.

### 4.1 End-to-end encryption interop

Orbit.iOS must interoperate byte-for-byte with ciphertext produced by browsers, in both directions,
against the same stored keys. The spec is fixed by `wwwroot/js/e2eeChat.js` and cannot be renegotiated
without breaking every existing conversation:

- **Key agreement:** ECDH on **P-256**. Public keys are exchanged as WebCrypto `raw` format — the
  uncompressed EC point, 65 bytes, base64 — matching CryptoKit's `x963Representation`.
- **Message key:** WebCrypto's `deriveKey(ECDH → AES-GCM, length 256)`. **This is the raw ECDH shared
  secret used directly as the AES key — there is no KDF, no HKDF, no hashing.**
  On iOS this means taking `SharedSecret`'s raw bytes as the key, *not*
  `hkdfDerivedSymmetricKey(...)`, which is what almost every CryptoKit example reaches for. Getting
  this wrong produces code that encrypts and decrypts happily against itself and cannot read a single
  message from the web client. **Pin it with a cross-platform test vector before building anything on
  top of it.**
- **Messages:** AES-GCM, 12-byte random nonce, nonce stored and transmitted alongside the ciphertext,
  both base64. CryptoKit's `AES.GCM.SealedBox` splits nonce/ciphertext/tag — the combined
  representation must be assembled to match what the web sends.
- **Private key backup:** the private key is exported as **JWK**, JSON-serialised, then AES-GCM
  encrypted under a key from **PBKDF2-HMAC-SHA256, 600,000 iterations**, with the salt and the
  iteration count stored per backup (so the count can be raised later without invalidating old
  backups). This backup is how an iPhone gets the user's existing chat identity at all — see below.

**Key storage: Keychain, not the Secure Enclave.** The Secure Enclave is the reflexive answer and it
is the wrong one here: its keys are non-exportable by design, and Orbit's password-change flow
requires exporting the private key to re-wrap it under the new password (`OwnEncryptionKeyProvider.RewrapAsync`).
A Secure Enclave key would make password changes silently destroy chat history. Use a software key in
the Keychain, `kSecAttrAccessibleWhenUnlockedThisDeviceOnly`, and gate access behind Face ID at the
app level instead.

**Onboarding consequence worth designing for:** a fresh iPhone has no private key. It gets one by
restoring the password-wrapped backup at sign-in — which means **sign-in must capture the password**,
and a Google-only account (no password set) therefore cannot read chat on a new device until it sets
one. Orbit.Web already handles this exact case; Orbit.iOS must too, not discover it late.

### 4.2 Push notifications: Web Push and APNs are not the same thing

This is the largest server-side change the iOS client forces.

What exists: `IPushNotificationSender` is properly transport-agnostic (`Orbit.Core.Notifications`), so
the domain layer is ready for a second implementation. Good.

What does not: the stored subscription is Web-Push-shaped all the way down. `PushSubscriptionEntity`
holds `Endpoint`, `P256dhBase64`, `AuthBase64` — a browser endpoint URL and its two encryption
parameters. An APNs registration is a **device token plus a topic**, which does not fit those columns
in any honest way.

Required work, server-side:

1. Add a platform discriminator to the push subscription (domain type, entity, migration) and make
   the token/endpoint fields shaped per platform rather than assuming Web Push.
2. Add an `ApnsPushNotificationSender` implementing `IPushNotificationSender`, with an APNs auth key
   (`.p8`), key id, team id, and bundle id as configuration — following the existing `VapidSettings`
   pattern, and staying silent-but-warning when unconfigured, exactly as `VapidPushNotificationSender`
   does today.
3. Teach `PushNotificationDispatcher` to route each subscription to the sender for its platform, and
   keep the existing expired-subscription pruning working for APNs' own "unregistered" response.
4. Decide how `PushNotificationPayload` (`{title, body, url}` today) maps onto the APNs envelope,
   including what `url` means when the target is an app route rather than a web path.

None of this is exotic, but it is a schema change plus a new integration, and it should be scoped and
merged **before** the iOS client needs it rather than alongside.

### 4.3 Google sign-in accepts exactly one audience

`GoogleAuthSettings` holds a single `ClientId`, and `GoogleIdentityVerifier` validates ID tokens with
`ValidationSettings { Audience = [ClientId] }`. An iOS app has its **own** OAuth client id, so tokens
it obtains will fail that check.

Small, contained server change: allow a set of accepted audiences (web client id, iOS client id)
rather than one. Worth doing carefully — the comment on that line correctly notes that the audience
check is the security-critical part, so widening it must stay an explicit allowlist and never become
"accept any audience".

### 4.4 There is no API contract to generate a client from

The API has no OpenAPI/Swagger document. Every one of the 107 endpoints and its request/response shape
currently lives only in C# (`Orbit.Contracts`) and in `functionality.md` prose. Hand-writing the Swift
mirror of all of it is both a large chunk of the project's cost and a permanent source of drift: a
contract change in C# breaks iOS silently, at runtime, in the field.

**Recommendation, and the highest-leverage thing to do first:** add OpenAPI generation to `Orbit.Api`
(.NET 10 ships `AddOpenApi`/`MapOpenApi`; the project already targets it), then generate the Swift
client from that document as a build step. This turns "keep two languages' DTOs in sync forever" into
a compile error. It also benefits the web client and the docs, so it is not iOS-only work.

## 5. Proposed architecture

```
Orbit.iOS/
  App/            entry point, routing, session
  Features/       one module per area, mirroring the web pages
    Dashboard, Notes, Tasks, Calendar, Inventory,
    Chat, Contacts, Map, Notifications, Options
  Core/
    Api/          generated client + auth interceptor  (§4.4)
    Crypto/       CryptoKit E2EE, matching e2eeChat.js  (§4.1)
    Storage/      Keychain, local cache
    Push/         APNs registration + handling          (§4.2)
  Widgets/        home-screen widgets, Live Activities
```

- **UI:** SwiftUI, with `Observable` view models per feature. UIKit only where SwiftUI genuinely
  cannot reach.
- **Networking:** the generated client (§4.4) behind an auth layer that attaches the access token and
  refreshes on 401. **Refresh must be single-flight** — the server rotates refresh tokens and invalidates
  the old one, so two concurrent refreshes race and log the user out mid-use. Orbit.Web hit exactly
  this bug and fixed it in `TokenRefreshService`; Orbit.iOS should be built with the fix, not discover
  it.
- **Tokens:** Keychain, never `UserDefaults`.
- **Offline:** the web client is online-only and polls. Deciding how far Orbit.iOS deviates is an open
  question (§9) — a phone is offline far more often than a browser tab, but a read-only cache is a
  much smaller commitment than sync.
- **Localization:** the web client keys its strings by the English text itself (`Translations` /
  `PolishTranslations`) and Polish is being added now. Orbit.iOS should use standard `String Catalog`
  localisation with the same English-as-key convention, so the two stay comparable.

## 6. What iPhone 15 Pro actually buys

These are the reasons to build native rather than wrap the web app. Each maps onto a feature Orbit
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

## 7. Phasing

Each phase should end somewhere shippable-to-TestFlight rather than half-integrated.

| Phase | Contains | Done when |
| --- | --- | --- |
| **0. Server prerequisites** | OpenAPI (§4.4), APNs support (§4.2), multi-audience Google (§4.3) | Merged into `main`, web client unaffected |
| **1. Walking skeleton** | Xcode project, generated API client, auth + Keychain + single-flight refresh, sign in/out, one real screen (Notes list) | A real account signs in on a device and sees real notes |
| **2. Crypto spine** | CryptoKit E2EE against cross-platform test vectors, key restore from backup, 1:1 chat | A message sent from the web decrypts on iPhone and vice versa |
| **3. The content features** | Notes, Tasks, Calendar, Inventory — CRUD, sharing, edit locks, private items behind Face ID | Feature parity with the web for everything non-chat |
| **4. The rest of chat** | Group chat, roles, edit/delete, read receipts, forwarding, contacts | Chat parity |
| **5. Location and maps** | CoreLocation, MapKit, recording, sharing, viewing shared, Live Activity | Location parity plus the Dynamic Island share |
| **6. Notifications** | APNs registration, notification settings, in-app feed, deep links into app routes | A push taps through to the right screen |
| **7. Platform polish** | Widgets, Action Button, App Intents, Always-On, accessibility, Dynamic Type, localisation | Ready for review |

Phase 0 is genuinely blocking for 2 and 6 and should start immediately; phase 1 can run alongside it
if the API client is hand-stubbed for a few endpoints in the meantime.

## 8. Risks

- **Crypto interop is the schedule risk.** If the shared-secret detail in §4.1 is discovered late, it
  invalidates every message-handling assumption above it. Mitigate by making phase 2 start with test
  vectors generated *from the browser*, checked into the repo, and asserted against by both clients.
- **Chat history is not portable to a device that never had the key.** This is by design, but it will
  read as a bug to users. Needs deliberate onboarding copy, not an error state.
- **Two clients, one contract, no compiler between them** unless §4.4 is done. This is the drift risk,
  and it grows with every feature.
- **Polling on a phone is not free.** The web client polls chat once a second and the dashboard every
  three. Reproducing that literally on iOS will cost battery and get throttled in the background;
  push-driven refresh plus polling only while foregrounded is the minimum adjustment.
- **Scope.** This is full parity with a web client that has grown a lot and is still growing weekly
  — the feature list in §3 is a snapshot, not a fixed target. Whether Orbit.Web is expected to keep
  moving during the build (it is) changes the plan's shape, and §9 asks about it.

## 9. Open questions

These need answers from the project owner before phase 1, and they change the plan materially:

1. **Native Swift or MAUI** (§1) — and if native, should the empty `src/Clients/Orbit.Maui` folder be
   removed so it stops reading as a live plan?
2. **Does Orbit.Web keep evolving during this build?** Full parity with a moving target is a very
   different project from parity with a frozen one.
3. **Offline behaviour** — online-only like the web, read-only cache, or real offline editing? This is
   the single biggest architectural fork in the client.
4. **Distribution** — App Store, TestFlight only, or personal/ad-hoc? This decides how much of the
   review-facing work (privacy manifest, data-use disclosure, account deletion in-app — already
   supported, and an App Store requirement) lands in scope.
5. **Is an Android client ever likely?** It is the one argument that would reopen §1.

## 10. Not started yet

Deliberately, nothing has been created — no Xcode project, and **no `src/Clients/Orbit.iOS` folder**.
The empty `src/Clients/Orbit.Maui` directory is the cautionary example: it reads as an in-progress
client, builds nothing, is absent from `Orbit.sln`, and needed an explicit note in
[Current Status](current-status.md) to stop confusing people. Orbit.iOS should be created when phase 1
begins, not reserved in advance.
