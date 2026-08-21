# Trusting the local dev TLS certificate (Windows + Chrome)

`orbit-web`'s `docker-entrypoint.d/10-generate-certificate.sh` generates a self-signed certificate
(`orbit.crt` / `orbit.key`, stored in the `orbit-web-certs` Docker volume) on first startup. Loading
`https://localhost:8443` works after clicking through Chrome's "Your connection is not private"
warning, but Chrome does **not** extend that same click-through exception to Service Worker
registration - `navigator.serviceWorker.register()` fails with:

```
Failed to register a ServiceWorker ... An SSL certificate error occurred when fetching the script.
```

Since push notifications (and any other feature relying on the service worker) require a
successfully registered service worker, this has to be fixed before that code can be exercised
locally. Pick one of the three options below.

## Option 1 - Chrome flag for localhost (quickest, dev-only)

Only works when `TLS_CERTIFICATE_HOSTNAME` is left at its default (`localhost`) - see
`docker-compose.yml`. Does not help when reaching the app from another device on the LAN by IP
address.

1. Open `chrome://flags/#allow-insecure-localhost` in Chrome.
2. Set **Allow invalid certificates for resources loaded from localhost** to **Enabled**.
3. Relaunch Chrome (the flags page has a "Relaunch" button).
4. Reload `https://localhost:8443` and retry the action that registers the service worker (e.g.
   reload the page - `MainLayout.razor` registers it on startup).

## Option 2 - Locally-trusted certificate via mkcert

Produces a certificate Chrome trusts fully, with no warnings and no per-machine flag - also works
from another device's IP on the LAN. Requires installing a local certificate authority on this
Windows machine, which needs administrator rights.

1. Install mkcert (pick one):
   ```
   choco install mkcert
   ```
   or
   ```
   scoop install mkcert
   ```

   If there was some problems like: "The term 'choco' is not recognized as the name of a cmdlet..."
   then try this:
   > Set-ExecutionPolicy AllSigned
   > Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
   All from https://chocolatey.org/install.

2. Install the local CA into Windows' and Chrome's trust stores (run once per machine, needs an
   elevated/administrator prompt):
   ```
   mkcert -install
   ```
3. Generate a certificate covering every hostname you use to reach `orbit-web` (add your LAN IP if
   you test from another device - it must match `TLS_CERTIFICATE_HOSTNAME` in `.env`):
   ```
   mkcert localhost 127.0.0.1 ::1 <your-LAN-IP>
   ```
   This produces two files, e.g. `localhost+3.pem` (certificate) and `localhost+3-key.pem` (key).
4. Get these files into the container in place of the ones `generate-certificate.sh` would
   otherwise generate, at `/etc/nginx/certs/orbit.crt` and `/etc/nginx/certs/orbit.key`. The
   currently committed setup uses a named Docker volume (`orbit-web-certs`) for that path rather
   than a bind mount, so the simplest way today is to copy the files into the running container and
   restart it:
   ```
   docker cp localhost+3.pem orbit-web:/etc/nginx/certs/orbit.crt
   docker cp localhost+3-key.pem orbit-web:/etc/nginx/certs/orbit.key
   docker compose restart orbit-web
   ```
   (`generate-certificate.sh` only generates a certificate when `orbit.crt`/`orbit.key` don't
   already exist, so it will leave the mkcert-issued files alone on restart.)

   A more permanent setup - switching `orbit-web-certs` to a bind mount pointing at a folder with
   the mkcert output, so this survives `docker compose down -v` - is a small `docker-compose.yml`
   change; ask if you want that done as a follow-up.

## Option 3 - Manually import the existing self-signed certificate

No mkcert install, but a manual step to repeat every time the certificate is regenerated (e.g.
after `docker compose down -v` removes the `orbit-web-certs` volume).

1. Copy the certificate out of the running container:
   ```
   docker cp orbit-web:/etc/nginx/certs/orbit.crt .
   ```
2. Import it into Windows' trusted root store, in an elevated PowerShell prompt:
   ```
   Import-Certificate -FilePath .\orbit.crt -CertStoreLocation Cert:\LocalMachine\Root
   ```
3. Restart Chrome and reload `https://localhost:8443`.
