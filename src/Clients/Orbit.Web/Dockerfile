# syntax=docker/dockerfile:1
#
# Build context is the repository root (see docker-compose.yml), so this can reach the sibling
# Orbit.Contracts project.

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY src/Shared/Orbit.Contracts/Orbit.Contracts.csproj src/Shared/Orbit.Contracts/
COPY src/Clients/Orbit.Web/Orbit.Web.csproj src/Clients/Orbit.Web/
RUN dotnet restore src/Clients/Orbit.Web/Orbit.Web.csproj

COPY src/Shared/Orbit.Contracts/ src/Shared/Orbit.Contracts/
COPY src/Clients/Orbit.Web/ src/Clients/Orbit.Web/

RUN dotnet publish src/Clients/Orbit.Web/Orbit.Web.csproj \
    --configuration Release \
    --no-restore \
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

EXPOSE 80 443
