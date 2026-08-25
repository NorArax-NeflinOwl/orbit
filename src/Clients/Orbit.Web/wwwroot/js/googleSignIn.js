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
                callback: (response) => dotNetRef.invokeMethodAsync("OnGoogleCredential", response.credential)
            });
            window.google.accounts.id.renderButton(container, { theme: "outline", size: "large", width: 280 });
        }
    };
})();
