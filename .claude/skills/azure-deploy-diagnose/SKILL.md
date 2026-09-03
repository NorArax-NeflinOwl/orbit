---
name: azure-deploy-diagnose
description: Step-by-step procedure for diagnosing Azure Container Apps failures for Orbit (orbit-api, orbit-web). Use whenever a Container App fails to start, a revision is unhealthy, the deploy job succeeds but the app does not respond, the API returns 502/503 through nginx, or the user mentions "orbit-api is failing", "revision", "provisioning failed", "container crashed", or "check the logs". Always use this before proposing any fix to a deployment problem.
---

# Diagnosing Orbit Container Apps

The point of this skill is to find the root cause before changing anything.
Deploys to `main` cost real money on this pay-as-you-go subscription, so a
guess-and-redeploy loop wastes both money and time. Read, then report, then fix.

## Context

- Resource group `Orbit`, region `polandcentral`
- Environment `orbit-environment`, domain suffix `victorioustree-36ad82ca`
- `orbit-api`: ASP.NET Core, listens on port 8080, ingress **internal**
- `orbit-web`: nginx serving Blazor WASM, port 80, ingress **external**,
  proxies `/api/` to `orbit-api`'s internal FQDN
- Images come from `orbitcontainerregistry.azurecr.io`
- Log Analytics workspace `ws-82ca0ad1-polandcent`

## Procedure

Run these in order. Stop at the first step that explains the failure.

### 1. Revision state

```bash
az containerapp revision list -g Orbit -n orbit-api \
  --query "[].{name:name,active:properties.active,state:properties.provisioningState,health:properties.healthState,replicas:properties.replicas}" -o table
```

`ProvisioningState: Failed` with zero replicas usually means the image never
started (pull error, bad port, crash on boot). `Provisioned` but `Unhealthy`
means the process runs but the ingress port or health probe is wrong.

### 2. System logs (platform-level: image pull, probes, scaling)

```bash
az containerapp logs show -g Orbit -n orbit-api --type system --tail 100
```

Look for: `ImagePullBackOff`, `unauthorized` (ACR pull identity), `probe failed`,
`target port`.

### 3. Console logs (the application's own stdout/stderr)

```bash
az containerapp logs show -g Orbit -n orbit-api --type console --tail 200
```

Look for .NET startup exceptions: missing configuration key, connection string
format, `Kestrel` binding to a port other than 8080, unhandled exception in
`Program.cs` (for example the Azure Monitor exporter throwing on an empty
connection string).

### 4. Historical logs via Log Analytics (when the revision already restarted)

```bash
az monitor log-analytics query -w ws-82ca0ad1-polandcent \
  --analytics-query "ContainerAppConsoleLogs_CL | where ContainerAppName_s == 'orbit-api' | order by TimeGenerated desc | take 100 | project TimeGenerated, Log_s" -o table
```

### 5. Configuration of the running app

```bash
az containerapp show -g Orbit -n orbit-api \
  --query "{ingress:properties.configuration.ingress,secrets:properties.configuration.secrets[].name,env:properties.template.containers[0].env,image:properties.template.containers[0].image,registries:properties.configuration.registries}" -o json
```

Verify: target port is 8080, the image tag matches the last successful pipeline
run, every env var the app requires is present, and the registry entry uses
`identity-orbit` (or a valid credential) for pulls.

## Known failure patterns for this project

| Symptom | Likely cause | Where to fix |
|---|---|---|
| `ImagePullBackOff` / `unauthorized` | Container App has no pull permission on ACR | Registry config on the app (`AcrPull` for `identity-orbit`) |
| Revision failed, console log empty | Wrong target port (app listens on 8080, ingress says 80) | Ingress `targetPort` |
| Console shows exception on startup | Missing env var / secret, or exporter misconfigured | Secrets + env on the app, `Program.cs` conditional |
| `orbit-api` healthy, `orbit-web` returns 502 on `/api/` | nginx `proxy_pass` points at wrong FQDN | `nginx.conf` — internal FQDN is `orbit-api.internal.victorioustree-36ad82ca.polandcentral.azurecontainerapps.io` (verify with `az containerapp show --query properties.configuration.ingress.fqdn`) |
| Pipeline green, old code still running | New revision not activated (single-revision mode expected) | Check revision mode and active revision |

## Reporting the result

Before applying any change, write a short root-cause report in this shape:

```
Root cause: <one sentence>
Evidence: <which command/log line shows it>
Proposed fix: <what changes, in which file or resource>
Cost impact: <none / new revision / new resource — see azure-cost-guard>
```

Then wait for the user to confirm if the fix touches Azure resources.
Code-only fixes (for example `Program.cs`, `nginx.conf`) go through the normal
PR workflow.
