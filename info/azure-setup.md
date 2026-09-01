# Azure setup

Everything needed to stand up Orbit's production deployment from zero, and to operate it day to day.
Resource group `Orbit`, region Poland Central throughout.

`.github/workflows/main_orbit.yml` builds and deploys `orbit-api` and `orbit-web` on every push to
`main` - see [CI/CD pipeline](#cicd-pipeline) below. That workflow only ever updates the running image;
it never sets configuration. Everything on this page has to be set up once, outside the pipeline, by
hand or by running the commands below - and reconfirmed if a resource is ever recreated.

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

## How the pieces talk to each other

```
Browser ──HTTPS──▶ orbit-web (external ingress, :80, TLS terminated by Container Apps)
                       │
                       │ nginx proxies /api/* over the environment's internal network
                       ▼
                    orbit-api (internal ingress, :8080)
                       │
                       │ TCP 5432, TLS required
                       ▼
                 PostgreSQL Flexible Server (public endpoint, firewalled to Azure IPs only)
```

`orbit-api` has no ingress reachable from outside the Container Apps environment - `orbit-web`'s own
nginx (`src/Clients/Orbit.Web/nginx.azure.conf`) is the only path in, proxying `/api/*` to `orbit-api`'s
internal FQDN. See [nginx.azure.conf gotchas](#nginxazureconf-gotchas) for three specific ways that
proxy config breaks if touched carelessly.

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

Two safety nets watch this table, because every "Optional" row fails *silently* when missing:

- The deploy workflow refuses to deploy while any variable above is absent from `orbit-api` (the
  "Verify orbit-api has every required environment variable" step in
  [main_orbit.yml](../.github/workflows/main_orbit.yml) - edit its list when deliberately dropping a
  feature). It checks names only, since secret values can't be read back.
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

az containerapp secret set -n orbit-api -g Orbit \
  --secrets orbit-db-connection-string="Host=$PG_SERVER_NAME.postgres.database.azure.com;Port=5432;Database=orbit;Username=orbitadmin;Password=$PG_PASSWORD;Ssl Mode=Require;Trust Server Certificate=true"
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
| Scale | `min-replicas 1`, `max-replicas 1` (no longer required to stay at exactly 1 for database-safety reasons now that it's PostgreSQL, not SQLite - see [History](#sqlite-and-azure-files) - but hasn't been revisited since) | `min-replicas 0`, `max-replicas 1` - scales to zero when idle, meaning a cold start (a few seconds) on the first request after a quiet period |

```bash
az containerapp ingress show -n orbit-api -g Orbit
az containerapp ingress show -n orbit-web -g Orbit
```

**Raising `max-replicas` on `orbit-api` needs a backplane at the same time.** The live-update hub (see
[Functionality — Live updates](functionality.md#live-updates)) keeps its registry of who is connected in
the process's own memory. With two replicas, an announcement raised on one reaches only the clients
connected to that one; everybody else hears nothing and falls back to their slow poll. Nothing errors,
nothing appears in a log, and the only symptom is that the app is slower for some people than for others.
Scaling out means adding Azure SignalR Service or a Redis backplane in the same change.

**`orbit-web` will stop scaling to zero.** A client holding a WebSocket open is not idle, so the
scale-to-zero rule above no longer fires while anybody has Orbit open. The cold start goes away with it;
the cost does not.

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

The avatar menu offers a link to this deployment's logs, for an account holding the **Debug**
permission. Locally that is the Aspire dashboard the compose stack runs; on Azure there is no Aspire
dashboard - `orbit-api` sends its OpenTelemetry traces straight to Application Insights instead (see
`APPLICATIONINSIGHTS_CONNECTION_STRING` above). So the address here is a portal one:

```bash
# Whichever of the two is meant to be read - the App Insights resource, or the container's own log
# stream, which is where Serilog's console output goes.
az containerapp update -n orbit-web -g Orbit \
  --set-env-vars DIAGNOSTICS_DASHBOARD_URL="https://portal.azure.com/#@<tenant>/resource$(az monitor app-insights component show --app appinsights-orbit -g Orbit --query id -o tsv)/logs"
```

Unset, the menu offers nothing rather than a dead link - see
[write-diagnostics-dashboard.sh](../src/Clients/Orbit.Web/write-diagnostics-dashboard.sh), which
writes it into the client's `appsettings.json` when the container starts. It is a link rather than a
credential: it lands in a file every visitor can download, and following it still needs a portal
sign-in with rights to that resource.

Both halves are there: traces under Application Insights' own transaction search, and Serilog's log
lines as traces alongside them (see the `WriteTo.ApplicationInsights` sink in
[Program.cs](../src/Server/Orbit.Api/Program.cs)). The Container App's log stream shows the same lines
live while a container is running; App Insights is what still has them tomorrow.

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
   header, which re-entered the same `/api/` location and looped forever. Fix: hardcode the `Host`
   header to orbit-api's own hostname.
3. **`proxy_pass` with a variable truncating the path.** Once `proxy_pass`'s target contains a
   variable (which a `resolver`-based DNS-refresh approach requires), nginx stops doing its usual
   "replace the matched location prefix" rewrite - the URI part becomes the literal, final path. A
   trailing `/api/` in that value sent every request upstream as a bare `/api/`, dropping
   `auth/login` etc. Fix: no path after the host in `proxy_pass` at all, so nginx forwards the original
   request URI unmodified.

If touching that file again: redeploy and watch `az containerapp logs show -n orbit-web -g Orbit --follow`
against a real login attempt before assuming it works - none of the three failures above were visible
from the HTTP status code alone without reading nginx's own error log.
