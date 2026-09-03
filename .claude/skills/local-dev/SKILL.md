---
name: local-dev
description: Running and debugging Orbit locally on macOS with docker compose, the Aspire dashboard, and the Blazor WASM frontend, including how to test from a phone or second device. Use whenever the user wants to run the app locally, reproduce a bug outside Azure, says "docker compose", "aspire", "localhost", "cannot reach the API from my phone", or asks how to inspect the local database.
---

# Local development

Local reproduction is free; every Azure redeploy costs real money. When a deploy
problem could plausibly be an application problem, reproduce it here first.

## Topology (dev only — do not port to Container Apps)

`docker-compose.yml` defines:

| Service | Role |
|---|---|
| `orbit-api` | ASP.NET Core, container port 8080, published as 8081 |
| `orbit-web` | nginx + Blazor WASM, HTTPS entry point on 8443, proxies `/api/` to the api |
| `aspire-dashboard` | receives OTLP telemetry (traces, logs, metrics), UI on 18888 |
| `postgres` | PostgreSQL, published on 5432 so tools outside Docker reach it too |

Container Apps does its own ingress; `postgres` and `aspire-dashboard` have no
cloud counterpart there. Cloud routing lives in the nginx config
(`/api/` → internal FQDN of `orbit-api`).

## Start

```bash
cp .env.example .env          # first time only; fill placeholders
docker compose up --build
```

Leave `APPLICATIONINSIGHTS_CONNECTION_STRING` empty locally — `Program.cs` then
exports to the Aspire dashboard over OTLP instead of Azure Monitor.

Check: https://localhost:8443 (web; the TLS certificate is self-signed unless
the mkcert override in `docker-compose.override.yml` is in place),
http://localhost:8081 (api directly), http://localhost:18888 (Aspire dashboard).

## Reproducing a cloud startup failure

Run the API image exactly as the pipeline builds it:

```bash
docker build -f src/Server/Orbit.Api/Dockerfile -t orbit-api:local .
docker run --rm -p 8080:8080 -e ASPNETCORE_URLS=http://+:8080 orbit-api:local
```

Add the same env vars the Container App has (names from
`az containerapp show`, values from `.env`). If it crashes here, the fix is
in code or configuration, not in Azure.

## Access from a phone or another machine

Blazor WASM runs in the browser, so `localhost` in the API base address means
*the phone*, not the Mac. The base URL must come from configuration:

- Local: set it to `http://<mac-lan-ip>` (find with `ipconfig getifaddr en0`).
- Cloud: relative `/api/` so nginx proxies it.

Never commit a hardcoded `localhost` base address.

## Database access on macOS

- `docker compose exec postgres psql -U orbit orbit` (user and database come
  from `.env`; `orbit`/`orbit` are the defaults), or
- TablePlus / DBeaver pointed at `localhost:5432`.
- The mobile client keeps its own on-device SQLite store (`Orbit.Mobile`);
  pull the file from the device/emulator and open it with `sqlite3` or TablePlus.

## Tests

```bash
dotnet test                                # whole solution
dotnet test tests/Orbit.Api.Tests          # one project
```

Run the whole solution before opening a PR, not only the project you touched.
