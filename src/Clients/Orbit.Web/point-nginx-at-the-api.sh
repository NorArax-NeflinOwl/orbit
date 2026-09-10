#!/bin/sh
# Tells nginx which orbit-api to proxy /api/ and /health to, by writing ORBIT_API_HOST into the nginx
# config before nginx starts.
#
# Only nginx.azure.conf asks: it names the API by its Container App FQDN, and that name belongs to one
# environment. A second environment - the production one planned beside the test one that runs today -
# reuses the same image and the same config, and only this one value differs, so it is answered at
# startup from the Container App's environment rather than baked in at build time. The config carries
# the placeholder __ORBIT_API_HOST__ where the name goes; nginx.conf (docker compose) has none, and this
# script leaves a config with no placeholder alone.
#
# Unset means the test environment, which is the deployment every existing pipeline run builds for -
# so nothing has to be configured to keep working as it did. A production app sets it with
# `az containerapp update -n orbit-web -g <group> --set-env-vars ORBIT_API_HOST=<its orbit-api's internal FQDN>`.
#
# Two things nginx.azure.conf's own comments insist on still hold after substitution: the value lands
# as a literal, not an nginx variable (a variable in proxy_pass changes how the path is forwarded), and
# it is the *internal* name, because the public one makes every browser user look like the egress NAT
# to the API - see info/azure-setup.md.
set -e

config=/etc/nginx/conf.d/default.conf
placeholder=__ORBIT_API_HOST__
default_host=orbit-api.internal.victorioustree-36ad82ca.polandcentral.azurecontainerapps.io

if ! grep -q "$placeholder" "$config"; then
    exit 0
fi

host="${ORBIT_API_HOST:-$default_host}"

# A hostname and nothing else. This lands inside proxy_pass and a Host header, and an environment
# variable is the one input here nobody reviews before it runs.
case "$host" in
    *[!A-Za-z0-9.-]*|"")
        echo "ORBIT_API_HOST is not a hostname: '$host'. Refusing to start nginx with it." >&2
        exit 1
        ;;
esac

sed -i "s|$placeholder|$host|g" "$config"

if [ -z "${ORBIT_API_HOST:-}" ]; then
    echo "ORBIT_API_HOST is not set - proxying /api/ to the test environment's orbit-api, $host."
else
    echo "Proxying /api/ to $host."
fi
