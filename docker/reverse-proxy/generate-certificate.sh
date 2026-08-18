#!/bin/sh
# Generates a self-signed TLS certificate on first startup if one doesn't already exist in the
# persisted volume (see docker-compose.yml's reverse-proxy-certs volume) - later restarts reuse it
# instead of generating a new one every time, since browsers would otherwise need to re-accept the
# untrusted-certificate warning after every `docker compose up`.
#
# TLS_CERTIFICATE_HOSTNAME should be set to whatever hostname or LAN IP address other devices actually
# use to reach this proxy (e.g. "192.168.1.50" - see .env.example). A self-signed certificate only
# satisfies the browser's "secure context" requirement (see nginx.conf and this Dockerfile's header
# comment) for the exact host it was issued for. Every device still has to click through one
# untrusted-certificate warning, since there's no real certificate authority behind this certificate.
set -e

certificate_directory=/etc/nginx/certs
certificate_hostname="${TLS_CERTIFICATE_HOSTNAME:-localhost}"

if [ -f "$certificate_directory/orbit.crt" ] && [ -f "$certificate_directory/orbit.key" ]; then
    exit 0
fi

mkdir -p "$certificate_directory"

# A certificate's subjectAltName needs "IP:" for a raw IP address and "DNS:" for an actual hostname -
# a value made up of only digits and dots is assumed to be an IPv4 address.
case "$certificate_hostname" in
    *[!0-9.]*) subject_alternative_name="DNS:$certificate_hostname" ;;
    *) subject_alternative_name="IP:$certificate_hostname" ;;
esac

openssl req -x509 -nodes -newkey rsa:2048 -days 3650 \
    -keyout "$certificate_directory/orbit.key" \
    -out "$certificate_directory/orbit.crt" \
    -subj "/CN=$certificate_hostname" \
    -addext "subjectAltName=$subject_alternative_name,DNS:localhost,IP:127.0.0.1"
