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
| a back link inside the conversation header | no back arrow anywhere | the spec removed it from the bar and the phone's own gesture replaced it |
| a "chat request" counter always on the dashboard | the counter only when somebody is waiting | Orbit.Web's own rule; a standing "0" is not news |

## The screens the design draws that the written spec never reached

This is the larger half of what the design is worth. `android-ui-parity.md` lists the screens the spec
does not describe; the design describes six of them, so they no longer have to be guessed at.

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

**The map's two lists.** These were invented on 9 September to satisfy "the lists open as their own
page", and the design has them after all - so they are worth checking against it rather than keeping.
Both are hairline rows with a 34px ring avatar. "Locations I share": name, the sharing mode underneath,
and a **"Stop" link** on the right. "Shared with me": name, where they are underneath, and a chevron.
Both carry a "‹ Map" link at the top, which is the one thing to drop - the spec has no back links.

**A conversation** and **Settings** are also drawn, and both are built already; what the design changes
about them is under "Screens already built" below.

## Screens already built, and what the design corrects

### The shell

- **A title menu is groups, not a list.** The design's menu is a stack of groups, each with its own
  heading in uppercase accent with a hairline above it, and each entry is *tick column, label, optional
  count*. `ScreenMenu` has **one** optional heading for the whole panel and a flat `Entries` list with
  no count, so a menu that is two things at once - "Show" *and* "Sort" on the dashboard, "Sharing"
  *and* "Locations" on the map - cannot be drawn. This is the single largest structural correction the
  design asks for, because nine of the twelve menus it specifies are grouped.
- **The counts on folder entries.** Notes and Tasks list their folders in the menu with the number of
  things in each. Nothing in `ScreenMenuEntry` can carry that.
- The tick is still the character `✓` (`ScreenMenuEntry.Mark`), which neither Lora nor Cormorant has -
  already written up under "Redrawing the rest of the phone" in `future-plan.md`.

### Dashboard

- **The counter strip is one baseline, not three columns.** The design puts a coloured dot, the number
  in Cormorant 20, and the label in 12px on a single row, three of them side by side under a hairline.
  The app stacks number over label with no dot.
- **A card's rows can carry more than they do.** The design's row is: optional colour dot, optional
  26px initials ring with a presence dot on it, the title, an optional priority chip, an optional
  48×3 progress bar, and the detail. Contacts rows on the dashboard therefore show who is online, and
  task-list rows show how far along they are.

### Notes

- **The tag chip belongs on the title's line, at the right.** The app puts it on its own line under the
  title - visible on any note tagged "copy".
- The preview line is **justified** (`text-align:justify`), which is the design's habit for running
  prose and is why the notes list reads as a column of paragraphs rather than a list.
- The notes list has **no search box**. Tasks, Inventory and Contacts each have one; Notes does not.
  The app matches this today - worth not "fixing".

### A note

- **Enter splits the line at the caret.** The design moves whatever is after the caret down onto the
  new line. `AddLineAfter` inserts an empty line and leaves the text where it was, so pressing Enter in
  the middle of a sentence loses nothing but does nothing either.
- **Backspace at the head of a checklist line takes the box off first.** The design's `lineKeyDown`
  strips the checkbox and leaves the text as a plain line; only a second backspace merges it upward.
  `MergeIntoTheLineAbove` merges straight away, so there is no way to un-tick-box a line from the
  keyboard.
- **Arrow up and arrow down move between lines**, keeping the column. Nothing does this today.
- **The editor has a foot.** A quiet line under the writing: who it is shared with and when it was last
  edited on the left, a hairline, and "Type [] for a checkbox" on the right - which is where the reader
  is told the trick at all.

### Tasks, and a task list

- A task list's row in the index carries a **full-width 3px progress bar** under its next-entry line.
- An entry's row is: circle, text, category chip, and its own **⋯ menu** - the app has no per-row menu
  on a task list.
- The list's title menu: Generate inventory, Refresh inventory, Edit, Share…, Delete list.

### Calendar

- The month grid's cell is 44 tall, the number with a **4px dot underneath** for a day that has
  something on it; today is a tint wash, the chosen day an inset accent ring. Two different marks for
  two different things.
- The day view is an **hour rail**: 07:00 to 21:00 at 52px an hour, labels 44 wide right-aligned with a
  hairline running off them, events absolutely placed with a 3px left border in the event's colour, and
  a short event laid out in a row rather than a column so its title and time sit side by side.
- Under it, an **"All day"** group with hairline rows and, when there is nothing, "Nothing all day." in
  italic.
- The month and year views share one list under the grid: a 58-wide date column (weekday and day over
  the time), a 3px colour bar, then title over place.

### An event

- The title carries a **3px accent bar** down its left side.
- Location is a group: the field, then two links under it - "Use my location" and "Open in Google Maps".
- When is **two bordered boxes side by side**, Starts and Ends, each with a small-caps label over the
  value in tabular figures.
- **Two floating buttons, not one**: cancel (✕, outlined in the line colour) and save (✓, accent).

### Inventory

- An inventory's row carries a **"low" chip outlined in the task colour** when something is short.
- A shelf row's note under the name is **coloured by what it says** - "Out — on Weekend errands" reads
  differently from "Running low".

### Contacts and a conversation

- A contact row: 38px ring avatar with a presence dot bottom-right, the name with a **danger dot** when
  there is a request waiting, the last message underneath, and when on the right.
- The conversation's own header inside the content: avatar, name, and **"Available · end-to-end
  encrypted"** under it - the app says who but not that the conversation is sealed.
- Bubbles are capped at **78%** of the width and outlined, with the meta line (time · read) *outside*
  the bubble underneath, aligned to the bubble's side.
- A centred small-caps day divider ("Today") between runs of messages.

### Settings

- The tabs are a **horizontally scrolling row with a 2px underline** on the chosen one, sitting on a
  hairline that runs the width of the screen.
- Accent swatches are a 36px ring with a smaller filled circle inside it, and the chosen one is filled
  further - the ring is always the colour, the fill says which.
- The Account tab ends with a **danger-outlined "Delete account"** block, set apart from the rows above
  it: "Everything goes, on every device."

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
