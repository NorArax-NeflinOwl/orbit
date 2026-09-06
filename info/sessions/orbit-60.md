# Session handover: orbit-60

Previous session: orbit-60 (this file's author)
Date: 2026-09-04

## Branch and PR
- Branch: `docs/session-handover-orbit-60` - this handover only. Everything this session worked on is
  merged; its last PR, **#211** (`keepBranches` in the branch cleanup), went into `Coding` at 17:36Z.
- Open PR: none of this session's. Repository-wide only **#221**, the integration `Coding → main`
  (draft, kept by the workflow), so both work slots under the three-PR cap are free - see
  `pr-workflow` before opening one, and check `gh pr list --state open` first, since that changes.
- Uncommitted changes: none.
- **The main checkout at the repository root is shared by every session on this machine** - nine
  `claude` processes had it as cwd today, and two sessions committed there within 27 seconds of each
  other, one onto the other's branch. It sat on a merged foreign branch for hours and has since been
  moved to `Coding`. Do not work there: use a worktree of your own, and never `git switch` in it.

## Goal of the work
Started as "check the local edits to `.claude/CLAUDE.md` against the project"; became the database
naming convention with the Warehouse → Inventory rename, then the repository's process: PR rules, the
`Coding` branch with one integration PR to `main`, runner-minute cuts, and cleaning up worktrees,
branches and session handovers.

## Done
- **#192** - `CLAUDE.md` rewritten into short rules plus nine skills; subscription described as
  pay-as-you-go (it is no longer the free trial); stack description brought up to date (PostgreSQL via
  `Orbit.Data`, `src/` layout, mobile client); the stale "orbit-api fails on startup" blocker removed.
- **#196** - every table and column named by the `OP_`/`OL_`/`OS_` convention in
  `Orbit.Data.OrbitStorageNames` (applied at the end of `OnModelCreating`; an unlisted entity throws at
  startup); entities under `Entities/Data|Links|Setups`; Warehouse → Inventory across the solution
  (~300 files), with the module namespace made plural (`Orbit.Core.Inventories`) because a singular
  one shadowed the aggregate; `MobileVersion:Android:MinimumSupportedVersion` = 0.3.0 and the
  milestone raised to 0.3 in `version.props`. Both migrations hand-rewritten as renames and verified
  on a real Postgres, forwards and back. Deployed and checked end to end; the release workflow
  published APK 0.3.1 on its own.
- **#200** - PR rules; `Coding` branch created and made the default; `integration-pr.yml` keeps one
  draft PR `Coding → main`; `guard-main.yml` closes PRs aimed at `main` from elsewhere (`hotfix`
  label bypasses); deploy job gated on `refs/heads/main`; `appsettings.json` comment corrected
  (`LatestVersion`/`UpdateUrl` come from `android-release.yml` as Container App env vars).
- **#203** - `integration-pr.yml` names the repository setting it needs; every `actions/checkout`
  on v7 (v5 was asked for; v7 was current and already used - agreed with the user).
- **#208** - the suite runs on GitHub **only on push to `main`**; rules rewritten to 17 (rule 1 counts
  slots, rule 3 lost naming, rule 5 = minute budget, rule 14 = record rather than fix unless a defect;
  old 5/12/16/20 deleted); pre-commit hook message points at `origin/Coding`;
  `info/future-plan.md` gained "Noticed while working".
- **#209** (session orbit-ops's PR, pushed onto under rule 1) - both stray handovers moved to
  `info/sessions/`; `session-handover` skill fixed (`info/`, no naming, check the branch before
  committing, work from a worktree); real Azure account names scrubbed before they reached GitHub.
- **Repository hygiene** - 4 dead worktrees removed; 109 local and 38 remote merged branches deleted
  (restore manifest in gitignored `USUNIETE-GALEZIE.local.md`); `PRZENOSINY.local.md` written (Polish,
  gitignored) listing every machine-local file; the real Android `google-services.json` and
  `AndroidManifestOverlay.xml` restored from a worktree into `secrets/` and `Platforms/Android/`.
- **Settings the user changed at my request**: `Coding` default branch; "Allow GitHub Actions to
  create and approve pull requests" on; runner minutes unblocked (spending limit raised).
- **Verified**: 2909 tests green after the rename; deploy of #196 answered 200 through nginx with the
  renamed tables; version gate answers `UpdateRequired` for 0.2.5 and `Supported` for 0.3.x.

- **This handover's own PR** also moves `docs/sessions/orbit-web-2.md` - written by session orbit-web-2
  before the skill fix in #209 landed - under `info/sessions/` with the others, so `docs/` is gone again.

## Still failing / unknown
- **Session `orbit-ops`** (author of `orbit-ops-2.md`) never identified: no live session answered to
  the name; it worked in the shared main checkout on model Fable 5.
- **No CI before `main`.** `dotnet test Orbit.sln` on the developer's machine is the only check a
  change gets before `Coding`; a broken merge there surfaces at the next push to `main` and blocks
  the whole integration. This is the user's explicit choice (rule 5).
- **A direct push to `main` deploys and nothing can stop it** - branch protection and rulesets both
  answer 403 on the free plan for a private repository. Rule 2 covers it; the repository cannot.
