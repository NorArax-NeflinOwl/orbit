// Shows one point on a Leaflet map (loaded from the CDN in index.html, the same one the event editor's
// picker uses). Read-only on purpose: this window displays a recorded location, it doesn't set one -
// that comes from the device, via geolocation.js.

const mapInstancesByElementId = new Map();

/// Draws latitude/longitude with a marker labelled `label`, replacing whatever was on this element
/// before. Async because it waits for the element to have a size first - see waitForSize.
export async function showLocation(elementId, latitude, longitude, label) {
    return showLocations(elementId, [{ latitude, longitude, label }]);
}

/// Draws several points at once and frames them all. Used when other people are sharing their position
/// alongside the viewer's own: one map showing everyone beats one map each.
export async function showLocations(elementId, points) {
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
    if (!points || points.length === 0) {
        return;
    }

    await waitForSize(element);

    const map = L.map(elementId).setView([points[0].latitude, points[0].longitude], 14);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(map);

    for (const point of points) {
        const marker = L.marker([point.latitude, point.longitude]).addTo(map);
        if (point.label) {
            marker.bindPopup(point.label);
        }
    }

    if (points.length === 1) {
        // A single point is what the viewer asked to look at, so keep it centred and readable rather
        // than letting fitBounds pick an arbitrary zoom for a one-point box.
        map.setView([points[0].latitude, points[0].longitude], 14);
    } else {
        map.fitBounds(points.map(point => [point.latitude, point.longitude]), { padding: [40, 40] });
    }

    // Later size changes - a window resize, the sidebar collapsing - still need the map told about them.
    const resizeObserver = new ResizeObserver(() => map.invalidateSize({ animate: false }));
    resizeObserver.observe(element);

    mapInstancesByElementId.set(elementId, { map, resizeObserver });
}

/// Puts the map's frame full screen, or takes it back out. Nothing here tracks the state: leaving can
/// also happen through Esc or a back gesture, which this code never sees, so the button's own label is
/// decided by CSS :fullscreen instead of by anything remembered here.
///
/// The map itself needs no telling - the ResizeObserver set up in showLocations already reports the new
/// size, which is the same path a window resize takes.
export async function toggleFullscreen(frameElement) {
    if (document.fullscreenElement) {
        await document.exitFullscreen();
        return false;
    }

    if (!frameElement?.requestFullscreen) {
        // Older iOS Safari has no Fullscreen API on ordinary elements. Saying so beats a button that
        // looks live and does nothing.
        return false;
    }

    await frameElement.requestFullscreen();
    return true;
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
