// Shows one point on a Leaflet map (loaded from the CDN in index.html, the same one the event editor's
// picker uses). Read-only on purpose: this window displays a recorded location, it doesn't set one -
// that comes from the device, via geolocation.js.

const mapInstancesByElementId = new Map();

/// Draws latitude/longitude with a marker labelled `label`, replacing whatever was on this element
/// before. Async because it waits for the element to have a size first - see waitForSize.
export async function showLocation(elementId, latitude, longitude, label) {
    dispose(elementId);

    const element = document.getElementById(elementId);
    if (!element) {
        return;
    }

    // Leaflet measures its container once, at creation, and lays the tile grid out from that. Blazor
    // adds this element in the same render pass that calls in here, so measuring now would measure a
    // box the browser hasn't laid out yet - which leaves the tiles covering a corner of the map and the
    // marker outside them. Waiting for a real height costs a frame and avoids the whole problem, rather
    // than correcting it afterwards: invalidateSize fixes the size Leaflet believes in, but a setView
    // back to the same centre and zoom is a no-op, so the tile grid keeps its stale origin.
    await waitForSize(element);

    const map = L.map(elementId).setView([latitude, longitude], 14);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(map);

    const marker = L.marker([latitude, longitude]).addTo(map);
    if (label) {
        marker.bindPopup(label).openPopup();
    }

    // Later size changes - a window resize, the sidebar collapsing - still need the map told about them.
    const resizeObserver = new ResizeObserver(() => map.invalidateSize({ animate: false }));
    resizeObserver.observe(element);

    mapInstancesByElementId.set(elementId, { map, resizeObserver });
}

export function dispose(elementId) {
    const instance = mapInstancesByElementId.get(elementId);
    if (instance) {
        instance.resizeObserver.disconnect();
        instance.map.remove();
        mapInstancesByElementId.delete(elementId);
    }
}

/// Resolves once the element has a height, or after a short grace period regardless - a map drawn into
/// a box that stays collapsed is still better than a page that waits forever for one that never will.
function waitForSize(element, timeoutMs = 1000) {
    if (element.clientHeight > 0) {
        return Promise.resolve();
    }

    return new Promise(resolve => {
        const observer = new ResizeObserver(() => {
            if (element.clientHeight > 0) {
                finish();
            }
        });
        const timeout = setTimeout(finish, timeoutMs);

        function finish() {
            clearTimeout(timeout);
            observer.disconnect();
            resolve();
        }

        observer.observe(element);
    });
}