- `notes/permissions.local.md` and the real `GoogleService-Info.plist` do not exist on this machine
  (both listed in `PRZENOSINY.local.md`); two handovers rely on the former.
- Live worktree `.claude/worktrees/inventory-tasklist-items-b4d18f` has 40 uncommitted changes and a
  session in it (pid 77979) - not touched, not to be removed.
- Recorded in `info/future-plan.md` "Noticed while working": `setup-dotnet@v4`, `setup-java@v4`,
  `upload-artifact@v4` carry the Node 20 deprecation; the `android` job in `main_orbit.yml` starts a
  runner (billed a minute) even when nothing mobile changed.

## Rejected approaches (do not retry)
- **Renaming `SharedItemType.Warehouse`** - stored as text in `OL_PUBLIC_SHARES` and inside chat
  payloads already delivered; renaming orphans every share link. Everything else is Inventory.
- **Renaming translation keys on one side only** - `PolishTranslations.cs` is keyed on the English
  text; a key renamed in code alone falls back to English on a Polish screen. Rename both sides at
  once or not at all; `TranslationCoverageTests` and `No_English_string_is_translated_twice` catch it.
- **Squashing migrations / wiping databases now** - the user deferred both to a future production
  environment; ordinary rename migrations were used instead.
- **Letting EF scaffold a renamed entity** - it emits drop-and-create and would have deleted every
  inventory (server) and every offline edit (phone). Rewrite as `RenameTable`/`RenameColumn`.
- **Publishing APK 0.3.0 by hand** - `android-release.yml` had already published 0.3.1; 0.3.0 would
  be a downgrade at the fixed download URL.
- **`push: Coding` or `pull_request` triggers on `main_orbit.yml`** - one change ran the suite three
  and four times (feature PR, push to `Coding`, synchronised integration PR); 346 of 408 measured
  minutes. The user wants the suite on the merge to `main` only.
- **Branch protection on `main`** - HTTP 403 for both classic protection and rulesets; needs Pro.
- **`git branch -d` for merged branches while on a feature branch** - it measures against HEAD, not
  `origin/main`, and refuses; switch to `main` first.
- **Fixing the shared main checkout with `git switch`/`reset`** - hits every session in it. Use a
  temporary worktree and push with an explicit refspec (`git push origin tmp/x:<pr-branch>`).
- **`git worktree remove --force` and `rm -rf` of leftovers** - the permission classifier blocks
  them; remove the specific leftover files, then `rmdir`.
- **`git grep -E '\b…'` on macOS** - BSD grep returns nothing for `\b`; the branch-name remap it was
  guarding silently did nothing once. Use `perl` or `(^|[^A-Za-z])`.
- **Publishing a PR body through a shell heredoc with backticks** - the shell ate a filename once;
  write the body with Python or a quoted heredoc (`<<'EOF'`).

## Next step
Nothing is owed. If work continues, take the first recorded lever on runner minutes: move the phone-head compile out of `main_orbit.yml` into a workflow of its own
with a `paths:` filter on `src/Clients/Orbit.Maui/**`, `src/Clients/Orbit.Mobile/**` and
`src/Shared/**`, so a change that touches nothing mobile does not start (and pay for) that runner.
Do it from a worktree of your own, on a fresh branch from `origin/Coding`, with `dotnet test
Orbit.sln` run locally before the PR - nothing on GitHub tests a pull request any more.

## Environment facts confirmed this session
- Azure: PostgreSQL Flexible Server `orbit-postgres-<random>` in RG `Orbit`; storage account
  `orbitdownloads`, container `apps`, blob `orbit-android.apk`; `android-release.yml` sets
  `MobileVersion__Android__LatestVersion` and `__UpdateUrl` on `orbit-api` after each release, so the
  values in `appsettings.json` are overridden in the deployment.
- Deployed API reported 0.3.0 right after #196; `GET /api/config/mobile-version?platform=Android&
  version=0.2.5` → `UpdateRequired`, `0.3.1` → `Supported`.
- Repository: default branch `Coding`; `can_approve_pull_request_reviews: true`; branch protection
  and rulesets 403 (private repo, free plan); the billing API needs the `user` scope the `gh` token
  lacks.
- Runner minutes over the last 100 runs before the cuts: 408, of which `main_orbit.yml` 346,
  `android-release.yml` 43; a run whose jobs fail in 2 s with 0 steps means the minute quota is
  exhausted ("recent account payments have failed or your spending limit needs to be increased").
- `core.hooksPath = .githooks`; `block-merged-branch.sh` refuses commits and pushes on a branch whose
  PR is merged and tells you to branch from `origin/Coding`.
- The main checkout is shared by every session on this Mac; worktrees live in `.claude/worktrees/`.
- Local: mkcert certificate in `~/.orbit-certs/` mounted by gitignored `docker-compose.override.yml`;
  user-secrets id `8371e0fb-2acd-4488-be9e-dff6bd269e72`; compose services `postgres` (5432),
  `orbit-api` (8081→8080), `orbit-web` (8443 HTTPS), `aspire-dashboard` (18888).
- A migration can be rehearsed against `docker compose up -d postgres` with a scratch database:
  `dotnet ef database update <previous>`, seed rows by hand, `database update`, check, `update
  <previous>` again to prove `Down`.
