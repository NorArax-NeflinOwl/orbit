# syntax=docker/dockerfile:1
#
# TLS-terminating reverse proxy in front of orbit-web and orbit-api. This is what makes browsers treat
# the app as a secure context, which the chat's end-to-end encryption (the Web Crypto API's
# crypto.subtle) requires in order to work at all - see README.md's "Accessing Orbit.Web from another
# device on your network" section for why plain HTTP over a LAN IP address doesn't satisfy that.

FROM nginx:alpine

# Needed at container startup to generate the self-signed certificate (see generate-certificate.sh) -
# not included in the base image.
RUN apk add --no-cache openssl

COPY nginx.conf /etc/nginx/conf.d/default.conf

# The base image's own docker-entrypoint.sh runs every executable script under /docker-entrypoint.d/
# before starting nginx - no need to override the image's ENTRYPOINT/CMD to hook this in.
COPY generate-certificate.sh /docker-entrypoint.d/10-generate-certificate.sh
RUN chmod +x /docker-entrypoint.d/10-generate-certificate.sh

EXPOSE 443
