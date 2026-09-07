# Session handover: orbit-web-5

Previous session: orbit-web-4's successor (worktree `orbit-web-improvements-04af57`)
Date: 2026-09-07

## Branch and PR

- Branch: `fix/one-meaning-for-pressing-an-entry`, rebased onto `origin/Coding` twice as the user
  merged out from under it.
- Open PR: **none of this session's own.** #253 and #256 were both opened by it and both merged. Only
  the integration PR (#251) is open, so two slots are free and the new session may open one.
- Uncommitted changes: none. **One commit is unmerged and unproposed**: `Give a private list's entries
  ids of their own`, sitting on the branch above with no PR. See "Next step" - it fixes a defect that is
  on `Coding` right now.

## Goal of the work

Items from `info/future-plan.md`, taken in order, on the browser client. Then - after the first PR
merged - a review of that PR's own diff, which is what found the last two defects.

## Done

**One meaning for pressing a task entry (#253).** Pressing an entry meant three different things by
page: the checklist opened the list's *form* on that entry; the calendar forked on whether the entry had
a place (opening the entry, or the **list** it sits on - a different object, chosen by a field no card
shows); the dashboard's Upcoming named an entry and opened its list. All four surfaces open
`/tasks/{listId}/items/{itemId}` now, with the form a named press further in. `CheckRow` grew
`OnTitlePressed` for the flat checklist, whose rows were `<label>`s - so pressing what an entry said
crossed it off there while the same words on the grouped view opened it. Gone with the fork:
`DueTaskDto.HasPlace`, `Calendar.GoToTaskList`, and the walk up the tree of group lists it used.

**Coverage for the chat thread (#253).** The last real testing gap in the browser client. Two rules that
are invisible when they break: nothing is polled behind a hidden tab, and the roster is read twice in
ten ticks rather than on every one. Also the two ways the loop must stop, and the page's own explanation
for an account the API will not resolve - which was the *other* entry on the not-covered list. Written
up under [The chat thread](../testing-and-running-locally.md#the-chat-thread).

**A guard on the documents' own links (#253).** Two cross-references in `info/` pointed at **bold
paragraphs** as though bold text made an anchor. It does not; only a heading does.
`DocumentationLinkTests` catches both and caught both before they were fixed.

**A shared thing carries the reader's own pin (#253, #256).** A recipient's pin lived in one browser's
localStorage. It is on the share row now (`NoteShare.IsPinnedByRecipient`,
`TaskListShare.IsPinnedByRecipient`, migration `PinASharedThingForItsRecipient`), stamped onto the DTO's
existing `IsPinned` by the access resolvers - so no DTO field and no endpoint changed, and the phone got
it without a release. It also closed a defect nothing had recorded: the phone sorts straight by
`IsPinned` and had no second answer to prefer, so a thing *its owner* had pinned led the recipient's
list. The phone's own pin control for a shared item went back too (#256).

### The two defects self-review found, after #253 had merged

Both are the same shape: a change made an existing quiet wrongness visible. Both were found by reading
the merged diff, not by anything failing.

1. **The caller's pin was stamped over the stored one** (fixed in #256, on `Coding`). The same access
   resolver feeds the *write* paths - `UpdateNoteCommandHandler` and `UpdateTaskListCommandHandler`
   resolve a thing for its caller and then save it - so a recipient with edit access would have written
   their pin onto the owner's row the next time they changed a word. `IsPinnedForCaller` is a field of
   its own now. `LinkedTaskCompletionResolver` has to carry it across its rebuild, for the reason its own
   comment already gives about the access context; **that rebuild is also why the task-list half looked
   like it worked**, since it carried the overwritten flag through as a persisted field.
2. **A private list's entries had no ids** (fixed, **not merged** - see "Next step"). Everything inside a
   private list's sealed payload was sealed with `Guid.Empty` for its id. Free while nothing addressed an
   entry; the day an entry got a page addressed by id, pressing the third entry of a private list opened
   the first - on the checklist, the calendar and the dashboard alike.

### Verified

- `dotnet test Orbit.CI.slnf` — 3375 passed, 0 failed (Api 1224, Web 943, Mobile 1208). **The suite
  command changed on `Coding` mid-session** from `Orbit.sln` to `Orbit.CI.slnf`.
- `dotnet build Orbit.CI.slnf --configuration Release` — clean, 0 warnings.
- `dotnet build src/Clients/Orbit.Maui -f net10.0-android -c Release` — 0 errors; its 19 warnings are
  pre-existing.
- The pin migration was applied by `dotnet ef database update` against the local PostgreSQL container:
  both columns exist, `not null default false`, recorded in `__EFMigrationsHistory`, and `Down` drops
  them.
- Every new test was checked by removing the behaviour it covers and watching it go red.

## Still failing / unknown

- **Nothing was checked in a signed-in browser or on a device**, for anything this session did. Every
  page it touched is behind a login. The private-list defect in particular cannot be reproduced without
  a signed-in account holding a key.

  Two independent things block an agent from closing this, and both were hit on 2026-09-07 rather than
  assumed. Signing in or creating an account is not something this assistant does - that is the user's
  to type, whoever asks. And **the browser pane refuses to navigate to `localhost` at all**: every form
  of it (`https://localhost:8443`, `http://localhost:8080`, `https://127.0.0.1:8443`) comes back
  "denied or failed" while the pane itself is open. So even a screen needing no login cannot be looked
  at from here.

  What *can* be done from here, and was: the stack was rebuilt from this worktree and started, and the
  whole chain answered - `https://localhost:8443/health` returns the API's own JSON through nginx's
  exact-match `= /health` location, which is rule 10's "a working request" for the proxy. The app shell
  serves 200. Everything past that is client-side routing, so `curl` cannot tell one page from another.
- ~~**`ci/verify-diagrams.mjs` has never been run on this machine.**~~ Run on 2026-09-07 - **16
  diagrams, 0 failed**, the first time from this machine - and it found a defect in itself on the way:
  the fences were matched with `` ```mermaid\n ``, a Windows checkout has CRLF, so it read *nothing*.
  Its own "no diagrams found" guard is what turned that into a message rather than a green run. Fixed
  with `\r?`, and the Docker recipe for a machine with no node is in
  [info/uml/README.md](../uml/README.md).
- `OnChatAnnounced` is uncovered and unreachable from a test: `LiveUpdatesConnection` raises its events
  from inside itself, so the live-connection half of the chat poll's pace (`ConnectedPollInterval`) is
  reasoned about rather than driven.

## Rejected approaches (do not retry)

- **A test that checks every backticked path in `info/` exists.** Measured: 4 of 43 name things that
  *should not* exist - a planned `ci/deploy-safety-gates`, a build output, a planned evaluation file, and
  `tests/Orbit.Maui.Tests` inside the sentence saying it does not exist. Anchors have no such class and
  are what `DocumentationLinkTests` checks instead.
- **A test that checks the code names the documents quote still exist.** Same problem from the other
  end: the documents deliberately name things that were removed ("`GoToTaskList`, now gone").
- **`bUnit`'s `WaitForAssertion` for anything about the chat poll loop.** It re-checks when the component
  renders, and a tick behind a hidden tab renders nothing at all - which is exactly the case being
  tested. `ChatThreadTests` does its own waiting, on the loop's own ticks rather than the clock.
- **Stamping a per-caller value over a stored field.** Defect 1 above. Anything that depends on who is
  asking needs a field of its own, because the resolvers feed the write paths too.
- **`TaskList.SetPinned` for the stamp.** It moves `UpdatedAtUtc`, which is what the phone syncs
  against; `SetPinnedForCaller` exists for that reason.

## Next step

**Open a PR for the one unmerged commit on this branch** (`Give a private list's entries ids of their
own`) against `Coding`, and say in the description that it fixes a defect currently on `Coding`. The
branch is already rebased onto the current `Coding` and the suite passes on that base; the work is done,
only the PR is missing. Both previous PRs were merged while further commits were still being written, so
**check `gh pr view <n> --json state` before assuming a commit went in with its PR.**

After that, `info/future-plan.md`'s remaining web items need a decision rather than more work - the ones
that do not are done. The phone's invitation screen has now been offered to the user twice and passed
over twice.

## Environment facts confirmed this session

- The suite is `dotnet test Orbit.CI.slnf` now, not `Orbit.sln`: the solution carries `Orbit.Maui` so
  Visual Studio can open it, and the filter drops it. `Orbit.Maui` still has to be built separately.
- Local PostgreSQL is already running on this machine as container `orbit-postgres`, with
  `POSTGRES_USER=orbit` and `POSTGRES_DB=orbit` (read from the container's own environment; `psql -U
  postgres` fails, that role does not exist).
- `dotnet ef migrations add` and `database update` both work from this worktree; the
  `HostAbortedException` they finish with is normal.
- **There is no `node` on this machine**, but Docker is enough for `ci/verify-diagrams.mjs` - see
  [info/uml/README.md](../uml/README.md) for the one-liner, which installs nothing and leaves nothing
  behind. The two *browser* verifiers still need a real node with Playwright.
- **The local stack runs from this worktree** like this, and was left running on 2026-09-07:
  `docker compose -p orbit -f <worktree>/docker-compose.yml --env-file E:\Git\orbit\.env up -d --build
  postgres orbit-api orbit-web`. The `-p orbit` and the main checkout's `.env` are both required, and
  building from the worktree's own compose file is what makes the image carry *this branch's* code
  rather than the main checkout's. It answers on **https://localhost:8443** with a self-signed
  certificate; 8080 is plain HTTP and redirects there.
- `docker ps` prints nothing through the Bash tool here but works through PowerShell.
- Line endings are CRLF here: a `perl -0pi -e` substitution written with `\n` silently matches nothing.
  Several edits looked applied and were not. Use the Edit tool, or `sed` line by line.
