# The phone's own look, and what it still shares with Orbit.Web

Orbit is one product with two faces, and until 2026-09-08 the phone's was a copy of the browser's. This
document used to say so: it recorded the pass of 2026-09-06 that pulled the Android head to Orbit.Web's
mobile view at `app.css`'s own numbers, down to a card heading at 14.5.

That is no longer the arrangement. The user designed a mobile-specific look in Claude Design — the
**Classical** system, an editorial one — and the phone is being redrawn in it. The two clients now share
what a colour *means* and what a screen *does*; they no longer share what either looks like.

Read this with [`orbit-maui-plan.md`](orbit-maui-plan.md), which is about what the phone does; this one
is about what it looks like.

## What is still shared, and why

**The roles.** Accent, task, success, danger, away, and the four presence colours are Orbit's, identical
to the hex in both clients (`Resources/Styles/Colors.xaml`). Red means the same thing in a browser and
on a phone; so does the amber that means "away". A reader who uses both learns one vocabulary of
meaning even while looking at two surfaces.

**The accent is the reader's.** `App.ApplyAccent` writes the four accent keys at runtime from the hue
chosen on the account screen, and everything that paints with them asks by `DynamicResource`. The
design named a dark-theme accent of its own (`#9F8DE8`); it is not used, because it would break the
picker and it is within a shade of what `AccentPalette` works out for hue 288 anyway (`#A79DF8`).

**The copy.** Every string is the app's own, and the design's prototype used them — it was written
against the real screens. Nothing here changed what a screen says, only how it is set.

**The behaviour.** Where a mobile question has no obvious answer, Orbit.Web is still the place to look:
it is the shipped, settled client. Two decided that way and still standing: a menu is closed on *any*
navigation rather than by the back gesture (`AppNavigator`), and the status bar takes the colour of
whatever Orbit draws under it.

## What the phone now decides for itself

| | Orbit.Web | The phone |
| --- | --- | --- |
| Type | IBM Plex Sans over Space Grotesk | **Lora over Cormorant Garamond** |
| Ground | warm — `#FAF6F2` / `#1B1410` | neutral — `#F3F2F2` / `#1C1A19` |
| Text and rules | separate greys per role | the ink at 62%, 45% and 16% of itself |
| Colour | fills — a filled primary button, a filled danger button | **stroke** — every button is an outline; the accent is an edge, never a block |
| Radius | 7 / 8 / 10 / 12 / 14 / 999 | **4**, everywhere |
| Sections | a sidebar, which becomes six icons in the top bar below 680px | a **drawer**, with the names back beside the icons |
| The top bar | logo, six icons, a bell, an avatar | `[≡]`, `‹` the screen's name `›`, the avatar |
| A screen's menu | three dots in the page header, or on the editing rail | one dropdown **under the screen's name** |
| Adding | a `+` beside the page heading | a **floating button** over the foot of the list |
| Editing screens | a rail along the foot | nothing; the way out is the navigation stack, popped by the phone's own gesture |
| A message | filled - the accent for yours, a grey for everybody else's | outlined - the accent over a wash of it, or a hairline over nothing |
| An avatar | a disc filled with the person's hue | a ring in it, with the initials written in it |
| A tick | the characters `○ ✓` and `☐ ☑` | one drawn circle - see `Controls/CheckCircle.xaml` |
| A list | cards | **rows**, separated by hairlines |

### The vocabulary, and where each part lives

Every number below is the design's. The phone reads them from
`src/Clients/Orbit.Maui/Resources/Styles/Styles.xaml`, which is the one place they are written.

