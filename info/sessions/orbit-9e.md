# Session handover: the session after orbit-9e

Previous session: orbit-9e (remote, session_01E6YVEpFWRp1pre7J7RQzyU)
Date: 2026-09-15

## Branch and PR
- Branch: `feat/future-plan-leftovers-without-sdk-or-android` - this session's work was merged into it
  at the user's request (`d70b55f`, `34b0583`, `eeadeea`). Its own branch, `claude/azure-cost-limits-5a2ljd`,
  is wholly contained in that one now.
- Open PR: #288 "[Web+Phone] Folders for events and inventories; a note's content model ...", a draft;
  its description's "Batch 8" is this session's part. #289 (this session's own, against `Coding`) is
  contained in #288 and can be closed. The new session inherits #288 rather than opening its own - see
  `pr-workflow`.
- Uncommitted changes: none.

## Goal of the work
Bound the subscription's bill (a 10 € warning, a 20 € ceiling, three mails that carry the commands to
run), then make the phone live through the stop those limits make routine, plus a security plan costed
against the ceiling. Everything was written; nothing was created in Azure - the mutating commands are
the owner's to run under rule 6.

## Done
- **Cost limits** - `info/azure-setup.md` "Cost limits": currency check, what the deployment costs, the
  action group, the budget (`az rest`, amount 20, thresholds 50% and 100% actual, 100% forecast to a
  plain address), the free-tier Automation account `orbit-automation` with runbook
  `scripts/send-cost-instruction-mail.ps1` (Reader only; mails at 10 €, at 20 € the five stop commands
  with the real server name, and on the last day of a month in which anything is still stopped the
  resume commands - daily schedule, the runbook decides whether tomorrow is the 1st).
  `scripts/stop-azure-compute.sh` stops the PostgreSQL server and empties both Container Apps
  (`--resume`, `--dry-run`, `--status`); it now writes `status.json` to `orbitdownloads`/`apps` before
  stopping and deletes it after resuming. `scripts/test-stop-azure-compute.sh` 33/33.
- **The phone through a paused server** - `info/orbit-maui-plan.md` §15 (analysis) and §15.6 (built).
  The API stamps every answer `Orbit-Api: <version>` (`AnswerHeader`, first in the pipeline; name in
  `Orbit.Contracts.OrbitAnswerHeader`). `OrbitAnswerHandler` is the innermost handler on every Orbit
  client (22 typed clients, the refresh client, the version gate) and replaces any unstamped answer
  with `AnswerNotFromOrbitException` (empty `StatusCode` on purpose - `SyncFailure` calls it worth
  retrying and unanswered, the outbox neither drops nor counts it, `TokenRefreshService` never sees a
  refusal). `PauseNoticeReader` reads the stop script's file only after such an answer;
  `ServerReachability` is the registered `INetworkStatus` (online = network and no pause; ten-minute
  recheck; Orbit answering ends the pause), `SyncCondition.Paused`, the corner says "Orbit is paused",
  `LiveUpdatesConnection` neither opens while paused nor keeps reconnecting into one.
  `OrbitPauseNoticeSettings` bakes the file's address (`-p:OrbitPauseNoticeAddress`, derived in
  `android-release.yml` from the APK's storage variables; absent = never paused, still safe).
- **Security** - `info/azure-security.md` (free hardening; Defender plans priced at ~45-60 €/month
  against a 20 € ceiling; what stays as it is and why), `scripts/audit-azure-security.sh` (read-only,
  22/22 tests). The recorded `ConnectionStrings__Orbit` corrected from `Trust Server Certificate=true`
  to `Ssl Mode=VerifyFull` in the docs; the repair for the running deployment is written out there.
- **Defects fixed on the way** - `TaskListChecklist.razor` loop variable `section` read as the
  `@section` directive by SDK 10.0.112 (renamed `checklistSection`); on #288's branch, `NotePicture.cs`
  missing `using Orbit.Core.Abstractions;` and `UpdateNoteCommandHandlerTests` building the handler
  with two arguments.
- **Verified** with SDK 10.0.112 installed from `packages.microsoft.com` (the apt feed; the direct
  download host is refused by the proxy): on this session's own branch every suite passed (Mobile 1526,
  Api 1457, Web 1362). On the merged #288 head: Api 1486 passed / 1 failed, Mobile 1608 passed / 9
  failed, `Orbit.Web` does not build - all of it #288's own, proven by running that branch alone at
  `4828b15` and `53ab40f` with only the two compile fixes: same results. Diagrams 18/18; links checked.

