#!/bin/sh
# Points the "Debug logs" entry in the avatar menu at wherever this deployment's logs are read, by
# writing DIAGNOSTICS_DASHBOARD_URL into the client's appsettings.json before nginx starts.
#
# Why at startup rather than at build time: where a deployment keeps its logs is not a property of the
# image. Locally that is the Aspire dashboard on http://localhost:18888; on Azure there is no Aspire
# dashboard at all, and the answer is a portal address - the Application Insights resource, or the
# Container App's log stream. Either can be changed with `az containerapp update --set-env-vars`
# without rebuilding anything.
#
# Nothing is written when it is unset, which is the ordinary state rather than a fault: the menu then
# offers no entry, which is honest for a deployment that publishes no dashboard - see
# DiagnosticsDashboard and MainLayout, where the entry is also gated on the Debug permission.
#
# Not a secret: it is a link, and it lands in a file every visitor can download. Whoever follows it
# still has to be signed in to the portal with rights to that resource.
set -e

url="${DIAGNOSTICS_DASHBOARD_URL:-}"
settings=/usr/share/nginx/html/appsettings.json

if [ -z "$url" ]; then
    echo "No DIAGNOSTICS_DASHBOARD_URL set - the menu will not offer a link to this deployment's logs."
    exit 0
fi

# & and \ mean something to sed on the right-hand side, and | is the delimiter - a portal URL can
# carry all three.
escaped=$(printf '%s' "$url" | sed 's/[&|\\]/\\&/g')
sed -i "s|\"DiagnosticsDashboardUrl\": *\"[^\"]*\"|\"DiagnosticsDashboardUrl\": \"$escaped\"|" "$settings"
echo "Debug logs will open $url."
