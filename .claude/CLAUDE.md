# Agent Instructions

## Language

- Chat responses to the user: **Polish**.
- Everything in the project (code, identifiers, file/commit content, comments, commit messages, documentation, log messages, error messages): **English**.
- Do not modify existing comments, log/commit messages, or strings unless the task explicitly requires it.

## Scope of changes

- Touch only the code directly required by the current task. Do not refactor, rename, or "clean up" unrelated code, even if you notice issues there.
- If you spot a significant problem outside the task's scope, mention it in your response instead of fixing it silently.
- Keep diffs minimal and focused: one task = one coherent change, not a mix of feature work and incidental cleanup.

## Naming

- No abbreviations in identifiers.
- No type encoded in the name (e.g. `userList` → `users`).
- Include units in the name when the type doesn't make it obvious (`timeoutMs`, `sizeBytes`).
- No generic prefixes like `Abstract`, `Base` — name the class for what it does, not its role in a hierarchy.
- If you find yourself naming something "Utils", "Helpers", or "Misc", stop and split responsibilities properly instead.

## Code structure

- Avoid deep nesting. Prefer:
  - **Extraction** — pull nested logic into a separate, named function.
  - **Inversion** — use early returns/guard clauses instead of wrapping the main logic in conditionals.
- Functions should do one thing. If a function needs a comment to explain its sections, split it.

## Grouping variables into objects

Prefer an object-oriented shape over loose primitive variables. Group related private fields/variables into a dedicated object instead of keeping them as separate primitives, when **any** of these signals apply:

- a method/constructor has many parameters (4+),
- a class accumulates several private fields that are conceptually related (even if there are only 2-3 of them, group them if they always travel together),
- a method is long and holds many local working variables,
- **a module or closure accumulates several loose `let` or `const` variables at the top level** — this is the JS equivalent of a class with too many fields; group them the same way. This applies equally to top-level constants (e.g. a colour palette, a set of config flags) — `const C_SKY`, `const C_GROUND` … belong in a single `colors` object, not as 30 separate top-level bindings.

How to group:

- **Related data that travels together** (e.g. `street`, `city`, `zipCode`) → extract into a small named value object (e.g. `Address`). Treat it as read-only/immutable unless there's a reason to mutate it.
- **Temporary/working variables inside a long method** → extract into a single helper/context object local to that method, instead of scattering many loose variables through the function body.
- **State that multiple methods/steps read AND modify over time** → keep it as a distinct mutable object (e.g. an internal `State`/`Context` class), separate from the read-only input/data objects. Don't mix mutable state and read-only data into the same object — keep "what describes the situation" (read-only) separate from "what changes as we go" (mutable).
- **Variables that are always reset together** (e.g. in a `reset()` / `startGame()` function) are a strong signal they belong in one object — the reset then becomes a single `Object.assign(obj, { … })` call.

Goal: a class/method/module should operate on a small number of meaningful objects (data objects + state objects to update), not on a long flat list of unrelated primitive variables.

Don't over-apply this: 2-3 primitives that are simple, unrelated, and locally scoped (e.g. a loop index and a single flag) don't need to be wrapped. Wrapping trivial, unrelated variables into an artificial object is the opposite of the goal — only group variables that genuinely belong together conceptually.

## Design patterns

- Use a design pattern (Singleton, Builder, Factory, Facade, Adapter, Strategy, Observer, etc.) only when it genuinely simplifies the problem — not by default and not to look idiomatic.
- When you do use one, add a short comment naming the pattern and why it fits here.
- Prefer the simplest solution that solves the actual problem. Do not introduce a pattern speculatively for hypothetical future needs.

## Comments

- Document the *purpose/intent* of non-obvious methods, classes, parameters, and properties — not what the code already states plainly.
- Do not write a comment that just restates the function/variable name in prose (e.g. `// gets the user` above `getUser()`).
- Keep comments in sync with the code they describe; remove comments that no longer apply.

## Refactoring

- Refactor only within the scope of the current task (see "Scope of changes" above).
- When you do refactor, briefly explain in your response what changed and why.
- Don't change public APIs/signatures as a side effect of refactoring unless that's the point of the task — flag it first if it seems necessary.

## Tests

- When adding or changing logic, add or update corresponding tests in the same change.
- Before considering a task done, run the relevant test suite (not just the files you touched) and report the result.
- Do not delete or weaken existing tests to make them pass — fix the underlying issue, or flag the conflict if the test seems wrong.

## Commits

- Keep commits scoped to a single logical change, matching the "Scope of changes" rule.
- Write commit messages in English, in imperative mood (`Fix`, `Add`, `Refactor`), with a short summary line and, when useful, a brief body explaining *why*.
- Don't bundle unrelated changes (e.g. a bug fix and a formatting pass) into one commit.

## Secrets and credentials

Never hardcode credentials, passwords, tokens, or connection strings in any committed file.

- All secrets must be read from environment variables: `${VAR}` in Docker Compose, `Environment.GetEnvironmentVariable`/`IConfiguration` in .NET code. Never put real values in a committed `appsettings.json`/`appsettings.*.json` — real local values belong in a gitignored `appsettings.Development.json`, with a `.example` counterpart tracked as the placeholder template.
- Every project that uses secrets must have a `.env.example` committed with placeholder values, and `.env` listed in `.gitignore`.
- When creating or modifying any of the following, check that no secret is embedded: `appsettings.json`, `appsettings.*.json`, `launchSettings.json`, `docker-compose.yml`, `Dockerfile`, CI config files.
- The `.gitignore` must cover: `.env`, `.env.*`, `*.pem`, `*.key`, `*.p12`, `*.jks`, `secrets/`, `credentials/`.