#!/bin/bash
# Stops everything in the Orbit resource group that bills by the hour, and starts it again afterwards.
#
# This is the enforcing half of the spending limit described in info/azure-setup.md, "Cost limits".
# An Azure budget only sends email: nothing in a pay-as-you-go subscription stops spending on its own,
# so reaching the monthly ceiling means somebody - or a runbook - runs this.
#
# What it stops:
#   - the PostgreSQL Flexible Server, whose compute is the largest steady charge (storage keeps
#     billing, and the database itself survives untouched)
#   - both Container Apps, by closing their ingress and letting them scale to zero
#
# What it cannot stop: the container registry, the storage account and Log Analytics bill whether or
# not anything is running - see "What the stop cannot stop" in info/azure-setup.md. Nothing here
# deletes anything.
#
# Usage:
#   scripts/stop-azure-compute.sh --status      # what is running, and this month's spend
#   scripts/stop-azure-compute.sh --dry-run     # print every command, change nothing
#   scripts/stop-azure-compute.sh               # stop, after asking
#   scripts/stop-azure-compute.sh --yes         # stop without asking - what an automated caller uses
#   scripts/stop-azure-compute.sh --resume      # bring it all back
set -uo pipefail

resource_group="Orbit"
budget_name="orbit-monthly-budget"

# Ingress and scale exactly as "5. Confirm ingress" in info/azure-setup.md records them, because
# --resume puts these values back rather than remembering what it found. A deployment that has moved
# away from them has to be corrected here first.
api_target_port=8080
api_minimum_replicas=1
web_target_port=80
web_minimum_replicas=0

mode="stop"
is_dry_run=false
is_confirmed=false
failures=0

while [ $# -gt 0 ]; do
    case "$1" in
        --status) mode="status"; shift ;;
        --resume) mode="resume"; shift ;;
        --dry-run) is_dry_run=true; shift ;;
        --yes|-y) is_confirmed=true; shift ;;
        -h|--help) sed -n '2,22p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "Unknown option: $1. Try --help."; exit 2 ;;
    esac
done

say() { echo "$(date '+%Y-%m-%d %H:%M:%S')  $*"; }

# Every mutating call goes through here, so --dry-run has one place to stop at, the log shows exactly
# what ran, and one failure does not abandon the resources after it - a half-stopped deployment keeps
# billing, which is the one outcome this script exists to avoid.
run_azure() {
    if [ "$is_dry_run" = true ]; then
        say "would run: az $*"
        return 0
    fi
    say "az $*"
    if ! az "$@" >/dev/null; then
        say "FAILED: az $*"
        failures=$((failures + 1))
    fi
}

# The server carries a random suffix (see info/azure-setup.md), so it is looked up rather than named.
find_postgres_server() {
    az postgres flexible-server list -g "$resource_group" --query "[0].name" -o tsv 2>/dev/null
}

report_spend() {
    local subscription_id budget_amount spent unit
    subscription_id=$(az account show --query id -o tsv)
    read -r budget_amount spent unit <<<"$(az rest --method get \
        --url "https://management.azure.com/subscriptions/$subscription_id/providers/Microsoft.Consumption/budgets/$budget_name?api-version=2023-05-01" \
        --query "properties.[amount, currentSpend.amount, currentSpend.unit]" -o tsv 2>/dev/null)"
    if [ -z "${budget_amount:-}" ]; then
        say "No budget named $budget_name on this subscription - see info/azure-setup.md, \"Cost limits\"."
        return
    fi
    say "Budget $budget_name: ${spent:-?} of ${budget_amount} ${unit:-} spent this month."
}

report_status() {
    local server="$1" application ingress minimum_replicas replicas_running
    say "Subscription: $(az account show --query name -o tsv)"
    if [ -n "$server" ]; then
        say "PostgreSQL $server: $(az postgres flexible-server show -g "$resource_group" -n "$server" --query state -o tsv)"
    else
        say "No PostgreSQL Flexible Server found in $resource_group."
    fi
    for application in orbit-api orbit-web; do
        ingress=$(az containerapp show -n "$application" -g "$resource_group" --query "properties.configuration.ingress.external" -o tsv 2>/dev/null)
        minimum_replicas=$(az containerapp show -n "$application" -g "$resource_group" --query "properties.template.scale.minReplicas" -o tsv 2>/dev/null)
        replicas_running=$(az containerapp replica list -n "$application" -g "$resource_group" --query "length(@)" -o tsv 2>/dev/null)
        say "$application: ingress ${ingress:-disabled}, min-replicas ${minimum_replicas:-?}, replicas running ${replicas_running:-?}"
    done
    report_spend
}

stop_everything() {
    local server="$1" application
    say "Stopping. Both apps go dark and the database stops answering; nothing is deleted."
    if [ -n "$server" ]; then
        run_azure postgres flexible-server stop -g "$resource_group" -n "$server"
    else
        say "No PostgreSQL Flexible Server found in $resource_group - skipping the database."
    fi
    # Closing the ingress is what makes scaling to zero hold: orbit-api is kept awake by the phone
    # syncing against it, so min-replicas 0 on its own would never empty it.
    for application in orbit-api orbit-web; do
        run_azure containerapp ingress disable -n "$application" -g "$resource_group"
        run_azure containerapp update -n "$application" -g "$resource_group" --min-replicas 0
    done
    say "Stopped. Azure starts a stopped Flexible Server again by itself after seven days, so check back."
}

resume_everything() {
    local server="$1"
    # The database first: orbit-api applies migrations at startup and never becomes healthy without it.
    if [ -n "$server" ]; then
        run_azure postgres flexible-server start -g "$resource_group" -n "$server"
    fi
    run_azure containerapp update -n orbit-api -g "$resource_group" --min-replicas "$api_minimum_replicas"
    run_azure containerapp ingress enable -n orbit-api -g "$resource_group" --type external --target-port "$api_target_port" --transport auto
    run_azure containerapp update -n orbit-web -g "$resource_group" --min-replicas "$web_minimum_replicas"
    run_azure containerapp ingress enable -n orbit-web -g "$resource_group" --type external --target-port "$web_target_port" --transport auto
    say "Back up. max-replicas was left alone, so whatever the autoscale workflow last chose still holds."
    say "Verify it the way a deploy is verified - see info/azure-setup.md, \"Verifying a deploy\"."
}

if ! az account show >/dev/null 2>&1; then
    say "Not signed in to Azure - 'az account show' fails. Run 'az login' first."
    exit 1
fi

postgres_server=$(find_postgres_server)

if [ "$mode" = "status" ]; then
    report_status "$postgres_server"
    exit 0
fi

if [ "$is_confirmed" = false ] && [ "$is_dry_run" = false ]; then
    if [ "$mode" = "stop" ]; then
        read -r -p "Stop Orbit's Azure compute now? Everyone using Orbit loses it until --resume. [y/N] " answer
    else
        read -r -p "Start Orbit's Azure compute again, and the billing with it? [y/N] " answer
    fi
    case "$answer" in
        [Yy]) ;;
        *) say "Nothing done."; exit 0 ;;
    esac
fi

if [ "$mode" = "stop" ]; then
    stop_everything "$postgres_server"
else
    resume_everything "$postgres_server"
fi

if [ "$failures" -gt 0 ]; then
    say "$failures command(s) failed, so the deployment is half-changed and may still be billing. Re-run, or finish by hand."
    exit 1
fi
