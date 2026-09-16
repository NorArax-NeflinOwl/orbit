# Session handover: orbit-web-13

Previous session: orbit-1d, working the branch below (no worktree; the main checkout)
Date: 2026-09-16

## Branch and PR

- Branch: `feat/future-plan-leftovers-without-sdk-or-android`, all pushed (`e8f01b4b`).
- Open PR: **#288** (draft, `Coding` from that branch). The new session **inherits it** rather than
  opening its own - see `pr-workflow`. Fourteen batches are recorded in its comments; **the description
  itself is stale** - it still says ten batches and lists three audit questions that are all closed now.
  Rewriting it is one of the first things to do (rule 1: the PR must document everything it carries).
- Uncommitted changes: none.

## Goal of the work

Build what `info/future-plan.md` records as missing, asking the user whenever two readings would mean
materially different work. This session ran batches 11-14 and answered a bug the user reported from
their own phone.

## Done

Sixteen commits, `1c7577e4..e8f01b4b`. PR #288's comments carry batch 11-14 in full; the four things
that shape the code:

1. **`RemindDaily` no longer means "this happens every day"** (`3df7f731`). The user reported an
   errand that came back unticked with its deadline moved. The loop gathered finished entries and
   reopened each one - clearing the tick, the cross and the due date - before saying its piece. It now
   asks until the errand is done and stops; the shelf's standing "Update stock levels" is the stated
   exception (`DailyTaskReminderCandidate.ComesRoundAgain`), recognised by the words the server writes
   it with **and** by its list being one an inventory keeps.
2. **A rule across a note** (`e32ae40b`) - a kind of line beside a table and a picture, its stamp
   written once at the press. Touches the surface, both clients, the editor JS, the server, the share
   link and the archive file.
3. **Filtered copy** (`7d6daec2`, `80ab000b`) - `WhatToCopy` shared by notes and task lists, on all
   four browser screens and both phone ones. `TaskListWords` writes the format a note's paste reads back.
4. **Choosing several cards** (`6ea601d5`) and **writing in a group's member lists** (`96105617`) -
   the last two of Batch 6's audit. Both browser-only.

Also fixed on the way, each its own commit: the reopen that did half of `TaskItem.Reopen` (`e3645d18`),
the door question that could not see a table edit (`d105de1a`), the Polish "Archive" that was a noun
where a verb belonged (`44dce7b8`), and two faults in the card work found by re-reading it (`dc41a637`).

**How it was verified**: no SDK in this session (the network policy 403s the download), so structural
replicas only - brace/paren balance on every changed `.cs`/`.razor` **measured against HEAD's own
counts** (this caught a real dropped brace when `ToFormModel` was extracted), `node --check` on the
editor JS, every touched XAML parsed, `node ci/verify-diagrams.mjs` (18/18), the Polish dictionary's
duplicate check and both translation sweeps.

## Still failing / unknown

- **Compiled and tested on 2026-09-16** (session continuing this handover, Windows, SDK 10.0.401):
  Release build clean, `dotnet test Orbit.CI.slnf` 4732 passed / 0 failed at `8f0e9f77`. What it found
  is Batch 15 in PR #288's description - two real defects (`IsArchived` dropped by
  `LinkedTaskCompletionResolver`; an unawaited group save on the phone that crashed the test host), the
  compile errors, and tests behind the code. `GroupMembers` (the static class) is `GroupMemberLists` now.
- **Used in a browser on 2026-09-16** against `orbit_pr288`, a copy of the local database (Batch 16 in
  PR #288). Three faults found and fixed: a note's rule dropped by the editor JS, no way out of choosing
  on an empty list, and the bar's folder picker unusable twice. **Enter adding the next entry could not be
  checked** - the browser pane's synthetic Enter never triggers a form's implicit submit.
- **Nothing has been seen on a device.**

## Rejected approaches (do not retry)

- **A second user-facing switch beside "remind daily"** ("comes round again", per entry). Put to the
  user on 2026-09-16; they chose *"Odhaczone zostaje odhaczone"* instead. The switch is still recorded
  in `info/future-plan.md` if the distinction is ever wanted per entry.
- **A subquery in `GetEligibleAsync`'s filter and projection.** Written, then replaced: the managed-list
  ids are read first and matched in memory, to keep the query to one shape every provider translates
  the same way. Do not put the subquery back to save a round trip - there is one row per inventory.
- **Lifting the group editor's per-entry panel into the member sections.** It is ~510 lines of
  `TaskEditor`'s markup bound to dozens of its methods; a group with four members would be four copies.
  A member's row is deliberately the entry's words and its box, with that list's own editor one press
  away. Extracting the panel into a component is worth doing **on its own terms**, not as a rider.
- **Writing a member list that nobody wrote in.** An untouched list coming back with a new
  `UpdatedAtUtc` is what every other client syncs against - see `WasWrittenIn`.

## Next step

**Look at batches 10-12 on the phone** (archiving, the rule across a note, filtered copy), then take PR
#288 out of draft. To run the branch locally without touching the `orbit` database: start `orbit-postgres`
alone (`docker compose -p orbit up -d --no-deps postgres`, with the root's `.env` and override copied in),
point the API at `orbit_pr288` on port 5080, and serve Orbit.Web on 5081.

## Environment facts confirmed this session

- No .NET SDK in this session's container, and it cannot be fetched (the agent proxy refuses the
  download). Node **is** available - `ci/verify-diagrams.mjs` and `node --check` both run.
- `TaskEditor.razor` is 3.4k lines; its per-entry markup is lines ~104-614.
- `TaskEditor` takes an **edit lock** on the list it edits, with a heartbeat - which is why editing a
  group's members means one lock per member (taken on open, released by every door out).
- `RestockListRefresh` is event-driven only (settings saved, a product placed, a list generated).
  Nothing runs it on a schedule, which is why the daily reminder is what carries the standing round's
  due date forward.
- The Polish dictionary has 1766 keys, no duplicates. Keys reached through a method rather than a
  literal (`T[what.Label()]`) are invisible to the coverage sweep - `FilteredCopyTests` guards those
  four by hand.
- PR #288: 96 commits, 365 files, +38938/-896 against `Coding`. Seven migrations on the branch (five
  server, two phone).
