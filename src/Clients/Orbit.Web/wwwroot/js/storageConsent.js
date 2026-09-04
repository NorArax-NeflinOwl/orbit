// What this browser is allowed to keep. Orbit sets no cookies at all - everything it remembers about a
// reader lives in localStorage - so "Manage cookies" in the footer manages this.
//
// The gate is here, at the storage itself, rather than in each of the fifteen places that write: a
// service that has to remember to ask permission is a service that will one day forget, and half of
// these writes happen in JS modules while the other half come through Blazor's interop as
// "localStorage.setItem". Both go through Storage.prototype, so one wrapper covers both and there is a
// single list of what belongs to which category.
//
// Loaded as a plain script before everything else in index.html, because clientLogging.js starts
// writing the moment an error happens and Blazor starts reading tokens the moment it boots.
window.OrbitStorageConsent = (function () {
    const CONSENT_KEY = 'orbit-storage-consent';

    const NECESSARY = 'necessary';
    const PREFERENCES = 'preferences';
    const DIAGNOSTICS = 'diagnostics';

    // Being signed in, being read in your own language, and the record of what you have consented to -
    // including this one. None of it can be declined: declining it is indistinguishable from signing
    // out, and a consent record that consent could erase would ask again on every visit.
    const necessaryKeys = [
        'orbit.authToken',
        'orbit.refreshToken',
        'orbit-language',
        'orbit-allow-location',
        'orbit-allow-google-extras',
        CONSENT_KEY
    ];

    // How this reader has arranged Orbit: the theme, the accent, what is pinned, what is put away, how
    // each list is sorted. Losing it costs nothing but the arranging.
    const preferenceKeys = [
        'orbit-theme',
        'orbit-accent-hue',
        'orbit-dashboard-pins',
        'orbit-dashboard-hidden-cards',
        'orbit-dashboard-card-filters',
        'orbit-checklist-views',
        'orbit-calendar-list-sort-order',
        'orbit-calendar-list-shows-everything',
        'orbit-conversation-pins',
        'orbit-warehouse-order'
    ];
    const preferencePrefixes = ['orbit-panel-', 'orbit-task-list-'];

    // What Orbit noticed going wrong on this device. Never leaves it on its own - see the Privacy page -
    // but it is still a record of what somebody was doing when it broke, so it is declinable.
    const diagnosticsKeys = ['orbit.clientLogs', 'orbit-diagnostics-mode'];

    function categoryOf(key) {
        if (necessaryKeys.includes(key)) {
            return NECESSARY;
        }
        if (diagnosticsKeys.includes(key)) {
            return DIAGNOSTICS;
        }
        // Anything unrecognised is read as a preference rather than as necessary: a key added later is
        // far more likely to be something somebody arranged than something they cannot sign in without,
        // and the narrower reading is the safe one to be wrong about.
        return PREFERENCES;
    }

    function isOurs(key) {
        return preferenceKeys.includes(key)
            || necessaryKeys.includes(key)
            || diagnosticsKeys.includes(key)
            || preferencePrefixes.some(prefix => key.startsWith(prefix));
    }

    // Everything is kept until somebody says otherwise. Orbit stores nothing on a first visit that a
    // reader has not asked for by using it, so there is no banner across the page and nothing to
    // agree to before reading - the footer offers the choice to whoever goes looking for it.
    let allowed = { [NECESSARY]: true, [PREFERENCES]: true, [DIAGNOSTICS]: true };

    function load() {
        try {
            const stored = JSON.parse(window.localStorage.getItem(CONSENT_KEY) ?? 'null');
            if (stored && typeof stored === 'object') {
                allowed[PREFERENCES] = stored[PREFERENCES] !== false;
                allowed[DIAGNOSTICS] = stored[DIAGNOSTICS] !== false;
            }
        } catch {
            // A browser with storage blocked outright, or a record written by an older build. Either
            // way the defaults above stand, and nothing can be written anyway.
        }
    }

    // The wrapper. sessionStorage shares this prototype and is not what any of this is about, so it is
    // let through untouched.
    const nativeSetItem = Storage.prototype.setItem;
    Storage.prototype.setItem = function (key, value) {
        if (this === window.localStorage && !allowed[categoryOf(String(key))]) {
            return;
        }

        return nativeSetItem.call(this, key, value);
    };

    function forget(category) {
        const doomed = [];
        for (let index = 0; index < window.localStorage.length; index++) {
            const key = window.localStorage.key(index);
            if (key !== null && isOurs(key) && categoryOf(key) === category) {
                doomed.push(key);
            }
        }

        doomed.forEach(key => window.localStorage.removeItem(key));
    }

    load();

    return {
        /// What is currently allowed, for the dialog that offers the choice.
        get() {
            return { preferences: allowed[PREFERENCES], diagnostics: allowed[DIAGNOSTICS] };
        },

        /// Records the choice and acts on it at once: a category turned off is cleared here rather than
        /// left to decay, because "off" that leaves yesterday's data in place is not off.
        set(preferences, diagnostics) {
            allowed[PREFERENCES] = preferences !== false;
            allowed[DIAGNOSTICS] = diagnostics !== false;
            nativeSetItem.call(
                window.localStorage,
                CONSENT_KEY,
                JSON.stringify({ [PREFERENCES]: allowed[PREFERENCES], [DIAGNOSTICS]: allowed[DIAGNOSTICS] }));

            if (!allowed[PREFERENCES]) {
                forget(PREFERENCES);
            }
            if (!allowed[DIAGNOSTICS]) {
                forget(DIAGNOSTICS);
            }
        },

        /// Every key of Orbit's this browser is holding, by category - so the dialog can say how much
        /// there is rather than only what there could be.
        counts() {
            const counted = { necessary: 0, preferences: 0, diagnostics: 0 };
            for (let index = 0; index < window.localStorage.length; index++) {
                const key = window.localStorage.key(index);
                if (key !== null && isOurs(key)) {
                    counted[categoryOf(key)]++;
                }
            }

            return counted;
        }
    };
})();