## Still failing / unknown
- **#288's own state at its head `b9fd0a0`** (the other session's last eight commits, which fixed
  `MapPage.razor`, the deletion sweep, the repeat's folder count and taught `PeriodicSync` to leave a
  paused deployment alone). Measured after fast-forwarding to it, `dotnet build Orbit.CI.slnf` clean:
  - **`Orbit.Mobile.Tests` no longer runs to the end.** The test host crashes with an unhandled
    `SqliteException: no such table: Inventories` - a background task (the periodic sync, most likely)
    still running after a test's `LocalStore` was disposed. Two runs, both aborted, at different points
    (69 and 497 tests in). On `eeadeea`, before those eight commits, the suite ran to completion
    (1608 passed, 9 failed).
  - `Orbit.Api.Tests`: 1494 passed, 2 failed - `LinkedTaskCompletionResolverRebuildTests.The_fixture_sets_every_field_to_something_it_would_not_have_by_default`,
    `LinkedTaskListTreeTests.A_list_that_is_not_a_group_is_the_whole_tree`.
  - `Orbit.Web.Tests`: 1531 passed, 4 failed - two `TaskEditorItemFormTests` about waiting for an
    unsaved entry, `InventoriesTests.A_private_inventory_says_what_that_means_and_cannot_be_shared`,
    `NoteEditorTests.The_styles_are_offered_by_name_under_the_letters_tool`.
  None of it touches this session's code; the earlier list (nine mobile failures, `MapPage`) is in
  #288's description under Batch 8 and is partly closed by those commits.
- **The 404 assumption is untested** (plan §15, item 1): that a stopped Container App's front door
  answers 404 rather than refusing the connection. Everything in §15.6 is safe either way, but the
  claim that the *old* phone signed out within fifteen minutes rests on it.
- The runbook has never run (no PowerShell here); `VerifyFull` has never been tried against the real
  database; the MAUI heads were not built (`Orbit.Maui` is outside `Orbit.CI.slnf`).

## Rejected approaches (do not retry)
- **Treating a 404 as "offline" in `SyncFailure`** - would hide a real 404 from a live Orbit too. The
  header is what tells the two apart; the exception's empty `StatusCode` is what makes every rule do
  the right thing without a special case.
- **Changing `INetworkStatus`'s thirty-three consumers** - unnecessary; registering `ServerReachability`
  as the `INetworkStatus` gives them the pause for free.
- **A monthly Automation schedule with "last day"** - replaced by a daily schedule and the runbook
  deciding whether tomorrow is the 1st, so nothing hangs on how Automation counts month lengths.
- **`az consumption budget create`** - deprecated and never covered subscription scope; `az rest` on
  `Microsoft.Consumption/budgets` instead.
- **Swapping lines 396/397 of `MapPage.razor`** - tried, the imbalance is deeper; restored to #288's
  version untouched.

## Next step
Get `Orbit.Mobile.Tests` running to the end again on #288's head (the test host crash above - look at
what the last eight commits started in the background and whether a test's store outlives it), then
the two API and four web failures, then `dotnet test Orbit.CI.slnf` clean. After that, the day the API is
deployed with `AnswerHeader`: stop the test environment with `scripts/stop-azure-compute.sh` and watch a
signed-in phone for twenty minutes with the diagnostic log open (plan §15, item 1); then items 4-6 of
§15.4 (reminders that ring from the phone, local name suggestions, the sign-in screen while paused).

## Environment facts confirmed this session
- SDK: `dotnet-sdk-10.0` 10.0.112 installs from `https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb`
  + `apt-get install dotnet-sdk-10.0` in the remote container; `builds.dotnet.microsoft.com` and
  `dotnetcli.azureedge.net` are refused by the proxy. Restore against nuget.org works.
- SDK 10.0.112's Razor compiler reads `@section` inside a code block as the directive - a loop variable
  named `section` breaks the build.
- Access tokens live 15 minutes (`JwtSettings.ExpiryMinutes`), refresh tokens 30 days
  (`RefreshTokenService.RefreshTokenLifetime`), the outbox gives up after five *answered* refusals.
- Npgsql 10.0.3: `Ssl Mode=Require` verifies the certificate; `Trust Server Certificate=true` is what
  turns verification off.
- The budget's thresholds are percentages of its amount; 10 of 20 is exactly 50%.
- No `PRZENOSINY.local.md` in the remote clone (gitignored); the SMTP password's new home (the Automation
  credential) still needs a line there, on the user's machine.
