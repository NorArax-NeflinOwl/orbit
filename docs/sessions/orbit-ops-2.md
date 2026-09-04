# Session handover: orbit-ops-2

Previous session: orbit-ops
Date: 2026-09-04

## Branch and PR
- Branch: none — every branch this session worked on is merged (most recently
  `chore/one-run-per-stage` via PR #208; this handover rides on its own small branch).
- Open PR: none of this session's own. Repo-wide: #207 (`feat/orbit-web-2026-09-04` → `Coding`,
  another session's) and #204 (the integration PR `Coding` → `main`). One work slot under the
  three-PR cap is free — see `pr-workflow`.
- Uncommitted changes: none.

## Goal of the work
Started as Azure production incident response (502s, SQLite-over-Azure-Files outage), grew into
deploy/CI hardening and operational planning for the repository.

## Done
- Migrated persistence to PostgreSQL (local compose + Azure Flexible Server); removed hardcoded DB
  credentials from `docker-compose.yml`/`appsettings.json` (`POSTGRES_PASSWORD` required via `.env`).
- Added `.githooks/` (`pre-commit`/`pre-push` refuse work on a branch whose PR already merged);
  enabled in this clone via `git config core.hooksPath .githooks`, documented in `info/build.md`.
  Hook messages now point at `origin/Coding` after the workflow change.
- Replaced GitHub's built-in "Automatic dependency submission" with
  `.github/workflows/dependency-submission.yml` (PR #84, merged) because the built-in one restores
  every csproj and always fails on `Orbit.Maui` (no MAUI workloads on Linux runners).
- CI runner-minutes budget work merged (PR #208): the test suite runs on GitHub only on push to
  `main` (integration merge); local `dotnet test Orbit.sln` is the pre-`Coding` gate (rule 5).
- Verified DB naming for ad-hoc SQL: tables `OP_`/`OL_`/`OS_`, columns like `OS_U_ID`,
  `OS_U_USERNAME`, `OP_T_USERID`; identifiers are quoted/case-sensitive; `user` is a reserved word
  in Postgres.
- Agreed (in conversation, not yet implemented) the two-environment Azure plan: the EXISTING
  environment becomes TEST (current auto-deploy pipeline unchanged), a NEW production resource
  group is added with a custom domain, and production installs happen from a release queue —
  `schedule-production-deploy.yml` (`workflow_dispatch`: image tag + date/time, written to
  `deploy/production-schedule.json` as the audit trail) plus a cron workflow (~10 min) that applies
  it when due, reusing the existing health-gate/rollback steps. Same `sha-<commit>` image that ran
  on test; never rebuilt.
- Advised on MAUI push transports: APNs needs the paid Apple Developer Program (no free path for a
  native app); free alternatives are PWA web push (iOS ≥ 16.4, existing VAPID infrastructure works
  as-is) and local notifications in MAUI for pre-known reminders.

## Still failing / unknown
- Unverified whether the user actually disabled the built-in "Automatic dependency submission"
  setting (Settings → Advanced Security; not readable via API without admin scope). If pushes to
  `feat/orbit-maui-phase0` still fail with `NETSDK1147` in a run named "Automatic Dependency
  Submission", it is still on.
- The three decisions blocking Azure resource creation for production: which custom domain, the
  production Postgres SKU (B1ms recommended), and confirmation that production starts with an
  empty database.

## Rejected approaches (do not retry)
- A workflow job that sleeps until the scheduled deploy time — GitHub's 6-hour job limit kills any
  realistic schedule.
- GitHub Environment required-reviewer approval as the production gate — approval is requested when
  the run starts, so the admin would have to click "approve" at the scheduled hour, defeating the
  point. The scheduling workflow itself is the approval act.
- Configuring the built-in dependency submission to skip `Orbit.Maui` — it has no such option;
  that is why it was replaced with the workflow.
- Keeping the current environment as production and building test alongside — the user explicitly
  chose the reverse (current env = test, fresh production), which is simpler and was adopted.
- Adding MAUI workloads to the CI runner to make restores pass — slow, costly in runner minutes,
  and unnecessary: `Orbit.Maui` is deliberately outside `Orbit.sln`.

## Next step
Implement stage 1 of the two-environment plan: parametrize the hardcoded `orbit-api` internal FQDN
in `src/Clients/Orbit.Web/nginx.azure.conf` (environment variable + `envsubst` at container start),
on a fresh branch, PR → `Coding`. It is required regardless of the pending decisions. Do not create
any Azure resources before the user answers the three open decisions (and `azure-cost-guard` says
ask first anyway).

## Environment facts confirmed this session
- `nginx.azure.conf` hardcodes `orbit-api.internal.victorioustree-36ad82ca.polandcentral.azurecontainerapps.io`.
- Container App names only need to be unique per resource group — a new production RG can reuse
  `orbit-api`/`orbit-web`.
- GitGuardian flags `${POSTGRES_PASSWORD:?...}` compose interpolations as "Generic Password" —
  false positive; dismiss with its "Skip: false positive" button, do not change the code.
- `git config core.hooksPath .githooks` is set in this clone and is shared by all its worktrees.
- Postgres 18+ image mounts its volume at `/var/lib/postgresql` (not `.../data`).
- The `Orbit.Maui` dependency graph is still fed: the component-detection action reads
  `PackageReference`s without restoring the project.