| Part | Where it lives | Notes |
| --- | --- | --- |
| the palette | `Resources/Styles/Colors.xaml` | roles, one Light/Dark pair each; the three ink levels carry alpha |
| the faces | `MauiProgram.cs` | `OrbitBody` (Lora), `OrbitBodySemibold`, `OrbitDisplay` (Cormorant SemiBold), `OrbitDisplayLight` (its normal cut, for display sizes) |
| the quiet button | the implicit `Button` style | no fill, a hairline, secondary text, set in the display face at 14/600 |
| the important one | `PrimaryButton` | the same shape outlined in the accent. Asked for by name, so a screen says which of its buttons it is for |
| the irreversible one | `DangerButton` | outlined in the danger colour |
| the top bar | `Controls/NavigationBar.xaml` | three things wide; the name is bound from the page's own `Title` |
| the drawer | `Controls/Drawer.xaml`, `Controls/DrawerEntry.xaml` | 280 across, the eight sections, the notification count, About at its foot |
| a screen's own menu | `Controls/ITitleMenu.cs` + `ScreenMenu` + `Controls/MenuOverlay.xaml` | centred under the name |
| a row's menu | `Controls/OverflowMenu.xaml` | stays on the row; opens upwards out of the foot |
| the add button | `Controls/Fab.xaml` | 56 across, above the ad bar where there is one |
| a tick | `Controls/CheckCircle.xaml` | a ring when open, the accent filled with the ground cut through it when done |
| a card, and a row | `Controls/ItemCard.xaml` (`IsBordered`) | one anatomy, two shapes |
| a plain row | `Controls/Row.xaml` | title 14, meta 12, a hairline under it |
| the avatar | `Controls/AvatarCircle.xaml` | initials on an outline, with the presence dot on its top-right edge |
| a settings section | `SettingsCard` + `Controls/SettingRow.xaml` | |
| the hairline | `Hairline` | the most-drawn thing in the app: it carries the structure cards used to |
| a field | `Platforms/Android/FieldBox.cs` | 4px radius, a hairline, 9x12 inside, and no fill |
| a bare field | `Controls/BareField.cs` | a note's lines, which are writing rather than a form |

## Where the phone cannot do what the design draws

**A menu cannot hang off the control that opened it.** In a browser the panel is positioned against its
trigger and clamped to the window. On Android a panel drawn inside a card is clipped by the row it sits
in, and a row in a `CollectionView` cannot draw outside itself. So the trigger and the panel are split:
the page carries one `MenuOverlay`, and whatever fills it says where it hangs from
(`MenuPlacement`) — a screen's own menu centred under its name, a row's opening upwards out of the foot,
because a menu hung off a row low down the page would open into the ground.

**Android draws a line under a field, not a box around it.** MAUI has no border on `Entry`, so the box
is drawn once through the handler mappers rather than by wrapping over a hundred fields in a `Border` —
see `Platforms/Android/FieldBox.cs`.

**A handler mapper has to be keyed to a property that actually changes.** `FieldBox`, `SwitchTrack` and
`StepperButtons` are appended under properties of the control's own rather than a name of ours: MAUI
runs every key once when a control is created and again whenever that property changes, and a theme
switch *is* such a change. `FieldBox` is keyed to `TextColor` as well as `Background`, and that is the
half that works: the background is a flat `Transparent` now and never changes, so it can no longer
carry the news that the hairline needs redrawing in the other theme's colour.

**A converter feeding a shape has to hand over a brush.** XAML converts a `Color` written into markup
into a `Brush` on its way to a `Shape.Fill`; a *binding* hands the value straight over, so a converter
returning a `Color` there leaves the shape unpainted and says nothing about it. And a converter that
throws does the same. `EventColourConverter` hands over a `SolidColorBrush` and falls back to `Accent`.

**A dynamic resource outlives the value set over it.** `SetDynamicResource` registers the property
against the dictionary and keeps it registered: a later `SetAppTheme` on the same property paints the
colour asked for, and then the dictionary is read again — on load, on a theme switch — and the resource
paints itself back. `ItemCard.Edge` and `DrawerEntry.Redraw` both take the resource off before deciding
rather than only on the branch that does not want one.

**A `BoxView` paints its `Color` and its `BackgroundColor` both.** The implicit style sets `Color` to
transparent and no background at all; nothing in Orbit draws a `BoxView` without saying what colour it
is.

**A `Border` fills from `Background`, and will not take a colour resource there.** It takes one on
`Stroke` happily, so the first ticked circle came out as an accent ring with nothing inside it. A
shape's `Fill` is the brush property that behaves - `CheckCircle` is an `Ellipse` for that reason, and
one shape doing both the ring and the fill is what a circle wants anyway.

**A value written in the markup is a *local* value, and a local value beats a dynamic resource.**
`Fill="Transparent"` on that same circle meant the accent could never get in afterwards. Anything a
control decides at runtime is left out of its XAML, and `ClearValue` follows `RemoveDynamicResource`
before the decision is made.

**A press target must not set a row's height.** The implicit `Button` style asks for 44, which is the
tap target a phone needs. A button laid *over* a row has to take its height from the row instead, or
every drawer entry is 68 tall and every note in the list a third taller than it should be. Those
buttons ask for 0 and let what they cover decide.

**A phone has no hover.** Everything the design says with a hover state is simply not drawn.

## What is done, and what is not

