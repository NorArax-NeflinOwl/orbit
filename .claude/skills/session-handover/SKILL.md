---
name: session-handover
description: Procedure and template for handing over work to a new Claude Code session when the context is about to fill up, or when the user asks to "start a new session", "hand over", "save context", "continue in a new session", or asks what the previous session did. Use proactively before the second context compaction, not only when asked.
---

# Session handover

Long debugging sessions on Orbit (deploy, pipeline) exhaust the context. A session
that has been compacted twice loses the early details — which command was tried,
which fix was rejected, which resource names are correct. Starting fresh with a
written handover is cheaper than working with a degraded context.

## When to hand over

- Before the **second** context compaction. After the first compaction, treat the
  next natural checkpoint (a PR opened, a root cause found, a test passing) as the
  moment to hand over.
- Whenever the user asks for it.

## Where the handover lives

Write it to `info/sessions/<session-name>.md` - documentation lives in `info/`, and
this is documentation, in English. Name the file after the session that wrote it
(whatever the session is called; there is no counter convention to follow).

Commit it on this session's own PR branch, or on a fresh branch from
`origin/Coding` if the session has none open - **never on whatever happens to be
checked out.** The main checkout is shared by every session on this machine, and a
`git switch` by one of them moves the ground under the rest: on 2026-09-04 a
handover was committed onto another session's branch that way, seconds after that
session had switched to it. So, immediately before committing:

```bash
git branch --show-current   # is this the branch you mean to commit to?
git status --short          # is anything here that is not yours?
```

If either answer is wrong, do not fix it with a checkout in the shared tree - work
from a worktree of your own (`git worktree add`). If there is no branch (the session
was read-only), write the file and tell the user to commit it.

## Handover template

Fill every section; write "none" rather than leaving a section out.

```markdown
# Session handover: <new-session-name>

Previous session: <current-session-name>
Date: <YYYY-MM-DD>

## Branch and PR
- Branch: <name>
- Open PR: <number and title, or none. The new session inherits it rather than
  opening its own - see `pr-workflow`.>
- Uncommitted changes: <list, or none>

## Goal of the work
<one or two sentences>

## Done
- <what was changed, which files>
- <what was verified and how>

## Still failing / unknown
- <symptom, and what has already been ruled out>

## Rejected approaches (do not retry)
- <approach> — <why it does not work here>

## Next step
<the single next action the new session should take>

## Environment facts confirmed this session
- <any resource name, FQDN, port, tag or setting that was verified>
```

## Starting the new session

The first message of the new session should be:

```
Read .claude/CLAUDE.md and info/sessions/<session-name>.md, then continue
from "Next step".
```

Do not repeat the diagnosis that the handover already contains — the "Rejected
approaches" section exists precisely to prevent that.
