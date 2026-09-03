---
name: orbit-conventions
description: Naming, structure, grouping, comment and design-pattern conventions for Orbit's C#, Razor and configuration code. Use whenever writing or modifying code in Orbit.Api, Orbit.Web, Orbit.Core or tests — including small fixes — and whenever reviewing a diff. Also use when the user asks "is this idiomatic for our project", "rename this", or "clean this up".
---

# Orbit coding conventions

These rules are about readability for the next person (or the next session)
reading the code. When a rule and readability conflict, say so in the response
rather than silently picking one.

## Scope of changes

- Touch only the code the task requires. No refactors, renames or cleanup of
  unrelated code, even when something looks wrong. Mention it in the response instead.
- One task = one coherent change.
- Do not change public APIs or signatures as a side effect. Flag it first.
- Do not modify existing comments, log messages or strings unless the task needs it.

## Naming

- No abbreviations: `configuration` not `cfg`, `repository` not `repo`,
  `synchronization` not `sync` in new identifiers (existing schema columns such
  as `sync_state` stay as they are).
- No type in the name: `users` not `userList`, `note` not `noteObject`.
- Units in the name when the type does not make them obvious:
  `timeoutMilliseconds`, `sizeBytes`, `retryDelaySeconds`.
- No `Abstract`, `Base`, `Utils`, `Helpers`, `Misc`, `Common`. Name a class for
  what it does (`NoteSynchronizer`, `CalendarEventMapper`). If the only name that
  fits is "Utils", split responsibilities.

**Example**
Before: `var lst = svc.GetAll(cfg.TimeoutMs);`
After: `var notes = noteService.GetAll(configuration.TimeoutMilliseconds);`

## Structure

- Guard clauses and early returns instead of nested `if`.
- Extract nested logic into a named method.
- A method does one thing. If it needs section comments, split it.

**Example**
Before:
```csharp
public async Task<Result> Save(Note note)
{
    if (note is not null)
    {
        if (note.IsValid())
        {
            // persist
            ...
            // publish
            ...
        }
    }
    return Result.Failure();
}
```
After:
```csharp
public async Task<Result> Save(Note note)
{
    if (note is null) return Result.Failure("Note is required.");
    if (!note.IsValid()) return Result.Failure("Note is invalid.");

    await Persist(note);
    await PublishChanged(note);
    return Result.Success();
}
```

## Grouping variables into objects

Group loose primitives when any of these apply: a method has 4+ parameters,
a class has several related private fields, a long method juggles many locals,
or a module has several top-level `const`/`static` values that belong together.

- Data that travels together → small immutable record
  (`record SyncWindow(DateTimeOffset From, DateTimeOffset To)`).
- Working variables of a long method → a private context type local to that class.
- State that several steps read and modify → a distinct mutable state object,
  kept separate from read-only input.
- Variables always reset together → one object; reset becomes one assignment.

Do not wrap two unrelated locals (a loop index and a flag) into an artificial object.

## Comments

- Explain intent or a non-obvious constraint, not what the code says.
- Never restate the name (`// gets the user` above `GetUser()`).
- When a design pattern is used deliberately, name it and say why in one line.
- Remove comments that no longer match the code you changed.

## Design patterns

Use one only when it simplifies the actual problem. No speculative abstractions
for hypothetical future modules — Orbit.Core's `sync_state`/`remote_id`/
`modified_at` convention already provides the extension point (see
`orbit-module-scaffold`).

## Secrets

Never in committed files. Read from environment variables / configuration
providers; placeholders go in `.env.example`; `.gitignore` must cover `.env`,
`.env.*`, `*.pem`, `*.key`, `*.p12`, `*.jks`, `secrets/`, `credentials/`.

## Tests

- New or changed logic → new or updated test in the same change.
- Run the relevant suite (`dotnet test`) before declaring the task done and
  report the outcome.
- Never delete or weaken a test to make it pass; fix the cause or flag the conflict.
