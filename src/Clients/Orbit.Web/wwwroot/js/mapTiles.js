// The one place a map's background comes from, and the one place that asks whether it may.
//
// The tiles are OpenStreetMap's, fetched by the browser one square at a time - so a reader who has said
// "do not share my personal information" is telling their own address to openstreetmap.org, and roughly
// where they are looking, every time a map moves. That is the one third-party request Orbit cannot
// simply serve itself: a world of map tiles is not something to keep in wwwroot.
//
// So it is the one that is gated. The answer lives on the account (see User.KeepsThirdPartiesOut) and
// is mirrored into local storage under a strictly-necessary key, because a map is drawn long before any
// API call could come back and a map that flashed the world and then blanked would be worse than
// either answer.
window.OrbitMapTiles = (function () {
    const MIRROR_KEY = 'orbit-keep-third-parties-out';

    function areAllowed() {
        try {
            return window.localStorage.getItem(MIRROR_KEY) !== 'true';
        } catch {
            // No storage to read the answer from, so nothing said otherwise.
            return true;
        }
    }

    return {
        areAllowed,

        /// Remembers the account's answer for the next first paint - called after the client reads it.
        remember(keepsThirdPartiesOut) {
            try {
                window.localStorage.setItem(MIRROR_KEY, keepsThirdPartiesOut ? 'true' : 'false');
            } catch {
                // A browser holding nothing will ask the server again next time, which is correct.
            }
        },

        /// Adds the background to a map, or leaves it plain and says why. The markers, the pins and the
        /// panning all work either way - what is missing is the picture behind them.
        addTo(map) {
            if (!areAllowed()) {
                const plain = L.control({ position: 'bottomleft' });
                plain.onAdd = function () {
                    const box = L.DomUtil.create('div', 'map-tiles-off');
                    box.textContent = 'Map images are off because you asked Orbit not to share anything with other sites.';
                    return box;
                };
                plain.addTo(map);
                return null;
            }

            return L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                maxZoom: 19
            }).addTo(map);
        }
    };
})();
