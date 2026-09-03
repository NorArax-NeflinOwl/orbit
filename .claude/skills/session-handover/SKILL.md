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

## Naming

The new session keeps the current session name and increments the trailing
counter:

```
orbit-deploy      → orbit-deploy-2
orbit-deploy-2    → orbit-deploy-3
orbit-messaging-1 → orbit-messaging-2
```

If the current session has no counter, add `-2`.

## Where the handover lives

Write it to `docs/sessions/<new-session-name>.md` and commit it on the current
feature branch (it is documentation, in English). If there is no branch
(session was read-only), write it to the file and tell the user to commit it.

## Handover template

Fill every section; write "none" rather than leaving a section out.

```markdown
# Session handover: <new-session-name>

Previous session: <current-session-name>
Date: <YYYY-MM-DD>

## Branch and PR
- Branch: <name>
- Open PR: <number and title, or none>
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
Read .claude/CLAUDE.md and docs/sessions/<new-session-name>.md, then continue
from "Next step".
```

Do not repeat the diagnosis that the handover already contains — the "Rejected
approaches" section exists precisely to prevent that.
