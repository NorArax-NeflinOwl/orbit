# Orbit.Maui

The mobile client: one .NET MAUI project producing both the **iOS** and **Android** apps. iPhone 15 Pro
is the reference device. See [`info/orbit-maui-plan.md`](../../../info/orbit-maui-plan.md) for the plan
this is being built against.

Phases 1 and 2 are built: the version gate, sign in/out with the session in the Keychain, and a notes
screen that reads a local SQLite database and works with no connection — changes queue in an outbox and
replay when the network returns. Everything else is still ahead — see the plan's phasing.

The local database is **not encrypted**. It sits in app-private storage and relies on the platform's
disk encryption, which is a deliberate deferral rather than a decision — see §5.1 and open question 2
of the plan, and `Platform/LocalDatabase.cs`, which is the one place that would change.

## Where the code lives, and why it is split

| Project | Target | In `Orbit.sln`? |
| --- | --- | --- |
| `Orbit.Mobile` | `net10.0` | **Yes** — everything decided without a device: the version gate, auth, the local store and the sync spine |
| `Orbit.Mobile.Tests` | `net10.0` | **Yes** — ordinary xUnit against the above |
| `Orbit.Maui` | `net10.0-ios`, `net10.0-android` | **No** — the two app heads: XAML, view models, platform services |

The split exists so the interesting logic can be unit-tested. A MAUI head cannot be referenced by a
normal test project, so anything left inside it is only ever verified by running the app.

`Orbit.Maui` stays out of the solution because CI builds `Orbit.sln` on `ubuntu-latest` (see
`.github/workflows/ci.yml`), which cannot build `net10.0-ios` at all — that needs macOS and Xcode — and
cannot build `net10.0-android` without the Android SDK. Build the heads explicitly instead:

```bash
dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-android
dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-ios
```

The server, web client, and `Orbit.Mobile` keep building and testing through `Orbit.sln` as before.

## Running it against a local server

The API address is `OrbitApiSettings.Development`, and it differs per platform because "the machine
running the server" is not the same address from each:

| Running on | Reaches the Mac's `localhost:5080` as |
| --- | --- |
| iOS simulator | `http://localhost:5080` — it shares the Mac's loopback |
| Android emulator | `http://10.0.2.2:5080` — the emulator's fixed alias for its host |
| A physical device | Neither. Use the Mac's LAN address, and note iOS refuses plaintext HTTP to it |

iOS blocks cleartext HTTP by default. `Platforms/iOS/Info.plist` carries `NSAllowsLocalNetworking`,
which permits it for local and loopback hosts only — a LAN address needs HTTPS or its own exception.

```bash
dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
xcrun simctl install booted src/Clients/Orbit.Maui/bin/Debug/net10.0-ios/iossimulator-arm64/Orbit.Maui.app
```

## Prerequisites

The .NET workloads (`maui`, `ios`, `android`) are only half of it — each platform also needs its own
native SDK, which the workload does not bring:

| Target | Also needs | Check it with |
| --- | --- | --- |
| `net10.0-ios` | **Full Xcode** (not just Command Line Tools), then `sudo xcode-select -s /Applications/Xcode.app` | `xcode-select -p` should print a path ending in `Xcode.app/Contents/Developer` |
| `net10.0-android` | **Android SDK** and a **JDK 17+** | `dotnet build ... -f net10.0-android` reports `XA5300` when the SDK is missing |

Installing Xcode is a multi-gigabyte download from the App Store. `xcode-select --install` gives only
the Command Line Tools, which are **not** enough — the iOS build fails with "Could not find a valid
Xcode app bundle".

If the Android SDK lives somewhere non-standard, point the build at it with `AndroidSdkDirectory`.

## Building for iOS from Windows

Possible, but the Mac is not optional — Apple's toolchain runs only on macOS. Under MAUI it can be a
*remote* machine (a Mac on the LAN via Pair to Mac, a cloud Mac, or a macOS runner in GitHub Actions)
rather than the machine you work on, which is not true of a native Swift app. See §1.1 of the plan.

## Versioning

`ApplicationDisplayVersion` in `Orbit.Maui.csproj` is the SemVer the server's forced-update gate
compares against (`GET /api/config/mobile-version`); `ApplicationVersion` is the build number stores
order by. Raise the display version for anything the server may later need to refuse.
