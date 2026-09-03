---
name: pr-workflow
description: Branching, commit message and pull request rules for the Orbit repository. Use whenever creating a branch, committing, opening or updating a PR, or when the user says "commit this", "open a PR", "push", "ready for review", or asks to start a new task while a PR is already open.
---

# PR workflow for Orbit

Merging to `main` runs the full build-push-deploy pipeline, which costs real
money on the pay-as-you-go subscription. The rules below exist to keep that
pipeline from running more than once per logical change.

## Before starting a task

```bash
gh pr list --state open
```

If a PR is open: finish it (address review, fix CI) or ask the user whether to
abandon it. Do not open a second one. Do not stack a new branch on top of an
unmerged one unless the user explicitly asks.

## Branch

- Start from up-to-date `main`: `git fetch origin && git switch -c <branch> origin/main`
- Name: `<type>/<short-kebab-description>`, for example
  `fix/orbit-api-startup-port`, `feat/calendar-module-schema`,
  `chore/pipeline-image-tags`. No ticket numbers unless the user gives one.

## Commits

- English, imperative mood, short summary line (≤ 72 chars), optional body
  explaining *why* — the diff already shows *what*.
- One logical change per commit. Do not mix a fix with formatting or unrelated
  cleanup.

**Example 1**
Input: changed Kestrel to listen on 8080 in the container so ingress can reach it
Output:
```
Bind Kestrel to port 8080 in container

Container Apps ingress targets 8080; the default 5000 was unreachable and the
revision reported unhealthy.
```

**Example 2**
Input: added the App Insights connection string to .env.example
Output:
```
Add APPLICATIONINSIGHTS_CONNECTION_STRING to .env.example
```

## Pre-commit checklist

- No secrets in the diff (`git diff --cached | grep -i -E "connectionstring|instrumentationkey|password|token|secret"` should only show variable *names*).
- New environment variable → added to `.env.example` with a placeholder.
- `dotnet build` passes; relevant tests run and reported.
- No changes outside the task's scope. If you touched something incidental, revert it.

## Opening the PR

```bash
gh pr create --base main --title "<summary line>" --body-file <body.md>
```

PR body template:

```markdown
## What
<one or two sentences>

## Why
<the problem this solves, link to logs or handover doc if relevant>

## Verified
- <command and result, e.g. "dotnet test Orbit.Api.Tests — 42 passed">
- <local docker compose check, if applicable>

## Environment / config changes
- <new env vars, secrets to set on the Container App, or "none">

## Out of scope / noticed but not fixed
- <anything spotted outside the task>
```

Open as a draft (`--draft`) if CI has not been checked locally yet.

## After opening

- Do not merge. The user merges.
- If CI fails on the PR, fix it in the same branch — do not open a new PR.
- When the user merges, watch the deploy with `gh run watch` and, on failure,
  switch to `azure-deploy-diagnose`.
