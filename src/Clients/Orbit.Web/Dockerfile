# syntax=docker/dockerfile:1
#
# Build context is the repository root (see docker-compose.yml), so this can reach the sibling
# Orbit.Contracts, Orbit.Core and Orbit.Localization projects.

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Inherited by every project below, so a property added to it applies here as well as locally. Copied
# rather than left out: without it a build in this image quietly ignores whatever it says, and the
# difference only shows up as "works on my machine".
#
# LICENSE comes along because the client serves it: the footer's licence link opens it on this site
# rather than on GitHub, and the file is linked into wwwroot from here - see Orbit.Web.csproj.
COPY Directory.Build.props version.props LICENSE ./

COPY src/Shared/Orbit.Contracts/Orbit.Contracts.csproj src/Shared/Orbit.Contracts/
COPY src/Shared/Orbit.Core/Orbit.Core.csproj src/Shared/Orbit.Core/
COPY src/Shared/Orbit.Localization/Orbit.Localization.csproj src/Shared/Orbit.Localization/
COPY src/Clients/Orbit.Web/Orbit.Web.csproj src/Clients/Orbit.Web/
RUN dotnet restore src/Clients/Orbit.Web/Orbit.Web.csproj

COPY src/Shared/Orbit.Contracts/ src/Shared/Orbit.Contracts/
COPY src/Shared/Orbit.Core/ src/Shared/Orbit.Core/
COPY src/Shared/Orbit.Localization/ src/Shared/Orbit.Localization/
COPY src/Clients/Orbit.Web/ src/Clients/Orbit.Web/

# What this build calls itself, worked out by the pipeline from the history - see ci/compute-version.sh.
# Empty for a local `docker compose build`, which is honest: nothing counted the days, so the footer
# says 0.0.0-dev rather than a number that looks real. See Orbit.Core's OrbitVersion.
ARG ORBIT_VERSION=

RUN dotnet publish src/Clients/Orbit.Web/Orbit.Web.csproj \
    --configuration Release \
    --no-restore \
    ${ORBIT_VERSION:+-p:InformationalVersion="$ORBIT_VERSION"} \
    --output /app/publish

# Blazor WebAssembly ships as static files; there's no .NET runtime to host it, so the final image
# is plain nginx instead of an aspnet base.
FROM nginx:alpine AS runtime

# Selects which nginx config this image ships: nginx.conf (default) terminates TLS itself and proxies
# to orbit-api over the local Docker network, for docker-compose; nginx.azure.conf instead proxies to
# orbit-api's Container App FQDN and skips TLS, since Azure Container Apps' ingress already terminates
# it - see main_orbit.yml, which is the only build that overrides this.
ARG NGINX_CONF_FILE=nginx.conf

COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/Clients/Orbit.Web/${NGINX_CONF_FILE} /etc/nginx/conf.d/default.conf
COPY src/Clients/Orbit.Web/nginx-app-locations.conf /etc/nginx/snippets/app-locations.conf

# `dotnet publish`'s output carries whatever file permissions the build stage's umask produced,
# which for plain wwwroot static files (as opposed to the specially processed _framework/ assets)
# can be too restrictive for nginx's worker processes to read - they drop privileges to the
# unprivileged "nginx" user rather than running as root. Without this, requests for those files
# (e.g. favicon.ico, wwwroot/js/*.js) fail with 403 Forbidden instead of being served.
RUN chmod -R a+rX /usr/share/nginx/html

# Needed at container startup to generate the self-signed TLS certificate (see
# generate-certificate.sh) - not included in the base image. This is what makes browsers treat the
# app as a secure context, which the chat's end-to-end encryption (the Web Crypto API's
# crypto.subtle) requires in order to work at all - see README.md's "Accessing Orbit.Web from
# another device on your network" section.
RUN apk add --no-cache openssl

# The base image's own docker-entrypoint.sh runs every executable script under /docker-entrypoint.d/
# before starting nginx - no need to override the image's ENTRYPOINT/CMD to hook this in.
COPY src/Clients/Orbit.Web/generate-certificate.sh /docker-entrypoint.d/10-generate-certificate.sh
RUN chmod +x /docker-entrypoint.d/10-generate-certificate.sh

# What lets a phone open Orbit's own links in the app rather than in a browser - see the script for
# what Android does with it and what happens when the deployment has not set a fingerprint.
COPY src/Clients/Orbit.Web/write-android-app-links.sh /docker-entrypoint.d/20-write-android-app-links.sh
RUN chmod +x /docker-entrypoint.d/20-write-android-app-links.sh

EXPOSE 80 443
