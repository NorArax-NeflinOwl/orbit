# Setting up a machine and building Orbit with Docker Compose

This covers everything needed to go from a fresh Windows or macOS machine to a running Orbit stack
via `docker compose up`. No secret values are included below - every placeholder must be filled in
with your own value; see "Configure environment variables" for how to generate them.

## 1. Prerequisites

Both platforms need:

- **Git**, to clone the repository.
- **Docker Desktop**, which provides both the Docker engine and Docker Compose v2 (the `docker
  compose` command used throughout this document and in `docker-compose.yml`).

### Windows

1. Install [Docker Desktop for Windows](https://www.docker.com/products/docker-desktop/).
2. During or after installation, make sure the **WSL 2** backend is enabled (Docker Desktop's
   installer offers this by default on current Windows 10/11) - Settings > General > "Use the WSL 2
   based engine".
3. Install [Git for Windows](https://git-scm.com/downloads/win) if it isn't already installed.
4. This repository ships a `.gitattributes` that forces LF line endings for `*.sh` files regardless
   of your local `core.autocrlf` setting, so shell scripts copied into Linux containers (e.g.
   `src/Clients/Orbit.Web/generate-certificate.sh`) work correctly - no extra Git configuration is
   needed for this on a fresh clone.

### macOS

1. Install [Docker Desktop for Mac](https://www.docker.com/products/docker-desktop/) (choose the
   Apple Silicon or Intel build matching your Mac).
2. Git ships with Xcode Command Line Tools; if it isn't already installed, running `git` in a
   terminal for the first time prompts you to install them, or install directly with:
   ```
   xcode-select --install
   ```

## 2. Clone the repository

```
git clone <this repository's URL>
cd orbit
```

## 3. Configure environment variables

Docker Compose reads secrets and settings from a `.env` file at the repository root, which is
gitignored and must never be committed. Create it from the template:

```
cp .env.example .env
```

Then open `.env` and fill in the values below. Only `JWT_SIGNING_KEY` is required to start the
stack - everything else can be left blank and the corresponding feature just logs a warning and
skips itself (see `README.md`'s "Calendar event reminders" and "Push notifications" sections).

| Variable | Required | How to get a value |
| --- | --- | --- |
| `JWT_SIGNING_KEY` | Yes | A random string, at least 32 characters. Generate one with `openssl rand -base64 48` (Git Bash/WSL on Windows, or Terminal on macOS). |
| `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER_NAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS` | No | Credentials for whatever SMTP provider you want calendar reminder emails sent through. Leave blank to skip email delivery. |
| `VAPID_PUBLIC_KEY`, `VAPID_PRIVATE_KEY`, `VAPID_SUBJECT` | No | Generate a key pair with `npx web-push generate-vapid-keys` (needs Node.js/npm). `VAPID_SUBJECT` is a `mailto:` address or HTTPS URL identifying you to push services. Leave blank to skip push delivery. |
| `WEB_CLIENT_LAN_ORIGIN` | No | Only needed if you call `Orbit.Api` directly from a different origin than `orbit-web`'s own (e.g. a `dotnet run` dev server) - see `docker-compose.yml`'s `WebClientOrigins` comment. |
| `TLS_CERTIFICATE_HOSTNAME` | No | Set to this machine's LAN IP (e.g. `192.168.1.50`) only if you need to reach Orbit.Web from another device on your network - see `README.md`'s "Accessing Orbit.Web from another device on your network" section. Leave unset to use `localhost`. |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | No | Only used when deploying to Azure Container Apps. Leave unset for local development - traces go to the local Aspire dashboard instead. |

## 4. Build and start the stack

From the repository root:

```
docker compose up --build
```

This builds the `orbit-api` and `orbit-web` images and starts three containers: `orbit-api`,
`orbit-web`, and `orbit-aspire-dashboard`. First build takes a few minutes (restoring and
publishing two .NET projects); subsequent builds are faster thanks to Docker's layer cache.

Once it's up:

- Web client: `https://localhost:8443` (the one real entry point - `http://localhost:8080`
  redirects here automatically)
- API: `http://localhost:8081` (`/health`, `/health/ready`, `/health/live`, `/api/*`)
- Aspire dashboard (live API logs and traces): `http://localhost:18888`

To stop the stack: `Ctrl+C`, or `docker compose down` from another terminal. `docker compose down
-v` additionally removes the named volumes (`orbit-api-logs`, `orbit-web-certs`) - do this if you
want a fresh self-signed certificate or log volume on the next start.

## 5. Trust the self-signed HTTPS certificate

`orbit-web` generates a self-signed certificate on first startup so it can serve HTTPS (required
for the chat's end-to-end encryption and for Service Worker registration - see "Push
notifications" below). Your browser will warn that this certificate isn't trusted the first time
you load `https://localhost:8443`, and Service Worker registration (needed for push notifications)
needs a step beyond just clicking through that warning - see `info/instructions.md` in this same
folder for the three ways to resolve that.

## 6. Troubleshooting

- `docker compose logs orbit-api` / `docker compose logs orbit-web` - see what a container actually
  logged, including any startup exception.
- `docker compose ps` - check which containers are running vs. exited.
- If `orbit-web` fails to start with a container exiting immediately, or a container reports
  `dependency failed to start`, check the logs above first - `orbit-web` only starts once
  `orbit-api`'s health check passes (see `docker-compose.yml`'s `depends_on` condition).
- `docker compose build orbit-web --no-cache` (or `orbit-api`) forces a full rebuild of one service
  without using Docker's build cache, useful after changing a file that a cached layer might not
  have picked up.
