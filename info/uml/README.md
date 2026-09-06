# Orbit in diagrams

Five views of the same system, each answering a question the prose in `info/` answers slowly.

| Document | Answers |
| --- | --- |
| [components.md](components.md) | What is the solution made of, and which way may the arrows point? |
| [domain-model.md](domain-model.md) | What are the things, and what do they have in common? |
| [database.md](database.md) | What is stored, under what names, and how is it joined? |
| [flows.md](flows.md) | What happens in what order, and why that order? |
| [deployment.md](deployment.md) | Where does it run, and what talks to what? |

Start with [components](components.md) if you are new: it establishes the one rule the rest assumes.

## Mermaid, not PlantUML

Every diagram is Mermaid fenced in Markdown, which means it renders on GitHub and in most editors with
no toolchain, and it diffs as text — a moved arrow shows up in review as a moved arrow rather than as a
changed binary. PlantUML draws prettier class diagrams, but it needs rendering to be seen at all, which
in practice means committing images beside the source and remembering to regenerate them. A diagram
nobody can see while reviewing the change that invalidated it is the failure mode worth avoiding here.

The cost is real and worth stating: Mermaid's UML is partial. There are no proper package diagrams
(`flowchart` with subgraphs stands in), no stereotypes beyond `<<interface>>`-style annotations, and
class diagrams get cramped past a couple of dozen boxes. Where that bit, the diagram was split rather
than shrunk.

## What these diagrams are for, and what they are not

They describe **what is built today**, not a target. Where something is deliberately unfinished or
deliberately limited, it says so on the diagram rather than in a footnote somewhere else — the E2EE
public-key trust assumption in [flows](flows.md#chat-that-the-server-cannot-read) and the single replica
in [deployment](deployment.md) are both examples. A diagram that quietly draws the intended system
instead of the real one is worse than no diagram, because it is believed.

Plans live elsewhere: [future-plan.md](../future-plan.md) for what is missing,
[orbit-maui-plan.md](../orbit-maui-plan.md) and [ai-assistant-plan.md](../ai-assistant-plan.md) for
designs not yet built.

## Keeping them honest

These have no test. Nothing fails when a diagram goes stale, which makes them the easiest documentation
in the repository to let rot, so:

- A change that adds a project reference, an entity, a table, or a hosted service belongs in the
  matching diagram **in the same commit** — the same rule `info/` already lives under.
- Prefer naming the type or the physical column (`OP_C_CIPHERTEXTBASE64`, `PostgresLiveUpdateFanOut`)
  over describing it. A stale name can be grepped for; a stale description cannot.
- When a diagram's *reason* changes, rewrite the prose under it too. Most of the value here is in the
  paragraph explaining why the arrows point that way, and an accurate picture under a false explanation
  still misleads.

Each diagram was drawn from the code rather than from the other documents in `info/` — the entity
properties, the `ProjectReference` graph, `OrbitStorageNames`, and the handlers and synchronisers named
on each page.

This is [rule 17](../../.claude/CLAUDE.md) — which lists the triggers, and is worth reading as the
short version of this section.

### Which file covers what

| You changed | Edit |
| --- | --- |
| a `ProjectReference`, or what a project is for | [components.md](components.md) |
| an aggregate, a value object, an enum the domain turns on | [domain-model.md](domain-model.md) |
| an entity, a table, a column worth naming, a relationship | [database.md](database.md) |
| the order of something, or a hosted service that coordinates | [flows.md](flows.md) |
| an Azure resource, a port, a workflow trigger | [deployment.md](deployment.md) |

A rename touches whichever files quote the old name. Grep for it — that is what naming things rather
than describing them buys.

### Checking that a diagram still draws

A diagram that fails to parse renders on GitHub as an **error box**, not as nothing — worse than a
missing diagram, because it looks like the page is broken. Mermaid's own parser is the only honest
judge; reading the source and thinking it looks right is a different test, and it always passes.

```bash
npm install --no-save playwright@1 && npx playwright install chromium   # once
node ci/verify-diagrams.mjs
```

It parses every block in this folder and exits non-zero on the first that would not draw. Nothing runs
it automatically — it is not wired into the pipeline (see [future-plan](../future-plan.md)), so it is a
thing to run before pushing a diagram change, alongside the solution's tests.

**Parsing is not rendering.** A diagram can parse and still come out an unreadable tangle, which no
script can assert. When one grows past a couple of dozen boxes, look at it — and split it rather than
shrinking the labels, which is why the database is five diagrams and not one.
