# syntax=docker/dockerfile:1
#
# Build context is the repository root (see docker-compose.yml), so this can reach the sibling
# projects Orbit.Api references.

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# .csproj files only, copied first: `dotnet restore` is only re-run when a project reference or
# package version actually changes, not on every source edit.
COPY src/Shared/Orbit.Core/Orbit.Core.csproj src/Shared/Orbit.Core/
COPY src/Shared/Orbit.Contracts/Orbit.Contracts.csproj src/Shared/Orbit.Contracts/
COPY src/Server/Orbit.Data/Orbit.Data.csproj src/Server/Orbit.Data/
COPY src/Server/Orbit.GoogleIntegration/Orbit.GoogleIntegration.csproj src/Server/Orbit.GoogleIntegration/
COPY src/Server/Orbit.Api/Orbit.Api.csproj src/Server/Orbit.Api/
RUN dotnet restore src/Server/Orbit.Api/Orbit.Api.csproj

COPY src/Shared/Orbit.Core/ src/Shared/Orbit.Core/
COPY src/Shared/Orbit.Contracts/ src/Shared/Orbit.Contracts/
COPY src/Server/Orbit.Data/ src/Server/Orbit.Data/
COPY src/Server/Orbit.GoogleIntegration/ src/Server/Orbit.GoogleIntegration/
COPY src/Server/Orbit.Api/ src/Server/Orbit.Api/

# What this build calls itself, worked out by the pipeline from the history - see ci/compute-version.sh.
# Empty for a local `docker compose build`: nothing counted the days, so it reports 0.0.0-dev rather than
# a number that looks real. See Orbit.Core's OrbitVersion.
ARG ORBIT_VERSION=

RUN dotnet publish src/Server/Orbit.Api/Orbit.Api.csproj \
    --configuration Release \
    --no-restore \
    ${ORBIT_VERSION:+-p:InformationalVersion="$ORBIT_VERSION"} \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Alpine's base image ships without the IANA time zone database, so without tzdata the container's
# clock - and every DateTimeOffset.Now-based log timestamp Serilog writes - is always UTC regardless
# of the TZ variable below. Orbit is a Poland-only product for now (see the Polish-language client),
# so logs are written in the operator's own local time rather than UTC.
RUN apk add --no-cache tzdata
ENV TZ=Europe/Warsaw

# Logs are volume-mounted (see docker-compose.yml); owning the directory before switching off root
# means the non-root $APP_UID user (built into the base image) can actually write to the volume.
RUN mkdir -p /app/logs && chown -R $APP_UID:$APP_UID /app/logs
USER $APP_UID

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# wget ships with Alpine's busybox, so no extra package is needed. /health/live only reports whether
# the process itself is responding, not its dependencies, matching what a container orchestrator
# should restart on.
# BusyBox wget only understands a small option set (-q, --spider, -O, -T, ...) - unlike GNU wget it
# has no --no-verbose or --tries, and passing them makes the check fail immediately every time.
HEALTHCHECK --interval=10s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Orbit.Api.dll"]
