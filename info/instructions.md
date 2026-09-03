# Trusting the local dev TLS certificate (Windows + Chrome)

> See also: [macOS + Brave/Chrome equivalents](#trusting-the-local-dev-tls-certificate-macos--bravechrome) and
> [Push notifications in Brave](#push-notifications-in-brave-any-os) further down this file.

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
   ```
   Set-ExecutionPolicy AllSigned
   Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
   ```
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

# Trusting the local dev TLS certificate (macOS + Brave/Chrome)

Same underlying problem as above - a self-signed certificate the browser doesn't trust breaks Service
Worker registration even after you've clicked through the page-load warning - just with macOS/Homebrew
commands and Brave's `brave://` URLs instead of Windows/Chrome ones. Brave is Chromium-based, so
everything here (flags, certificate trust, `docker cp`) works the same way it does in Chrome; only the
`brave://` scheme differs from `chrome://`.

## Option 1 - Browser flag for localhost (quickest, dev-only)

Same caveat as the Windows version: only works when `TLS_CERTIFICATE_HOSTNAME` is left at `localhost`,
and doesn't help when reaching the app from another device on the LAN by IP address.

1. Open `brave://flags/#allow-insecure-localhost` (Chrome: `chrome://flags/#allow-insecure-localhost`).
2. Set **Allow invalid certificates for resources loaded from localhost** to **Enabled**.
3. Relaunch the browser (the flags page has a "Relaunch" button).
4. Reload `https://localhost:8443` and retry the action that registers the service worker (e.g. reload
   the page - `MainLayout.razor` registers it on startup).

## Option 2 - Locally-trusted certificate via mkcert

No administrator/`sudo` password needed on macOS in the common case - mkcert installs its local CA into
your user's login keychain, not the system one.

1. Install mkcert via Homebrew:
   ```
   brew install mkcert
   ```
   Only if you also test in Firefox (which keeps its own certificate store instead of using macOS's):
   ```
   brew install nss
   ```
2. Install the local CA into macOS's trust store (run once per machine):
   ```
   mkcert -install
   ```
   If Brave/Chrome still shows "Not Secure" after this - some macOS versions don't fully apply the
   trust settings mkcert requests - open **Keychain Access**, find the certificate named
   `mkcert <your-name>@<your-machine>`, double-click it, expand **Trust**, and set **When using this
   certificate** to **Always Trust**.
3. Generate a certificate covering every hostname you use to reach `orbit-web` (add your LAN IP if you
   test from another device - it must match `TLS_CERTIFICATE_HOSTNAME` in `.env`):
   ```
   mkcert localhost 127.0.0.1 ::1 <your-LAN-IP>
   ```
   This produces two files, e.g. `localhost+3.pem` (certificate) and `localhost+3-key.pem` (key).
4. Put the two files where the container will read them, under the names it expects:
   ```
   mkdir -p ~/.orbit-certs
   cp localhost+3.pem ~/.orbit-certs/orbit.crt
   cp localhost+3-key.pem ~/.orbit-certs/orbit.key
   chmod 600 ~/.orbit-certs/orbit.key
   ```
5. Point `orbit-web` at that folder, so the certificate lives outside Docker and survives
   `docker compose down -v`. Compose reads `docker-compose.override.yml` on top of
   `docker-compose.yml` automatically, and that file is gitignored, so this changes your machine and
   nobody else's:
   ```
   cat > docker-compose.override.yml <<'YAML'
   services:
     orbit-web:
       volumes:
         - ${HOME}/.orbit-certs:/etc/nginx/certs
   YAML
   docker compose up -d orbit-web
   ```
   The bind mount replaces the committed `orbit-web-certs` volume for that path, and
   `generate-certificate.sh` only issues a certificate when `orbit.crt`/`orbit.key` are missing - so
   it leaves the mkcert-issued pair alone.

   Without this step the certificate lives in the `orbit-web-certs` volume, `docker compose down -v`
   deletes it, and the next start issues a fresh self-signed one that no browser trusts. What that
   looks like from the app is worth knowing, because it does not look like a certificate problem: the
   page loads from cache and then every `_framework/*.wasm` and every `/api` call fails with
   `TypeError: Failed to fetch` and no status code. Nothing is wrong with the server - `curl -k`
   against it answers 200 for the same files.

   To check what is actually being served:
   ```
   echo | openssl s_client -connect localhost:8443 -servername localhost 2>/dev/null | openssl x509 -noout -issuer -dates
   ```
   An issuer of `mkcert development CA` is the trusted one; a self-signed certificate names itself.

## Option 3 - Manually import the existing self-signed certificate

No mkcert install, but a manual step to repeat every time the certificate is regenerated (e.g. after
`docker compose down -v` removes the `orbit-web-certs` volume).

1. Copy the certificate out of the running container:
   ```
   docker cp orbit-web:/etc/nginx/certs/orbit.crt .
   ```
2. Import it into your login keychain from the terminal:
   ```
   security add-trusted-cert -d -r trustRoot -k ~/Library/Keychains/login.keychain-db ./orbit.crt
   ```
   Or via the Keychain Access app: double-click `orbit.crt` to add it to the login keychain, then
   double-click the imported certificate, expand **Trust**, and set **When using this certificate** to
   **Always Trust**.
3. Restart Brave (or Chrome) and reload `https://localhost:8443`.

# Push notifications in Brave (any OS)

Separate from the certificate trust issue above: Brave ships with Google's push messaging
infrastructure disabled by default, for privacy reasons, so Web Push doesn't work in Brave out of the
box regardless of certificate setup - this would still block Orbit's "Enable push notifications" toggle
(`MainLayout.razor`) even with a fully-trusted certificate in place.

1. Open `brave://settings/privacy`.
2. Near the bottom, enable **Use Google Services for Push Messaging**.
3. Restart Brave.
4. Reload `https://localhost:8443`, click Orbit's push-notification toggle, and accept the browser's
   notification-permission prompt.
5. If notifications still don't arrive, check macOS's own permission gate: **System Settings ->
   Notifications -> Brave Browser** must have notifications allowed there too - a browser-level
   "Allow" only satisfies the browser's own permission model, not macOS's.
