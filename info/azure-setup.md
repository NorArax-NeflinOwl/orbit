# Azure Container Apps setup

`.github/workflows/main_orbit.yml` builds and deploys the `orbit-api` and `orbit-web` images on
every push to `main` - see [Architecture](architecture.md#production-deployment-azure-container-apps).
That workflow only ever runs `az containerapp update --image ...`; it never sets configuration. This
page is the checklist for everything that has to be configured on the Container Apps themselves, once,
outside the pipeline - written up after an incident where most of it turned out to be missing and had
to be rediscovered live, one container-log grep at a time.

Resource group: `Orbit`. Environment: `orbit-environment`. Registry: `orbitcontainerregistry`.

## orbit-api: required configuration

Without these, the container crashes on startup (`Log.Fatal` + exit), which Container Apps reports as
a perpetual crash loop and surfaces to every caller as a `502` - including through orbit-web's proxy.

| Setting | Where it comes from | Notes |
|---|---|---|
| `Jwt__SigningKey` | Container App secret, e.g. `openssl rand -base64 48` | **Required.** At least 32 characters - see [Program.cs](../src/Server/Orbit.Api/Program.cs)'s startup check. |
| `ConnectionStrings__Orbit` | Container App secret - PostgreSQL connection string | **Required**, throws on startup if unset - see [OrbitDataServiceCollectionExtensions.cs](../src/Server/Orbit.Data/OrbitDataServiceCollectionExtensions.cs). Provisioning steps below. |

```bash
az containerapp secret set -n orbit-api -g Orbit \
  --secrets jwt-signing-key="$(openssl rand -base64 48)"

az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "Jwt__SigningKey=secretref:jwt-signing-key"
```

## orbit-api: optional configuration

The app starts and runs fine without these - each feature just logs a warning and no-ops instead.

| Setting | Feature | Notes |
|---|---|---|
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Traces/telemetry | From the `appinsights-orbit` resource's Overview page. Must be the full connection string (starts with `InstrumentationKey=`) - a malformed value crashes startup the same as a missing JWT key, since `AddAzureMonitorTraceExporter` throws on construction. |
| `Vapid__PublicKeyBase64Url`, `Vapid__PrivateKeyBase64Url`, `Vapid__Subject` | Push notifications | Generate with `npx web-push generate-vapid-keys`. Without these, `PushNotificationManager.EnableAsync` on the client silently returns `false` - the "enable push notifications" toggle just never turns on, with no visible error. |
| `Smtp__Host`, `Smtp__Port`, `Smtp__UserName`, `Smtp__Password`, `Smtp__FromAddress` | Calendar event reminder emails | `Smtp__Password` should be a secret. |

```bash
# Application Insights
APPINSIGHTS_CS=$(az monitor app-insights component show \
  --app appinsights-orbit -g Orbit --query connectionString -o tsv)
az containerapp secret set -n orbit-api -g Orbit --secrets appinsights-cs="$APPINSIGHTS_CS"
az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "APPLICATIONINSIGHTS_CONNECTION_STRING=secretref:appinsights-cs"

# Push notifications
npx web-push generate-vapid-keys
az containerapp secret set -n orbit-api -g Orbit --secrets vapid-private-key="<private key>"
az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "Vapid__PublicKeyBase64Url=<public key>" \
  "Vapid__PrivateKeyBase64Url=secretref:vapid-private-key" \
  "Vapid__Subject=mailto:you@example.com"

# Calendar reminder emails
az containerapp secret set -n orbit-api -g Orbit --secrets smtp-password="<password>"
az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "Smtp__Host=<host>" "Smtp__Port=587" "Smtp__UserName=<user>" \
  "Smtp__FromAddress=<address>" "Smtp__Password=secretref:smtp-password"
```

## orbit-api: database (PostgreSQL)

Orbit.Api used to run on a SQLite file, made persistent across restarts/redeploys by mounting an Azure
Files share into the container. **That approach caused a real production outage** - the mounted volume
briefly had two container replicas writing to it at once during a routine deploy (normal Container Apps
rollover behavior), and SQLite's WAL journal mode, which coordinates readers/writers through a
memory-mapped file, doesn't work reliably over a network filesystem (SMB/NFS/CIFS - this is called out
in SQLite's own documentation). It left the database in a state where every subsequent connection
attempt hung indefinitely, even back down to a single replica. Orbit.Api now runs on a real PostgreSQL
server instead - no shared file, no network-filesystem locking semantics, ordinary concurrent
connections. The old Azure Files share and volume mount, if still present from before, can be deleted
once this is live (`az containerapp env storage remove`).

1. If this Azure subscription has never had a PostgreSQL Flexible Server before, register the resource
   provider first (one-time per subscription; safe to skip if already registered - the next step's
   error message will say `MissingSubscriptionRegistration` if this was needed and wasn't done):
   ```bash
   az provider register --namespace Microsoft.DBforPostgreSQL
   # Takes a minute or two - poll until this prints "Registered":
   az provider show --namespace Microsoft.DBforPostgreSQL --query registrationState -o tsv
   ```
2. Provision an Azure Database for PostgreSQL Flexible Server (Burstable tier - cheapest that still
   gives dedicated compute; adjust `--sku-name`/`--storage-size` to taste), then create the `orbit`
   database on it as a separate step:
   - `--database-name` on `flexible-server create` is rejected by newer `az cli` versions unless
     `--node-count` (elastic clusters) is also given, which doesn't apply to a plain single-server
     instance like this one - hence the separate `db create` call below.
   - The server name is a global DNS label (`<name>.postgres.database.azure.com`) shared across every
     Azure customer, not just this resource group - a plain name like `orbit-postgres` can collide with
     someone else's server and fail with "Specified server name is already used" even though
     `az postgres flexible-server list -g Orbit` shows nothing. Append a random suffix if that happens.
   ```bash
   PG_PASSWORD="$(openssl rand -base64 24)"
   echo "SAVE THIS PASSWORD: $PG_PASSWORD"   # az does not print it back out anywhere else
   PG_SERVER_NAME="orbit-postgres-$(openssl rand -hex 3)"   # random suffix - see the note above
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
   `--public-access 0.0.0.0` is an Azure CLI special case, not "open to the entire internet" - it adds a
   firewall rule allowing traffic from any Azure-internal IP, which is what lets `orbit-api` (running in
   a Container Apps environment with no VNet integration to this database) reach it at all without
   private networking set up. The server still requires the admin password to authenticate.
3. Build the connection string and store it as a secret (substitute the actual `$PG_SERVER_NAME` and
   `$PG_PASSWORD` from step 2 if running this in a new shell session):
   ```bash
   az containerapp secret set -n orbit-api -g Orbit \
     --secrets orbit-db-connection-string="Host=$PG_SERVER_NAME.postgres.database.azure.com;Port=5432;Database=orbit;Username=orbitadmin;Password=$PG_PASSWORD;Ssl Mode=Require;Trust Server Certificate=true"
   az containerapp update -n orbit-api -g Orbit --set-env-vars \
     "ConnectionStrings__Orbit=secretref:orbit-db-connection-string"
   ```
   `Ssl Mode=Require` is mandatory - Flexible Server rejects unencrypted connections by default.
   `Trust Server Certificate=true` skips validating the server's certificate against a local CA bundle;
   fine for this setup, but validating against Azure's actual CA chain would be the more rigorous option
   if this ever needs hardening.
4. `orbit-api` can go back to its normal scaling (`--min-replicas` doesn't need to stay pinned at `1`
   for database-safety reasons anymore - PostgreSQL handles concurrent connections normally. Whether to
   actually run more than one replica is a separate question, unrelated to this incident).

### orbit-api: database backups

Flexible Server enables automated backups by default (7-day retention, locally redundant), but this
is worth confirming rather than assuming - especially soon after standing up a new server, and
especially given today's SQLite incident already cost this project one round of lost data. Check the
current settings:

```bash
az postgres flexible-server show -g Orbit -n "$PG_SERVER_NAME" \
  --query "{backupRetentionDays: backup.backupRetentionDays, geoRedundantBackup: backup.geoRedundantBackup}" -o json
```

To extend retention (up to 35 days, still within the Burstable tier):

```bash
az postgres flexible-server update -g Orbit -n "$PG_SERVER_NAME" --backup-retention 35
```

**Geo-redundant backup can only be set at server creation time**, not changed afterward - if that's
wanted, it means recreating the server with `--geo-redundant-backup Enabled` added to the
`flexible-server create` command above (a bigger step, since it also means a fresh database and
re-pointing `ConnectionStrings__Orbit`). Not done as part of this initial setup; worth revisiting if
this deployment moves from "personal project" to "something people depend on."

To restore from a backup (point-in-time restore, within the retention window), see
[`az postgres flexible-server restore`](https://learn.microsoft.com/cli/azure/postgres/flexible-server#az-postgres-flexible-server-restore)
- it creates a new server from the backup rather than restoring in place, so restoring is itself an
exercise in re-pointing `ConnectionStrings__Orbit` at the new server once it's ready.

## orbit-api: ingress

- Target port: `8080` (matches `ASPNETCORE_URLS` in the [Dockerfile](../src/Server/Orbit.Api/Dockerfile)).
- Traffic: internal-only (`external: false`) is fine - `orbit-web`'s nginx reaches it over the
  environment's internal FQDN, and internal ingress still gets one regardless of the external setting.

## orbit-web: ingress

- Target port: `80` (Azure Container Apps terminates TLS itself before forwarding plain HTTP - see
  [nginx.azure.conf](../src/Clients/Orbit.Web/nginx.azure.conf)'s header comment).
- Traffic: external.

`nginx.azure.conf` proxies `/api/*` to orbit-api's internal FQDN. Three things about that proxy are
easy to get wrong and each one shipped broken at least once during the incident that produced this
document - see the comments in that file for the specifics (missing TLS SNI, a `Host` header pointing
at the wrong app, and a `proxy_pass` variable silently truncating the request path). If touching that
file again, redeploy and check `az containerapp logs show -n orbit-web -g Orbit --follow` against a
real login attempt before assuming it works.

## Verifying a deploy

```bash
# Is the latest revision actually healthy, or is Container Apps still serving an old one?
az containerapp revision list -n orbit-api -g Orbit -o table
az containerapp revision list -n orbit-web -g Orbit -o table

# What is orbit-api's own log saying right now? (Verbose-by-default local logging does NOT apply here
# in Production - see Program.cs - so this should be readable without heavy filtering.)
az containerapp logs show -n orbit-api -g Orbit --follow

# Same for orbit-web's nginx access/error log.
az containerapp logs show -n orbit-web -g Orbit --follow
```

`latestReadyRevisionName` lagging behind `latestRevisionName` in the revision list means the newest
revision never became healthy - in `Single` revision mode, Container Apps still routes 100% of traffic
to it anyway, so a broken deploy is live and serving errors, not silently rolled back.
