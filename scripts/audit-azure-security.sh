#!/bin/bash
# Reports how Orbit's Azure resources are actually configured, against the hardening decisions recorded
# in info/azure-security.md.
#
# Read-only throughout - every call is a show or a list, so this is safe to run at any time and needs
# no permission under the azure-cost-guard skill. It changes nothing and costs nothing.
#
# Two things it does that reading the portal on a phone does not: it pulls Defender for Cloud's own
# failing assessments (the ones behind the secure score), and it checks the handful of settings this
# deployment has a recorded decision about - so a drift away from that decision is visible rather than
# waiting to be noticed.
#
# It is a checklist, not a scanner. A clean run means the things written down here are as they were
# decided, not that the subscription is secure.
#
# Usage:
#   scripts/audit-azure-security.sh              # everything
#   scripts/audit-azure-security.sh --defender   # only Defender's own findings
#   scripts/audit-azure-security.sh --resources  # only this deployment's settings
set -uo pipefail

resource_group="Orbit"
sections="all"
findings=0

while [ $# -gt 0 ]; do
    case "$1" in
        --defender) sections="defender"; shift ;;
        --resources) sections="resources"; shift ;;
        -h|--help) sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "Unknown option: $1. Try --help."; exit 2 ;;
    esac
done

heading() { echo; echo "== $* =="; }

# Three verdicts, and only one of them counts as a finding: "decided" is for a setting that is
# deliberately not what a hardening guide would want, and says where that decision is written down.
report_good() { printf '  ok       %s\n' "$*"; }
report_decided() { printf '  decided  %s\n' "$*"; }
report_finding() { printf '  FIX      %s\n' "$*"; findings=$((findings + 1)); }

# Most of this file is one setting compared against one expected value, so it goes through here rather
# than through a chain of && and ||, where a helper returning non-zero would silently invert a verdict.
check_setting() { # description actual expected
    if [ "$2" = "$3" ]; then
        report_good "$1 ($2)"
    else
        report_finding "$1 - is '${2:-unreadable}', expected '$3'."
    fi
}

# Azure's security commands are partly preview and vary by CLI version, so a missing one is reported as
# unavailable rather than being allowed to look like a clean result.
query_azure() { # jmespath-query az-arguments...
    local query="$1"; shift
    az "$@" --query "$query" -o tsv 2>/dev/null
}

if ! az account show >/dev/null 2>&1; then
    echo "Not signed in to Azure - 'az account show' fails. Run 'az login' first."
    exit 1
fi

subscription_id=$(az account show --query id -o tsv)
echo "Subscription: $(az account show --query name -o tsv) ($subscription_id)"

audit_defender() {
    heading "Defender for Cloud - what the secure score is counting"
    local score assessments contacts
    score=$(query_azure "[0].properties.score.percentage" security secure-scores list)
    if [ -n "$score" ]; then
        echo "  Secure score: $score (as a fraction of 1)"
    else
        echo "  Secure score unavailable - 'az security secure-scores list' is preview and may need"
        echo "  'az extension add --name security', or the portal: Defender for Cloud > Secure score."
    fi

    assessments=$(az security assessment list \
        --query "[?properties.status.code=='Unhealthy'].properties.displayName" -o tsv 2>/dev/null | sort -u)
    if [ -n "$assessments" ]; then
        echo "  Failing assessments - these are the missing points:"
        echo "$assessments" | sed 's/^/    - /'
    else
        echo "  No failing assessments returned. Either nothing is failing, or the assessment API is"
        echo "  not answering for this subscription - check the portal before believing the first one."
    fi

    # A security contact is free, is itself a scored control, and is the only reason anybody hears about
    # a high-severity alert at all.
    contacts=$(query_azure "[].email" security contact list)
    if [ -n "$contacts" ]; then
        report_good "Security contact set: $contacts"
    else
        report_finding "No security contact on the subscription - nobody is emailed about an alert."
    fi
}

audit_registry() {
    heading "Container registry"
    local admin_enabled public_access
    admin_enabled=$(query_azure "adminUserEnabled" acr show -n orbitcontainerregistry)
    public_access=$(query_azure "publicNetworkAccess" acr show -n orbitcontainerregistry)
    case "$admin_enabled" in
        true) report_finding "orbitcontainerregistry has the admin account enabled - a static password nothing uses. The pipeline signs in with OIDC (az acr login)." ;;
        false) report_good "Admin account disabled; the pipeline uses OIDC." ;;
        *) echo "  orbitcontainerregistry: could not read adminUserEnabled." ;;
    esac
    [ "$public_access" = "Enabled" ] && report_decided "Registry reachable from the public network - Basic SKU has no private link; see info/azure-security.md."
}

