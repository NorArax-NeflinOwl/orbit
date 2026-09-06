# The Android head against Orbit.Web's mobile UI

Orbit is one product with two faces. The browser's is the settled one - it is where the look is
decided, and `src/Clients/Orbit.Web/wwwroot/css/app.css` is where that decision is written down. This
document says what the phone takes from it, where the two are now the same thing, and the handful of
places where they cannot be and why.

Read it with [`orbit-maui-plan.md`](orbit-maui-plan.md), which is about what the phone *does*; this one
is about what it *looks like*.

## What "the web's mobile UI" is

Below `680px` app.css turns the browser into a phone, and that state - not the desktop one - is what
the Android head copies. Three rules make most of it:

- **The sidebar becomes the bar along the top.** Logo, then the section icons without their labels,
  then the notification bell and the avatar pushed to the far right. `NavigationBar.xaml` is that bar.
- **The page gets 16px either side** (`.main-content`, 24/32 on a wide window) and every screen opens
  with `.page-header`: an optional leading control, the screen's name at 26px in the display face, an
  optional line under it, and an optional menu at the other end.
- **The column beside a form becomes a bar along the foot of the window** (`.editor-rail`), because a
  form has no fixed height and buttons at its end sit wherever the writing happens to stop.

## The vocabulary, and where each part lives on the phone

Every number below is app.css's own. The phone reads them from
`src/Clients/Orbit.Maui/Resources/Styles/Styles.xaml`, which is the one place they are written.

| Orbit.Web | The phone | Notes |
| --- | --- | --- |
| the `:root` palette | `Resources/Styles/Colors.xaml` | the oklch tokens converted to hex, one Light/Dark pair per role |
| `button` (the plain one) | the implicit `Button` style | quiet: the lifted surface, a hairline, secondary text, 13.5/600 at 15x8 |
| `.btn-primary` | `PrimaryButton` | filled with the accent at 16x9. Asked for by name, so a screen says which of its buttons is the one it is for |
| `.btn-secondary` | `SecondaryButton` | the same as the default, kept for a screen that wants to say so |
| `.btn-danger` | `DangerButton` | |
| `.icon-btn` | `Controls/IconButton.xaml`, `IconButtonVariant.Plain` | 30 across, no edge |
| `.page-add` | `IconButtonVariant.Add` | the same size, outlined in the accent |
| `.icon-btn.page-action` | `IconButtonVariant.Action` / `.ActionPrimary` | 44 across with an edge - the desktop's 60, halved by the breakpoint |
| `.page-header` | `Controls/PageHeader.xaml` | `LeadingAction` / `Title` / `Subtitle` / `Actions` |
| `.item-card` | `Controls/ItemCard.xaml` | radius 12, padding 14x12, name 15 in the display face over two lines, a hairline above its footnote |
| `.item-card-list` | the cards' own `Margin="0,5"` | a 10 gap between cards |
| `.list-row` | `Controls/Row.xaml` | title 13.5, meta 12, a hairline under it |
| `.card` | `CardBorder` | radius 14, padding 18 |
| `.filter-chip` | `FilterChip` + `FilterChipLabel` | a bordered pill, filled with the accent when it is the chosen one |
| `.empty-hint` | `EmptyHint` | a quiet line where the reading starts, not centred in the middle of the screen |
| `.error` / `.info` | `ErrorLabel` / `InfoLabel` | |
| `.overflow-menu` | `Controls/OverflowMenu.xaml` + `ScreenMenu` + `Controls/MenuOverlay.xaml` | see below |
| `.avatar-dropdown-item` | `MenuItemButton` | one entry in any of Orbit's menus |
| `.editor-rail` | `Controls/EditorRail.xaml` | the bar along the foot |
| `.card-badge` | `CardBadge` + `CardBadgeLabel` | a short fact about a card, quieter than a filter chip |
| `.calendar-month-grid-day` | `CalendarDayCell` + `CalendarWeekday` | the lifted cell, today tinted, the days either side quiet |
| `.chat-bubble` | the message template on both conversation screens | 70% of the thread, one corner squared off on the side it was written from |
| `.options-card` / `.options-row` | `SettingsCard` + `Controls/SettingRow.xaml` | one setting, what it does underneath, the control at the far edge |
| `.options-card-danger` | `DangerCard` | the one section where a wrong press cannot be undone |
| `.options-tab` | `SettingsTab` | the chosen one underlined in the accent, on a row with a hairline |
| `.map-panel-section` / `.map-panel-heading` | `PanelSection` + `PanelHeading` | one group on the map screen, as its own card |
| `ObjectList.razor` | `Controls/ObjectList.xaml` | loading / empty / here-it-is |
| `input`, `textarea`, `select` | `Platforms/Android/FieldBox.cs` | the box itself: 8px radius, a hairline, 9x12 inside |

## Where the phone cannot copy the browser, and what it does instead

