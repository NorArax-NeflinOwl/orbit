# Orbit.Maui

The mobile client: one .NET MAUI project producing both the **iOS** and **Android** apps. iPhone 15 Pro
is the reference device. See [`info/orbit-maui-plan.md`](../../../info/orbit-maui-plan.md) for the plan
this is being built against.

Phases 1 to 4 are built, and group conversations with them: the version gate, sign in/out with the
session in the Keychain, end-to-end-encrypted chat that interoperates with Orbit.Web byte for byte, and
notes, task lists, calendar events and warehouses all on one offline sync spine — every screen reads a
local SQLite database, and changes queue in one outbox and replay in order when the network returns.
A group message is sealed once per member at the moment it goes out, never when it is typed, which is
what lets one be written offline and still reach a group somebody has since joined. Still ahead from
phase 5: roles, editing and deleting, read receipts and forwarding — then background sync and the
iPhone-specific work in the plan's §9.

The local database is managed with **EF Core migrations** (`Orbit.Mobile/Data/Migrations`). Add one with
`dotnet ef migrations add <Name> --project src/Clients/Orbit.Mobile --output-dir Data/Migrations`. The
earlier `EnsureCreated` shortcut lasted exactly until the second table: it does nothing at all to a
database that already exists, so new tables were simply missing at runtime.

The local database is **not encrypted**. It sits in app-private storage and relies on the platform's
disk encryption, which is a deliberate deferral rather than a decision — see §5.1 and open question 2
of the plan, and `Platform/LocalDatabase.cs`, which is the one place that would change.

## Where the code lives, and why it is split

| Project | Target | In `Orbit.sln`? |
| --- | --- | --- |
| `Orbit.Mobile` | `net10.0` | **Yes** — everything decided without a device: the version gate, auth, the local store, the sync spine, and the screens' view models |
| `Orbit.Mobile.Tests` | `net10.0` | **Yes** — ordinary xUnit against the above |
| `Orbit.Maui` | `net10.0-ios`, `net10.0-android` | **No** — the two app heads: XAML, page code-behind, platform services |

The split exists so the interesting logic can be unit-tested. A MAUI head cannot be referenced by a
normal test project, so anything left inside it is only ever verified by running the app.

**The view models live in `Orbit.Mobile`, not in the head.** They were in the head at first, and it cost
a real bug: the task-list screen kept the copy it had read before syncing, so ticking an entry it had
just added sent no id and the server made a second one. Nothing below the screen was wrong, and nothing
below the screen could have caught it. None of them touch a MAUI type - the two things that genuinely
are platform calls are behind interfaces the head implements: `IScreenNavigator` (swapping the window's
page) and `IUpdateLink` (leaving the app for a store listing).

`Orbit.Maui` stays out of the solution because CI builds `Orbit.sln` on `ubuntu-latest` (see
`.github/workflows/main_orbit.yml`), which cannot build `net10.0-ios` at all — that needs macOS and
Xcode — and cannot build `net10.0-android` without the Android SDK and a JDK. **The Android head is
built anyway**, by a separate job in that same workflow which installs both first; iOS is the head
nothing checks but a Mac. Build either explicitly:

```bash
dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-android
dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-ios
```

The server, web client, and `Orbit.Mobile` keep building and testing through `Orbit.sln` as before.

The same boundary is why dependency submission has its own workflow
(`.github/workflows/dependency-submission.yml`) rather than GitHub's **Automatic dependency submission**
setting: that setting cannot be told what to build, so it restores every project file it can find and
fails on this one every time. Keep it switched off.

## Debugging from VS Code

`.vscode/launch.json` carries **Orbit.Maui (iOS simulator)** and **Orbit.Maui (Android emulator)**, and a
compound for each that starts `Orbit.Api` alongside it — without the server the app gets no further than
the sign-in screen. Breakpoints need the **.NET MAUI extension** (`ms-dotnettools.dotnet-maui`,
recommended in `.vscode/extensions.json`); it owns the `maui` debug type and the device picker in the
status bar. Pick a device there before pressing F5 — the picker lists what is *running*, so an emulator
has to be booted first.

Without that extension the app can still be built and run, just with no debugger attached — the tasks in
`.vscode/tasks.json` do it:

| Task | Does |
| --- | --- |
| `maui-ios: build` | The simulator build, runtime identifier and all |
| `maui-ios: run on simulator` | Opens the simulator, builds, installs **over the top**, launches — keeps the local database |
| `maui-ios: reinstall clean` | The same but wipes the app's container first, for testing a fresh install |
| `ios-simulator: list iPhones` | What the installed Xcode actually ships, which is what the next task accepts |
| `ios-simulator: boot a chosen iPhone` | Switches which model everything targets, shutting the current one down first |
| `maui-android: build` | The Android build. No runtime identifier: one build carries every architecture |
| `maui-android: run on emulator` | Builds, installs **over the top**, launches — keeps the local database |
| `maui-android: reinstall clean` | Uninstalls first, for testing a fresh install |
| `android-emulator: list AVDs` | The virtual devices this machine has |
| `android-emulator: boot a chosen AVD` | Starts one detached and waits for Android to finish booting |