audit_database() {
    heading "PostgreSQL"
    local server public_access secure_transport rules
    server=$(query_azure "[0].name" postgres flexible-server list -g "$resource_group")
    if [ -z "$server" ]; then
        echo "  No Flexible Server found in $resource_group."
        return
    fi
    public_access=$(query_azure "network.publicNetworkAccess" postgres flexible-server show -g "$resource_group" -n "$server")
    secure_transport=$(query_azure "value" postgres flexible-server parameter show -g "$resource_group" --server-name "$server" --name require_secure_transport)
    rules=$(query_azure "[].[name, startIpAddress, endIpAddress]" postgres flexible-server firewall-rule list -g "$resource_group" --server-name "$server")

    check_setting "$server requires TLS" "$secure_transport" "on"
    [ "$public_access" = "Enabled" ] && report_decided "$server has a public endpoint, firewalled to Azure IPs - a private endpoint needs a VNet-integrated environment; see info/azure-security.md."

    echo "  Firewall rules:"
    if [ -z "$rules" ]; then
        report_finding "$server has no firewall rule at all - the API cannot reach it. This has gone missing before; see azure-setup.md."
        return
    fi
    echo "$rules" | sed 's/^/    /'
    # 0.0.0.0-0.0.0.0 is Azure's "services within Azure" rule, not the whole internet. Anything else is
    # a real address range somebody opened, and is worth a second look every time.
    local wider_rules
    wider_rules=$(echo "$rules" | grep -vc "0\.0\.0\.0.0\.0\.0\.0")
    [ "$wider_rules" -gt 0 ] && report_finding "$wider_rules firewall rule(s) open an address range beyond Azure's own - check the list above."
}

audit_storage() {
    heading "Storage accounts"
    local account public_blobs minimum_tls https_only shared_key
    for account in $(query_azure "[].name" storage account list -g "$resource_group"); do
        public_blobs=$(query_azure "allowBlobPublicAccess" storage account show -n "$account" -g "$resource_group")
        minimum_tls=$(query_azure "minimumTlsVersion" storage account show -n "$account" -g "$resource_group")
        https_only=$(query_azure "supportsHttpsTrafficOnly" storage account show -n "$account" -g "$resource_group")
        shared_key=$(query_azure "allowSharedKeyAccess" storage account show -n "$account" -g "$resource_group")

        echo "  $account:"
        check_setting "$account requires HTTPS" "$https_only" "true"
        check_setting "$account floors TLS at 1.2" "$minimum_tls" "TLS1_2"
        if [ "$public_blobs" = "true" ]; then
            if [ "$account" = "orbitdownloads" ]; then
                report_decided "orbitdownloads serves the APK anonymously on purpose - see info/azure-security.md."
            else
                report_finding "$account allows anonymous blob reads and has no reason to."
            fi
        fi
        [ "$shared_key" = "true" ] && report_decided "$account still accepts its account keys; the release workflow writes the APK with a role assignment instead."
    done
}

audit_container_apps() {
    heading "Container Apps"
    local application transport allow_insecure plain_secrets
    for application in orbit-api orbit-web; do
        transport=$(query_azure "properties.configuration.ingress.transport" containerapp show -n "$application" -g "$resource_group")
        allow_insecure=$(query_azure "properties.configuration.ingress.allowInsecure" containerapp show -n "$application" -g "$resource_group")
        # A Key Vault reference shows a keyVaultUrl; a plain Container Apps secret shows none.
        plain_secrets=$(query_azure "length(properties.configuration.secrets[?keyVaultUrl==null])" containerapp show -n "$application" -g "$resource_group")

        echo "  $application:"
        check_setting "$application refuses plain HTTP (transport ${transport:-unknown})" "$allow_insecure" "false"
        if [ -n "${plain_secrets:-}" ] && [ "${plain_secrets}" != "0" ]; then
            report_decided "$application holds $plain_secrets secret(s) in Container Apps rather than Key Vault - see info/azure-security.md."
        fi
    done
    echo
    echo "  Not checkable from here: whether ConnectionStrings__Orbit still carries"
    echo "  'Trust Server Certificate=true'. Secret values cannot be read back - see"
    echo "  info/azure-security.md, \"The database connection does not check who answers\"."
}

case "$sections" in
    defender) audit_defender ;;
    resources) audit_registry; audit_database; audit_storage; audit_container_apps ;;
    all) audit_defender; audit_registry; audit_database; audit_storage; audit_container_apps ;;
esac

echo
if [ "$findings" -eq 0 ]; then
    echo "No findings against what info/azure-security.md decided. That is not the same as secure."
    exit 0
fi

# Non-zero on findings, so this can gate something later without the exit code having to be reinterpreted.
echo "$findings finding(s). Each one is either a fix to apply or a decision to write down."
exit 1
