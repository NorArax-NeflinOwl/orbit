---
name: azure-cost-guard
description: Checklist to run before any Azure CLI or portal action that creates, modifies, scales, or deletes resources for Orbit. Use whenever about to run az containerapp update/create, az acr, az group, az monitor, az identity, or any command that is not purely read-only — including "just add a revision", "bump memory", "create a test environment", or "clean up". The subscription is pay-as-you-go — every resource bills real money.
---

# Azure cost guard

The Orbit subscription is pay-as-you-go. There is no prepaid credit: every
resource, revision, and scale-up bills real money for as long as it exists, so a
careless `az containerapp create` or a scaled-up revision keeps costing until
someone notices it. Because of that, mutating commands need the user's explicit
go-ahead, every time.

## Classify the command first

### Read-only — run freely

`az ... show`, `az ... list`, `az containerapp logs show`,
`az containerapp revision list`, `az monitor log-analytics query`,
`az acr repository list/show-tags`, `az identity federated-credential list`,
`az account show`, `gh run list/view`.

### Mutating — ask before running

- `az containerapp update` (new revision; also check `--cpu` / `--memory`,
  `--min-replicas`, `--max-replicas` — never raise these without a stated reason)
- `az containerapp create`, `az containerapp env create`
- `az acr create`, `az acr update --sku`, anything ACR Tasks (`az acr build`,
  `az acr task` — deliberately unused in this project, see `ci-pipeline`)
- `az group create/delete`
- `az monitor app-insights component create`
- `az identity create`, federated credential create/update/delete
- `az role assignment create`
- Anything with `delete` or `purge`

## What to tell the user before a mutating command

```
Command: <exact command>
Effect: <new revision / new resource / changed SKU / deletion>
Cost impact: <none expected / small (new revision) / recurring (new resource, higher CPU-memory)>
Reversible: <yes, how / no>
Why needed: <one sentence tied to the current task>
```

Then wait for a yes. A "do whatever it takes" from earlier in the session does
not carry over — permission is per command.

## Never do on this subscription

- Create a second Container Apps environment "for testing".
- Scale `min-replicas` above 1, or set CPU above 0.5 / memory above 1Gi,
  without the user explicitly asking for it.
- Create additional registries, workspaces, or Application Insights components.
  The existing ones are: `orbitcontainerregistry`, `ws-82ca0ad1-polandcent`,
  `appinsights-orbit`.
- Delete anything to "start clean". Re-creation costs time and money.
- Enable higher-priced SKUs or features (ACR Premium, dedicated workload
  profiles, zone redundancy) — they add recurring cost.

## Cheap alternatives to try first

- Config-only change on the app: `az containerapp update --set-env-vars` still
  creates a revision, but no new image — prefer it over a full pipeline run for
  configuration experiments.
- Reproduce with `docker compose` locally (see `local-dev`) before touching Azure.
- Read logs from Log Analytics instead of redeploying to "see the error again".