Redrawn: the shell (bar, drawer, title menu, floating button, the two optional arrows beside a screen's
name) and every screen the design
covers — the dashboard, both list-and-detail pairs for notes and tasks, the calendar and an event, the
inventory and a shelf, the chat screens, the notification feed, sign-in and the account screen. The
`EditorRail` is gone from the codebase, not only from the screens: nothing drew one any more.

The five screens the design does not cover — the two copy screens, diagnostics, the update screen, the
shared-link page and the place picker — were redrawn by applying its rules rather than by copying a
picture. There was no picture: the prototype never drew them. What that meant in practice:

- **No page heading where the bar already carries the name.** Diagnostics was the last screen still
  writing its own name at 26pt under a bar that had just said it.
- **One quiet line of context** (12px, secondary) where a screen needs one, which is what the design
  gives the settings screen. Everything else was a `PageHeader`, and that control no longer draws a
  title.
- **Hairlines above a row, and one more under the last** — the shape `ItemCard` draws for every list.
  The copy-history rows drew theirs underneath, which leaves a rule hanging under a list that has
  ended.
- **The accent outline on the one thing a screen is for**, and the danger outline on what cannot be
  undone: "Send to Orbit" against "Clear", "Save to my account", "Download for Android".
- **A bordered group where several answers are one choice** — the same idiom as the calendar's
  Day/Month/Year and the shelf's stepper. Keep mine / Keep theirs / Keep both is one decision, and
  three separate link buttons read as three separate things to press.
- **Orbit's own tick rather than the platform's.** The shared-link screen was the last `CheckBox` in
  the app; it draws a `CheckCircle` now, with no command, so it is read as a state rather than
  offered as a control.

**The place picker was crashing, and nobody had opened it.** `MapPage` and the task entry both take
their map out of the page when the build has no Google Maps key - on Android a map built without one
throws from inside Play Services and ends the process. The place picker never did, so opening it on
such a build took the app down. It guards now like the other two, and what is left still answers the
question the screen exists for: an address can be searched for and confirmed; only pointing at the map
is gone, because there is no map to point at. Found by trying to screenshot the redrawn screen.

Eight "Back" ways out went at the same time - six buttons under the content, and two entries at the
head of a title menu - and this was a defect rather than a preference: they were
right while screens replaced each other, and the navigation stack gave every detail screen an arrow in
the bar. `ConversationPage`'s even carried a comment explaining that there was no bar to go back
through - which had stopped being true.

Deliberately not copied from the design:

- **A note in the list has no preview line.** The design shows one; `NoteListItem` carries no preview
  and nothing on the phone derives one. It is missing for want of a sentence, not a control.
- **A card's menu offers no "Edit".** The phone keeps one screen for reading and writing, so an Edit
  entry would do exactly what pressing the row already does.
- **A group task list is deleted without its second question**, because the local store deletes one
  list at a time.
- **The month grid carries a dot per day, not a chip per event** — see `orbit-maui-plan.md` §14.1.
- **The phone keeps a map** where Orbit.Web hides one below 680px: on Android it is the platform's own
  control, pinchable, on the one device that actually has a location.
- **A conversation shows no count of what is waiting.** `LocalContact` has no unread count and nothing
  on the device derives one.

## The screens the written spec does not describe yet

The Classical prototype was rejected as built on 2026-09-09, and what replaced it is a written,
screen-by-screen description in the user's own words - the shell, the dashboard, notifications, About,
the two note screens, the three task screens, the calendar and an event, contacts, the map, and the two
inventory screens. Those are done.

These are the ones it has not reached. They are listed here rather than guessed at, because guessing at
the last one is exactly what produced the rejected version.

**Six of them need not be guessed at after all.** Read as a specification of composition rather than as
a style guide, the Classical prototype does draw sign-in, create-an-account, one entry on its own, the
map's two lists, a conversation and Settings - and it corrects a dozen things about the screens that
were built from the written spec. All of it is set out in
[`android-design-deltas.md`](android-design-deltas.md), which also lists the six places the design and
the written spec disagree and says which of the two wins (the spec, every time).

