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
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/Clients/Orbit.Web/nginx.conf /etc/nginx/conf.d/default.conf

# `dotnet publish`'s output carries whatever file permissions the build stage's umask produced,
# which for plain wwwroot static files (as opposed to the specially processed _framework/ assets)
# can be too restrictive for nginx's worker processes to read - they drop privileges to the
# unprivileged "nginx" user rather than running as root. Without this, requests for those files
# (e.g. favicon.ico, wwwroot/js/*.js) fail with 403 Forbidden instead of being served.
RUN chmod -R a+rX /usr/share/nginx/html

EXPOSE 80