**A menu cannot hang off the control that opened it.** In the browser the panel is positioned against
its trigger and clamped to the window. On Android a panel drawn inside a card is clipped by the row it
sits in, and a row in a `CollectionView` cannot draw outside itself. So the trigger and the panel are
split: `OverflowMenu` fills the screen's one `ScreenMenu`, and the single `MenuOverlay` the page carries
draws it above everything, taking the edge the trigger is nearest - a header's menu hangs from the top,
a card's or the rail's opens upwards from the foot. That last one is app.css's own rule for a rail menu
at the breakpoint, for the same reason: a bar on the bottom edge would otherwise open into the ground.

**A list is made from one field, not from a form on a route of its own.** The web's four list screens
open `/notes/new` and the rest. The phone has no such route, and should not: a local row has to exist
the moment it is named, so that it is there offline and syncs afterwards. The field stays, folded away,
and the plus at the head of the screen - where the web keeps its own - is what unfolds it (see
`Controls/NewItemForm.cs`). A list screen at rest is then what the browser shows: its name and its
cards.

**Android draws a line under a field, not a box around it.** MAUI has no border on `Entry`, so this is
done once through the handler mappers rather than by wrapping over a hundred fields in a `Border` -
see `Platforms/Android/FieldBox.cs`. A field that asks to be transparent is left alone and loses
Android's line too: a note's lines are written in `Entry`s so they can be corrected where they are
read, and a note drawn as a stack of boxes is a form rather than a note.

**A `BoxView` paints its `Color` and its `BackgroundColor` both.** The MAUI template's implicit style
gave every one of them a grey, which showed wherever `Color` was left clear - a sheet of fog behind an
open menu, a grey bar under every unchosen tab on the account screen. The implicit style now sets
`Color` to transparent and no background at all; nothing in Orbit draws a `BoxView` without saying what
colour it is.

**A phone has no hover.** Everything app.css says with `:hover` - a card lighting up, a menu entry
taking the primary text colour, a row's title turning accent - has no phone equivalent and is simply
not drawn. What those rules signalled is said by shape instead, which the phone already had to do.

## What is still not the same, deliberately or not

- **A card's menu offers no "Edit".** The browser's card menu opens with it, because there a card's
  press opens the thing to be *read* and Edit is one press further in, at the form. The phone keeps one
  screen for both - a note's lines are ticked where they are written - so an Edit entry would do
  exactly what pressing the card already does, and an entry that repeats the press is noise. What the
  menu does carry is everything the press cannot reach: Delete (or "Remove from my list" for somebody
  else's note), and Share on an inventory.
- **A group task list is deleted without its second question.** The browser asks whether the other
  lists it gathers should go too; the phone's local store deletes one list at a time and cannot carry
  that answer, so the group list goes and what it gathered stays - which is the browser's own answer
  when somebody cancels that question.
- **A calendar card's menu holds Delete and nothing else**, for the same reason a note's does.
- **The month grid carries a dot per day, not a chip per event.** app.css gives a day cell a 5.5rem
  minimum and fills it with event chips; the phone's grid is a fraction of that height on purpose,
  because it gets out of the way as the list beneath it is read (see `orbit-maui-plan.md` §14.1, which
  is a decision about the phone rather than a gap). Everything else about the cell - the lifted
  surface, the hairline, today tinted with the accent, the days either side on the quiet fill - is the
  browser's.
- **The phone keeps a map where the browser hides one.** Below 680px app.css sets
  `.map-canvas { display: none }` outright: a Leaflet map at that width is a postage stamp somebody has
  to pinch at, and it pushes the lists it illustrates off the screen. On Android the map is the
  platform's own control - pinchable, and on the one device that actually has a location - so it stays,
  in the rounded, hairline frame `.location-map-frame` gives it. This is the one place the phone
  deliberately keeps something the browser's mobile view drops.
- **The phone keeps one screen where the browser has two.** A note's lines are ticked where they are
  written; a shelf is counted up and down on the screen that edits it. This is recorded at length in
  future-plan.md's "Smaller identified follow-ups" and is the right answer for a phone, so the rail
  simply carries no Save on the screens that write as they go.
- **The `.item-card-unseen` pulse is a colour here, not an animation.** The edge takes the danger
  colour; it does not breathe. Worth adding only if somebody misses it.
- **The chat screens were not walked on a device.** The emulator account has not unlocked Contacts, so
  the navigation bar draws no way into them. They build and their view models are covered; the bubbles
  and the menus want a walk on an account that can chat.
- **Every screen has now had the pass.** What is left against the browser is the four differences
  above, each of them a decision rather than a gap - and whatever the light theme turns up, which has
  not been walked at all.

## How to check it

There is no test that can see a screen, so the check is the emulator and the browser side by side, the
browser narrowed to a phone's width. `info/testing-and-running-locally.md` has both halves of that, and
[`build.md`](build.md) has the Android build itself. The traps in driving the emulator by `adb` are
worth reading before starting.
