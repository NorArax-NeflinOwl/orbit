---
name: pr-workflow
description: Branching, commit message and pull request rules for the Orbit repository. Use whenever creating a branch, committing, opening or updating a PR, or when the user says "commit this", "open a PR", "push", "ready for review", or asks to start a new task while a PR is already open.
---

# PR workflow for Orbit

Merging to `main` runs the full build-push-deploy pipeline, which costs real money
on the pay-as-you-go subscription. Every limit below is protecting that.

## Where a pull request goes

**`Coding`, never `main`.** Work lands on `Coding` first; a merge there costs a
build and deploys nothing. `Coding` reaches `main` through a single integration
pull request that `.github/workflows/integration-pr.yml` opens and rewrites on
every push, so many merges arrive at production as one - which is the saving.

Nobody opens or merges that integration PR by hand: the workflow keeps it current,
and merging it is the user's decision because it is the expensive one. When the two
branches agree again, the workflow closes it.

`Coding` is the repository's default branch, so `gh pr create` already proposes it.
A PR aimed at `main` from any other branch is closed by `guard-main.yml` with a
comment saying how to retarget it; a change that genuinely has to skip `Coding`
carries the `hotfix` label, which says the deploy was a decision. That guard exists
because branch protection does not: GitHub gates it behind Pro for private
repositories, so nothing can stop a *direct push* to `main` - which is why rule 2
still says never to do it.

## How many pull requests may be open

- **Three at once, and the integration PR is one of them.** `Coding` → `main` is
  always open while the branches differ, so two slots are left for work.
- **A session uses the PR it opened.** Everything it does afterwards goes on that
  branch - web, phone or documentation alike - and the description is extended or
  rewritten each time, so the PR still documents everything it carries. Splitting by
  subject buys nothing and spends a slot.
- **Merged or closed frees the slot.** A session whose PR is gone may open another,
  if the cap allows.
- **A full cap is never a reason to stall.** Push to an open PR this session did not
  open, after telling that session (`ListAgents`, then `SendMessage`). Several
  sessions on one branch is normal, not an exception.

## Before starting a task

```bash
gh pr list --state open
```

Then, in order:

- **This session already has one open** → keep using it. Commit onto its branch,
  push, and update the description; do not open a second, and do not ask to.
- **This session's PR is merged or closed, and a slot is free** → open one (below).
- **Three are open** (the integration PR counts) → do not open a fourth and do not
  wait for one. Push to whichever open PR the work belongs with, after telling that
  session - `ListAgents`, then `SendMessage`. Ask the user only if none of them fits.

Do not stack a new branch on top of an unmerged one unless the user explicitly asks.

## Branch

Only when opening a PR - work that joins an existing one uses that PR's branch.

- Start from up-to-date `Coding`: `git fetch origin && git switch -c <branch> origin/Coding`
- Name: `<type>/<short-kebab-description>`, for example
  `fix/orbit-api-startup-port`, `feat/calendar-module-schema`,
  `chore/pipeline-image-tags`. No ticket numbers unless the user gives one.
- Name it for the first thing that goes on it, and leave it at that when later
  work joins: renaming a branch other sessions may be pushing to costs more than
  a name that has aged.

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
gh pr create --base Coding --title "<summary line>" --body-file <body.md>
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

The title opens with this session's tag - `[Web]`, `[Android]`, `[DB]`, `[Docs]` -
so a glance at the list says which session is behind which PR.

A session's PR accumulates, so treat the description as living: when later work
lands on it, extend **What** and **Verified** rather than leaving them describing
only the first commit (`gh pr edit <n> --body-file <body.md>`). A reviewer reads
the description, not the reflog.

## Sharing a branch with another session

A shared branch is the normal case now, so treat it as somebody else's too:

```bash
git pull --rebase origin <branch>     # before every push
```

Never force-push a branch another session is on - `--force-with-lease` included,
since it protects your own view, not their unpushed work. Say what you are about
to push before you push it, and if a push is rejected, pull and rebase rather than
overwriting. Which sessions are live is what `ListAgents` answers.

## After opening

- Do not merge. The user merges.
- Later work in this session goes on this same PR, whatever it touches.
- If CI fails on the PR, fix it in the same branch — do not open a new PR.
- When the user merges into `Coding`, nothing deploys - the suite runs and the
  integration PR updates itself. Watch the deploy only when the user merges *that*
  one into `main`: `gh run watch`, and on failure switch to `azure-deploy-diagnose`.
