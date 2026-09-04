# Orbit — Claude Code instructions

Orbit is a cross-platform productivity application (notes/tasks, calendar sync,
encrypted messaging, location sharing). Repository: `NorArax-NeflinOwl/orbit`.

Stack (.NET 10): ASP.NET Core backend (`src/Server/Orbit.Api`, port 8080) with
PostgreSQL via EF Core (`src/Server/Orbit.Data`), Blazor WebAssembly frontend
(`src/Clients/Orbit.Web`, served by nginx, port 80), .NET MAUI mobile client
(`src/Clients/Orbit.Maui` with shared logic in `src/Clients/Orbit.Mobile`),
shared projects in `src/Shared` (`Orbit.Contracts`, `Orbit.Core`,
`Orbit.Localization`), deployed to Azure Container Apps via GitHub Actions.

Rules in this file are always in context. Longer procedures live in
`.claude/skills/` and load on demand — consult them instead of improvising.

## Language

- Chat responses to the user: Polish.
- Everything in the repository (code, identifiers, comments, commit messages,
  documentation, log and error messages): English.
- Do not modify existing comments, log messages, or strings unless the task requires it.

## Workflow

1. **Three pull requests may be open at once, and one of them is the integration PR**
   (`Coding` → `main`, opened by a workflow) - so two are left for work. A session
   that opened one keeps using it: further work goes on that branch, and its
   description is extended or rewritten so the PR still documents everything it
   carries. Once that PR is merged or closed, the session may open another if the cap
   allows. When the cap is reached, a session does not stall waiting for a slot - it
   pushes to an open PR it did not open, telling that session first. Several sessions
   sharing one PR is normal. See skill `pr-workflow`.
2. Never push directly to `main`, and never open a pull request against it. Work on
   a feature branch and open the PR against `Coding`, which is where everything
   lands first. `Coding` reaches `main` through one integration PR that
   `.github/workflows/integration-pr.yml` keeps open on its own - merging *that* is
   what deploys, so it is deliberately rare. A PR aimed at `main` from anywhere else
   is closed automatically by `.github/workflows/guard-main.yml` (label it `hotfix`
   to mean it on purpose). Branch protection would say this more firmly, but GitHub
   gates it behind Pro for private repositories. Do not merge any PR yourself; the
   user merges.
3. Before the context fills up for the second time, start a new session and hand the
   context over - see skill `session-handover` for the template. The successor
   inherits this session's open PR rather than opening its own.
4. One logical change per commit. A session's PR usually carries several, so the
   commits - not the PR - are where that separation lives.
5. **Runner minutes are a hard budget: 2000 a month, and a badly triggered pipeline
   spent them in four days once.** Before adding a workflow trigger, work out how many
   times one change would run the suite - a feature PR, the push that merges it and
   the integration PR it synchronises are three chances to test the same tree. Keep
   one per stage. `paths-ignore` documentation out of every trigger, and never add a
   trigger "to be safe". See skill `ci-pipeline`.

## Azure constraints (pay-as-you-go subscription)

6. Do not create, scale up, or delete Azure resources without asking first.
   The subscription is pay-as-you-go: every resource and every scale-up bills
   real money for as long as it exists. See skill `azure-cost-guard`.
7. Existing resource names (do not rename or duplicate):
   - resource group `Orbit`, region `polandcentral`
   - Container Apps environment `orbit-environment`
     (domain suffix `victorioustree-36ad82ca`)
   - Container Apps `orbit-api` (port 8080) and `orbit-web` (nginx, port 80)
   - ACR `orbitcontainerregistry` (all lowercase)
   - PostgreSQL Flexible Server `orbit-postgres-<random suffix>` (check the actual
     name with `az postgres flexible-server list -g Orbit -o table`)
   - Storage account `orbitdownloads`, container `apps` (the Android APK)
   - Application Insights `appinsights-orbit`
   - Log Analytics workspace `ws-82ca0ad1-polandcent`
   - Managed identity `identity-orbit` (OIDC for GitHub Actions)
8. GitHub Actions OIDC uses the immutable-subject federated credential format
   (`repo:<org>@<orgId>/<repo>@<repoId>:ref:refs/heads/main`). If Azure login
   fails in CI, check the federated credential subject before touching anything else.

## Deployment

9. When a Container App fails to start or the API stops responding, diagnose
   first, change second: follow skill `azure-deploy-diagnose` and report the
   root cause before applying any fix.
10. Validate end to end after deploy: frontend → nginx `/api/` proxy → `orbit-api`
    internal FQDN. A green pipeline is not "done"; a working request is.

## Code rules

11. Secrets and connection strings (including
    `APPLICATIONINSIGHTS_CONNECTION_STRING`) only via environment variables or
    Container Apps secrets. Never in `appsettings*.json`, `docker-compose.yml`,
    Dockerfiles, or workflow files. Keep `.env.example` in sync when adding a variable.
12. `docker-compose.yml` (`postgres`, `aspire-dashboard`, local nginx TLS) is
    dev-only. Do not port that topology to Container Apps. See skill `local-dev`.
13. Telemetry: Azure Monitor exporter when the connection string is set, OTLP/Aspire
    fallback locally. Keep that conditional intact. See skill `telemetry`.
14. Touch only what the task requires. Work you notice on the way is **written down,
    not done**: add it to the matching section of `info/future-plan.md` in the same
    change, and say in the report that you did. The exception is a defect - a bug, an
    error, a broken build, anything already not working - which is fixed when found,
    or flagged immediately if fixing it is out of proportion.
15. Run the relevant test suite before declaring a task done and report the result.

## Documentation upkeep

16. Keep `info/` current as you work: when a change makes a statement in an
    `info/` document stale (architecture, functionality, setup, plans, status),
    update that document as part of the same change — do not leave it for later.
    New non-obvious knowledge (a gotcha, a procedure) belongs in the matching
    `info/` file, not only in the conversation.
17. `PRZENOSINY.local.md` (repo root, gitignored, in Polish) is the user's
    machine-migration checklist of every gitignored file needed to work. Whenever
    a change introduces, moves, or renames such a file (secrets, machine-local
    config, certificates, keystores, user-secrets) or adds a `.gitignore` entry
    covering one, update that checklist — and `secrets/README.md` when the file
    lives in `secrets/` — so a machine change cannot silently lose it. If the
    checklist file is missing on this machine, recreate it before relying on it.
