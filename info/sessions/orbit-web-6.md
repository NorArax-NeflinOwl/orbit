# Session handover: orbit-web-6

Previous session: orbit-web-5 (worktree `orbit-web-improvements-04af57`)
Date: 2026-09-09

## Branch and PR

- Branch: `feat/ten-from-the-user`, nine commits ahead of `origin/Coding`, all pushed.
- Open PR: **#263 - "[Web] The rest of the fourteen, and the two that needed digging"** (`Coding` from
  `feat/ten-from-the-user`). The new session inherits it rather than opening its own; extend its
  description as work lands. #260 was this session's first PR and was merged mid-session, which is why
  there are two: check `gh pr view <n> --json state` before assuming an open PR is still open - the same
  warning orbit-web-5's own handover carried.
- Also open: #264, the integration PR (`Coding` to `main`). #262 (mobile) is gone, so one slot is free.
- Uncommitted changes: none.

## Goal of the work

Two lists of items the user dictated in Polish - ten, then four more - on the browser client. All
fourteen are done. Two of them (a list showing finished work, filtering reaching outside the folder) had
no reproduction and were left until last; both turned out to be real and were found by reading.

## Done

Every item below is one commit, in this order.

**Stop the advert sitting on top of Save on a phone (14729e35).** The editor rail is a fixed bar along
the foot of the window below 680px, and the ad banner sat over it - Save, Edit and Menu were invisible.
`.app-body:has(.editor-rail) .ad-banner { display: none }`.

**List what somebody has been given, and let it be taken back (8530a8ae).** A contact's card lists every
note, list, event and shelf this reader has shared with them (`GET /api/shares/with/{userId}`), with a
cross that withdraws one (`DELETE /api/shares/{kind}/{shareId}`). New slice
`Orbit.Core/Sharing/{GetSharesWith,RevokeShare}`; all four share repositories gained `GetSharesToAsync`
and `RemoveAsync`.

**Take an invitation back with the share it announced (11f1fc7b).** Withdrawing a share left its chat
invitation in the conversation, still offering an "Accept" that answers "no such share". The server
cannot find that message by reading it - the chat is sealed and the share id is inside the sealed
payload - so the sender now says which share an invitation announces in the clear, alongside the
ciphertext: `SendMessageRequest.AnnouncesShareId` to `SendMessageCommand` to `ChatMessage` to
`OP_C_ANNOUNCESSHAREID` (migration `TakeAnInvitationBackWithItsShare`, indexed).
`RevokeShareCommandHandler` tombstones every message naming that share and announces the change to both
ends. Five of the six `isShareInvitation: true` call sites pass a share id; `MapPage` does not, because
a shared position is not a share row.

**Say when a contact was last here (188d225e).** `ContactDto.LastSeenAtUtc`, fed by the new
`UserPresence.LastSeenToTheMinuteUtc`, drawn as a **Last active** row. To the minute on the wire and on
the screen; the stored instant keeps its seconds because `StatusAt` measures the away and offline
thresholds against it - rounded down, somebody last seen at 10:00:59 turns "away" five seconds later.
"Never" for an account nobody has seen.

**Fold a page's filters and folders into one button on a phone (93c61ad8).** New `PhoneToolbar`, used on
the dashboard, the notes and the task lists. On anything wider than 680px the wrapper and its panel are
`display: contents`, so the controls lay out exactly where they did and only the button is hidden -
nothing is drawn twice. The task lists' search and chips moved out of `ObjectList` to sit with the tabs,
so a phone has one button rather than two.

**Put the map back on a phone, above the lists it illustrates (c9eb9e7e).** `.map-canvas` was
`display: none` below 680px; it is back, between the heading and the lists, by making `.map-panel`
`display: contents` and ordering its children around the canvas. Start and the share picker are hidden
there instead, with one line standing in for them - see "Still failing / unknown".

**Give the calendar a week, and let a day answer about that day (229aa864).** Three interlocking things:
`CalendarViewMode.Week`, drawn by `CalendarMonthGrid` with one row in it
(`CalendarGridBuilder.BuildWeekGrid`, `IsRoomy`); `ListedPeriod()` now returns whatever the grid is
showing rather than always the month; and `Calendar.ShowsEverythingInThisView` makes the day view show
finished work whatever the Show menu says, with that menu entry ticked and greyed there. Plus
`[SupplyParameterFromQuery]` `view` and `on`, obeyed once (`_openedAs`), which is how the dashboard's
today strip opens the calendar on today rather than on the month today is in.

**Ask the chips about the folder somebody is standing in (d52a381d).** The task list page filtered its
cards by the open folder and then described the whole account: the "All" chip counted every list the
reader owns, the category chips were built from every entry on every one of them, and the search looked
through all of it. New `Tasks.TaskListsInTheOpenFolder` is what all of that reads now. An empty tab says
"Nothing is in this folder yet." rather than "No lists are all."

