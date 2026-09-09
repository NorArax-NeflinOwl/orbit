# Session handover: orbit-mobile-4

Previous session: orbit-mobile-3 (walking the written spec on a device, then reading the design)
Date: 2026-09-09

## Branch and PR

- Branch: `claude/orbit-mobile-continue-131a2c`, cut from `origin/Coding`.
- Open PR: **#267 — [Mobile] Walk the written spec on a device, and fix the three things that were
  dead**, against `Coding`. The new session inherits it: further work goes on that branch and #267's
  description is extended so it still documents everything it carries. The other open PRs are **#264**
  (the integration PR a workflow keeps open) and **#266** (a web session's), so the cap of three is
  reached - do not open a fourth.
- Uncommitted changes: none.

## What this session was asked to do, in order

1. Continue from orbit-mobile-3's "Next step": build, install and walk the app against the written spec.
2. Move the map's crosshair - first clear of the zoom buttons, then to the top right.
3. Read the design bundle and write up what it would still change.
4. **Start implementing the design where the app is missing it.** This is the live task.

## Done

Nine commits. The first five are on #267 already and described there; what matters here is (4).

**`ScreenMenu` is groups now** (`f4d4f151`). The design's menu is a stack of groups each under its own
heading, and nine of the twelve it specifies are grouped; `ScreenMenu` could draw one heading over a flat
list, so four screens made the reader open a *second* panel to reach the second half of the same
question. `Groups` + `ShowGroups` carry it, an empty group is left out rather than drawn as a heading
over nothing, and `Entries` stays as the flattened list. `ScreenMenuEntry.Count` is a quiet column at the
end of the line. Converted and device-verified: **notes, tasks, map, note**. Still flat and named in the
design as grouped: **dashboard** ("Show" + "Sort") and **calendar** ("Show" + "Calendar").

**The note editor's first two behaviours** (`2d11d96e`). Enter carries what follows the caret onto the new
line; a checklist continues until a line is left empty. Backspace at the head of a line with a box takes
the box off, and only a second press joins upwards. The editor gained its foot - a hairline and
"Type [] for a checkbox", the only place that trick is written down anywhere.

## Where to pick it up

`info/android-design-deltas.md` is the whole list, screen by screen, with what is done marked. Its own
recommended order, with the first two items now partly done:

1. ~~Groups and counts in `ScreenMenu`~~ - **finish it**: the dashboard and calendar menus are still flat.
2. The note editor - two of four done. **Arrow up/down between lines** is the next one and wants the
   Android key hook `NoteLineBackspace` already owns (same file, one more key). The foot's left half is
   deliberately not built.
3. ~~The three screens the spec never reached that the design draws.~~ **Done 2026-09-09** - all three
   existed already, so this was composition rather than plumbing. Sign in and create-an-account are
   device-verified. `TaskItemSummaryPage` got the composition and **none of the editing the design
   draws**, because that screen is deliberately read-only, and it is **the one thing not walked on the
   device** - see `android-design-deltas.md` under its own heading for why it is hard to reach.
4. The per-screen corrections, independent of each other. This is what is left.

## Rules that came from the user this session, and are not negotiable

- **Every back control the design draws is a mistake.** The Android head goes back through the
  navigation stack and Android's own back gesture, and nothing else. The design draws three that must
  not be built: a `‹` in the conversation header, and "‹ Map" at the top of each of the map's two lists.
  Recorded in `android-design-deltas.md` under its own heading.
- **Where the design and the written spec disagree, the spec wins.** Six of those are listed at the top
  of the same document. Do not "correct" the spec back to the design.
- The map's crosshair is **top right**, which departs from the design deliberately - see below.

## Still failing / unknown

- **`TaskItemSummaryPage` has not been seen running.** It opens from the calendar only for a deadline
  that `IsSomewhere`; every other tap lands on the task list. Switching an entry's Type to Calendar and
  giving it a place did not bring it into reach quickly enough. Walk it first.
