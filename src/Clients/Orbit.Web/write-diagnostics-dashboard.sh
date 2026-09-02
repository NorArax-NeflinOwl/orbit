#!/bin/sh
# Points the "Debug logs" entry in the avatar menu at wherever this deployment's logs are read, by
# writing DIAGNOSTICS_HISTORY_URL and DIAGNOSTICS_LIVE_URL into the client's appsettings.json before
# nginx starts.
#
# Two of them, because the question has two answers and they are not interchangeable: what was logged
# (yesterday's failure, last week's flood) and what is happening this second. The menu asks which one is
# wanted when both are set, and opens the one there is when only one is - see DiagnosticsDashboard.
#
# Why at startup rather than at build time: where a deployment keeps its logs is not a property of the
# image. Locally that is the Aspire dashboard on http://localhost:18888; on Azure there is no Aspire
# dashboard, and the answers are portal addresses - the Application Insights resource for the history,
# the Container App's log stream for the live view. Either can be changed with
# `az containerapp update --set-env-vars` without rebuilding anything.
#
# Nothing is written for a variable that is unset, which is the ordinary state rather than a fault: the
# menu then offers one entry, or none at all, which is honest for a deployment that publishes neither -
# see MainLayout, where the entry is also gated on the Debug permission.
#
# Not secrets: they are links, and they land in a file every visitor can download. Whoever follows one
# still has to be signed in to the portal with rights to that resource.
set -e

settings=/usr/share/nginx/html/appsettings.json

# & and \ mean something to sed on the right-hand side, and | is the delimiter - a portal URL can carry
# all three.
write_setting() {
    key="$1"
    url="$2"
    if [ -z "$url" ]; then
        return 0
    fi

    escaped=$(printf '%s' "$url" | sed 's/[&|\\]/\\&/g')
    sed -i "s|\"$key\": *\"[^\"]*\"|\"$key\": \"$escaped\"|" "$settings"
    echo "$key set to $url."
}

write_setting DiagnosticsHistoryUrl "${DIAGNOSTICS_HISTORY_URL:-}"
write_setting DiagnosticsLiveUrl "${DIAGNOSTICS_LIVE_URL:-}"

if [ -z "${DIAGNOSTICS_HISTORY_URL:-}${DIAGNOSTICS_LIVE_URL:-}" ]; then
    echo "Neither DIAGNOSTICS_HISTORY_URL nor DIAGNOSTICS_LIVE_URL is set - the menu will not offer a link to this deployment's logs."
fi
