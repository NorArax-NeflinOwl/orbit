# What the Classical design still asks for, screen by screen

The design bundle is `~/Downloads/Orbit.Maui mobile app design-handoff.zip` (8 September 2026), one
file: `orbit-maui-mobile-app-design/project/Orbit Mobile.dc.html`, 1004 lines, each screen in its own
`<sc-if value="{{ is.<screen> }}">` block with the sample data and the interaction logic in a
`<script type="text/x-dc">` at the foot. `_ds/classical-*/styles.css` is the token sheet;
`android-frame.jsx` frames it at 412×892, which is the Pixel 8 in device points - so the emulator and
the prototype can be measured against each other rather than eyeballed.

**Read it as a specification of composition, not as a style guide.** That mistake has already been made
once: the first pass applied its tokens to the screens that were there and the whole redesign was
rejected. What the file says is *what is on each screen, in what order, and what a press does* - the
colours are the least of it.

## The one rule for reading this document

**Where the design and the written spec disagree, the written spec wins.** The spec is the user's own
screen-by-screen description, given on 9 September, after the design was rejected as built. The
disagreements are listed first precisely so that nobody working from the design "corrects" the spec back.

| the design draws | the app does, and keeps doing | why |
|---|---|---|
| Map above Contacts in the drawer | Contacts above Map | the spec names the order |
| a drawer foot of "Settings" + "Orbit 1.4.2 · Offline changes queue and sync later" | an **About** entry and the version on the last line | the spec makes About a screen; the design has no About at all |
| a bottom-right "locate me" button on the map | the crosshair top-right | the design's map is a placeholder tile with no furniture; the real one has Android's zoom buttons bottom-right and Google's logo bottom-left. See `android-ui-parity.md` |
| Day, Month, Year on the calendar | Day, **Week**, Month, Year | the spec asked for the week view |
| a back link on any screen | no back control anywhere, ever | see below - this one is not a preference |
| a "chat request" counter always on the dashboard | the counter only when somebody is waiting | Orbit.Web's own rule; a standing "0" is not news |

### Every back control the design draws is a mistake

Said by the user on 2026-09-09, and it is a rule rather than a screen-by-screen judgement: **the Android
head goes back through the navigation stack and Android's own back gesture, and nothing else.** The
design draws three that must not be built - a `‹` in the conversation's header, and a "‹ Map" link at
the top of each of the map's two lists. They are an artefact of the prototype being a web page with no
system back of its own.

Six leftover "Back" buttons were already taken out of the app when the navigation stack arrived (see
`future-plan.md`, "Redrawing the rest of the phone"), so nothing has to be undone - this is here to stop
them coming back in with the screens above.

One "Back" is left in the app and is **not** one of these: `ChatKeyGatePage` has a link that backs out
of the password-reset form to the password form on the same screen. It navigates nowhere and the stack
never sees it. Its label is the misleading part, not its existence.

## The screens the design draws that the written spec never reached

This is the larger half of what the design is worth. `android-ui-parity.md` lists the screens the spec
does not describe; the design describes six of them, so they no longer have to be guessed at.

***All three of the screens below were redrawn on 2026-09-09.*** They existed already - this was their
composition, not their plumbing. Sign in and create-an-account are device-verified; the entry screen is
not, for the reason under its own heading.

**Sign in.** No bar, no ad bar. Centred column: the Orbit mark at 72, "Orbit" at 44 in Cormorant
regular, a gap, "Email or login" and "Password" as labelled fields (12px label above a 44-tall box),
the accent-outlined "Sign in", an "or" rule with the word in the middle, "Continue with Google" outlined
in the line colour rather than the accent, and then **two links side by side** - "Forgotten your
password?" and "Create an account".