**Let a closed list stop being owed (4ba1cce7).** Marking a list finished with work still on it filed it
under Finished and changed nothing else - its unticked deadlines stayed on the dashboard's Upcoming
card, in the count of what is due today, and on the calendar's list and grid. An entry on a closed list
is now done whatever its own tick says, in `Calendar.LoadDueTasksAsync`, `Calendar.IsTickedOff`,
`Calendar.TickedOffEventIds`, `Dashboard.UpcomingDeadlines`, `Dashboard.IsStillToDo`,
`Dashboard.TasksDueTodayCount`, and the phone's `CalendarDeadline` and `DashboardViewModel`.

**Verified.** `dotnet test Orbit.CI.slnf` after every commit - **3427 passed, 0 failed** (Api 1249, Web
969, Mobile 1209). Release build clean each time, and it is the build that gates: an xUnit analyzer rule
came back as an *error* only there. `ci/verify-diagrams.mjs` through the Docker recipe - 16 diagrams, 0
failed. The migration is applied against the local PostgreSQL container. `info/functionality.md`,
`info/uml/database.md` and `info/future-plan.md` are current.

## Still failing / unknown

- **Nothing here has been looked at in a browser.** The pane cannot reach `localhost` in any form, and
  this session does not sign in or create accounts. The phone layouts (the Menu popup, the map above the
  lists, the advert), the week view and the day view are argued from the markup and pinned by bUnit,
  which says nothing about how they look at 375px. This is the largest open risk in #263.
- **Why the map's Start and share picker do nothing on a phone.** Hidden below 680px
  (`.map-panel-start`, `.map-panel-share`) on the user's report, with a line in their place. That is a
  cover, not a fix, and `info/future-plan.md` says so: the code path is the one a desktop browser runs,
  and every way it can fail already puts a message on the screen. To rule out in order - the Options
  switch never turned on for that device, a browser refusing geolocation to a self-signed certificate on
  `https://localhost:8443`, a permission denied once and now denied silently. Needs somebody watching
  the console on the actual device.
- **The phone does not set `AnnouncesShareId`**, nor the older `IsShareInvitation`. A share offered from
  a phone and withdrawn from anywhere leaves its invitation behind. Nothing regressed; the new capability
  has not reached the phone. It sends by enqueueing, so both fields need to reach `OutgoingChatMessage`
  and the queue row behind it.
- **Whether the two dug-out fixes are what the user actually saw** is unconfirmed until #263 deploys.

## Rejected approaches (do not retry)

- **`perl -0pi -e` with a multi-line pattern.** The checkout is *mixed*: some files are LF, some CRLF,
  and a pattern written for the wrong one matches nothing while reporting success. Worse, this harness
  collapses a double backslash to a single one inside a Bash command string, so a pattern meant to say
  "either ending" arrives saying "a real newline" and silently does nothing. What worked: a small script
  that normalises the file to LF, substitutes, writes back in whichever ending the file arrived in, and
  **dies when the pattern does not match**. Any substitution that cannot say "no match" will eventually
  lie to you here.
- **A large heredoc through the Bash tool.** Two attempts to write this very file with `cat <<'EOF'`
  died with "unexpected EOF while looking for matching quote", pointing at a line in the middle of the
  prose. The Write tool took it unchanged. Use Write for anything long; keep heredocs to a few lines.
- **Reusing scratch filenames between substitutions.** Three razor call sites were rewritten with a
  *previous* step's replacement text, because that file still held it - the command meant to rewrite it
  had failed at an earlier `&&` and never ran. Give every replacement its own name.
- **Comparing two `Guid.NewGuid()` values in sorted order.** Two random ids sort into whichever order
  they happen to sort into; the assertion passed by luck for one run and failed on the next. Compare as
  a set.
- **Splitting the calendar work three ways.** The week view, the day view's scope and the query
  parameters all rewrite the same handful of members (`ListedPeriod`, `PeriodLabel`, `Step`,
  `ShowsEverythingInThisView`); they went in as one commit whose message names the three parts.

## Next step

Ask the user whether to keep going on the browser client or move to the phone. If the browser: take
`info/future-plan.md` in order, as orbit-web-5 did. If the phone: the two named gaps above are the
smallest real ones - `AnnouncesShareId` through the phone's send queue, and the product and
generate-inventory parity listed under "Smaller identified follow-ups".

## Environment facts confirmed this session

- **The browser pane cannot navigate to `localhost`** in any form. The deployed app and the local stack
  are both unreachable from it; only Docker and the CLI are.
- **There is no `node` and no `python` on this machine.** `ci/verify-diagrams.mjs` runs through the
  Docker recipe in `info/uml/README.md`, passing the path as a Windows path rather than `$PWD`. Perl is
  present, and is what the edit script above is written in.
- **The local PostgreSQL container is `orbit-postgres`**, user and database both `orbit`:
  `docker exec orbit-postgres psql -U orbit -d orbit`. `orbit-api` and `orbit-web` were up throughout.
  There is no `.env` at the root of the main checkout on this machine, so `dotnet ef database update`
  has no connection string to read - migrations were applied by hand and stamped into
  `__EFMigrationsHistory`. Mind the timestamp there: the id is the migration *file's*, and guessing it a
  second out writes a row that matches no migration.
- **`dotnet ef migrations add` "fails" with `HostAbortedException`** and still writes the migration -
  that is how `Program.cs` ends for the design-time host, not an error.
- This session does not enter passwords or create accounts, test ones included. That stood when asked
  directly and stands for the successor.
