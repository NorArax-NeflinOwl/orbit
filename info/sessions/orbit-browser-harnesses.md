# Session handover: orbit-browser-harnesses-2

Previous session: orbit-browser-harnesses (worktree `orbit-android-ui-continue-a9292f`)
Date: 2026-09-19

## Branch and PR

- Branch: `claude/orbit-browser-harnesses`
- Open PR: **#299** — *[Web+Phone] The browser modules nothing could test, and the presses that said
  nothing*, against `Coding`. The new session inherits it rather than opening its own (see
  `pr-workflow`). Its description is current and long; extend it rather than rewriting it.
- Uncommitted changes: none.

**The branch before this one was merged mid-session and it was not noticed for four commits.** Check
`gh pr view 299 --json state` **before pushing**, not before opening — a push to a merged branch
succeeds silently and the work is stranded. Recovering is cheap: branch from `origin/Coding`,
cherry-pick, open the next PR.

## Goal of the work

Work through the user's 2026-09-18 list, then the older issues, then whatever the code itself turned
out to be hiding. The list is finished; the round ended on test flakiness and on two old issues.

## Done

Sixteen commits. The ones worth knowing about:

- **Three browser harnesses** (`ci/verify-note-surface.mjs`, `verify-menu-anchor.mjs`,
  `verify-map-markers.mjs`), in `main_orbit.yml`'s existing `test` job on the browser the other two
  already install. They cover `checklistTextEditor.js`, `menuAnchor.js` and `updateLocations` — none of
  which bUnit can reach, and each of which had already produced a user-visible fault. Each was checked
  by putting its fault back and watching the right checks, and only the right checks, go red.
- **#294 explained**: the separator fault is what `af12718d` fixed on 2026-09-16, the evening before the
  list that reported it. The deployed `checklistTextEditor.js` is byte-identical to this branch's and
  round-trips every line in a real browser. Left open for a confirmation on the current build.
- **#186 fixed** on both clients: a link to a deleted list was sent back on every save, so the server
  refused *every later save of that list*. Dropped as the browser fills its form and as the phone sends.
  `FakeTasksServer` refuses such a link now, in the server's own words — it used to take one.
- **#185 audited**: all seven components that hand a `DotNetObjectReference` to a module tell the module
  to let go before disposing it. Nothing left to reproduce from the code. The audit found and fixed a
  live fault of the same family in `menuAnchor.js` (one binding slot, cleared by whichever panel closed).
- **"Say why" round**: greyed entries on the phone say what they are waiting for
  (`ScreenMenuEntry.Note`); a refused press on a card is said out loud on the calendar and the notes; the
  map says "The map is up to date." when a refresh changed nothing.
- **Conversation recency**, both clients: `Orbit.Core.Chat.ConversationRecency` is the one rule, and
  `GetContactsQueryHandler.LastMessageIn` answers an *emptied* conversation from its messages rather than
  from the contact row — asking only about the conversations that can differ, since this list is re-read
  on every poll tick. `PersonRow` shows the time on both clients.

Verified at the end: `dotnet test Orbit.CI.slnf` 4930 passed / 0 failed (Api 1558, Web 1666, Mobile
1706); `dotnet build Orbit.CI.slnf -c Release` clean; the Android head Release clean; five browser
harnesses 55/55; `ci/verify-diagrams.mjs` 18/18.

## Still failing / unknown

- **`NameSuggestionSourceTests.A_kind_switched_off_is_offered_as_words_only` flakes under load** — about
  one run in five with a second suite alongside. Reproduced twice. **Ruled out: the deadline.** The
  failing run took 201 ms against a five-second wait, so it is a race at the moment the list first
  appears, not a slow machine. The press does not reach `OnChosen` and `chosen` stays null. Two real
  defects in `NameSuggestions` were fixed while looking (re-entrant `LookUpAsync`; choosing and Escape
  not stopping the lookup in flight) and it has not recurred in eight runs since — **that is eight runs,
  not a fix**: the test only ever starts one lookup, so neither fix obviously reaches it. The assertion
  now reports which branch ran, the render count either side of the press, and how many options are on
  screen, so the next occurrence names the cause.
- **`ChatThreadTests.A_notification_stays_while_their_newest_message_is_not_yet_in_view`** fired once in
  the last full run of this session and has never been caught with its message. A capture loop was
  running when the session ended; nothing had been caught.
- **`GroupConversationPagesTests`** has done it once historically, uncaught.
- Issues **#293**, **#294**, **#296** need somebody to see them on a real device or browser. So does the
  map's Start/Share on a phone, which is now one press: Options → Location on, press Start, read the
  line under it.

## Rejected approaches (do not retry)

- **Do not "fix" the NameSuggestions flake by widening the wait.** The 201 ms capture rules the deadline
  out; a longer wait would hide the race rather than explain it.
- **Do not read the last message for every conversation on the contacts list.** The first version of
  `GetLastMessageTimesAsync` aggregated every message a reader had ever exchanged, on a list re-read on
  every poll tick, for an answer the contact row already gives — `LastMessageAtUtc` is moved forward on
  every send. Only an *emptied* conversation can differ, so only those are asked about.
- **Do not add the same sentence to the four browser editors for a read-only share.** It was tried and
  reverted: the banner above the form already says it (`SharedItemAccess.Description`).
- **Do not settle an async command with a fixed number of `Task.Yield()`s.** That is a guess at how many
  turns the scheduler needs, and it is what made `PeriodicSyncTests` flake. Wait for the thing the press
  was to bring about.

## Next step

Catch `ChatThreadTests.A_notification_stays_while_their_newest_message_is_not_yet_in_view` with its
message, by running the web suite in a loop with a second suite alongside and grepping for its failure
block (it fires roughly one run in ten). Read the message before touching the test: its own waits are
already 15 seconds, so it is not a deadline either, and the same "hope instead of wait" shape that
explained `PeriodicSyncTests` is the first thing to look for in `WaitUntilTheLoopHasTickedAsync`.

## Environment facts confirmed this session

- **No node or npm on this machine.** The `ci/verify-*.mjs` harnesses run in Docker; the browser ones
  need `mcr.microsoft.com/playwright:v1.56.0-noble` **and `npm install playwright@1.56.0`** — installing
  `playwright@1` pulls a newer one than the image's browser build and fails with *"Executable doesn't
  exist at /ms-playwright/chromium_headless_shell-…"*. The exact commands are in
  `info/uml/README.md` (diagrams) and `info/testing-and-running-locally.md` (the five browser ones).
- The deployed web is `https://orbit-web.victorioustree-36ad82ca.polandcentral.azurecontainerapps.io`,
  and `/js/*.js` is served with `Cache-Control: no-cache`, so a deploy is picked up without a hard
  reload. Fetching `/js/checklistTextEditor.js` from it is how "is the fix live?" was answered.
- `sed`/`perl` line-anchored edits fail on this checkout: it is CRLF. Anchor on content, not on `$`.
- `Orbit.Core` has **no project references at all** — a rule shared by both clients cannot see
  `Orbit.Contracts` and has to take primitives (see `ConversationRecency`).
