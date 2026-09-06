# Session handover: orbit-web-3

Previous session: the one that built PRs #210, #216 and #218 (unnamed; worktree
`inventory-tasklist-items-b4d18f`)
Date: 2026-09-05

## Branch and PR

- Branch: `docs/session-handover-orbit-web-3`, holding only this file.
- Open PR: **none inherited.** Everything this session opened is merged — #210, #213, #214, #216, #218 —
  and `Coding` and `main` agree, so the integration workflow has closed its own PR too. The new session
  may open one of its own (see `pr-workflow` for the cap).
- Uncommitted changes: none.

## Goal of the work

Four separate asks, in order: inventory items on a task list; finishing a deploy that had been lost;
saying which dashboard row a notification is about; and opening a shared pin in the phone's map app from
the mobile web.

## Done

**Inventory items on a task list (PR #210).** An Inventory entry now carries the whole product form
(`TaskItemProduct` — amounts, unit, product type, categories, expiry, "check every round") while it has
no shelf item; pointing it at one hands the answer over and drops the description, so there is never a
second copy to go stale. "Generate inventory" opens a form (`GenerateInventoryOverlay`) asking the
storage's name and how its "Restock supplies" list should behave, and the shelf is then built from what
the entries say rather than from their names alone, with each entry ending up linked to the row it asked
for. Two new restock settings came with it: `OnlyCheckedRegularly` and `ReminderChannel`. Server + web
only; the phone was deliberately left at parity zero.

**The lost deploy (no PR — done by hand, with the user's approval).** The push to `main` for the merge
of #204 failed at `Deploy orbit-api` with `ContainerAppOperationInProgress`; `Deploy orbit-web` and the
health check were skipped behind it, so both apps sat on the previous image. Finished by two
`az containerapp update` calls onto the images that run had already pushed — no rebuild, no runner
minutes — and both revisions came up Healthy.

**The auto-retry that never retried (PR #213).** `gh run rerun` with no `-R` and no checkout dies on
"failed to determine base repo", which is why the deploy above was never retried automatically. One
flag.

**A start screen (PR #214).** `#app` used to hold the bare word "Loading…" in the top-left. It now holds
the Orbit mark, the name and a spinner, centred. `ci/verify-app-boots.mjs` was changed in the same
commit: it used to decide the app had booted by the text in `#app` no longer being exactly "Loading…",
which any nicer loading screen would satisfy — it now waits for `.app-boot` to be gone.

**Saying which row the bell means (PR #216).** A dashboard card said "something happened here" over six
rows and left the reader to open all six. The row a notification is about is now outlined in red
(`.row-unseen`), matched from the notification's own `Url` via `NotificationFeedState.HasNewsAbout`; the
Tasks card gained the unseen mark it never had; a contact with messages waiting is marked the same way.

**A shared pin in the map app, on the mobile web (PR #218).** PR #217 (another session) gave this to the
phone app; the web did not have it. Every "Sharing with you" row on `/map` now carries Open in Maps
(`geo:` everywhere, `maps://` on iOS), and on a phone pressing the row itself does the same — but only
when Orbit's own map cannot answer (`MapAppHandoff`): tiles withheld, or Orbit never told where the
reader is.

**Verified:** `dotnet test Orbit.sln` green at every step (3021 at the end: Api 1097, Web 777, Mobile
1147). PR #210 was also checked live end to end against a real Postgres. The deployed site was checked
in a browser: Blazor boots, `/api/config/client-flags` returns 200 through the nginx proxy, no console
errors.

## Still failing / unknown

- **Why two provisioning operations overlapped on `orbit-api`** on 2026-09-04 is not answered. A revision
  carrying the *previous* image was created six seconds after the failure, so something else was updating
  the app at that moment. Ruled out: it was not the retry workflow (that had already died on its own
  error), and not a second run of `main_orbit.yml`.
- `docs/sessions/orbit-web-2.md` is still under `docs/` although the skill now writes handovers to
  `info/sessions/`. Left where it is; moving it is a one-line change nobody has asked for.

## Rejected approaches (do not retry)

- **Re-running the failed deploy job on GitHub to finish a lost deploy.** The `deploy` job builds and
  pushes the images too, so `gh run rerun --failed` repeats ~6 minutes of a 2000-minute monthly budget to
  produce images that are already in ACR. Two `az containerapp update` calls do it for nothing.
- **Editing the integration PR's description by hand.** `integration-pr.yml` rewrites it on every push to
  `Coding`, so anything written there is transient.
- **`@bind` on a `<select>` whose field is a `bool`.** It keeps the default whatever is chosen. Bind
  `value=` and `@onchange` by hand.
- **Scaffolding one more EF migration after merging a branch that reshaped an entity.** The merge can drop
  your own properties from `OrbitDbContextModelSnapshot.cs`, and the new migration then adds the columns a
  second time. Regenerate the branch's migrations against the merged snapshot instead.

## Next step

Nothing is in flight, so ask the user which of the open items to take. If they have no preference, the
largest gap is the phone's parity for PR #210 — an Inventory entry on a list with no storage still names
a thing and nothing else there, and "Generate inventory" still posts an empty body. It is written up in
`info/future-plan.md` under "Smaller identified follow-ups". The other known one is smaller: only the
Tasks and Recent chats cards mark their rows from notifications; Notes, Inventory, Groups and Upcoming
can carry notifications too and `HasNewsAbout` is generic, so each is a one-line change.

## Environment facts confirmed this session

- Deployed image on both apps: `b6ac93c5f3d25a710290c91bb8a7c97bbeb2334a` = `main`'s tip. `orbit-api`
  revision `0000197`+ and `orbit-web` `0000162`+ Healthy; site answers at
  `https://orbit-web.victorioustree-36ad82ca.polandcentral.azurecontainerapps.io`.
- **A working request, not a green revision, is the check.** `/` is 200 and `/api/tasks` through nginx is
  **401** — that 401 is the proof the proxy reaches the API. `/api/health` is 404 on this build and is not
  a symptom; the live health endpoint is `/health/live`.
- The local dev Postgres is shared across worktrees and drifts. A live check failing with
  `42703 column ... does not exist` is drift, not the change: run the API from the worktree on its own
  port against a fresh database (`create database orbit_<name>_check`). Doing so **writes that branch's
  migrations into whichever database it points at** — if the migrations are later regenerated, undo that
  first, or the next local run tries to add columns that already exist.
- The Browser pane works here: it rendered the deployed app at 800x450 and clicks by coordinate landed.
  An older handover says the pane reports `innerWidth`/`innerHeight` of 0 — that is no longer true.
- A static harness plus `python3 -m http.server` in the scratchpad is enough to check CSS and plain JS
  against the real `app.css` without a login: both `.row-unseen` and `mapApp.js`'s two URL shapes were
  checked that way.