**Create an account.** Heading at 32, one line of context ("One account for the browser and this
phone."), then Display name, Email, Password, Repeat password, the accent-outlined "Create account", and
a centred "Already have an account? Sign in".

**One entry on its own** (`TaskItemSummaryPage`). A 26px circle and the entry's text at 28 in Cormorant
on one row; a bordered "Add a note to this item" text area; then hairline label/value rows in small
caps on the left and the value on the right; and a foot line of *list name — hairline — position in the
list*. Its title menu: Mark as done/open, Move to another list…, Duplicate, Delete item.

*Redrawn as far as the screen goes, which is less than the design draws.* The design's entry is
**editable** - a live tick box, a typed name, a note field - and this screen took the composition first
and the editing since: the tick circle and the name at 28 as one row at the head, the facts as hairline
label/value rows, and the list's name moved from over the head of the screen into the foot. **The tick
is pressable as of 2026-09-09**, on this screen and on Orbit.Web's own entry page - the division that
kept it read-only ("the list it is on is where it can be ticked off") is gone, because the one screen
about an entry was the one place the entry could not be finished. It writes to this phone and queues
from there, and says under the entry when a list shared to read refuses it or the save is still waiting
to go out. **The position in the list is on the foot line as of 2026-09-11** ("2 of 5", on the right of
the list's name): the screen reads the whole list to find the entry in, so the count was there all
along. **"Duplicate" and "Delete item" are in the menu as of 2026-09-11**, on this screen and on
Orbit.Web's entry page together - the user's decision, since adding them to one client alone would have
made the two answer different questions. A copy goes straight under the entry, made the way
`DuplicateTaskListCommandHandler` copies entries (everything but the appointment, which one entry
raises), and opens; a removal asks first, leaves the appointment in the calendar as the list's own
row menu does, and lands on the list. Still not built: the note field, and "Move to another list…",
which needs a list picker the design does not draw.

***Walked on the device on 2026-09-09***, and it reads as drawn. Getting to it used to be the awkward
part: it opened from the calendar only for a deadline that `IsSomewhere`
(`LinkedCalendarEventId is not null || Location.Length > 0`). **Since 2026-09-10 every deadline on
the calendar opens it** - the fork is gone, see `CalendarViewModel.OpenDeadline` - so the rest of this
paragraph is history rather than instructions. A deadline whose event falls on the same day is still
dropped from the list and drawn as that event instead. What used to get you there was **a
Calendar-kind entry with an address and no event yet**, because
`TaskItemSubject` keeps a location only for `kind == Calendar && LinkedCalendarEventId is null` - every
other kind has it blanked on the way in, which is why typing an address on a Checklist entry looks like
it saves and does not. That state is real (a calendar entry made offline, before its event exists) but
the phone's own form cannot produce it, because choosing Calendar and saving creates the event. It was
seeded in the local database.

The walk found one defect, now fixed: **the map did not centre on the place.** The pin arriving is also
what makes the map visible - `MapArea` is bound to `HasPin` - so `MoveToRegion` was called on a view
that had not been laid out yet, and Google's own map dropped it without a word. The screen showed the
whole Atlantic, which reads as a map that failed to find the address rather than one told where to go
too early. It moves on the next turn of the loop now.

**The map's two lists.** These were invented on 9 September to satisfy "the lists open as their own
page", and the design has them after all - so they are worth checking against it rather than keeping.
Both are hairline rows with a 34px ring avatar. "Locations I share": name, the sharing mode underneath,
and a **"Stop" link** on the right. "Shared with me": name, where they are underneath, and a chevron.
Both carry a "‹ Map" link at the top, which must **not** be built - see the rule above.

*Half verified on 2026-09-09.* Both screens open, each carries its own name in the bar, neither has a
back link, and each has an empty state of its own ("Nobody." / "Nobody is sharing their position with
you."). **The rows themselves are still unseen**, and cannot be seen cheaply: a row needs a live
location share, whose position is stored encrypted (`OP_LOCATIONS.OP_L_CIPHERTEXTBASE64`) and so cannot
be seeded, and the app offers somebody as a candidate to share with only once there is a **conversation**
with them - which needs that person to have set up chat on a device of their own.

**A conversation** and **Settings** are also drawn, and both are built already; what the design changes
about them is under "Screens already built" below.

## Screens already built, and what the design corrects

### The shell

- **A title menu is groups, not a list.** ***Done 2026-09-09.*** The design's menu is a stack of groups,
  each with its own heading in uppercase accent with a hairline above it, and each entry is *tick
  column, label, optional count*. `ScreenMenu` had **one** optional heading for the whole panel and a
  flat `Entries` list with no count, so a menu that is two things at once - "Show" *and* "Sort" on the
  dashboard, "Sharing" *and* "Locations" on the map - could not be drawn, and four screens worked around
  it by opening a second panel.

  `ScreenMenu.Groups` and `ShowGroups` now carry it (named rather than overloaded: an empty collection
  expression fits both signatures, so `Show([])` would not compile), an empty group is left out rather
  than drawn as a heading over nothing, and `Entries` stays as the flattened list of everything in the
  menu. **Every menu the design names as grouped is grouped**: notes, tasks, the map, a note, the
  dashboard and the calendar. Four of them lost a second press the reader used to have to make.

  Two of the six do not carry the design's own entries, and deliberately. The map has no "Off" in its
  Sharing group - sharing is not a state that screen is in but a set of people it is shared with. And
  the calendar's two groups are *Sort* and *Show* rather than the design's *Show* (layers) and
  *Calendar* (go to today, share this day…): the layers do not exist, "go to today" is already a button
  on the page, and sharing a day is not built. What was taken from the design there is the shape - it
  had always asked two different questions under one heading.
- ~~**The counts on folder entries.** Notes and Tasks list their folders in the menu with the number of
  things in each. Nothing in `ScreenMenuEntry` can carry that.~~ ***Done - the column on 2026-09-09
  (`ScreenMenuEntry.Count`), the folders themselves on 2026-09-10, walked on the device.*** A *Folders*
  group on both screens, built-in ones first and then the reader's own, each with how many of the
  screen's rows are in it and a tick on the one being read; a folder holding nothing shows no number at
  all, because a standing "0" is not news. Beside it a *Folder* group - **New folder**, which unfolds
  the same kind of naming row the plus unfolds for a new note, and **Delete folder** while one somebody
  made is open. Filing is under the note's or the list's own name once it is open, the lists having
  given up their per-row menus. The **dashboard** has the group too, drawing both pages' folders at once
  and narrowing to the card the open one is about - it offers no way to make a folder, there being no
  dashboard card to file into one. See `info/functionality.md` for the rest, including what the phone
  does that the browser cannot: all of it offline.
- ~~**The task list's own filters write their count into their label**~~ ***Done 2026-09-09.***
  "All 4", "Pending 0" were one string from `TaskListFilter.Label`, so the number was the same size and
  weight as the words and a filter with nothing behind it said "0". `Label` is gone; the count is the
  entry's own column, and `ScreenMenuEntry.CountOf` carries the rule that a zero shows nothing - the map
  and the categories use it too, so it lives in one place. (No test read `Label`; an earlier note here
  said otherwise and was wrong.)
- ~~The tick is still the character `✓` (`ScreenMenuEntry.Mark`), which neither Lora nor Cormorant
  has~~ - **and that stopped mattering when the faces went back to IBM Plex Sans over Space Grotesk,
  which carries it.** Device-verified in the task list's own menu on 2026-09-10. It is worth
  remembering that the string is still a glyph and not a drawing: a third change of face would bring
  the problem back. See `future-plan.md`, which says the same about `☐ ☑` - neither face has those,
  which is why `CheckCircle` draws its circle.

### Dashboard

- ~~**The counter strip is one baseline, not three columns.**~~ ***Done 2026-09-10, walked on the
  device.*** A coloured dot, the number, and what it counts, side by side under the hairline - which is
  Orbit.Web's own strip as well as the design's. The colour moved from the number to the dot, so the
  three numbers read as three of the same thing and the dot says which; stacked, each label sat under
  its number in small caps and read as a heading over the next counter along. The labels are shorter
  than the browser's ("tasks due", "events", "chat requests"): three of "tasks due today" side by side
  do not fit in 412 points, and the heading over them already says *Today*.
- **A card's rows can carry more than they do.** The design's row is: optional colour dot, optional
  26px initials ring with a presence dot on it, the title, an optional priority chip, an optional
  48×3 progress bar, and the detail. Contacts rows on the dashboard therefore show who is online, and
  task-list rows show how far along they are.

  **Standing decision: the app draws three of the six** - the title, the priority chip and the detail.
  A dashboard card holds six rows to be glanced down and the section itself is one press away for the
  rest, so a dot, a face and a bar on every row of every card is five cards' worth of furniture on the
  first screen. The row's own comment in `DashboardPage.xaml` used to justify this by saying the design
  gives a row three things and no more, which is simply wrong about the design; it says what it is
  now - a departure, and why.

### Notes

- ~~**The tag chip belongs on the title's line, at the right.**~~ ***Done 2026-09-10.*** The "copy" tag
  moved out of `ItemCard.Tags`, which is the line below, into `Trailing` beside the priority chip, and
  it is drawn as a chip rather than as a bare word - a word beside a chip reads as the end of the title
  instead of as a fact about it. **Not walked**: a note is a copy only after somebody else's note has
  been taken for offline editing, and there is none on the emulator. The priority chip beside it is
  device-verified in the same slot, and `CardBadge` is the calendar's own chip.
- The preview line is **justified** (`text-align:justify`), which is the design's habit for running
  prose and is why the notes list reads as a column of paragraphs rather than a list. **MAUI cannot do
  this**: `TextAlignment` is Start, Center or End, with no Justify, so it would take a platform handler
  on every Label - and the app's preview is one truncated line rather than a paragraph anyway, which
  was a deliberate departure the day the list was drawn.
- The notes list has **no search box**. Tasks, Inventory and Contacts each have one; Notes does not.
  The app matches this today - worth not "fixing".

### A note

- ~~**Enter splits the line at the caret.**~~ ***Done 2026-09-09.*** `AddLineAfter` takes the caret now
  and carries whatever follows it down onto the new line. A checklist goes on being a checklist without
  the button in the corner being touched, and an empty line ends it - which is the design's own rule and
  is how a reader stops one. ***Since 2026-09-12*** the press is worked out by `NoteSurfaceEdits.Enter`,
  the browser's own rule, so an empty box ends the list *in place* rather than leaving the box behind.
- ~~**Backspace at the head of a checklist line takes the box off first.**~~ ***Done 2026-09-09.***
  `MergeIntoTheLineAbove` takes the box off instead of joining, and only a second press joins the line
  upwards. It is the one way to undo a box from the keyboard. ***Since 2026-09-12*** that is the browser's
  rule, `NoteSurfaceEdits.Backspace`, and it holds for a box with words on it; an **empty** box has
  nothing to keep and goes whole in one press. The press answers whether the line is gone rather than
  where the caret lands, which `CaretPlaced` says.
- ~~**Arrow up and arrow down move between lines**, keeping the column.~~ ***Done 2026-09-10, walked on
  the device.*** The
  same Android key hook backspace already owned, which is named `NoteLineKeyPresses` now that it reads
  three keys rather than one. The caret keeps its column and lands at the end of a line too short to
  keep it; arrow up in the first line goes to the note's name, because the name is the note's first
  line and Enter at the end of it already goes the other way; and arrow down at the last line does
  nothing, because starting a line is Enter's job. Walked with `adb shell input keyevent
  KEYCODE_DPAD_UP`/`_DOWN` and a character typed after each press to see which line took it: the caret
  crosses a ticked line (which opens as it arrives and closes again as it leaves) and lands in the
  note's name from the first line, at the column it left.
- ~~**The editor has a foot.**~~ ***Done 2026-09-11.*** The hairline and "Type [] for a checkbox" came
  on 2026-09-09; the design's left half - who the note is shared with and when it was last edited - is
  `NoteDetailViewModel.Footnote` now, in the words the note's card on the list already uses: "Shared by
  ala · Yesterday" for a note shared in, the day alone for the reader's own (`LastChanged`). The state was
  on the row all along. One part is not drawn: **who the reader shared a note *with***, because this
  phone keeps only that a note is shared out (`IsSharedWithOthers`), not with whom. The foot stays on a
  note nobody can edit, without the checkbox hint.

### Tasks, and a task list

- ~~A task list's row in the index carries a **full-width 3px progress bar** under its next-entry line.~~
  ***Done 2026-09-10, walked on the device*** ("Weekend errands", 1 of 4, measured 244 of 975 points
  filled - a quarter exactly). Beside "Done: 1 of 4" rather than instead of it: the words are the exact
  answer and the bar is the one a column of lists is read at.

  **Not MAUI's `ProgressBar`.** That draws Android's, whose track is a mid-grey the platform picks and
  offers no way to change - `BackgroundColor` does not reach it - and three points of `#686565` across a
  card reads as a rule between two things rather than as an empty bar. It is two `BoxView`s, the second
  in a Grid whose columns are the fraction in star units (`ProgressColumnsConverter`). A `MultiBinding`
  of the track's measured `Width` and the fraction was tried first and never drew the filled half at
  all; star units need no measurement, so nothing has to be laid out before the bar is right.
- ~~An entry's row is: circle, text, category chip, and its own **⋯ menu** - the app has no per-row menu
  on a task list.~~ **Already so**, and device-verified: circle, the line, its categories as chips, and
  an `OverflowMenu` at the right of every row. This entry was stale when it was written.
- ~~The list's title menu: Generate inventory, Refresh inventory, Edit, Share…, Delete list.~~ ***Done
  2026-09-10, walked on the device.*** All five were there; what was not is that they were **one flat
  run** under a single "List options" heading, so "In list order" and "Delete list" sat one under the
  other a thumb's width apart. It is three groups now - *Sort*, *Inventory*, *List* - which is the
  design's own menu and the shape every other menu took on 2026-09-09. The shelf's two are their own
  group because they are about the inventory behind the list rather than about the list, and the group
  disappears rather than showing a heading over nothing when there is no shelf to price against.

### Calendar

- ~~The month grid's cell is 44 tall, the number with a **4px dot underneath** for a day that has
  something on it; today is a tint wash, the chosen day an inset accent ring. Two different marks for
  two different things.~~ **Already so**, and device-verified on 2026-09-10: the cell is 44, the dot is
  5 rather than 4 (`Ellipse` under the number, only where `HasEvents`), today takes `AccentSubtle` as a
  wash and the chosen day an accent stroke. This entry was stale when it was written.
- ~~The day view is an **hour rail**: 07:00 to 21:00 at 52px an hour, labels 44 wide right-aligned with a
  hairline running off them, events absolutely placed with a 3px left border in the event's colour, and
  a short event laid out in a row rather than a column so its title and time sit side by side.~~
  ***Done, and the hours are the user's own decision rather than the design's*** (2026-09-10, walked on
  the device). **Midnight to midnight, all twenty-four, every day** - not the design's 07:00 to 21:00
  and not the hours the day happens to use, which is what it drew before. A clock whose length changes
  with what is on it cannot be read at a glance, because nine o'clock is somewhere different on every
  day of the week. The screen opens scrolled to the day's first thing instead, which answers the
  objection the cropping existed for without moving the hours about.
- ~~Under it, an **"All day"** group with hairline rows and, when there is nothing, "Nothing all day." in
  italic.~~ ***Done 2026-09-10, walked on the device.*** And it holds more than the design's: **anything
  on the day with no hour to be drawn at**, which on this app means all-day events *and* deadlines - the
  calendar files a deadline by its date alone (see `CalendarListEntry.For(CalendarDeadline)`), so it has
  no place on a clock either. Without that they would have vanished from the day view entirely, because
  the day view now shows the clock and this list **and nothing else**: the list of the whole period that
  every other view draws under the grid would be the same things a second time.
- ~~The month and year views share one list under the grid: a 58-wide date column (weekday and day over
  the time), a 3px colour bar, then title over place.~~ ***Done 2026-09-10, walked on the device***
  ("Thu 10" over "9:00 AM", the accent bar, then "Dentist" over "Marszałkowska, Warszawa, Poland").
  The date column is what makes a list of a whole period readable at a glance; before it, every row
  carried the entire timestamp at its foot.

  It is not an `ItemCard` any more - a card's anatomy has nowhere to put a leading column of two lines,
  and this list leads with when. The redraw also took out a duplication the design happened to fix: a
  deadline read *"Weekend errands: Flour, rice, olive oil"* with a `WEEKEND ERRANDS` chip under it, and
  now says the list once, on the line the design keeps for where a thing is - a list being the nearest
  thing a deadline has to somewhere, since it is where you would go to tick it off. `Name` still
  carries both, because that is what the list is sorted and searched by.

  The `EVENT` chip went with it: an appointment is what a calendar is for, and a chip on every second
  row saying so is not news. The `TASK` chip stays. *The deadline half of the row is covered by
  `CalendarScreenTests` but **not** walked - the account signed in on the emulator has no deadline the
  calendar will draw, and the chip itself is device-verified on the notes and inventory lists.*

### An event

***Walked on the device on 2026-09-10***, on a "Dentist check-up" made from the calendar's own Add box.

- ~~The title carries a **3px accent bar** down its left side.~~ ***Done.*** The name is bare beside it
  and set in the display face at 24 - the bar is the whole of the heading's decoration, because the name
  is the screen. It is what a calendar row already carries, so an event opened from the list is
  recognisably the thing that was pressed.
- ~~Location is a group: the field, then two links under it - "Use my location" and "Open in Google
  Maps".~~ ***Done.*** All four hand-offs are links on one line under the box now - the app has two the
  design has not, "Remove location" and "Directions". They were two outlined buttons over two links
  before, so the same four things read as two kinds of control and took two rows doing it.
  `CanRemoveLocation` is its own property because a line has nowhere to hang the "and this reader may
  edit" half the row around them used to carry.
- ~~When is **two bordered boxes side by side**, Starts and Ends, each with a small-caps label over the
  value in tabular figures.~~ ***Done.*** The four pickers were two rows of two - the start's day beside
  the end's day, then the start's time beside the end's time - which reads as two ranges rather than as
  two moments, and is why an event spanning days looked like a mistake on the screen meant to show it.

  Two departures. The day and the time are **stacked** inside each box rather than on one line as the
  design has them ("Tue 8 Sep · 14:30"): the design's is a label and these are two things to press, and
  two pickers side by side in half of 412 points do not fit. And both are **bare** (`BareField`),
  because a picker draws Orbit's own hairline box and a box inside a box is two edges saying the same
  thing - a bare picker still opens its dialog when pressed, which was the thing worth checking. All
  day empties both boxes down to their dates and the page closes up around them.
- ~~**Two floating buttons, not one**: cancel (✕, outlined in the line colour) and save (✓, accent).~~
  **Already so**, and device-verified: `Save` on the right and `Cancel` on the left, the second at 44
  across and carrying `GoBackCommand`. It is the one ✕ in the app that is *not* a back control by the
  rule at the top of this document - it is a form's cancel, and it leaves the screen the same way the
  note editor's back does.

**The walk found one defect, now fixed: the foot of this form was underneath those two buttons.** The
page left 28 points of padding under its last row and the buttons stand over the page rather than in
it, so scrolled all the way down the switch that says whether to be told as the event starts sat
exactly under Save - pressing it saved the event instead of setting it, and there was nowhere further
to scroll to get it out from under. It leaves the note editor's 96 now. The colour swatches were half
under the ✕ for the same reason.

### Inventory

- ~~An inventory's row carries a **"low" chip outlined in the task colour** when something is short.~~
  ***Done 2026-09-10, walked on the device*** ("Pantry" reads "3 LOW", "Workshop" carries nothing).
  `InventoryRow.RunningLow` counts the items whose minimum is set and not met - the same test the
  shelf's own rows make - and is blank on a locked private inventory, because what it holds is exactly
  what being private keeps back. The "copy" tag moved onto the name's line beside it while the row was
  open, as the notes list's did.
- ~~A shelf row's note under the name is **coloured by what it says** - "Out — on Weekend errands" reads
  differently from "Running low".~~ **Deliberately not built.** The design's note is a status it makes
  up from the quantity ("Out of stock" / "Running low" / "In stock", coloured when the last is not
  true), and it is the only place its shelf says so. The app's shelf says it on the number itself - the
  amount goes bold and warning-coloured when it is under its minimum - and what sits under the name
  there is facts the design has no notion of: what kind of thing it is, its minimum, when it expires,
  when the batch arrived. A "Running low" line under a number already coloured for being low is the
  same fact twice.

### Contacts and a conversation

- A contact row: 38px ring avatar with a presence dot bottom-right, the name with a **danger dot** when
  there is a request waiting, the last message underneath, and when on the right. **Half already so,
  and the other half deliberately not.** The ring, the presence dot (top right, as `AvatarCircle` has
  always drawn it and as app.css does) and the mark for a waiting request are all there - the mark is
  an accent dot *before* the face rather than on the name, because a mark drawn on an avatar reads as
  being about the person rather than about what is waiting.

  What is not there is the **last message and when**. Both are about how recent the conversation is,
  and Orbit.Web's own `PersonRow` shows neither - it carries the name and a subtitle, and the phone
  carries the same. Adding recency here and not there would make the two clients differ in *what they
  say about a person*, which is the line this project draws: the looks follow the design, what a
  feature does follows the browser. The preview is the harder half of the two - a message is sealed, so
  a list of twenty contacts would decrypt twenty messages to draw itself. If it is wanted it is wanted
  on both clients, and it is written up in `future-plan.md` as that.
- ~~The conversation's own header inside the content: avatar, name, and **"Available · end-to-end
  encrypted"** under it - the app says who but not that the conversation is sealed.~~ ***Done
  2026-09-10, walked on the device*** ("Chat Partner", "Offline · end-to-end encrypted"). The two halves
  belong together: what a reader wants to know before typing is whether it will be seen and who else
  could see it. The encryption half is left off for somebody who has not set up chat - there is no key
  to seal anything with, so saying so would be a promise about an empty screen. No `‹` beside the
  circle, whatever the design draws.

  The dot on that circle stays **top right**, where `AvatarCircle` has always put it because app.css
  does; the design puts it bottom right. One dot in one place across every screen beats matching the
  design on one of them.
- ~~Bubbles are capped at **78%** of the width~~ ***(done 2026-09-10 - `WidthFractionConverter`, both
  conversation screens and its own default; Orbit.Web's `.chat-bubble-row` says 70% and the phone
  follows the design here)*** ~~and outlined, with the meta line (time · read) *outside* the bubble
  underneath, aligned to the bubble's side~~ **- already so.** The cap is not walked: it takes a message
  long enough to reach it, and there is nobody on the emulator to exchange one with.
- ~~A centred small-caps day divider ("Today") between runs of messages.~~ ***Done 2026-09-10, walked on
  the device*** (two messages, one of them backdated in both databases, drew "TUESDAY" and "TODAY").
  `ChatDays.Divide` names the **first** message of each day and leaves the rest blank, so the thread
  needs no grouping and the row template needs no notion of what came before it. Days are the reader's
  own local ones: grouped by UTC, a message sent at 23:40 in London belongs to the day before in
  Warsaw, and the divider would name a day nobody was having. The group thread gets the same divider,
  applied after the announcements are woven in - a day whose only line is somebody joining still needs
  saying which day it was. The four words come from `LastChanged`, which a note's list already uses.

### Settings

***Walked on the device on 2026-09-10***, against the design side by side. Three of the four things
listed here were already built; the fourth was the one nobody had looked at.

- ~~The tabs are a **horizontally scrolling row with a 2px underline** on the chosen one, sitting on a
  hairline that runs the width of the screen.~~ **Already so**, and device-verified: the row scrolls
  ("Debugger" is half off the right edge), the chosen tab is named in the accent and underlined in it,
  and the hairline runs the width under all four.
- ~~Accent swatches are a 36px ring with a smaller filled circle inside it, and the chosen one is filled
  further - the ring is always the colour, the fill says which.~~ **This entry was wrong about the
  design.** The prototype's swatches are `fill: '0px'` for every colour but the chosen one, which gets
  `22px` - so an unchosen swatch is a bare ring, which is exactly what the app draws. Nothing to do.
- ~~The Account tab ends with a **danger-outlined "Delete account"** block, set apart from the rows above
  it: "Everything goes, on every device."~~ **Already so**: a `DangerCard` at the foot of the Account
  tab and nowhere else. The app says more than the design's one line, and asks for the password on its
  own row - which the design has no notion of, and which is the difference between a mock-up and a
  screen that really deletes an account.
- ~~**The theme is a strip of three, not a list to open.**~~ ***Done 2026-09-10, walked on the device.***
  This is what was actually missing here, and it was not in this document. The design's Appearance
  section is a bordered strip - System | Light | Dark, divided by hairlines, the one in force named in
  the accent and ringed in it - and so is Orbit.Web's own theme picker. The phone had a MAUI `Picker`,
  which shows only the answer already in force: a reader had to open it to discover there was a choice,
  and choosing was two presses. `SegmentedStrip` and `SegmentButton` carry it, and the hairlines between
  the segments are the strip's own background showing through a one-point gap, because a separator
  inside a bound layout has nowhere else to live.

## What the design is not evidence about

- **Titles.** `TITLES` in the prototype maps `shelf` to "Inventory" and `tasklist` to "Tasklist".
  Those are prototype placeholders; the real screens are named after the thing they show, which is what
  the bar already does.
- **The map itself.** It is a grid-lined placeholder tile. Nothing it does with pins, the position card
  or the corners survives contact with a real Google map - see the departure recorded above.
- **Version and copy.** "Orbit 1.4.2", "Synced 2 min ago", "orbit.app" and the ad text are sample data.

## Where this leaves things

Nothing here is a defect: the app does what the written spec asked and the suite is green. This is the
list of what the design would still change if it is taken as the specification for the screens the
written spec did not reach, plus the corrections it makes to screens that were built from the spec. The
order worth doing it in, cheapest and most load-bearing first:

1. Groups and counts in `ScreenMenu`, because nine menus depend on it and nothing else can be drawn
   correctly until it exists.
2. The four note-editor behaviours (split at the caret, backspace off a box, the arrows, the foot),
   because the editor is the screen the spec cared most about and these are all in one file.
3. The three screens the spec never reached that the design does draw - sign in, create an account, one
   entry on its own.
4. The per-screen corrections above, which are independent of each other and can be taken one at a time.