| screen | what it is now |
|---|---|
| Sign in, Register, Forgotten password | named as a group, never described. They carry no bar and no drawer today - nobody is signed in - and no ad bar either |
| Account (the avatar menu's **Settings**) | theme, accent, language, notification settings, permissions, Google, the encryption key |
| A conversation, and a group's | the two chat screens: bubbles, the composer, a message's own menu |
| A group's details | who is in it, and what can be done to it |
| Contact info | who somebody is, apart from what they have said |
| The encryption key gate | what it asks and what it offers to reset |
| One entry on its own | `TaskItemSummaryPage`, opened from the calendar rather than from its list - distinct from the entry form inside a task list, which *is* described |
| Copies to review, and a thing's copy history | the two screens behind the offline-copy offer |
| Update | where a newer Orbit comes from |
| Diagnostics | the app's own log, behind the Debug permission |
| A shared link | what somebody sees following a public link into the app |
| The place picker | choosing where an entry happens, on a map |
| Startup | the screen the app opens on before it knows whether it may run |
| The map's two lists | who can see you, and who is sharing with you - **new on 2026-09-09**, invented to satisfy "the lists open as their own page", so worth confirming rather than assuming |

## What the first walk against the written spec found (2026-09-09)

The spec was built without anything being run: it compiled, the suite was green, and none of that says
whether a screen behaves. Walked on `Orbit_Pixel_8_API_36` against a local API, four things were wrong,
and **no test could have caught any of them** - two were platform ordering, one was a value copied
where a binding was meant, and one was two controls fighting over the same corner.

- **Backspace at the head of a line never joined it to the line above.** `NoteLineBackspace` decides
  whether to listen for the key while the field's handler is being built; `NoteDetailPage` attached the
  command in the field's `Loaded`, which is later. It read null every time and listened to nothing.
  The command is bound in the template now - see `NoteLineKeys`, which says so out loud.
- **Enter started the next line but left the caret behind.** The caret was to be put in the new line
  when its field raised `Loaded`, but a `BindableLayout` builds that field while `AddLineAfter` is still
  running, so `Loaded` had come and gone before there was a row to match it against. Nothing asked for
  the caret, and Android's own answer to `ReturnType="Next"` moved focus on to the tick-box button in
  the corner. The ask is made where the line is made, and honoured on the next turn of the loop.
- **The bar spoke a name it was no longer showing.** `NavigationBar` binds the title *label* to the
  page's Title and copied the *spoken* name once, so the calendar went on announcing the month it opened
  on however far the reader had moved. Bound now, like the label.

Verified on the device afterwards: Enter keeps the caret, backspace joins two lines and leaves the caret
where they met, a typed `[]` becomes a real tick box, ticking strikes the line through, the tick-box
button carries a box onto each new line, the drawer and the note's own menu read as the spec describes,
Sort and Filter hang under the screen's name, the week view starts on Monday and writes a straddling
week as "28 September - 4 October 2026", and the map fills the screen with its panels under its name.

About was walked both ways round: a build told nothing lists the licence alone, and one built with
`-p:OrbitWebBaseAddress=https://…/` lists the whole row Orbit.Web's footer carries - Privacy, Security,
Docs, "Do not share my personal information", the licence - and pressing one opens the browser on it.

The fourth thing the walk found was on the map: **the crosshair button covered Android's own zoom
buttons and took their presses.** Nothing in the markup knows those buttons are there - the map draws
them itself, at its bottom-right, which is the corner the design gives the button - so a tap well inside
the visible `+` read the phone's position instead of zooming in.

The crosshair is at the **top right** now (`Fab.IsAtTheTop`), which is the one corner of a map that is
the app's to use: Android owns the bottom right with the zoom buttons and the bottom left with Google's
logo, which may not be covered at all. This is a deliberate departure from the design, which draws the
button bottom-right - the design's map is a placeholder tile with no furniture of its own, so it never
had to share the corner. The card that says where you were last read to be gives the button room
(`Margin="12,10,80,10"`), or a long address runs underneath it.

## How to check it

There is no test that can see a screen. The check is the emulator and the design side by side:
`Orbit_Pixel_8_API_36` is 412×892 in device points, which is the frame the design was drawn in, so the
two can be compared by measuring rather than by impression.
[`testing-and-running-locally.md`](testing-and-running-locally.md) has the setup and
[`build.md`](build.md) the Android build. `Orbit.Maui` is **not** in `Orbit.CI.slnf`, so nothing in CI
compiles this markup: a local `dotnet build -c Release` is the only build check, and the only automated
guards are the four tests in `tests/Orbit.Mobile.Tests` that read the XAML off disk
(`SpokenNameTests`, `TranslationCoverageTests`, `AvatarMenuBindingTests`, `CalendarMonthLayoutTests`).

Something that moves cannot be checked by looking at one screenshot. The card pulse was read off the
pixels instead: a burst of `adb exec-out screencap -p`, and the average colour of a band just outside
the card's edge compared against the same band beside a card with no news. With
`settings put global animator_duration_scale 0` the band is flat in every frame and the danger stroke
stays. Set the scale back to `1` afterwards.
