#!/bin/sh
# Fails (exit 1) when the given branch already has a merged pull request and no open one. Such a
# branch is finished: main will never pull from it again, so any commit added to it is silently
# stranded (this exact mistake shipped a broken migration once - a fix was pushed to a branch
# minutes after its PR merged and never reached main). New work belongs on a fresh branch with its
# own pull request.
#
# Called by the pre-commit and pre-push hooks in this directory. Enable them once per clone with:
#   git config core.hooksPath .githooks
#
# The check needs the GitHub CLI and network access; when either is unavailable it allows the
# operation rather than blocking all offline work.
branch="$1"

[ -z "$branch" ] && exit 0                 # detached HEAD (rebase, bisect) - nothing to check
[ "$branch" = "main" ] && exit 0           # main is what gets merged into, never a PR head here

command -v gh >/dev/null 2>&1 || exit 0

merged_pull_request=$(gh pr list --head "$branch" --state merged --json number --jq '.[0].number' 2>/dev/null) || exit 0
[ -z "$merged_pull_request" ] && exit 0

open_pull_request=$(gh pr list --head "$branch" --state open --json number --jq '.[0].number' 2>/dev/null) || exit 0
[ -n "$open_pull_request" ] && exit 0

cat >&2 <<MESSAGE
error: branch '$branch' was already merged via pull request #$merged_pull_request.
Commits added to it now reach nothing. Start a fresh branch from Coding, which is where work
lands - see .claude/CLAUDE.md rule 2:

  git checkout -b <new-branch-name> origin/Coding

(To bypass this check deliberately, re-run with --no-verify.)
MESSAGE
exit 1
