// On-device diagnostics: keeps the last MAX_ENTRIES warning/error log lines in localStorage so a person
// without access to devtools (most notably on a phone) can still retrieve what went wrong - see the
// "Copy error details" link on #blazor-error-ui in index.html, and PersistentLoggerProvider.cs which
// mirrors .NET ILogger output in here via appendLog. The window.onerror/unhandledrejection listeners
// below are registered independently of Blazor and .NET, so this still captures plain JS/interop failures
// (e.g. a rejected pushManager.subscribe() promise) even if the Blazor app itself never gets involved.
(function () {
    "use strict";

    const STORAGE_KEY = "orbit.clientLogs";
    const MAX_ENTRIES = 200;

    function readEntries() {
        try {
            const raw = window.localStorage.getItem(STORAGE_KEY);
            return raw ? JSON.parse(raw) : [];
        } catch {
            return [];
        }
    }

    function writeEntries(entries) {
        try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(entries));
        } catch {
            // Storage full, disabled, or unavailable (e.g. private browsing) - diagnostics are
            // best-effort only and must never break the app.
        }
    }

    function appendLog(level, category, message, exceptionText) {
        const entries = readEntries();
        entries.push({
            t: new Date().toISOString(),
            lvl: level,
            cat: category,
            msg: message,
            ex: exceptionText || null
        });
        while (entries.length > MAX_ENTRIES) {
            entries.shift();
        }
        writeEntries(entries);
    }

    function getLogsAsText() {
        const entries = readEntries();
        if (entries.length === 0) {
            return "No logs recorded.";
        }

        return entries
            .map(function (entry) {
                let line = "[" + entry.t + "] " + entry.lvl + " " + entry.cat + ": " + entry.msg;
                if (entry.ex) {
                    line += "\n" + entry.ex;
                }
                return line;
            })
            .join("\n\n");
    }

    // linkElement is optional - passed by the onclick handler in index.html so this can show brief
    // inline feedback ("Copied ✓") without needing a separate toast/alert mechanism.
    async function copyLogsToClipboard(linkElement) {
        const text = getLogsAsText();
        const originalLabel = linkElement ? linkElement.textContent : null;

        function showFeedback(label) {
            if (!linkElement) {
                return;
            }
            linkElement.textContent = label;
            setTimeout(function () {
                linkElement.textContent = originalLabel;
            }, 2000);
        }

        try {
            await navigator.clipboard.writeText(text);
            showFeedback("Copied ✓");
        } catch (error) {
            appendLog("Error", "ClientLogging", "Failed to copy logs to clipboard", String(error));
            showFeedback("Failed to copy");
        }
    }

    window.addEventListener("error", function (event) {
        appendLog(
            "Error",
            "window.onerror",
            event.message || "Unknown error",
            event.error && event.error.stack ? event.error.stack : null);
    });

    window.addEventListener("unhandledrejection", function (event) {
        const reason = event.reason;
        const message = reason && reason.message ? reason.message : String(reason);
        const stack = reason && reason.stack ? reason.stack : null;
        appendLog("Error", "unhandledrejection", message, stack);
    });

    function clearEntries() {
        try {
            window.localStorage.removeItem(STORAGE_KEY);
        } catch {
            // Same best-effort handling as writeEntries - storage being unavailable must never break the app.
        }
    }

    window.OrbitClientLogging = {
        appendLog: appendLog,
        getLogsAsText: getLogsAsText,
        copyLogsToClipboard: copyLogsToClipboard,
        // Read-only access to the structured entries, for the Notifications panel's exception list
        // (see MainLayout.razor) to render and copy them individually rather than only as one big blob.
        getEntries: readEntries,
        // Backs the notifications panel's "Clear", which empties this browser's own error list alongside
        // the server-side feed - the panel presents them as one list, so clearing has to cover both.
        clearEntries: clearEntries
    };
})();
