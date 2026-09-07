# Session handover: orbit-android-ui-2

Previous session: the one that carried PR #248 (unnamed; worktree
`orbit-web-android-ui-27d179`)
Date: 2026-09-07

## Branch and PR

- Branch: `claude/android-ui-the-last-three`.
- Open PR: **#248, "[Android] Finish the look Orbit.Web set, and give the dashboard the pass"**,
  against `Coding`. The new session inherits it rather than opening its own - see `pr-workflow`.
  Its description was rewritten twice as the branch grew and documents everything it carries.
- Uncommitted changes: none. Six commits on the branch, all pushed.

## Goal of the work

Carry Orbit.Web's mobile UI onto the Android head one for one - the standing ask from the session
before this one, which supersedes the older "behaviour now, looks later" rule. `info/android-ui-parity.md`
is the map of what matches and what does not; this session closed the last items on it.

## Done

**The pulse, verified rather than assumed.** The `.item-card-unseen` halo was walked on the emulator
and read off the pixels: a burst of `screencap`, then the average colour of a band just outside the
card's edge against the same band beside a card with no news. It swells from the dark theme's
background exactly - (27,20,16) - to about six points redder and back; the control band never moves.
With `settings put global animator_duration_scale 0` the band is flat in every frame and the danger
stroke stays. The method is written down in `android-ui-parity.md` under "How to check it".

**Four defects, each found by walking rather than by a test.**

1. A card that was pinned *and* had news kept the accent edge. `SetDynamicResource` stays registered
   against a property after a plain value is set over it and paints itself back the next time the
   dictionary is read; `ItemCard.Edge` takes the resource off before it decides now.
2. The coloured dot beside a dashboard event had **never once been drawn**. Two causes stacked:
   `EventColourConverter` returned a `Color` where a `Shape.Fill` wants a `Brush` (XAML converts one
   written into markup, a binding hands the value straight over), and its fallback asked
   `Application.Current.Resources["PrimaryDark"]` - a key that has never existed, and the indexer
   throws, which leaves the target unset silently.
3. A dashboard card its filter had emptied left the page and took its own filter menu with it, so the
   choice could not be undone. This is the bug Orbit.Web's own comment records fixing.
4. Putting every dashboard part away said "add a note or a task to get started", which to somebody
   with a full account reads as work the app has lost.

