// Loads Google Identity Services on demand and renders its sign-in button. Kept out of index.html so a
// deployment without a Google client id never contacts Google at all - see ClientFlagsDto.GoogleClientId.
(function () {
    "use strict";

    const SCRIPT_SOURCE = "https://accounts.google.com/gsi/client";
    let scriptPromise = null;

    function loadScript() {
        if (scriptPromise) {
            return scriptPromise;
        }

        scriptPromise = new Promise((resolve, reject) => {
            const script = document.createElement("script");
            script.src = SCRIPT_SOURCE;
            script.async = true;
            script.onload = resolve;
            script.onerror = () => reject(new Error("Failed to load Google Identity Services."));
            document.head.appendChild(script);
        });
        return scriptPromise;
    }

    window.OrbitGoogleSignIn = {
        // dotNetRef receives the ID token through OnGoogleCredential - the token is the whole point of
        // this flow, and the server is what actually validates it (see GoogleIdentityVerifier).
        renderButton: async (containerId, clientId, dotNetRef) => {
            await loadScript();
            const container = document.getElementById(containerId);
            if (!container) {
                return;
            }

            window.google.accounts.id.initialize({
                client_id: clientId,
                // Google keeps this callback for as long as its own script lives, which outlasts the
                // component that handed us the reference. A credential arriving after that - a late
                // click, One Tap choosing for itself - would reject against a reference .NET has already
                // dropped, and land as an unhandled rejection nobody can act on.
                callback: (response) => {
                    dotNetRef.invokeMethodAsync("OnGoogleCredential", response.credential).catch(() => {
                        // The page that asked for this is gone. Nothing to report and nobody to report to.
                    });
                }
            });
            window.google.accounts.id.renderButton(container, { theme: "outline", size: "large", width: 280 });
        },

        /// Stops Google calling back, and takes its button out of the page. Called before the component
        /// drops its .NET reference, so the callback above is gone before the thing it points at is.
        dispose: (containerId) => {
            if (window.google?.accounts?.id) {
                window.google.accounts.id.cancel();
                window.google.accounts.id.disableAutoSelect();
            }

            const container = document.getElementById(containerId);
            if (container) {
                container.replaceChildren();
            }
        }
    };
})();
