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
| `ConnectionStrings__Orbit` | Literal value `Data Source=/app/data/orbit.db` | Falls back to a relative path under `/app` if unset, which isn't writable by the non-root container user - see [OrbitDataServiceCollectionExtensions.cs](../src/Server/Orbit.Data/OrbitDataServiceCollectionExtensions.cs). |

```bash
az containerapp secret set -n orbit-api -g Orbit \
  --secrets jwt-signing-key="$(openssl rand -base64 48)"

az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "Jwt__SigningKey=secretref:jwt-signing-key" \
  "ConnectionStrings__Orbit=Data Source=/app/data/orbit.db"
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

## orbit-api: persistent storage

**Currently disabled as of this writing** - the SQLite database lives on the container's ephemeral
local disk again and is wiped on every restart, redeploy, or scale-to-zero. It was briefly enabled via
an Azure Files mount and caused a real production outage (see below); re-enabling it needs the code
fix described here to already be deployed first.

### What went wrong the first time

1. Created a file share on the existing `orbitb722` storage account, registered it with the
   environment as `orbit-data`, and mounted it into `orbit-api` at `/app/data`.
2. A routine deploy briefly ran the old and new revisions side by side (normal Container Apps rollover
   behavior) - which meant two separate containers had the same SQLite file, over the same Azure Files
   share, open at once.
3. SQLite's WAL journal mode (enabled unconditionally in [Program.cs](../src/Server/Orbit.Api/Program.cs)
   at the time) coordinates readers/writers through a memory-mapped `-shm` file - which SQLite's own
   documentation says is unreliable over a network filesystem (NFS/SMB/CIFS). The two-writer moment
   left the `-wal`/`-shm` files in a state that made *every subsequent connection attempt hang
   indefinitely* trying to recover them - even back down to a single replica, even after a full
   scale-to-zero-and-back restart. The fix had to be reverting the volume mount entirely to restore
   service, since nothing short of that unstuck it.

**Program.cs now reads a `Database:UseWriteAheadLog` setting** (default `true`) and only runs
`PRAGMA journal_mode=WAL` when it's true - otherwise it explicitly sets `PRAGMA journal_mode=DELETE`
(SQLite's traditional rollback journal, which uses ordinary file locks rather than shared memory, and
is what actually works over SMB). **This must be set to `false` before ever re-mounting Azure Files**:

```bash
az containerapp update -n orbit-api -g Orbit --set-env-vars "Database__UseWriteAheadLog=false"
```

Leave it at its default (`true`, i.e. don't set the env var) for local development, which uses a real
local disk and has no reason to avoid WAL.

### Re-enabling the mount

1. Confirm `Database__UseWriteAheadLog=false` is set (above) and deployed *before* doing anything else
   here - mounting the volume first and fixing this after is exactly what caused the outage.
2. Create a file share on `orbitb722` and register it with the environment (skip if `orbit-data` already
   exists from the earlier attempt):
   ```bash
   az storage share-rm create --storage-account orbitb722 -g Orbit --name orbit-api-data --quota 5
   STORAGE_KEY=$(az storage account keys list --account-name orbitb722 -g Orbit --query "[0].value" -o tsv)
   az containerapp env storage set \
     --name orbit-environment -g Orbit \
     --storage-name orbit-data \
     --azure-file-account-name orbitb722 \
     --azure-file-account-key "$STORAGE_KEY" \
     --azure-file-share-name orbit-api-data \
     --access-mode ReadWrite
   ```
3. Mount it into the `orbit-api` container at `/app/data`. There's no simple CLI flag for this - do it
   in the Portal (`orbit-api` > Containers > Volumes, then Volume mounts) or via
   `az containerapp update --yaml` with a `volumes` + `volumeMounts` block referencing `orbit-data`.
4. Keep `--min-replicas 1 --max-replicas 1` on `orbit-api` (already set) - even with WAL disabled,
   SQLite still does not support multiple processes writing to the same file at once, and Container
   Apps' own deploy rollover can still momentarily run two replicas regardless of this setting.
5. If it's ever stuck again despite the above: `az storage file list-handles` /
   `az storage share close-handle --close-all` on the `orbit-api-data` share can force-clear a stale
   SMB handle left by a client that didn't shut down cleanly - this is what a next occurrence would
   most likely need, now that WAL itself is no longer the culprit.

If Azure Files' default mount permissions block the non-root container user from writing to
`/app/data`, that's a known SMB/Container-Apps interaction to look into next, not a new bug - check
`az containerapp env storage` mount-option support for uid/gid at that point.

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