**The dashboard, which had never had the pass** and was the last screen speaking the old vocabulary.
It now opens with `PageHeader`; both its "⋯" are the shared `OverflowMenu` filling the screen's one
`ScreenMenu` (the parts menu stays open, a card's filter closes after one pick); its rows are `Row`s;
the strip of today's counts is the way to the calendar and drops its chat-request line at nought; and
every card and every row a notification can name carries the mark. `UnreadNews` is the one matching
rule both the dashboard and the Tasks screen now ask - a path-segment match, as `NotificationFeedState`
does; the Tasks screen's own `StartsWith` would also have taken `/tasks/{a}bc`.

**The shelves and the faces**, the two things the first dashboard pass left written down.
`DashboardCardKind.Inventories` sits between what is coming up and who is around; a private shelf is
hidden while private things are locked, and the card carries the news because an expiry names no shelf.
Groups, Recent chats, Shared with you and Contacts lead with the avatar, at `.avatar-sm`'s own 26. The
circle came out of `PersonRow` into `Controls/AvatarCircle.xaml` on the way, so three lists draw one
avatar rather than three.

**Verified:** `dotnet test Orbit.sln` green at every step, last run 3210 passing (1202 mobile, 863 web,
1145 API); `dotnet build -c Release` clean; the dashboard, the avatars and the Inventory card walked on
the emulator in both themes, with the theme put back to Dark and the emulator shut down cleanly.

## Still failing / unknown

- **The chat screens have still not been walked on a device.** This is the one item left in
  `future-plan.md` under "What the Android head's look still owes Orbit.Web". The emulator account has
  no Contacts permission from the server, so the navigation bar draws no way into them. Staging rows in
  the device's own database gets the *lists* on screen (see below) but not a real conversation: the
  fabricated contacts carry no public key, so opening one would fail on the encryption rather than show
  the bubbles. A real walk wants an account the server has actually unlocked Contacts and Chat for.
- **A conversation shows no count of what is waiting.** The one part of Orbit.Web's avatar the phone
  does not draw, and it is missing for want of a number rather than a control: `UnreadBadge` reads a
  per-conversation unread count, `LocalContact` has none, and `LocalChatMessage.IsReadByEveryone` is
  about messages this reader *sent*. Recorded in `future-plan.md` as a chat feature, not a look.

## Rejected approaches (do not retry)

- **`adb install -r` of the `-Signed.apk` from an ordinary `dotnet build`.** It installs and then dies
  on launch with `ClassNotFoundException: crc64a05c27c563ec9e41.MainApplication`, because the ordinary
  build expects fast deployment to push the assemblies separately. Build with
  `-p:EmbedAssembliesIntoApk=true -p:AndroidUseFastDeployment=false` for anything installed by hand.
- **Installing over an app that still has `files/.__override__`.** That directory holds assemblies a
  previous fast-deployment build pushed, and they shadow the ones embedded in the new APK - so a fresh
  install can quietly run old code. `adb shell run-as com.orbitmaui.android rm -rf files/.__override__`
  before every relaunch.
- **Judging an animation, a halo or a hairline by eye from a screenshot.** Read the pixels instead;
  there is a small PNG decoder recipe in `android-ui-parity.md`'s "How to check it" and the numbers it
  produced are quoted there, so a later session can tell a change from a coincidence.
- **`cmd uimode night no` to see the light theme.** The app keeps its own theme in `IPreferences`
  (`orbit.appearance.theme`), the preferences file is encrypted, and the stored value here is Dark - so
  the system setting is ignored. Change it through Settings → Appearance in the app, and put it back to
  Dark afterwards.

## Next step

Open `info/future-plan.md`, section "What the Android head's look still owes Orbit.Web": one item is
left unstruck, the chat screens never walked on a device. Either get an account the server has unlocked
Contacts and Chat for and walk the two conversation screens, their bubbles and their three menus - or,
if no such account is to hand, say so to the user and move to "Smaller identified follow-ups", whose
first entry is the inventory work the phone still owes the web from 2026-09-04.

## Environment facts confirmed this session

- Emulator AVD `Orbit_Pixel_8_API_36_Clean`, launched `-no-snapshot-save -no-boot-anim`, so nothing
  written to the device survives a restart - which is what makes staging rows in its database safe.
- The launcher activity is `com.orbitmaui.android/crc64a05c27c563ec9e41.MainActivity`. The other hash
  seen in logs, `crc64e1fb321c08285b90`, is not it; `adb shell cmd package resolve-activity --brief
  com.orbitmaui.android` is the reliable way to ask.
- `/system/bin/sqlite3` exists on that image, and `adb shell run-as com.orbitmaui.android sqlite3
  files/orbit.db3` reads and writes the local store. Tables include `Permissions` (one `Name` column -
  inserting `Contacts` and `Chat` unlocks those sections locally), `Contacts`, `Inventories`,
  `Notifications`.
- The dashboard's navigation-bar tap target is (344, 217) for Tasks and (92, 217) for the dashboard
  itself, in the emulator's own 1080x2400 pixels.
- Debug build for hand installation:
  `dotnet build src/Clients/Orbit.Maui/Orbit.Maui.csproj -f net10.0-android -p:OrbitDevelopmentApiPort=8081
  -p:EmbedAssembliesIntoApk=true -p:AndroidUseFastDeployment=false`, which lands a ~103 MB APK at
  `src/Clients/Orbit.Maui/bin/Debug/net10.0-android/com.orbitmaui.android-Signed.apk`.
- `DangerDark` is `#ED756E` and `DangerLight` `#C74B47`; the dark theme's page background reads
  (27,20,16) and a card's surface (37,29,25). These are the numbers the pixel checks above compare to.