The iOS tasks all target `booted`, which is the only handle `simctl` offers once a device is running — so
choosing the model is a step of its own, and only one may be booted at a time or `booted` is ambiguous.
The Android tasks reach the SDK through `ANDROID_HOME`, falling back to wherever that platform's
command-line tools install one by default: `~/Library/Android/sdk` on macOS,
`%LOCALAPPDATA%\Android\Sdk` on Windows. Each of them carries a `windows` override spelling the same
thing in PowerShell, because the shell VS Code runs a task in is the machine's own.

**Wait for the emulator, not just for `adb`.** `adb wait-for-device` returns as soon as the device is
listed, which is well before Android has finished booting; an install into that window fails. The boot
task polls `sys.boot_completed` afterwards for that reason.

An uninstall clears the local SQLite database on both platforms. On iOS it leaves the **chat key**,
which lives in the Keychain and survives one; on Android the key is app data and goes with everything
else, so a clean reinstall there means restoring the key backup on the next sign-in.

## Running it against a local server

The API address is `OrbitApiSettings.Development`, and it differs per platform because "the machine
running the server" is not the same address from each:

| Running on | Reaches the Mac's `localhost:5080` as |
| --- | --- |
| iOS simulator | `http://localhost:5080` — it shares the Mac's loopback |
| Android emulator | `http://10.0.2.2:5080` — the emulator's fixed alias for its host |
| A physical device | Neither. Use the Mac's LAN address, and note iOS refuses plaintext HTTP to it |

**A change to `Platforms/iOS/Info.plist` does not always survive an incremental build.** A permission
string added there was missing from the built `.app` until `bin`/`obj` for `net10.0-ios` were deleted -
and iOS answers a permission request with an instant refusal when the string is absent, with no prompt
and nothing in the log, which looks exactly like the reader saying no. Delete the iOS output after
editing that file.

**Android has the same trap, and the Google Maps key walks straight into it.** Creating
`Platforms/Android/AndroidManifestOverlay.xml` does not make the build regenerate the manifest, so the
key is simply absent from the APK and the map screen goes on saying it has no map — which reads as a
key that was rejected rather than one that never arrived. Delete
`obj/Debug/net10.0-android/android/AndroidManifest.xml` after adding or changing the overlay. With that
done the merge works: the key reaches the packaged manifest and `MapAvailability` finds it.

iOS blocks cleartext HTTP by default. `Platforms/iOS/Info.plist` carries `NSAllowsLocalNetworking`,
which permits it for local and loopback hosts only — a LAN address needs HTTPS or its own exception.

```bash
dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
xcrun simctl install booted src/Clients/Orbit.Maui/bin/Debug/net10.0-ios/iossimulator-arm64/Orbit.Maui.app
```

Android has one command for the lot — `-t:Run` is the Android SDK's own build, install and launch target,
so nothing has to name the activity, whose class name carries a generated hash:

```bash
dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-android -t:Run
```

Android refuses cleartext HTTP the way iOS does, and the exception is
`Platforms/Android/Resources/xml/network_security_config.xml`: `10.0.2.2` and loopback only. A LAN
address needs HTTPS or its own entry there.

## Testing a tapped notification without sending one

Push is not wired up on either head yet (`PhonePushNotifications` under each platform folder says why), but the
tap handling behind it is, and it can be exercised without Firebase. The destination arrives as an
ordinary intent extra on Android: the Firebase SDK turns each entry of the message's `data` block into
one, and `Orbit.Api`'s `FirebasePushNotificationSender` puts the tap target there under `url`. So an
`am start` carrying that extra is indistinguishable from a real tap as far as the app is concerned.

The activity's class name carries a generated hash, so ask the device for it rather than writing it
down anywhere:

```bash
adb shell am start -n "$(adb shell cmd package resolve-activity --brief com.orbitmaui.android | tail -1 | tr -d '\r')" -e url /calendar
```

Run it against a stopped app for the cold-start path and against a running one for the warm path — they
are handled in different places (`OnCreate` and `OnNewIntent`) and only the second needs the activity to
be `SingleTop`. The paths the app understands are the ones `NotificationDestination.Parse` lists:
`/calendar`, `/inventory`, `/map`, `/tasks/{id}`, `/chat/{userId}`, `/chat/groups/{groupId}`.

**A tap is only followed once the reader is signed in.** `StartupViewModel.ContinueToAppAsync` checks
for a session first and sends them to sign-in otherwise, deliberately - following it would put a
conversation behind the sign-in screen. On a freshly installed app the destination is recorded and then
dropped, which looks like nothing happening.

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

### The Android SDK on macOS

The Android SDK is not one download but a set of packages, and the workload installs none of them. With
the command-line tools on `PATH` (`brew install --cask android-commandlinetools`), this is the set the
app needs, installed where the build looks for it without being told:

```bash
sdkmanager --sdk_root="$HOME/Library/Android/sdk" --licenses
```

```bash
sdkmanager --sdk_root="$HOME/Library/Android/sdk" "platform-tools" "build-tools;36.0.0" "platforms;android-36" "emulator" "system-images;android-36;google_apis;arm64-v8a" "cmdline-tools;latest"
```

Then a device to run it on. **Pixel 8 is the reference**, the Android counterpart of the plan's iPhone 15
Pro. Use the `avdmanager` inside the SDK rather than one from elsewhere: it finds system images relative
to its own location, and one installed alongside a different SDK reports that there are none.

```bash
"$HOME/Library/Android/sdk/cmdline-tools/latest/bin/avdmanager" create avd -n Orbit_Pixel_8_API_36 -k "system-images;android-36;google_apis;arm64-v8a" -d pixel_8
```

**Worth a second device at the floor.** `SupportedOSPlatformVersion` is 29, and the newest emulator
cannot show what happens there: API 29 still has the three-button navigation bar and no predictive back
gesture, its status bar is painted with `colorPrimaryDark` where API 35 and up are edge-to-edge, and
`BiometricPrompt` only accepts a biometric and the screen lock together from 29 on. Swap the two numbers
in the commands above (`system-images;android-29;google_apis;arm64-v8a`) for one to check against.

```bash
"$HOME/Library/Android/sdk/cmdline-tools/latest/bin/avdmanager" create avd -n Orbit_Pixel_8_API_29 -k "system-images;android-29;google_apis;arm64-v8a" -d pixel_8
```

**Dark mode on that image needs the setting written directly.** `adb shell cmd uimode night yes` is
refused there — the image reports `mNightModeLocked=true` — which looks like dark mode being
unavailable rather than one route to it being closed. The setting takes, and survives a restart:

```bash
adb shell settings put secure ui_night_mode 2 && adb reboot
```

### The Android SDK on Windows

**Visual Studio's .NET MAUI workload brings both halves, and the build finds them unaided.** With
`ANDROID_HOME` and `JAVA_HOME` unset it still resolves `C:\Program Files (x86)\Android\android-sdk` and
`C:\Program Files\Android\openjdk\jdk-21.0.8` — so a newer JDK on `PATH` is not the one the build uses,
and does not have to be removed to keep the Android SDK happy.

That SDK builds the app but cannot run it: it ships no `emulator` package and no system image, and it
lives under Program Files, where `sdkmanager` cannot add them without elevation. Install a second SDK
where the command-line tools look by default, and point `ANDROID_HOME` at it so the tasks and the build
agree on one.

The licence prompt in front of that can be answered once, or skipped: the Visual Studio SDK's licences
are already accepted, and copying its `licenses` folder into the new SDK root carries that acceptance
across — which is what lets the install below run unattended.

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\Android\Sdk" | Out-Null; Copy-Item -Recurse "${env:ProgramFiles(x86)}\Android\android-sdk\licenses" "$env:LOCALAPPDATA\Android\Sdk\"
```

```powershell
& "${env:ProgramFiles(x86)}\Android\android-sdk\cmdline-tools\latest\bin\sdkmanager.bat" --sdk_root="$env:LOCALAPPDATA\Android\Sdk" "platform-tools" "build-tools;36.0.0" "platforms;android-36" "emulator" "system-images;android-36;google_apis;x86_64" "cmdline-tools;latest"
```

```powershell
setx ANDROID_HOME "$env:LOCALAPPDATA\Android\Sdk"
```

**`x86_64`, where the Mac takes `arm64-v8a`.** The emulator does not emulate a foreign architecture at
any usable speed, so the image has to match the host — the one place where the two machines' setup
genuinely differs rather than just being spelled differently.

Then the same two reference devices as above, for the same reasons — only the tool's path and the image
architecture change:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\cmdline-tools\latest\bin\avdmanager.bat" create avd -n Orbit_Pixel_8_API_36 -k "system-images;android-36;google_apis;x86_64" -d pixel_8
```

**The emulator needs a hypervisor, and says so late.** Without Windows Hypervisor Platform it starts and
then crawls, which reads as a slow machine rather than a missing feature. Check before blaming the
build:

```powershell
(Get-CimInstance Win32_ComputerSystem).HypervisorPresent
```

## Building for iOS from Windows

Possible, but the Mac is not optional — Apple's toolchain runs only on macOS. Under MAUI it can be a
*remote* machine (a Mac on the LAN via Pair to Mac, a cloud Mac, or a macOS runner in GitHub Actions)
rather than the machine you work on, which is not true of a native Swift app. See §1.1 of the plan.

## Versioning

`ApplicationDisplayVersion` in `Orbit.Maui.csproj` is the SemVer the server's forced-update gate
compares against (`GET /api/config/mobile-version`); `ApplicationVersion` is the build number stores
order by. Raise the display version for anything the server may later need to refuse.
