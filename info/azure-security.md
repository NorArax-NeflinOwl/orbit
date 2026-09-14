# Azure security

What Defender for Cloud is complaining about, which of it is worth fixing on this subscription, and
which of it costs more than the deployment it protects.

The secure score on 2026-09-14 was **42% (about 5 of 12 points), with 4 resources in bad health.** This
page is the plan for that number - and the first thing it has to say is that the number is not the goal.

## "Fully secured" and the 20 € ceiling pull against each other

The [cost limits](azure-setup.md#cost-limits) put a warning at 10 € a month and a ceiling at 20 €.
Most of the missing seven points are not settings - they are **paid Defender plans and premium SKUs**,
and turning on the ones that would clear the score costs more per month than everything Orbit currently
runs on. The arithmetic is in [What the remaining points cost](#what-the-remaining-points-cost) below,
and it comes to roughly two to three times the entire ceiling.

So "w pełni zabezpieczony" has to mean something achievable, and on this subscription it means:

- **Nothing is weaker than it needs to be for free.** Every protection that costs nothing is on. That
  is the section below, and it is the part that should actually get done.
- **Every remaining gap is a decision somebody made on purpose**, written down here with what it would
  cost to close, rather than an oversight nobody noticed.

A 100% secure score is not that, and chasing it here would mean paying Microsoft more to watch a
prototype than the prototype costs to run.

## What the score is, and what it is not

Secure score is Defender for Cloud grading the subscription against the Microsoft Cloud Security
Benchmark. It is a useful list. It is not a measure of whether Orbit is safe, for three reasons worth
keeping in mind before treating 42% as a grade:

- **Several controls are "have you bought this Defender plan".** Buying it changes the score without
  changing a single attack surface.
- **It does not know what a resource is for.** `orbitdownloads` serves one Android APK anonymously on
  purpose - that is the feature - and the score counts it as a finding.
- **It says nothing about the application.** Orbit's own authentication, the end-to-end encryption, the
  rate limits and the JWT handling are where a real attack on this system would go, and none of it is
  in that 42%.

## Reading the real list

The portal's own list is the authority, and this repository cannot see it. `scripts/audit-azure-security.sh`
pulls it, together with the settings this page has decisions about:

```bash
scripts/audit-azure-security.sh              # everything
scripts/audit-azure-security.sh --defender   # only Defender's failing assessments
scripts/audit-azure-security.sh --resources  # only this deployment's settings
```

Every call it makes is a `show` or a `list`, so it changes nothing and needs no permission under
`azure-cost-guard`. It exits non-zero when it finds something. A clean run means the things written
down here are still true - not that the subscription is secure.

## The one finding that is a defect rather than a trade-off

### The database connection does not check who answers

`ConnectionStrings__Orbit`, as [azure-setup.md](azure-setup.md#2-configure-orbit-api) has recorded it
since the server was provisioned, ends with:

```
Ssl Mode=Require;Trust Server Certificate=true
```

Under Npgsql 10 (`Npgsql 10.0.3`, pinned in `Orbit.Api.csproj`) `Ssl Mode=Require` verifies the
server's certificate on its own. **`Trust Server Certificate=true` is what switches that verification
off.** The traffic stays encrypted, and the encryption stops proving anything about who is on the other
end: anything that can answer on `<server>.postgres.database.azure.com:5432` - a DNS answer that is
wrong on purpose, a compromised resolver, a future misrouting inside the platform - can present its own
certificate, and `orbit-api` will hand over the database credentials and every row it reads or writes.

It is not a likely attack inside an Azure region, which is presumably why it was set. It is also the
only protection on this list that costs nothing at all to have, so there is no trade being made - just
a setting that was easier to type when the server was new.

**The fix, once, on the running deployment:**

```bash
PG_SERVER_NAME=$(az postgres flexible-server list -g Orbit --query "[0].name" -o tsv)
# The password is not readable back from the secret - take it from where it was saved when the server
# was created (see azure-setup.md, step 1).
az containerapp secret set -n orbit-api -g Orbit --secrets \
  orbit-db-connection-string="Host=$PG_SERVER_NAME.postgres.database.azure.com;Port=5432;Database=orbit;Username=orbitadmin;Password=<the password>;Ssl Mode=VerifyFull"
az containerapp update -n orbit-api -g Orbit --set-env-vars \
  "ConnectionStrings__Orbit=secretref:orbit-db-connection-string"
```

`VerifyFull` checks the certificate chain *and* that the hostname matches, which is the setting to want.
Azure's PostgreSQL certificates chain to roots that `mcr.microsoft.com/dotnet/aspnet:10.0-alpine`
already trusts, so this is expected to just work - but **expected is not verified**, and a failure here
is a startup failure, because `Program.cs` applies migrations before the app answers anything:

```bash
az containerapp revision list -n orbit-api -g Orbit -o table   # the new revision must reach Healthy
az containerapp logs show -n orbit-api -g Orbit --tail 50      # a TLS failure says so plainly
```

If the new revision does not become healthy, put `Ssl Mode=Require;Trust Server Certificate=true` back
the same way, and the deployment is exactly where it started. Then the question is which root is
missing from the image, which is a smaller problem than it looks.

This page's copy of the connection string, and the one in `azure-setup.md`, are now written the safe
way, so a deployment set up from zero never gets the weaker one.

## Free, and worth doing

None of these costs anything. Each is one command.

### 1. A security contact, so an alert reaches a person

Defender raises alerts whether or not anybody is listening, and by default nobody is. This is also one
of the scored controls, so it is the cheapest point on the board.

```bash
az security contact create -n default --emails "<your address>" \
  --notifications-by-role '{"state":"On","roles":["Owner"]}' \
  --alert-notifications '{"state":"On","minimalSeverity":"High"}'
```

### 2. Turn off the registry's admin account

`orbitcontainerregistry` almost certainly still has the admin user enabled - it is on by default, and
it is a static username and password that grants push rights to everything Orbit deploys. The pipeline
does not use it: `main_orbit.yml` signs in with `az acr login` under the OIDC identity, and both
Container Apps pull with their own system-assigned identities.

```bash
az acr update -n orbitcontainerregistry --admin-enabled false
```

The audit script reports what it is set to now. If anything *does* break, it will be a local
`docker login` somebody does by hand, and `az acr login` replaces it.

### 3. Floor the storage accounts at TLS 1.2, over HTTPS only

```bash
az storage account update -n orbitdownloads -g Orbit \
  --min-tls-version TLS1_2 --https-only true
```

This does not stop anonymous reads of the APK - that is deliberate, see below - it stops the download
happening over plain HTTP or an obsolete TLS version.

### 4. More than one owner, with MFA

Two separate findings, both about the subscription rather than about Orbit, and both free:

- **MFA on every account with owner rights.** Entra ID security defaults turn this on for the whole
  tenant in one switch (Entra ID → Properties → Manage security defaults). On a personal subscription
  this is the single most valuable thing on this page: the account that can delete every resource and
  read every secret is protected by a password and nothing else until it is done.
- **A second owner.** One owner is a lockout waiting to happen - lose that account and the subscription
  goes with it. A break-glass second owner is a resilience fix that Defender happens to also score.

### 5. Delete the leftover storage account

`orbitb722` is left over from the abandoned SQLite-on-Azure-Files design and nothing uses it
([azure-setup.md](azure-setup.md#resource-inventory) says so). It is attack surface and a small charge
for nothing, and it is very likely one of the four unhealthy resources.

Deleting is irreversible and falls under [rule 6](../.claude/CLAUDE.md), so confirm it is empty first
and then decide deliberately:

```bash
az storage container list --account-name orbitb722 -o table    # expect nothing that matters
az storage account delete -n orbitb722 -g Orbit                # only after deciding
```

## Cheap enough to consider

**Key Vault for the secrets.** `Jwt__SigningKey`, the connection string and the VAPID private key live
as Container Apps secrets today - readable by anyone with rights over the Container App, with no audit
trail of who read what and no rotation story. Key Vault Standard bills about $0.03 per 10,000
operations, which at Orbit's volume is pennies a month, and Container Apps can reference a secret by
its Key Vault URL directly. It is already the intended direction:
[future-plan.md](future-plan.md#deployment) names it in the infrastructure-as-code item. The cost is
not money, it is one more moving part between a container start and a working app.

## What the remaining points cost

Approximate list prices, to check in the portal's pricing calculator rather than to trust from here -
and all of them recurring, every month, against a 20 € ceiling:

| What it would close | Roughly per month | Against the 20 € ceiling |
| --- | --- | --- |
| Defender CSPM (the "enable Defender" controls) | ~$5 per billable resource - six or so resources here | ~28 €, on its own - already over the ceiling |
| Defender for open-source relational databases (the PostgreSQL server) | ~$15 per server | ~14 € - most of the ceiling |
| Defender for Storage | ~$10 per account | ~9 € per account |
| Defender for Containers (image scanning) | ~$7 per vCore | ~7 € and up |
| A private endpoint for PostgreSQL | ~$7 each, plus a VNet-integrated Container Apps environment (workload profiles are not the free consumption plan) | a few euro, plus rebuilding the environment |
| Private link to the registry | ACR Premium, ~$50 | ~46 €, replacing a ~5 € Basic |
| A WAF in front of `orbit-web` | Front Door Premium or Application Gateway WAF v2 | ~250 € and up |
| Azure DDoS Network Protection | ~$2,944 | ~2,700 € - listed only to be dismissed |

Enabling just the Defender plans - the ones that raise the score fastest, because "Defender for X should
be enabled" is itself a control - lands somewhere around **45-60 € a month**, on a deployment whose
whole ceiling is 20 €. That is the honest reason this page stops where it does.

If any single one of them is worth buying here, it is **Defender for open-source relational databases**:
the database holds every user's data, it is the one resource with a public endpoint and a password, and
it is the cheapest of the plans. That is a decision to take deliberately, against the ceiling, not a
default.

## Deliberately accepted, and why

Each of these will be reported by Defender forever. They are choices, and the audit script prints them
as `decided` rather than as findings:

- **`orbitdownloads` serves the APK anonymously.** The repository is private, so a GitHub release asset
  needs a sign-in and a phone cannot follow that link. Anonymous read on one container holding one
  public build is the feature ([azure-setup.md](azure-setup.md#7-where-the-phone-apps-are-downloaded-from)).
  What is *not* accepted is anonymous access on any other account - the audit flags that.
- **PostgreSQL has a public endpoint.** It is firewalled to Azure's own address range and requires TLS.
  Closing it properly means a private endpoint and a VNet-integrated Container Apps environment, which
  means leaving the consumption plan - see the table above.
- **`orbit-api` has external ingress.** The phone calls it directly; the alternative cost every browser
  caller their real address and collapsed the rate limits into one bucket
  ([azure-setup.md](azure-setup.md#how-the-pieces-talk-to-each-other)). The application's own
  `FloodStop` limiter is what bounds that path.
- **nginx in `orbit-web` runs as root.** The image is `nginx:alpine`, whose master process starts as
  root to bind port 80; `orbit-api` already runs as `$APP_UID`. Moving to `nginxinc/nginx-unprivileged`
  means changing the container port and the ingress with it - a real change with a real test, not a
  setting, and it is in [future-plan.md](future-plan.md#deployment).

## So what is left to do

In order, and none of it needs a card:

1. Run `scripts/audit-azure-security.sh` and read the failing assessments - the real list, which this
   page is only predicting.
2. Fix the connection string (`Trust Server Certificate`), verify the revision goes healthy.
3. Security contact, registry admin off, storage TLS floor.
4. Security defaults (MFA) and a second owner.
5. Decide about `orbitb722`.

Then the score will still not be 100%, and the reason will be written down.
