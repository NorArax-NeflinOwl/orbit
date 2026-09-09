# Session handover: orbit-mobile-5

Previous session: orbit-mobile-4 (the design's menus, the note editor, the type, the dialog)
Date: 2026-09-09

## Branch and PR

- Branch: `claude/mobile-design-continued`, cut from `origin/Coding`.
- Open PR: **#268 — [Mobile] The last menus, the note editor, and Orbit.Web's type back on the phone**,
  against `Coding`. Seven commits. `Coding` was merged into it at the end of the session and it is
  `MERGEABLE` again - the new session inherits it rather than opening its own.
- Uncommitted changes: none.

**A branch can die under you.** #267 was merged mid-session, and the next commit was refused by a
pre-commit hook: *"branch was already merged via pull request #267. Commits added to it now reach
nothing."* Do not force past that - cut a fresh branch from `origin/Coding` and carry the working tree
over, which is what produced this branch.

## Done

`info/android-design-deltas.md` is the map of the design work, with everything done marked. In short:

- **Every menu the design names as grouped is grouped** - notes, tasks, map, note, dashboard, calendar.
  `ScreenMenu.Groups`/`ShowGroups`, and `ScreenMenuEntry.Count` as its own quiet column.
- **The note editor**: Enter splits at the caret, backspace takes a tick box off before it merges, the
  editor has its foot, Enter on the name goes into the writing, a ticked line can be reached by the
  keyboard at all, the note is written by **Save and nothing else**, and back asks before it throws the
  writing away.
- **Three screens redrawn** - sign in, create an account, one entry on its own.
- **Orbit.Web's type is back on the phone** (IBM Plex Sans over Space Grotesk) and the confirmation
  question is Orbit's own panel rather than Android's alert.
- **The map's crosshair is top-right**, out of both corners Android owns.

## Where to pick it up

The structure is done; what is left is per-screen and independent. `android-design-deltas.md` lists it
screen by screen - the dashboard's counter strip, the notes tag chip, the calendar's day rail, an
event's second floating button, the inventory's "low" chip, the conversation's presence and encryption
line, the settings tabs. Plus **arrow up/down between lines** in the note editor, which wants the
Android key hook `NoteLineBackspace` already owns.

## Rules from the user, not negotiable

- **Every back control the design draws is a mistake.** The Android head goes back through the
  navigation stack and Android's own gesture, and nothing else.
- **Where the design and the written spec disagree, the spec wins.** Six of those are listed at the top
  of `android-design-deltas.md`.
- **Only Save writes a note.** Leaving abandons the edit, ticks included; back asks first.

## Still failing / unknown

- **The map's two list screens are half verified.** Both open, carry their own name, have no back link
  and have their own empty states. **The rows are unseen** and cannot be seen cheaply: a row needs a
  live location share, whose position is stored encrypted (`OP_LOCATIONS.OP_L_CIPHERTEXTBASE64`) so it
  cannot be seeded, and the app offers a candidate to share with only once there is a **conversation** -
  which needs that person to have set up chat on a device of their own.
- **Long names wrap more often than they did** now the faces are wider - the calendar's list especially.
  That is the face, not a fault, but if it is to change it is a decision about size.
- **Folders are still not built on the phone at all.** The design puts them in the Notes and Tasks
  menus with counts.

## Rejected approaches (do not retry)

- **Do not overload `ScreenMenu.Show` for groups** - an empty collection expression fits both
  signatures, so `Show([])` stops compiling. It is `ShowGroups`.
- **Do not add "Off" to the map's Sharing group**, and do not give the calendar the design's *Show*
  (layers) and *Calendar* groups: those name things this app does not have.
- **Do not put the map's crosshair back in a bottom corner.** Android owns both - zoom right, Google's
  logo left, which may not be covered at all.
- **Do not put `TextDecorations` on an `Entry`.** Still true, and it is why a ticked line is a Label and
  why reaching it needed `IsBeingWrittenIn`.
- **Do not read the design bundle as a style guide.** It is a specification of composition.
- **Do not `git stash`** in the shared checkout, and commit only from a worktree.

## Environment facts confirmed this session

Everything in `orbit-mobile-4.md` still holds. Plus:

- **`TaskItemSummaryPage` is all but unreachable, and here is the key.** It opens from the calendar only
  for a deadline that `IsSomewhere` = `LinkedCalendarEventId is not null || Location.Length > 0`, and a
  deadline whose event is on the same day is dropped from the list and drawn as that event - so a linked
  event cannot get you there. What can is a **Calendar-kind entry with an address and no event yet**,
  because `TaskItemSubject` keeps a location only for `kind == Calendar && LinkedCalendarEventId is
  null` and blanks it for every other kind. **That is why typing an address on a Checklist entry looks
  like it saves and does not.** The phone's own form cannot produce that state; it was seeded in the
  local Postgres.
- **A file written by the Write tool is LF, while the repository is CRLF.** A `python .replace()` whose
  pattern was converted to `\r\n` silently matched nothing and reported success. Check the file's line
  endings before a scripted edit, or use the Edit tool.
- **The local API blanks fields the domain says are meaningless**, so a raw `UPDATE` can be invisible
  through the API and read as a broken sync. Check the entity, not only the table.
- **`docker restart orbit-api`** is the quick way to rule out in-process state; it did not help here,
  which is what pointed at the domain rule.
- Login for the local API is `POST /api/auth/login` with `{"emailOrUserName","password"}`; the token
  comes back as `token`.

## Left on the emulator, and harmless

The note "Enter test"; an empty conversation with Anna Kowalska; "Update stock levels" on *Restock
supplies - Workshop* switched to a Calendar type; and **"Book the car inspection" seeded as a Calendar
entry with the address "Stacja kontroli, Zabłocie" and no event** - which is the only way to reach
`TaskItemSummaryPage`, so it is worth leaving in place.
