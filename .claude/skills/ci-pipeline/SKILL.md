---
name: ci-pipeline
description: How Orbit's GitHub Actions pipeline (main_orbit.yml) builds Docker images, pushes them to ACR and deploys to Azure Container Apps, including the cost constraints and OIDC federated credential setup. Use whenever editing .github/workflows, when a pipeline run fails, when Azure login in CI fails, when the user mentions "actions", "workflow", "ACR", "docker push", "OIDC", "federated credential", or asks why the build does not use az acr build.
---

# Orbit CI/CD pipeline

## What the pipeline does

## The minute budget comes first

2000 runner minutes a month, and a pipeline triggered three times per change spent
them in four days. Measured afterwards: `main_orbit.yml` was 346 of 408 minutes
across 100 runs. Before adding or widening a trigger, count how many times one
change would run the same suite.

`main_orbit.yml` therefore has exactly two triggers, one per stage:

| Trigger | What it proves |
| --- | --- |
| `pull_request` into `Coding` | the tree as it will be once merged - what review needs |
| `push` to `main` | the last gate before Azure is paid; the deploy job hangs off it |

Both carry `paths-ignore` for `info/**` and `**/*.md`. What this gives up: two
branches, each green against `Coding`, are not tested *together* until the push to
`main` - caught there, before anything deploys, rather than twice per merge.

The deploy job is gated on `main` alone (`github.ref == 'refs/heads/main'`), so a
merge into `Coding` costs a build and only the integration PR reaching `main` costs
a deploy. On a push to `main` it:

1. Log in to Azure via OIDC (managed identity `identity-orbit`, no stored secret)
2. `docker build` the `Orbit.Api` and `Orbit.Web` images on the runner
3. `docker push` them to `orbitcontainerregistry.azurecr.io`
4. Update the `orbit-api` and `orbit-web` Container Apps to the new image tag

Every run costs real money on the pay-as-you-go subscription (ACR storage, new
revisions). That is why `main` receives only merged PRs and never direct pushes.

## Hard constraints

### The build stays on the runner

ACR Tasks (`az acr build`) were blocked on the old free-trial subscription,
which is why the pipeline builds on the runner and pushes the image. The
subscription is now pay-as-you-go, but the runner build remains the established,
verified path. If a pipeline error looks like it could be "solved" by moving the
build into ACR, that is a topology change, not a fix — find the actual cause
instead, and propose switching to ACR Tasks only as its own task with the
user's approval.

### OIDC federated credential subject format

GitHub migrated this repo to the immutable-subject format. The Azure federated
credential on `identity-orbit` must match exactly:

```
repo:NorArax-NeflinOwl@29899734/orbit@1331906032:ref:refs/heads/main
```

Symptoms of a mismatch: `AADSTS70021: No matching federated identity record found`
or `Error: Login failed` in the `azure/login` step. Fix on the Azure side:

```bash
az identity federated-credential list -g Orbit --identity-name identity-orbit -o table
```

and compare the `subject` with the value above. Do not create a second
credential; update the existing one.

Required GitHub repository secrets (already configured, never echo their values):
`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

## Inspecting runs without triggering them

```bash
gh run list --workflow main_orbit.yml --limit 10
gh run view <run-id> --log-failed
gh run view <run-id> --log | grep -i -E "error|denied|unauthorized" | head -50
```

Never re-run or trigger the workflow just to "see if it works". Reproduce build
problems locally with `docker build` first (see `local-dev`).

## Editing the workflow

- Keep the runner-side `docker build` / `docker push` steps.
- Image tags: use the commit SHA, not `latest`, so a revision can be traced to a commit.
- No secrets inline; only `${{ secrets.* }}` references.
- If a change to the workflow is needed, it goes through a PR like any other
  change. Explain in the PR description what the change does and why it is
  expected to work on this subscription.
- After the workflow is edited, the first run after merge is the test. Watch it
  with `gh run watch` and, if it fails, follow `azure-deploy-diagnose` — do not
  push a second speculative fix.
