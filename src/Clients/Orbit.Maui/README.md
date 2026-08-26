# Orbit.Maui

The mobile client: one .NET MAUI project producing both the **iOS** and **Android** apps. iPhone 15 Pro
is the reference device. See [`info/orbit-maui-plan.md`](../../../info/orbit-maui-plan.md) for the plan
this is being built against.

Nothing here works yet beyond the template — this is the phase 1 skeleton.

## It is deliberately not in `Orbit.sln`

CI builds `Orbit.sln` on `ubuntu-latest` (see `.github/workflows/ci.yml`). An `ubuntu` runner cannot
build `net10.0-ios` at all — that needs macOS and Xcode — so adding this project to the solution would
fail every pull request. Build it explicitly instead:

```bash
dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-android
dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-ios
```

The server and web client keep building and testing through `Orbit.sln` exactly as before.

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
