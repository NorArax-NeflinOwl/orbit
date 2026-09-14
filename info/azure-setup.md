# Azure setup

Everything needed to stand up Orbit's production deployment from zero, and to operate it day to day.
Resource group `Orbit`, region Poland Central throughout.

`.github/workflows/main_orbit.yml` builds and deploys `orbit-api` and `orbit-web` on every push to
`main` - see [CI/CD pipeline](#cicd-pipeline) below. That workflow only ever updates the running image;
it never sets configuration. Everything on this page has to be set up once, outside the pipeline, by
hand or by running the commands below - and reconfirmed if a resource is ever recreated.

Two neighbouring pages: [Cost limits](#cost-limits) below is what keeps the bill bounded, and
[azure-security.md](azure-security.md) is what Defender for Cloud wants changed, what of it is worth
buying at this budget, and what is deliberately left as it is.

## Resource inventory

| Resource | Type | Purpose |
|---|---|---|
| `orbit-environment` | Container Apps Environment | Hosts both Container Apps below. |
| `orbit-api` | Container App | The ASP.NET Core API (`src/Server/Orbit.Api`). Internal ingress only. |
| `orbit-web` | Container App | The Blazor WebAssembly client behind nginx (`src/Clients/Orbit.Web`). External ingress. |
| `orbitcontainerregistry` | Container Registry | Holds the `orbit-api`/`orbit-web` images the pipeline builds. |
| `identity-orbit` | Managed Identity | Used for GitHub Actions' OIDC login to Azure (`azure/login`). |
| `orbit-postgres-<random>` | PostgreSQL Flexible Server | The application database. Name has a random suffix - see [why](#1-provision-postgresql) - check the actual name with `az postgres flexible-server list -g Orbit -o table` rather than assuming. |
| `appinsights-orbit` | Application Insights | Traces/telemetry from `orbit-api`. |
| `orbitb722` | Storage account | Left over from an earlier SQLite-on-Azure-Files design that's no longer in use - see [History](#sqlite-and-azure-files). Safe to leave or delete; nothing depends on it now. |

Each Container App also has its own **system-assigned managed identity** (separate from
`identity-orbit`), used to pull images from `orbitcontainerregistry` without a stored registry
password - visible as `"identity": "system"` under each app's `registries` config.

Three more objects belong to this deployment and are deliberately not in the table, because none of
them bills: the `orbit-monthly-budget` consumption budget, which sits on the subscription rather than
in the resource group; the `orbit-cost-alerts` action group it notifies; and `orbit-automation`, the
free-tier Automation account whose one runbook writes the mails those notifications turn into. They
are what keeps the bill from running away unnoticed - see [Cost limits](#cost-limits) for what they do,
what they emphatically do not do, and how to check they still exist.

## How the pieces talk to each other

```
Browser ──HTTPS──▶ orbit-web (external ingress, :80, TLS terminated by Container Apps)
                       │
                       │ nginx proxies /api/* to orbit-api
                       ▼
Phone ────HTTPS──▶ orbit-api (external ingress, :8080)
                       │
                       │ TCP 5432, TLS required
                       ▼
                 PostgreSQL Flexible Server (public endpoint, firewalled to Azure IPs only)
```

**Both apps have external ingress, and they are protected differently.** The browser goes through
`orbit-web`'s nginx (`src/Clients/Orbit.Web/nginx.azure.conf`), which keeps `/api/` same-origin and
applies the edge rate limits declared there. The phone calls `orbit-api` directly, so those limits never
see it - `RateLimiterPolicies.FloodStop` in the application is what bounds that path instead. See
[nginx.azure.conf gotchas](#nginxazureconf-gotchas) for three specific ways that proxy config breaks if
touched carelessly.

`orbit-api` had internal ingress only until the phone was pointed at it. The detour through nginx meant
every sync woke `orbit-web`, which is set to scale to zero and therefore never did.

**nginx must reach the API by its `.internal` name, and that is load-bearing.** For one deploy it used
the public name instead, and the effect was measured in the API's request log: every browser user was
`from 20.215.81.0`. Leaving the environment and coming back in through the public ingress makes the
egress NAT address the "caller", the forwarded-header walk stops on it, and every per-caller limit in the
application collapses into one bucket for everybody using a browser - the exact failure the whole
forwarded-address work exists to prevent. Inside the environment the internal ingress appends the nginx
pod instead, which the API knows to walk past (`ForwardedCaller`, `ForwardLimit = 2`; the chains are
pinned in `ForwardedCallerTests`). The phone, which calls the API directly, was correct either way.

The ingress switch itself:

```bash
az containerapp ingress update -n orbit-api -g Orbit --type external
```

**What was actually observed when this was done on 2026-09-06:** the switch took about fifteen seconds
and *nothing broke* - the web client, its `/api/` proxy and its `/health` proxy all kept answering 200
with `nginx.azure.conf` still naming `orbit-api.internal`. That was not proof the internal name survives
the switch: the nginx replica answering had started four and a half hours earlier and was holding a
resolution made before the change (nginx resolves its upstream once at startup - see
[gotchas](#nginxazureconf-gotchas)).

**A fresh nginx does resolve `orbit-api.internal` with the ingress external - measured on 2026-09-07**,
from inside a newly created `orbit-web` replica (a different one from the morning's, so a real cold
start, not a cached resolution): the internal ingress answered `{"status":"Healthy"}`. The check is a
one-liner from inside the environment, and it is the thing to run again if the ingress is ever changed:

```bash
az containerapp exec -n orbit-web -g Orbit --command "wget -qO- https://orbit-api.internal.victorioustree-36ad82ca.polandcentral.azurecontainerapps.io/health/live"
```

`Healthy` back means the name resolves and the internal ingress answers. Should that ever stop being
true, the alternative is not the public name - that collapses every browser user into the egress NAT
address, see above - but a VNet-integrated environment with a stable egress that `KnownIPNetworks`
could name; this environment has none (`staticIp` is inbound only). `--type internal` reverses the
switch, with the phone losing its direct route.

**Which orbit-api nginx proxies to is the container's to say, not the image's.** Since 2026-09-10
`nginx.azure.conf` carries the placeholder `__ORBIT_API_HOST__` where the FQDN used to be written, and
[point-nginx-at-the-api.sh](../src/Clients/Orbit.Web/point-nginx-at-the-api.sh) fills it in when the
container starts, from `ORBIT_API_HOST`. Unset means this environment's
`orbit-api.internal.victorioustree-36ad82ca.polandcentral.azurecontainerapps.io`, so nothing here had to
change; a second environment - the production one planned beside this test one - runs the same image
and sets its own:

```bash
az containerapp update -n orbit-web -g <that group> --set-env-vars \
  ORBIT_API_HOST=$(az containerapp show -n orbit-api -g <that group> --query properties.configuration.ingress.fqdn -o tsv | sed 's/^orbit-api\./orbit-api.internal./')
```

It has to be the `.internal` name, for the reason just above, and the script refuses anything that is
not a bare hostname. What it chose is the first line `orbit-web` logs
(`az containerapp logs show -n orbit-web -g Orbit`: "Proxying /api/ to …"), which is where to look when
`/api/` answers 502 after a deploy to a new environment.

## First-time setup from zero

Assumes the resource group, `orbit-environment`, `orbitcontainerregistry`, `identity-orbit` (with
GitHub OIDC federation already configured), and the two empty Container Apps already exist. If
starting completely from nothing, those need to exist first (out of scope for this page - this covers
configuring an `orbit-api`/`orbit-web` pair that already exist against a fresh database).

### 1. Provision PostgreSQL

```bash
# One-time per subscription - skip if already done. If this step is needed and skipped, the next
# one fails with "MissingSubscriptionRegistration".
az provider register --namespace Microsoft.DBforPostgreSQL
az provider show --namespace Microsoft.DBforPostgreSQL --query registrationState -o tsv   # poll for "Registered"
```

```bash
PG_PASSWORD="$(openssl rand -base64 24)"
echo "SAVE THIS PASSWORD NOW, SOMEWHERE PERSISTENT (not just this shell variable): $PG_PASSWORD"
PG_SERVER_NAME="orbit-postgres-$(openssl rand -hex 3)"
echo "SERVER NAME: $PG_SERVER_NAME"

az postgres flexible-server create \
  --resource-group Orbit \
  --name "$PG_SERVER_NAME" \
  --location polandcentral \
  --admin-user orbitadmin \
  --admin-password "$PG_PASSWORD" \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --storage-size 32 \
  --version 16 \
  --public-access 0.0.0.0

az postgres flexible-server db create \
  --resource-group Orbit --server-name "$PG_SERVER_NAME" --name orbit
```

Why the random suffix, the separate `db create` call, and `--public-access 0.0.0.0`: see
[PostgreSQL CLI gotchas](#postgresql-cli-gotchas).

**Verify the firewall rule actually exists before moving on** - it has gone missing at least once in
this project's history for no confirmed reason (see [History](#a-vanishing-firewall-rule)):

```bash
az postgres flexible-server firewall-rule list -g Orbit --server-name "$PG_SERVER_NAME" -o table
```

Expect exactly one row, `AllowAllAzureServicesAndResourcesWithinAzureIps`, `0.0.0.0`-`0.0.0.0`. If the
list is empty, recreate it:

```bash
az postgres flexible-server firewall-rule create \
  -g Orbit --server-name "$PG_SERVER_NAME" \
  --name AllowAllAzureServicesAndResourcesWithinAzureIps \
  --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
```

### 2. Configure orbit-api

All required and optional settings in one place. Every `az containerapp secret set` /
`--set-env-vars` pair below is independent - run only the ones relevant to what's being (re)configured.

| Setting | Required? | Where it comes from |
|---|---|---|
| `Jwt__SigningKey` | **Required.** Crashes startup if missing/short - see [Program.cs](../src/Server/Orbit.Api/Program.cs). | Container App secret, ≥32 chars, e.g. `openssl rand -base64 48`. |
| `ConnectionStrings__Orbit` | **Required.** Throws on startup if unset - see [OrbitDataServiceCollectionExtensions.cs](../src/Server/Orbit.Data/OrbitDataServiceCollectionExtensions.cs). | Container App secret. PostgreSQL connection string from step 1. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Optional - traces *and* log lines; unset, both go to the OTLP endpoint instead. A malformed value (not empty - see [gotcha](#a-malformed-app-insights-string-crashes-startup-same-as-missing-jwt)) crashes startup the same as a missing JWT key. | Container App secret. From the `appinsights-orbit` resource. |
| `Vapid__PublicKeyBase64Url` / `Vapid__PrivateKeyBase64Url` / `Vapid__Subject` | Optional - push notifications. Missing means the "enable push notifications" toggle silently never turns on, no visible error. | Public key/subject as plain env vars, private key as a secret. `npx web-push generate-vapid-keys`. |
| `Smtp__Host` / `Smtp__Port` / `Smtp__UserName` / `Smtp__Password` / `Smtp__FromAddress` | Optional - all outgoing email: calendar reminders, email verification codes, password reset codes. | `Smtp__Password` as a secret, rest as plain env vars. |
| `GoogleAuth__ClientId` | Optional - "sign in with Google". Missing means the Google button never renders, no visible error. Public by design, so a plain env var. | The OAuth web client in Google Cloud Console → Credentials; the production `orbit-web` URL must be in its Authorized JavaScript origins. |
| `WebClientBaseUrl` | Optional - the link a shared-item email carries (see [IWebClientLinks](../src/Shared/Orbit.Core/Notifications/IWebClientLinks.cs)). Missing means the email still sends, just without a link. Public by design, so a plain env var. | `orbit-web`'s own public URL, e.g. `https://orbit-web.<environment>.<region>.azurecontainerapps.io` or the custom domain if one is set up. |

Two safety nets watch this table, because every "Optional" row fails *silently* when missing:

- The deploy workflow refuses to deploy while any variable above is absent from `orbit-api` (the
  "Verify orbit-api has every required environment variable" step in
  [main_orbit.yml](../.github/workflows/main_orbit.yml) - edit its list when deliberately dropping a
  feature). It checks names only, since secret values can't be read back. Every row above is on that
  list, including `WebClientBaseUrl`, which was the last one missing from it: the variable was added to
  this table while still unset in production, and a name on that list is a name whose absence stops the
  deploy, so listing it then would have blocked every deploy until somebody set it. It was set on
  `orbit-api` on 2026-09-04 and listed in the same change.
- The API's `configuration` health check reports on `GET /health` which integration is unconfigured
  (Degraded) or - always a mistake - only partially configured (Unhealthy), naming the missing keys.
  `orbit-api` has no external ingress, so read it via
  `az containerapp exec -n orbit-api -g Orbit --command "curl -s localhost:8080/health"`.

```bash
# Required
az containerapp secret set -n orbit-api -g Orbit \
  --secrets jwt-signing-key="$(openssl rand -base64 48)"
az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "Jwt__SigningKey=secretref:jwt-signing-key"

# Ssl Mode=VerifyFull, and no "Trust Server Certificate": under Npgsql 10 that trust setting is what
# switches certificate verification off, leaving a connection that is encrypted without proving who
# answered. See info/azure-security.md - the deployment this was written on may still carry the old
# value, which cannot be read back out of the secret.
az containerapp secret set -n orbit-api -g Orbit \
  --secrets orbit-db-connection-string="Host=$PG_SERVER_NAME.postgres.database.azure.com;Port=5432;Database=orbit;Username=orbitadmin;Password=$PG_PASSWORD;Ssl Mode=VerifyFull"
az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "ConnectionStrings__Orbit=secretref:orbit-db-connection-string"

# Optional: Application Insights
APPINSIGHTS_CS=$(az monitor app-insights component show \
  --app appinsights-orbit -g Orbit --query connectionString -o tsv)
echo "$APPINSIGHTS_CS"   # sanity-check it starts with "InstrumentationKey=" before using it - see gotcha above
az containerapp secret set -n orbit-api -g Orbit --secrets appinsights-cs="$APPINSIGHTS_CS"
az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "APPLICATIONINSIGHTS_CONNECTION_STRING=secretref:appinsights-cs"

# Optional: push notifications
npx web-push generate-vapid-keys
az containerapp secret set -n orbit-api -g Orbit --secrets vapid-private-key="<private key>"
az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "Vapid__PublicKeyBase64Url=<public key>" \
  "Vapid__PrivateKeyBase64Url=secretref:vapid-private-key" \
  "Vapid__Subject=mailto:you@example.com"

# Optional: outgoing email (calendar reminders, verification and password reset codes)
az containerapp secret set -n orbit-api -g Orbit --secrets smtp-password="<password>"
az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "Smtp__Host=<host>" "Smtp__Port=587" "Smtp__UserName=<user>" \
  "Smtp__FromAddress=<address>" "Smtp__Password=secretref:smtp-password"

# Optional: "sign in with Google"
az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "GoogleAuth__ClientId=<client id>.apps.googleusercontent.com"
```

**A secret's *value* can be updated without creating a new revision** - existing running replicas keep
using whatever value they started with until explicitly restarted:

```bash
az containerapp revision restart -n orbit-api -g Orbit --revision <latest-revision-name>
```

Changing which secret an env var *points to* (or adding/removing an env var) does create a new
revision automatically, which picks up the current secret value on its own.

### 3. Allow the pg_trgm extension

Orbit's name suggestions are a trigram search, so a migration runs `CREATE EXTENSION pg_trgm`. Because
`Program.cs` applies migrations at startup, a server that refuses the extension is a server the API
cannot start against - and nothing in CI would find it first, since the smoke test runs against a plain
`postgres:18-alpine`, where the extension needs no permission.

**On this deployment it worked with `azure.extensions` empty**, on 2026-08-31: `orbit-postgres-djgiwo`
allowed the extension without being told to. So the allowlist is not the absolute gate it is often
described as - at least not for `pg_trgm`, and at least not here. This section stays because the failure
mode is real and expensive when it happens, and because a different server, region or Postgres version
may well answer differently. Check rather than assume:

```bash
az postgres flexible-server parameter show \
  --resource-group Orbit --server-name <server> --name azure.extensions --query value -o tsv
```

If `PG_TRGM` is not in that list, add it - keeping whatever is already there - and restart the server:

```bash
az postgres flexible-server parameter set \
  --resource-group Orbit --server-name <server> --name azure.extensions --value PG_TRGM
```

### 4. Confirm database backups

Flexible Server enables automated backups by default (7-day retention, locally redundant) - worth
confirming rather than assuming, especially given the SQLite incident already cost this project one
round of lost data before PostgreSQL was even in the picture:

```bash
az postgres flexible-server show -g Orbit -n "$PG_SERVER_NAME" \
  --query "{backupRetentionDays: backup.backupRetentionDays, geoRedundantBackup: backup.geoRedundantBackup}" -o json
```

To extend retention (up to 35 days, still within the Burstable tier):

```bash
az postgres flexible-server update -g Orbit -n "$PG_SERVER_NAME" --backup-retention 35
```

**Geo-redundant backup can only be set at server creation time**, not changed afterward - wanting it
means recreating the server with `--geo-redundant-backup Enabled` added to the `create` command in
step 1 (a bigger step, since it also means a fresh database and re-pointing `ConnectionStrings__Orbit`).
Not done as part of the current setup; worth revisiting if this deployment moves from "personal
project" to "something people depend on."

To restore from a backup (point-in-time restore, within the retention window), see
[`az postgres flexible-server restore`](https://learn.microsoft.com/cli/azure/postgres/flexible-server#az-postgres-flexible-server-restore)
- it creates a new server from the backup rather than restoring in place, so restoring is itself an
exercise in re-pointing `ConnectionStrings__Orbit` at the new server once it's ready.

### 5. Confirm ingress

| | orbit-api | orbit-web |
|---|---|---|
| Target port | `8080` (matches `ASPNETCORE_URLS` in [its Dockerfile](../src/Server/Orbit.Api/Dockerfile)) | `80` (Container Apps terminates TLS itself before forwarding plain HTTP - see [nginx.azure.conf](../src/Clients/Orbit.Web/nginx.azure.conf)'s header comment) |
| Traffic | Internal only | External |
| Scale | `min-replicas 1`, `max-replicas 1` while autoscaling is off (the default), `3` while it is on - see [Scaling](#scaling) below | `min-replicas 0`, `max-replicas 1` while autoscaling is off, `3` while it is on - scales to zero when idle either way, meaning a cold start (a few seconds) on the first request after a quiet period |

```bash
az containerapp ingress show -n orbit-api -g Orbit
az containerapp ingress show -n orbit-web -g Orbit
```

#### Scaling

**Autoscaling is off by default, and one button away.** `.github/workflows/autoscale.yml` ("Turn
autoscaling on or off" in the Actions tab, run from `main`) takes one choice:

- **off** - `max-replicas 1` on both apps, which is how they have always run.
- **on** - `max-replicas 3` on both, with an HTTP rule named `http-concurrency` that starts another replica
  once one is carrying more than the given number of concurrent requests (30 unless you say otherwise).

It touches nothing else: not the image, not `min-replicas` (orbit-web still scales to zero when idle,
orbit-api keeps its one warm replica), not CPU or memory. Off is a ceiling of one rather than "no rule",
because an app with no rule of its own still gets Container Apps' default HTTP rule and would scale all
the same. Each run makes a new revision of both apps - scale settings live on the revision template - and
logs the resulting `properties.template.scale` of each, so the run is the record of what changed. The
deploy never touches scale settings, so whatever was chosen last survives every release. By hand, the same
two commands are:

```bash
az containerapp update -n orbit-api -g Orbit --max-replicas 3 --scale-rule-name http-concurrency --scale-rule-type http --scale-rule-http-concurrency 30
az containerapp update -n orbit-api -g Orbit --max-replicas 1
```

**Several replicas of `orbit-api` need no backplane of their own any more.** The live-update hub, the
privacy choice cache and the rate limiter each count across instances through PostgreSQL (see
[Functionality — Live updates](functionality.md#live-updates) and `info/uml/deployment.md`). What nobody
has done yet is watch them do it: the first time autoscaling is on and a second replica starts is the first
real test of all three, so look at the live updates and the rate limits that day.

**`orbit-web` will stop scaling to zero while anybody has Orbit open.** A client holding a WebSocket open
is not idle, so the scale-to-zero rule above no longer fires. That is true with autoscaling off as well;
turning it on only adds replicas above the first.

### 6. Let a release record itself as the newest build

The Android release workflow tells `orbit-api` what it just published, so the app's update row lights up
(`MobileVersion__Android__LatestVersion`). It needs one repository variable naming the resource group the
Container App is in, and skips the step silently when it is absent:

```bash
gh variable set API_CONTAINER_APP_RESOURCE_GROUP --body Orbit
```

Only `LatestVersion` is set. `MinimumSupportedVersion` is the one that **blocks** an app that is too old,
and while Orbit is a prototype it should stay empty so every build keeps working - see
`MobileVersionPolicy`, where the two verdicts are `UpdateAvailable` and `UpdateRequired`.

### 7. Where the phone apps are downloaded from

Optional, and only needed once there is a build to hand out. `/download` in the web client offers
whatever [`MobileDownloads`](../src/Clients/Orbit.Web/wwwroot/appsettings.json) names, and says nothing
is published where it names nothing - so the page is safe to deploy before any of this exists.

This repository is private, so a GitHub release asset is not a link a phone can follow: downloading one
needs a GitHub sign-in. A storage container with anonymous read on the blobs is what makes a plain link
work.

```bash
az storage account create -n orbitdownloads -g Orbit -l polandcentral --sku Standard_LRS \
  --allow-blob-public-access true
az storage container create --account-name orbitdownloads -n apps --public-access blob
```

Then give the release workflow somewhere to put the file, as repository *variables* rather than secrets
(neither value is one):

| Variable | Value |
| --- | --- |
| `DOWNLOADS_STORAGE_ACCOUNT` | `orbitdownloads` |
| `DOWNLOADS_CONTAINER` | `apps` |

The identity the workflow signs in as needs **Storage Blob Data Contributor** on that account -
`azure/login` gets it in, and nothing else grants it the right to write a blob:

```bash
az role assignment create --assignee <identity-orbit's object id> \
  --role "Storage Blob Data Contributor" \
  --scope $(az storage account show -n orbitdownloads -g Orbit --query id -o tsv)
```

Finally, point the two places at it. `MobileDownloads:Android` in orbit-web's
`wwwroot/appsettings.json` is what the page links to, and `MobileVersion:Android:UpdateUrl` in
orbit-api's configuration is where the forced-update gate sends an app that is too old - the same
address, since the page is where a new build comes from:

    https://orbitdownloads.blob.core.windows.net/apps/orbit-android.apk

The blob name never changes, so neither setting has to be touched again when a newer build is released.

### 8. Where the "Debug logs" entry leads

The avatar menu offers this deployment's logs to an account holding the **Debug** permission, and asks
which of the two are wanted: what was logged, or what is happening this second. They are different
places. Locally both are pages of the Aspire dashboard the compose stack runs; on Azure there is no
Aspire dashboard - `orbit-api` sends its OpenTelemetry traces *and* its Serilog log lines straight to
Application Insights instead (see `APPLICATIONINSIGHTS_CONNECTION_STRING` above), while the container's
own console is a stream that keeps nothing. So the two addresses are portal ones:

```bash
az containerapp update -n orbit-web -g Orbit --set-env-vars \
  DIAGNOSTICS_HISTORY_URL="https://portal.azure.com/#@/resource$(az monitor app-insights component show --app appinsights-orbit -g Orbit --query id -o tsv)/logs" \
  DIAGNOSTICS_LIVE_URL="https://portal.azure.com/#@/resource$(az containerapp show -n orbit-api -g Orbit --query id -o tsv)/logstream"
```

Either unset drops that half of the choice; both unset and the menu offers nothing rather than a dead
link - see [write-diagnostics-dashboard.sh](../src/Clients/Orbit.Web/write-diagnostics-dashboard.sh),
which writes them into the client's `appsettings.json` when the container starts. They are links rather
than credentials, and they are deliberately not committed: they land in a file every visitor can
download, so the resource path lives in the deployment's own configuration rather than in the
repository. Following one still needs a portal sign-in with rights to that resource.

## Cost limits

Two numbers govern this subscription: **10 € a month is a warning, 20 € a month is the ceiling** - and
three mails carry them, each saying what to do rather than only what the number is. Setting that up is
the rest of this section, and the first thing to understand is that Azure enforces none of it.

**A budget does not stop anything.** On a pay-as-you-go subscription there is no spending cap to turn
on: the "spending limit" Azure documents belongs to credit-based offers (free trial, Azure for
Students, Visual Studio credit), where it exists because there is a credit to run out of. Here there is
a card, and a budget is a *notification* resource - it watches the running total, fires at the
thresholds it was given, and lets the meter keep running. So the 20 € ceiling is enforced by the
person reading the mail and running the commands in it, which are the ones
[`scripts/stop-azure-compute.sh`](../scripts/stop-azure-compute.sh) runs.

**And the total it watches is stale.** Pay-as-you-go usage is rated in batches, and a charge can take
most of a day to appear in Cost Management - so the mail saying "20 €" is really saying "20 € as of
some hours ago". Whatever this deployment burns in those hours is spent before anybody is told. That is
the reason for the fourth, plainer notification below, which fires on the projected end-of-month total
rather than the current one, and is the only one that arrives in time to act on calmly.

### The three mails

Azure's own budget mail is a number and a link. The mails asked for here carry the commands, so they
are written by a runbook in an Automation account -
[`scripts/send-cost-instruction-mail.ps1`](../scripts/send-cost-instruction-mail.ps1) - which the budget's
action group calls, and which a daily schedule calls again for the third one:

| When | What the mail says | Who writes it |
| --- | --- | --- |
| 10 € spent | Halfway to the ceiling: where to look for the reason, and that the next mail carries the stop commands | the runbook, called by the action group |
| 20 € spent | The five commands that stop the spending, with the real server name filled in, and `scripts/stop-azure-compute.sh` as the short form | the runbook, called by the action group |
| Last day of the month, **only if something is still stopped** | What is stopped, and the commands that bring it back before the counter resets tomorrow | the runbook, from its daily schedule |
| Forecast says the month will reach 20 € | Azure's own plain mail - no commands, but early | the budget itself |

"Only if something is still stopped" is how "if the shutdown mail came" is read here, and on purpose:
the runbook looks at the deployment rather than remembering a mail, so a shutdown mail nobody acted on
produces no reminder (there is nothing to resume), and a stop done by hand without the mail still gets
one. It checks the PostgreSQL server's state and both apps' ingress, because Azure restarts a stopped
Flexible Server by itself after seven days and the apps do not come back with it.

**The runbook never stops or starts anything.** Its identity holds Reader on the resource group and
nothing more, so it could not; the person holding the card stays the one who runs the commands. Making
it act is a decision recorded in [Future Plan — Deployment](future-plan.md#deployment), not a default.

### First, check what currency the subscription bills in

A budget's amount carries no currency of its own: it is denominated in the subscription's billing
currency, whatever that is, and the amounts below assume that is **EUR**.

```bash
az consumption usage list --top 1 --query "[0].currency" -o tsv
```

`EUR` back means the numbers below are used as they are. Anything else means converting 10 € and 20 €
into that currency first, and writing the rate and date next to the amounts, because the ceiling then
drifts with the exchange rate. The portal says the same thing under **Cost Management + Billing →
Billing scopes → Properties**.

### Second, check what the deployment already costs

A ceiling below the running cost is a monthly outage, not a limit. Before creating anything, get last
full month's bill broken down by resource:

```bash
az consumption usage list --start-date 2026-08-01 --end-date 2026-08-31 \
  --query "[].[instanceName, pretaxCost, currency]" -o tsv | awk -F'\t' '
    { split($1, segments, "/"); total[segments[length(segments)]] += $2; sum += $2; currency = $3 }
    END { for (resource in total) printf "%10.2f %s  %s\n", total[resource], currency, resource
          printf "%10.2f %s  TOTAL\n", sum, currency }' | sort -rn
```

It takes a while - the Consumption API pages through every usage record of the month - and the TOTAL
line sorts to the top. What to expect from it, at list prices and in rough order of size:

| What bills | Why it bills | Can the stop commands reach it? |
| --- | --- | --- |
| Container Apps vCPU-seconds and GiB-seconds | `orbit-api` runs at `min-replicas 1`, so it bills around the clock even with nobody using Orbit - very likely the largest line. The consumption plan's monthly free grant (180,000 vCPU-seconds, 360,000 GiB-seconds) covers only the first days of one always-on replica, and how many days depends on what the app was created with: `az containerapp show -n orbit-api -g Orbit --query "properties.template.containers[0].resources"`. | **Yes** - to zero. |
| PostgreSQL Flexible Server compute (`Standard_B1ms`) | Always on unless stopped. | **Yes** - compute only. |
| PostgreSQL storage and backups (32 GB) | Billed whether the server is running or stopped. | No. |
| `orbitcontainerregistry` (Basic) | A flat daily charge for the registry existing, independent of pushes or pulls. | No. |
| Log Analytics ingestion behind `appinsights-orbit` | Per GB ingested, with 5 GB free per month. Small at this traffic, and bounded by the API logging at Information level in production. | No, but see the note below. |
| `orbitdownloads` blob storage | Pennies for one APK. | No. |
| `orbit-automation` (the runbook) | 500 job-minutes a month free; a run here takes well under one, and there are about thirty-five a month. | Not billing to begin with. |

**If that TOTAL is already near or above 20 €, the ceiling is in the wrong place** - stopping
everything on, say, the 20th of each month is not a cost limit, it is a scheduled outage. Then the
choice is to raise the numbers, or to make the deployment cheaper first: `orbit-api` at
`min-replicas 0` is the single largest saving available, at the price of a cold start on the first
request after a quiet spell (which is exactly how `orbit-web` already runs). Decide that before
creating the budget, not after the first mail.

### 1. Create the action group the alerts are delivered to

An action group is the "who gets told" half; the budget only references it. It starts with a plain
email action - Azure's own mail, as a fallback that keeps working if the runbook ever does not - and
gains the runbook's webhook in step 4. It is free to keep, and email notifications are free for the
first thousand a month, so this adds no recurring charge - but it is still a resource being created,
so it falls under [rule 6](../.claude/CLAUDE.md) and the `azure-cost-guard` skill.

```bash
az monitor action-group create \
  --name orbit-cost-alerts \
  --resource-group Orbit \
  --short-name orbitcost \
  --action email orbit-owner "<your address>"
```

`--short-name` is what shows up in the email subject and is capped at 12 characters.

### 2. Create the budget

Thresholds on an Azure budget are **percentages of the budget amount, not amounts**. With the amount
set to the 20 € ceiling, the 10 € warning is exactly 50% - which is the one piece of luck in this
section, since 10 and 20 divide cleanly and most pairs do not.

The two amount notifications go to the action group, so the runbook writes their mails. The forecast
one goes to a plain address instead: the runbook's mail is about an amount that has been spent, and a
forecast is not that.

```bash
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
ACTION_GROUP_ID=$(az monitor action-group show -n orbit-cost-alerts -g Orbit --query id -o tsv)

cat > /tmp/orbit-budget.json <<JSON
{
  "properties": {
    "category": "Cost",
    "amount": 20,
    "timeGrain": "Monthly",
    "timePeriod": { "startDate": "2026-09-01T00:00:00Z", "endDate": "2036-09-01T00:00:00Z" },
    "notifications": {
      "Warning10": {
        "enabled": true, "operator": "GreaterThanOrEqualTo",
        "threshold": 50, "thresholdType": "Actual",
        "contactGroups": ["$ACTION_GROUP_ID"], "locale": "en-us"
      },
      "Ceiling20": {
        "enabled": true, "operator": "GreaterThanOrEqualTo",
        "threshold": 100, "thresholdType": "Actual",
        "contactGroups": ["$ACTION_GROUP_ID"], "locale": "en-us"
      },
      "Forecast20": {
        "enabled": true, "operator": "GreaterThanOrEqualTo",
        "threshold": 100, "thresholdType": "Forecasted",
        "contactEmails": ["<your address>"], "locale": "en-us"
      }
    }
  }
}
JSON

az rest --method put \
  --url "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/providers/Microsoft.Consumption/budgets/orbit-monthly-budget?api-version=2023-05-01" \
  --body @/tmp/orbit-budget.json
```

Things that will reject the call if changed carelessly:

- `startDate` must be the first day of a month, and Azure refuses one more than three months in the
  past. Use the current month; a monthly budget resets on the 1st regardless, so nothing is lost by
  starting late.
- `timeGrain: Monthly` is what "a month" means here - the counter resets on the 1st and every
  threshold is re-armed. `endDate` is only how long the budget itself lives; ten years out is a way of
  saying "until somebody removes it".
- A budget holds at most five notifications, and a *forecast* one only fires once Azure has enough
  history on the subscription to project from - expect it to be quiet for the first couple of months.
- `az rest` is used rather than `az consumption budget create` because that command group is
  deprecated and never covered subscription scope properly. The portal does the same job under **Cost
  Management → Budgets → Add**, and this whole section is reproducible there if the CLI argues.

Read it back, which is also the quickest "where am I this month":

```bash
az rest --method get \
  --url "https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/providers/Microsoft.Consumption/budgets/orbit-monthly-budget?api-version=2023-05-01" \
  --query "properties.[amount, currentSpend.amount, currentSpend.unit]" -o tsv
```

`scripts/stop-azure-compute.sh --status` prints that same line alongside what is actually running.

### 3. Create the Automation account that writes the mails

One account, `orbit-automation`, on the free tier: 500 job-minutes a month at no charge, and a run of
this runbook takes seconds. It is a resource all the same - rule 6 applies. The `automation` CLI
extension covers the runbook; everything it does not cover goes through `az rest`, with the same
`AUTOMATION` path each time.

```bash
az extension add --name automation --upgrade

az automation account create -n orbit-automation -g Orbit -l polandcentral --sku Free
az resource update -g Orbit -n orbit-automation --resource-type Microsoft.Automation/automationAccounts \
  --set identity.type=SystemAssigned

AUTOMATION="https://management.azure.com/subscriptions/$SUBSCRIPTION_ID/resourceGroups/Orbit/providers/Microsoft.Automation/automationAccounts/orbit-automation"
API="api-version=2023-11-01"
```

**Reader, and nothing more.** The runbook reads the PostgreSQL server's state and the apps' ingress
to know what to put in the month-end mail. Reader lets it do that and forbids everything else, which
is what keeps "the runbook never stops anything" a property of the identity rather than of the code:

```bash
az role assignment create \
  --assignee-object-id $(az resource show -g Orbit -n orbit-automation --resource-type Microsoft.Automation/automationAccounts --query identity.principalId -o tsv) \
  --assignee-principal-type ServicePrincipal \
  --role Reader \
  --scope $(az group show -n Orbit --query id -o tsv)
```

**What the runbook reads from the account.** The SMTP settings are the same ones `orbit-api` sends
reminders with (`Smtp__*` in step 2 of the setup above), so no new mail account is involved. Variable
values are JSON, hence the quoted quotes:

```bash
put_variable() { # name value
  az rest --method put --url "$AUTOMATION/variables/$1?$API" \
    --body "{\"name\":\"$1\",\"properties\":{\"value\":\"\\\"$2\\\"\",\"isEncrypted\":false}}" >/dev/null
}
put_variable ResourceGroup Orbit
put_variable BudgetAmount 20
put_variable SmtpHost "<Smtp__Host>"
put_variable SmtpPort "<Smtp__Port>"
put_variable SmtpFromAddress "<Smtp__FromAddress>"
put_variable MailToAddress "<your address>"

az rest --method put --url "$AUTOMATION/credentials/SmtpCredential?$API" \
  --body "{\"name\":\"SmtpCredential\",\"properties\":{\"userName\":\"<Smtp__UserName>\",\"password\":\"<Smtp__Password>\"}}"
```

The credential is stored encrypted and is never readable back, which is the point of it being a
credential asset rather than a variable. It is one more place the SMTP password lives - note it in
`PRZENOSINY.local.md` next to the others, so a rotation reaches it.

**The runbook itself**, from this repository - so a change to the mail is a change to a file here,
reviewed like any other, and re-imported with the same two commands:

```bash
az automation runbook create --automation-account-name orbit-automation -g Orbit \
  -n send-cost-instruction-mail --type PowerShell72 --location polandcentral
az automation runbook replace-content --automation-account-name orbit-automation -g Orbit \
  -n send-cost-instruction-mail --content @scripts/send-cost-instruction-mail.ps1
az automation runbook publish --automation-account-name orbit-automation -g Orbit -n send-cost-instruction-mail
```

`PowerShell72` is the runtime the runbook is written for. It needs only `Az.Accounts`, which every
account ships with - every Azure read goes through `Invoke-AzRestMethod`, so there is no module to
import and nothing to fall out of date.

### 4. Wire it: the webhook for the budget, the schedule for the month end

A webhook is how the action group reaches the runbook. Azure hands out the address exactly once, at
creation, and it is the only thing that authorises a call - so it goes straight into the action group
and nowhere else:

```bash
WEBHOOK_URI=$(az rest --method post --url "$AUTOMATION/webhooks/generateUri?$API" -o tsv)
az rest --method put --url "$AUTOMATION/webhooks/orbit-cost-alerts?$API" \
  --body "{\"name\":\"orbit-cost-alerts\",\"properties\":{\"isEnabled\":true,\"uri\":\"$WEBHOOK_URI\",\"expiryTime\":\"2036-09-01T00:00:00Z\",\"runbook\":{\"name\":\"send-cost-instruction-mail\"}}}" >/dev/null
az monitor action-group update -n orbit-cost-alerts -g Orbit \
  --add-action webhook orbit-cost-runbook "$WEBHOOK_URI"
unset WEBHOOK_URI
```

The month-end mail comes from a **daily** schedule, not a monthly one: the runbook checks whether
tomorrow is the 1st (UTC, which is when the budget resets) and exits at once on every other day. That
costs about thirty trivial runs a month inside the free grant, and it keeps the behaviour off the
question of how Automation counts month lengths. 17:00 UTC is evening in Poland, so the mail is read
the same day.

```bash
az rest --method put --url "$AUTOMATION/schedules/daily-month-end-check?$API" \
  --body "{\"name\":\"daily-month-end-check\",\"properties\":{\"startTime\":\"$(date -u -v+1d +%Y-%m-%d)T17:00:00+00:00\",\"frequency\":\"Day\",\"interval\":1,\"timeZone\":\"Etc/UTC\"}}" >/dev/null
az rest --method put --url "$AUTOMATION/jobSchedules/$(uuidgen | tr A-Z a-z)?$API" \
  --body "{\"properties\":{\"schedule\":{\"name\":\"daily-month-end-check\"},\"runbook\":{\"name\":\"send-cost-instruction-mail\"}}}" >/dev/null
```

(`date -u -v+1d` is the macOS spelling of "tomorrow"; on Linux it is `date -u -d tomorrow`. Azure
refuses a start time in the past, and refuses one less than five minutes out.)

### 5. Make it send something before trusting it

Nothing above has been proven by being run. Two calls exercise both entry points without waiting for
a real month to go wrong:

```bash
# The 20 € mail, exactly as Azure would post it. Change NotificationThresholdAmount to 10 for the other.
curl -s -X POST "$(az rest --method get --url "$AUTOMATION/webhooks/orbit-cost-alerts?$API" --query properties.uri -o tsv 2>/dev/null || echo '<the webhook uri, if still to hand>')" \
  -H 'Content-Type: application/json' -d '{
    "schemaId": "AIP Budget Notification",
    "data": { "SubscriptionName": "Azure subscription 1", "BudgetName": "orbit-monthly-budget",
              "SpendingAmount": "20.4", "Budget": "20", "Unit": "EUR", "NotificationThresholdAmount": "20" } }'

# The month-end path, on an ordinary day: expect "Not the last day of the month" in the job output.
az automation runbook start --automation-account-name orbit-automation -g Orbit -n send-cost-instruction-mail
az automation job list --automation-account-name orbit-automation -g Orbit -o table
```

The webhook URI is not readable back after creation, which the first command's fallback is admitting:
if it was `unset` above, the portal's **Test pane** on the runbook takes the same JSON as
`WEBHOOKDATA`. A mail arriving from the first call is the whole check; a job that ends in `Failed`
names the missing variable or the SMTP refusal in its output.

### 6. When the 10 € mail arrives

Nothing has to be turned off. It is a prompt to find out *why* - half of a 20 € month by the middle of
it is normal; half of it by the 5th is not:

```bash
scripts/stop-azure-compute.sh --status
# and the per-resource breakdown from "Second, check what the deployment already costs",
# with this month's dates
```

The usual culprits, in the order they are worth checking: autoscaling left on after a test (the
`max-replicas 3` of [Scaling](#scaling) above), `orbit-web` never scaling to zero because a client is
holding a live-update connection open, or a deploy loop that pushed far more images than usual.

### 7. When the 20 € mail arrives

The mail is not the block. The block is running what it says - five commands, or their short form:

```bash
scripts/stop-azure-compute.sh --dry-run     # read what it is about to do
scripts/stop-azure-compute.sh               # and do it
```

It stops the PostgreSQL Flexible Server, closes both Container Apps' ingress and sets them to
`min-replicas 0`. Both apps then hold no replicas, so they bill nothing; the database keeps only its
storage charge. **Nothing is deleted** - not the database, not the images, not the configuration - so
this is a pause, not a teardown.

Three things to know before running it:

- Orbit goes down for everyone, web and phone alike, until it is resumed.
- Closing the ingress is not decoration. `orbit-api` is kept awake by the phone syncing against it, so
  `min-replicas 0` on its own would never let it empty.
- **Azure starts a stopped Flexible Server again by itself after seven days.** That is Flexible Server
  behaviour, not something this repository chose, and it means the block quietly expires. If the month
  still has time to run at that point, stop it again. The month-end mail notices this: it reports the
  server as running and the apps as still closed, and its commands cover both.

### 8. Bringing it back - the month-end mail

On the last day of the month, if anything is still stopped, the runbook mails what is stopped and the
commands that bring it back, in order. The short form:

```bash
scripts/stop-azure-compute.sh --resume
```

The database starts first, because `orbit-api` applies migrations at startup and never becomes healthy
without it; then the apps get their ingress and `min-replicas` back. `--resume` restores the values in
[Confirm ingress](#5-confirm-ingress) above rather than remembering what it found, so if that table ever
changes, the constants at the top of the script - and the commands the runbook mails - have to change
with it. `max-replicas` is deliberately left alone, so whatever the autoscale workflow last chose
survives the whole round trip. Then verify it the way a deploy is verified - see
[Verifying a deploy](#verifying-a-deploy).

Nothing says it has to wait for the mail. A stop that was overcautious can be resumed the same day;
the mail exists so that a month does not end with Orbit forgotten in the dark.

### What the stop cannot stop

`orbitcontainerregistry` (a flat daily charge), PostgreSQL storage and backups, the storage account and
whatever Log Analytics has already ingested all keep billing with everything switched off. That is the
floor the ceiling sits on: no command gets the month to zero, and getting below that floor means
*deleting* resources, which costs the images, the database or the telemetry history to get back.

Log Analytics is worth one extra note, because it is the one line on the bill that a code change can
send climbing: the workspace bills per GB ingested, so a logging change that turns a hot path chatty at
Information level shows up as cost rather than as a failure. If a bill jumps with no infrastructure
change behind it, look there before anywhere else.

### Checking the limits are still there

A budget is easy to lose track of: it is invisible until it fires, and it lives on the subscription
rather than in the `Orbit` resource group, so anything that reasons about "what is in the resource
group" will not see it. The runbook is quieter still - it says nothing on the thirty days it has
nothing to say.

```bash
az rest --method get \
  --url "https://management.azure.com/subscriptions/$(az account show --query id -o tsv)/providers/Microsoft.Consumption/budgets?api-version=2023-05-01" \
  --query "value[].{name: name, amount: properties.amount, spent: properties.currentSpend.amount}" -o table
az monitor action-group show -n orbit-cost-alerts -g Orbit --query "{emails: emailReceivers[].emailAddress, webhooks: webhookReceivers[].name}"
az automation job list --automation-account-name orbit-automation -g Orbit --query "[?ends_with(runbook.name, 'mail')].[creationTime, status]" -o tsv | head -5
```

The last one should show a job from every day, each `Completed`; a gap means the schedule is gone or
the account is, and a `Failed` means the mail would not have gone out either. Worth running after any
subscription change, and worth a thought when a month passes with no mail at all: silence means either
a cheap month or a deleted budget, and the two look identical from here.

## Verifying a deploy

```bash
# Is the latest revision actually healthy, or is Container Apps still serving an old one?
az containerapp revision list -n orbit-api -g Orbit -o table
az containerapp revision list -n orbit-web -g Orbit -o table

# What is orbit-api's own log saying right now? (Production logs at Information level, not the
# Verbose default used locally - see Program.cs - so this should be readable without heavy filtering.)
az containerapp logs show -n orbit-api -g Orbit --follow
az containerapp logs show -n orbit-web -g Orbit --follow
```

`latestReadyRevisionName` lagging behind `latestRevisionName` means the newest revision never became
healthy - in `Single` revision mode (what both apps use), Container Apps still routes 100% of traffic
to it regardless, so a broken deploy is live and serving errors, not silently rolled back. The CI/CD
pipeline now checks for and corrects this automatically on every deploy - see below - but it's worth
knowing how to check by hand for anything done outside the pipeline (a manual `az containerapp update`,
a secret rotation, etc.).

## CI/CD pipeline

`.github/workflows/main_orbit.yml`, triggered on every push to `main`:

1. Builds both images.
2. **Smoke-tests `orbit-api`** against a real `postgres:18-alpine` service container in the runner -
   applies migrations, checks `/health/ready` - before pushing anything or touching Azure. Also a
   lighter "does it serve a response" check for `orbit-web`. This validates the image can start and
   migrate against *a* PostgreSQL; it can't validate connectivity to the real Azure database
   (firewall, DNS, SSL) or `nginx.azure.conf`'s proxy path, both of which are specific to the deployed
   environment.
3. Pushes both images to `orbitcontainerregistry`, tagged with the commit SHA.
4. Deploys each Container App, capturing the previously-running image first.
5. **Polls each new revision's `HealthState`** for up to 3 minutes. If it never becomes `Healthy`, the
   workflow redeploys the previously-captured image and fails the run - turning a bad deploy into a CI
   failure with production already back on the last known-good image, instead of a silent outage.

This closes the loop on most of what's in [History](#history) below happening again unnoticed - but
it only catches what's reproducible in a GitHub-hosted runner. It would not have caught, for example,
the vanishing Postgres firewall rule, since that's a property of the live Azure resource, not the
application image.

Deliberately **not** using a GitHub Environment / manual approval gate on this workflow - see
[History](#a-broken-approval-gate-attempt).

## History

Condensed record of what's already gone wrong here and why the current setup looks the way it does -
kept short on purpose; see git history / PR descriptions around 2026-08-23 and 2026-08-24 for the full
blow-by-blow if actually needed.

### SQLite and Azure Files

Orbit.Api originally ran on a SQLite file, made "persistent" by mounting an Azure Files share into the
container. This caused a real outage: a routine deploy briefly ran two replicas with the same file
open at once (normal Container Apps rollover behavior), and SQLite's WAL journal mode - which
coordinates readers/writers through a memory-mapped file - doesn't work reliably over a network
filesystem. Every subsequent connection attempt hung indefinitely, even back down to one replica, until
the volume was unmounted entirely. Orbit.Api now runs on PostgreSQL instead - no shared file, no
network-filesystem locking semantics. The volume mount was removed from `orbit-api` on 2026-08-24; the
underlying Azure Files share/storage account (`orbitb722`) and the environment-level storage
registration (`orbit-data`, visible via `az containerapp env storage list -n orbit-environment -g
Orbit`) are inert leftovers, safe to delete whenever convenient.

### A vanishing firewall rule

At some point after being correctly created, the Postgres server's `AllowAllAzureServicesAndResourcesWithinAzureIps`
firewall rule was found completely absent - `publicNetworkAccess: Enabled` but zero rules, which behaves
as a total block. Root cause unconfirmed - one plausible theory: the very first provisioning attempt
partially failed client-side on `MissingSubscriptionRegistration` (the resource provider wasn't
registered yet) while the server creation continued provisioning in the background regardless, possibly
without the firewall parameter surviving that path. Not reproduced deliberately, so treat this as "known
to happen at least once," not "understood." **Verify the firewall rule exists after creation and if
connectivity ever silently breaks again** - it's the first thing to check, per the command in
[step 1](#1-provision-postgresql).

### A broken approval gate attempt

Added `environment: production` to `main_orbit.yml`'s job once, to gate deploys behind manual approval.
It broke `azure/login` outright: targeting a GitHub Environment changes the OIDC token's subject claim
from `repo:<org>/<repo>:ref:refs/heads/main` to `repo:<org>/<repo>:environment:<name>`, which the
federated identity credential on `identity-orbit` didn't trust. Reverted. See
[`info/future-plan.md`](future-plan.md) for exactly what a correct retry needs (a second federated
credential with an environment-shaped subject, added in Entra ID first).

### A malformed App Insights string crashes startup, same as missing JWT

`AddAzureMonitorTraceExporter` throws during service construction if
`APPLICATIONINSIGHTS_CONNECTION_STRING` is set but doesn't start with `InstrumentationKey=` - this once
happened because a shell command's warning output got captured into the variable instead of the actual
connection string. Always `echo` and eyeball a fetched connection string before feeding it into
`secret set`.

## Permission unlock codes

They are rows in the database, not configuration: made on the first start that finds a permission
without one, and left alone by every start after that, so a deploy never changes a code somebody was
told. Read them with a plain query (see [PostgreSQL CLI gotchas](#postgresql-cli-gotchas) for getting a
`psql` session against the Azure server):

```sql
SELECT "Permission", "Code" FROM "PermissionCodes";
```

Rotating one is an `UPDATE`, run when it is wanted rather than on every release - whoever holds the old
code loses it the moment it runs, which is the point. Nothing caches a code, so it takes effect on the
next code somebody types, with no restart. The deployment's own note (git-ignored, since it names
accounts) carries the statements.

There is nothing to configure in the Container App for this. An earlier design derived the codes from a
`Permissions__Secret` environment variable backed by a `permission-secret` secret; both are **left over
and unused** - nothing in the repository reads either. Removing them:

```bash
az containerapp update -g Orbit -n orbit-api --remove-env-vars Permissions__Secret
az containerapp secret remove -g Orbit -n orbit-api --secret-names permission-secret
```

The env-var removal starts a new revision, as any template change does. The secret has to go second: a
secret still referenced by an environment variable cannot be removed.

## PostgreSQL CLI gotchas

Small `az` CLI quirks hit while setting this up, kept here so they don't have to be rediscovered:

- **`--database-name` on `flexible-server create`** is rejected by newer CLI versions unless
  `--node-count` (elastic clusters) is also given - not applicable to a plain single-server instance.
  Create the database with a separate `flexible-server db create` call instead.
- **`flexible-server db create` takes `--name`, not `--database-name`** - despite the sibling `create`
  command's flag being `--database-name` when it does work. Also takes `--server-name`, not `-n`/`--name`
  for the server (`-n`/`--name` there means the *database's* name).
- **`flexible-server firewall-rule create`/`list` also need `--server-name`**, not `-n`/`--name` for
  the server - same shape of gotcha, different subcommand.
- **Server names are a global DNS label** (`<name>.postgres.database.azure.com`), shared across every
  Azure customer, not scoped to this resource group. A plain name like `orbit-postgres` can collide
  with someone else's server and fail with "Specified server name is already used" even though
  `az postgres flexible-server list -g Orbit` shows nothing in *this* subscription. Use a random
  suffix.
- **A subscription that has never had a PostgreSQL Flexible Server** needs
  `Microsoft.DBforPostgreSQL` registered first (`MissingSubscriptionRegistration` otherwise) - see
  [step 1](#1-provision-postgresql).

## nginx.azure.conf gotchas

`orbit-web`'s nginx (`src/Clients/Orbit.Web/nginx.azure.conf`) proxies `/api/*` to `orbit-api`'s
internal FQDN. Three specific things about that proxy shipped broken at least once each:

1. **Missing TLS SNI.** nginx doesn't send the SNI extension to an HTTPS upstream by default. Container
   Apps' internal ingress is a shared endpoint that routes by SNI - without it, it can't tell which app
   the connection is for and resets the handshake. Fix: `proxy_ssl_server_name on;`.
2. **`Host` header pointing at the wrong app.** `proxy_set_header Host $host;` forwards the *browser's*
   original host (`orbit-web...`), not orbit-api's. Once SNI got the TLS handshake working, the wrong
   Host header made Container Apps' internal ingress route the request back to `orbit-web` by that
   header, which re-entered the same `/api/` location and looped forever. Fix: set the `Host`
   header to orbit-api's own hostname - the same `ORBIT_API_HOST` the `proxy_pass` uses, never
   `$host`.
3. **`proxy_pass` with a variable truncating the path.** Once `proxy_pass`'s target contains a
   variable (which a `resolver`-based DNS-refresh approach requires), nginx stops doing its usual
   "replace the matched location prefix" rewrite - the URI part becomes the literal, final path. A
   trailing `/api/` in that value sent every request upstream as a bare `/api/`, dropping
   `auth/login` etc. Fix: no path after the host in `proxy_pass` at all, so nginx forwards the original
   request URI unmodified.

If touching that file again: redeploy and watch `az containerapp logs show -n orbit-web -g Orbit --follow`
against a real login attempt before assuming it works - none of the three failures above were visible
from the HTTP status code alone without reading nginx's own error log.
