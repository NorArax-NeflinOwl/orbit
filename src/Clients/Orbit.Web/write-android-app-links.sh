#!/bin/sh
# Writes the Digital Asset Links file Android checks before it will open Orbit's own links in the app
# instead of in a browser - https://<this host>/.well-known/assetlinks.json.
#
# What it is for: Orbit's Android app declares an intent filter for /s/ links on this host (see
# MainActivity). From Android 12 on, such a filter routes nothing on its own - the system fetches this
# file over HTTPS and checks that it names the app's package and the certificate the installed build was
# signed with. Without it the link opens in a browser, and the reader has to allow the app by hand under
# Settings > Apps > Orbit > Open by default.
#
# ANDROID_APP_SHA256 is the signing certificate's SHA-256 fingerprint, colon-separated and upper case -
# the one printed by:
#
#   keytool -list -v -keystore <release.keystore> -alias <alias>
#
# It is not a secret: Android publishes it to every device that installs the app. It is deployment
# configuration all the same, because it belongs to whichever keystore the release was signed with.
#
# Nothing is written when it is unset, which is an ordinary state rather than a fault - the same
# arrangement the Google Maps key has on the app side. A file naming no certificate would be worse than
# no file: Android would fetch it, fail the check, and stop asking.
set -e

fingerprint="${ANDROID_APP_SHA256:-}"
package="${ANDROID_APP_PACKAGE:-com.orbitmaui.android}"

if [ -z "$fingerprint" ]; then
    echo "No ANDROID_APP_SHA256 set - Orbit links will open in a browser rather than in the app."
    exit 0
fi

directory=/usr/share/nginx/html/.well-known
mkdir -p "$directory"

cat > "$directory/assetlinks.json" <<JSON
[{
  "relation": ["delegate_permission/common.handle_all_urls"],
  "target": {
    "namespace": "android_app",
    "package_name": "$package",
    "sha256_cert_fingerprints": ["$fingerprint"]
  }
}]
JSON

chmod a+r "$directory/assetlinks.json"
echo "Wrote assetlinks.json for $package."
