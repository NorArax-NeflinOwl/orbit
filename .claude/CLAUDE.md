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

1. One PR per session, and at most three open in the repository at once. If this
   session already has a PR open, put the work on its branch instead of opening a
   second one - it makes no difference whether the work touches the web, the phone
   or documentation. Several sessions may share one PR. Merging to `main` triggers
   an expensive pipeline (Docker build, push to ACR, Container Apps deploy), which
   is what the cap protects. See skill `pr-workflow`.
2. Never push directly to `main`. Work on a feature branch and open a PR.
   Do not merge the PR yourself — the user merges it.
3. Before the context fills up for the second time, start a new session and hand
   over the context. Name the new session like the current one with the numeric
   suffix incremented (`orbit-deploy-2` → `orbit-deploy-3`).
   See skill `session-handover` for the handover template.
4. One logical change per commit. A session's PR usually carries several, so the
   commits - not the PR - are where that separation lives.

## Azure constraints (pay-as-you-go subscription)

5. The pipeline builds with `docker build` + `docker push` on the GitHub Actions
   runner (ACR Tasks were blocked on the old free trial). That is the established
   path — do not switch to `az acr build` without asking first. See skill
   `ci-pipeline`.
6. Do not create, scale up, or delete Azure resources without asking first.
   The subscription is pay-as-you-go: every resource and every scale-up bills
   real money for as long as it exists. See skill `azure-cost-guard`.
7. Existing resource names (do not rename or duplicate):
   - resource group `Orbit`, region `polandcentral`
   - Container Apps environment `orbit-environment`
     (domain suffix `victorioustree-36ad82ca`)
   - Container Apps `orbit-api` (port 8080) and `orbit-web` (nginx, port 80)
   - ACR `orbitcontainerregistry` (all lowercase)
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
12. Blazor WASM must never hardcode `localhost` as the API base address. Use the
    configurable base URL (relative `/api/` behind nginx in the cloud).
13. `docker-compose.yml` (`postgres`, `aspire-dashboard`, local nginx TLS) is
    dev-only. Do not port that topology to Container Apps. See skill `local-dev`.
14. Telemetry: Azure Monitor exporter when the connection string is set, OTLP/Aspire
    fallback locally. Keep that conditional intact. See skill `telemetry`.
15. Touch only what the task requires. No refactors, renames, or cleanup outside
    scope; mention issues you notice instead of fixing them silently.
16. Follow naming and structure rules from skill `orbit-conventions` when writing code.
17. Run the relevant test suite before declaring a task done and report the result.

## Documentation upkeep

18. Keep `info/` current as you work: when a change makes a statement in an
    `info/` document stale (architecture, functionality, setup, plans, status),
    update that document as part of the same change — do not leave it for later.
    New non-obvious knowledge (a gotcha, a procedure) belongs in the matching
    `info/` file, not only in the conversation.
19. `PRZENOSINY.local.md` (repo root, gitignored, in Polish) is the user's
    machine-migration checklist of every gitignored file needed to work. Whenever
    a change introduces, moves, or renames such a file (secrets, machine-local
    config, certificates, keystores, user-secrets) or adds a `.gitignore` entry
    covering one, update that checklist — and `secrets/README.md` when the file
    lives in `secrets/` — so a machine change cannot silently lose it. If the
    checklist file is missing on this machine, recreate it before relying on it.

## Reporting

20. At the end of each task report: what changed (files), what was verified
    (commands, results), what remains open.