- **Left on the emulator, and harmless**: "Update stock levels" on *Restock supplies - Workshop* was
  switched to a Calendar type and given the place "Marszalkowska 1, Warszawa" while trying to reach that
  screen.
- **The task list's filters still write their count into their label** - "All 4", "Pending 0",
  "Overdue 3" come out of `TaskListFilter.Label` as one string, so the number is the same size as the
  words and a filter with nothing behind it says "0". `ScreenMenuEntry.Count` exists for this now; the
  categories beside them were moved onto it, these were not, because the label is built in the view model
  and its own tests read it.
- **The `✓` in a menu is still a character, not a drawing** (`ScreenMenuEntry.Mark`). It renders on
  API 36 but neither Lora nor Cormorant has the glyph, so Android substitutes. Long-standing, in
  `future-plan.md`.
- The two map list screens were **invented** by orbit-mobile-3 and the design does draw them - they have
  not been checked against it.

## Rejected approaches (do not retry)

- **Do not overload `Show` for groups.** `Show(IEnumerable<ScreenMenuEntry>, …)` and
  `Show(IEnumerable<ScreenMenuGroup>, …)` are ambiguous for an empty collection expression, so `Show([])`
  stops compiling and the existing test broke. It is `ShowGroups` for that reason, and the comment on it
  says so.
- **Do not add "Off" to the map's Sharing group.** The design has it; there is nothing here for it to
  mean. Sharing is not a state the screen is in but a set of people it is shared with, and stopping is
  per person on the list screen - which is where the design puts it too.
- **Do not put the map's crosshair back in the bottom-right corner**, however plainly the design draws it
  there. Android owns both bottom corners of a real map - the zoom buttons on the right (the crosshair was
  covering them and taking their presses, measured and confirmed) and Google's logo on the left, which may
  not be covered at all. The design's map is a placeholder tile with no furniture, so it never had to
  share. The card that says where you were last read to be stops 80 short on the right for the same reason.
- **Do not read the design bundle as a style guide.** That is what produced the rejected version. It is a
  specification of composition: what is on each screen, in what order, and what a press does.
- **Do not `git stash`** in the shared checkout, and commit only from a worktree.

## Environment facts confirmed this session

Everything in `info/sessions/orbit-mobile-3.md` under the same heading still holds. Plus:

- **A worktree needs four gitignored files, not three.** `.env` and
  `Platforms/Android/AndroidManifestOverlay.xml` come from the main checkout, but
  `src/Clients/Orbit.Maui/google-services.json` is **not there** - it lives in `orbit/secrets/`. Without
  the overlay *and* it, the map draws a label instead of tiles, which reads as an unfinished screen
  rather than an unconfigured build.
- **Build with `-p:OrbitWebBaseAddress=https://…/`** to see the About screen's row of documents; told
  nothing, a build lists the licence alone, which is correct and is what the emulator shows by default.
- **`uiautomator dump` returns nothing while the app is starting.** Wait 25s after
  `am start` before the first tap - a tap at 14s produced an ANR ("Input dispatching timed out") that
  looks like a crash and is not. The trap is in [[orbit-android-head-gotchas]] and cost time again here.
- **`adb shell input text` does not decode `%5B`.** Only `%s` is special (it becomes a space). To type
  `[` and `]`, use `input keyevent KEYCODE_LEFT_BRACKET KEYCODE_RIGHT_BRACKET`.
- **`dumpsys input_method | grep mServedView` is the truth about focus.** `uiautomator`'s
  `focused="true"` sat on a button while typing went somewhere else entirely; `mServedView` named the
  right control every time.
- **Lora has `[` and `]`.** "Type [] for a checkbox" at 11px looks like a single tofu box in a
  screenshot and is not - the two brackets adjacent simply draw a rectangle. Verified by parsing the
  font's cmap; the same check showed `✓` (U+2713) genuinely missing, which is the menu-tick problem.
- Release build: **19 warnings, unchanged all session**. `dotnet test Orbit.CI.slnf`: **3430 pass**
  (3424 at the start, plus six new tests).
