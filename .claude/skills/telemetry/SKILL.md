---
name: telemetry
description: How Orbit's OpenTelemetry setup works (Azure Monitor exporter in the cloud, OTLP to the Aspire dashboard locally), how to verify that traces and logs reach Application Insights, and useful KQL queries. Use whenever touching OpenTelemetry code in Program.cs, when "nothing shows up in App Insights", when adding logging or metrics to a module, or when the user mentions "telemetry", "traces", "Application Insights", "KQL", or "Aspire dashboard".
---

# Telemetry

## Design

`Program.cs` in `Orbit.Api` chooses the exporter at startup:

- `APPLICATIONINSIGHTS_CONNECTION_STRING` set → `Azure.Monitor.OpenTelemetry.Exporter`
  (v1.8.3) sends to `appinsights-orbit`.
- Not set → OTLP exporter to the Aspire dashboard (local `docker compose`).

This conditional is deliberate: it keeps local runs free of Azure dependencies
and keeps the cloud free of a dashboard container. Do not replace it with
"always Azure" or "always OTLP", and do not let an empty string reach the Azure
exporter — treat empty as unset.

## Configuration

- Cloud: the connection string is a Container App **secret** referenced by an
  env var of the same name. Never paste it into `appsettings*.json`, the
  workflow, or a Dockerfile. The value itself is known to the user; never echo
  it into logs or chat.
- Local: `.env` (gitignored); `.env.example` carries the placeholder.

## Verifying that data arrives

1. Confirm the env var exists on the app (name only):
   ```bash
   az containerapp show -g Orbit -n orbit-api --query "properties.template.containers[0].env[].name" -o tsv
   ```
2. Generate traffic (one request to `/api/health` through `orbit-web`).
3. Query Application Insights (ingestion delay is typically 1–3 minutes):
   ```bash
   az monitor app-insights query -g Orbit --app appinsights-orbit \
     --analytics-query "requests | where timestamp > ago(15m) | order by timestamp desc | take 20 | project timestamp, name, resultCode, duration"
   ```
4. If nothing arrives, check console logs for exporter errors
   (`azure-deploy-diagnose`, step 3) — a malformed connection string fails
   silently in some versions.

## KQL snippets

Startup exceptions:
```kusto
exceptions | where timestamp > ago(1h) | order by timestamp desc | project timestamp, type, outerMessage, problemId
```

Failed requests through nginx:
```kusto
requests | where success == false | summarize count() by name, resultCode | order by count_ desc
```

Container platform logs (Log Analytics, not App Insights):
```kusto
ContainerAppSystemLogs_CL | where ContainerAppName_s == "orbit-api" | order by TimeGenerated desc | take 50 | project TimeGenerated, Reason_s, Log_s
```

## Adding telemetry in a module

- Reuse the existing `ActivitySource` and `Meter` instances registered in
  `Program.cs`; register new names there, not in the module.
- Log with structured properties (`logger.LogInformation("Synced {Count} notes", count)`),
  in English.
- Never log message bodies (Messaging), raw coordinates (Location), or
  connection strings.
