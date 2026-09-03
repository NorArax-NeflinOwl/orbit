// Shows points on a Leaflet map (loaded from the CDN in index.html, the same one the event editor's
// picker uses). What it draws comes from elsewhere - a recorded position comes from the device, via
// geolocation.js - and the one thing it reports back is where somebody pressed: a caller that hands in
// a .NET reference is told about each click, which is how the map page turns a point into a pin.

const mapInstancesByElementId = new Map();

// Warsaw, and only for a map with nothing on it yet - the map page keeps its map on screen whether or
// not anybody has recorded a position, so it needs somewhere to open. Matches mapPicker.js.
const defaultCenter = [52.2297, 21.0122];

/// Draws latitude/longitude with a marker labelled `label`, replacing whatever was on this element
/// before. Async because it waits for the element to have a size first - see waitForSize.
export async function showLocation(elementId, latitude, longitude, label) {
    return showLocations(elementId, [{ latitude, longitude, label }]);
}

/// Draws several points at once and frames them all. Used when other people are sharing their position
/// alongside the viewer's own: one map showing everyone beats one map each.
///
/// `dotNetHelper` is optional. Given one, every press on the map is reported to it as
/// `OnMapPressed(latitude, longitude)`; without one the map is read-only, which is what every other
/// caller wants.
export async function showLocations(elementId, points, dotNetHelper) {
    dispose(elementId);

    const element = document.getElementById(elementId);
    if (!element) {
        return;
    }

    const drawn = points ?? [];

    // Leaflet measures its container once, at creation, and lays the tile grid out from that. Blazor
    // adds this element in the same render pass that calls in here, so measuring now would measure a
    // box the browser hasn't laid out yet - which leaves the tiles covering a corner of the map and the
    // marker outside them. Waiting for a real height costs a frame and avoids the whole problem, rather
    // than correcting it afterwards: invalidateSize fixes the size Leaflet believes in, but a setView
    // back to the same centre and zoom is a no-op, so the tile grid keeps its stale origin.
    await waitForSize(element);

    // An empty map is still a map: the map page keeps one on screen from the moment it opens, so
    // somebody who has recorded nothing has somewhere to search rather than a blank panel.
    const start = drawn.length > 0 ? [drawn[0].latitude, drawn[0].longitude] : defaultCenter;
    const map = L.map(elementId).setView(start, drawn.length > 0 ? 14 : 6);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(map);

    const markersByKey = drawMarkers(map, drawn);

    if (drawn.length === 1) {
        // A single point is what the viewer asked to look at, so keep it centred and readable rather
        // than letting fitBounds pick an arbitrary zoom for a one-point box.
        map.setView([drawn[0].latitude, drawn[0].longitude], 14);
    } else if (drawn.length > 1) {
        map.fitBounds(drawn.map(point => [point.latitude, point.longitude]), { padding: [40, 40] });
    }

    if (dotNetHelper) {
        map.on('click', event => {
            // Wrapped before it leaves, for the reason mapPicker.js gives: the tile layer repeats the
            // world sideways and Leaflet keeps counting past the antimeridian, so a click two worlds
            // east of Warsaw arrives as longitude 741 rather than 21.
            const { lat, lng } = event.latlng.wrap();
            dotNetHelper.invokeMethodAsync('OnMapPressed', lat, lng);
        });
    }

    // Later size changes - a window resize, a panel opening - still need the map told about them.
    const resizeObserver = new ResizeObserver(() => map.invalidateSize({ animate: false }));
    resizeObserver.observe(element);

    mapInstancesByElementId.set(elementId, { map, resizeObserver, markersByKey });
}

/// Draws each point's marker and returns them keyed by point.key (falling back to its coordinates, for
/// showLocation's single-point callers, which never carry one) - the identity updateLocations matches
/// an old marker against a new point by, and focusOn looks a marker up by.
function drawMarkers(map, points) {
    const markersByKey = new Map();
    for (const point of points) {
        const marker = L.marker([point.latitude, point.longitude], iconFor(point.color)).addTo(map);
        if (point.label) {
            marker.bindPopup(point.label);
        }
        markersByKey.set(point.key ?? `${point.latitude},${point.longitude}`, marker);
    }

    return markersByKey;
}

/// Moves the markers on a map that is already there to wherever the given points now are, without
/// moving its own pan or zoom or touching a marker that has not moved - the light alternative to
/// showLocations' own dispose-and-rebuild, for whatever must not reset what the reader is looking at: a
/// live share's once-a-minute refresh, and a press on the map that has to draw its own pin without
/// losing the view the press itself was made on.
///
/// A marker already on the map for a point's key is moved in place rather than replaced, so anything
/// about the marker itself survives a refresh nobody asked for - an open popup someone pressed to read,
/// most of all. Only a key no longer present drops its marker; a new key gets a new one. Does nothing
/// before the map exists at all - the render that follows draws one fresh with whatever is current by
/// then.
export function updateLocations(elementId, points) {
    const instance = mapInstancesByElementId.get(elementId);
    if (!instance) {
        return;
    }

    const drawn = points ?? [];
    const next = new Map();

    for (const point of drawn) {
        const key = point.key ?? `${point.latitude},${point.longitude}`;
        const existing = instance.markersByKey.get(key);
        if (existing) {
            existing.setLatLng([point.latitude, point.longitude]);
            if (point.label) {
                existing.setPopupContent(point.label);
            }

            next.set(key, existing);
        } else {
            next.set(key, drawMarkers(instance.map, [point]).get(key));
        }
    }

    for (const [key, marker] of instance.markersByKey) {
        if (!next.has(key)) {
            marker.remove();
        }
    }

    instance.markersByKey = next;
}

/// Pans to one point on a map that is already there and opens its pin - see the "Sharing with you"
/// list, where pressing a name is how to look at where they are.
export function focusOn(elementId, key) {
    const instance = mapInstancesByElementId.get(elementId);
    const marker = instance?.markersByKey.get(key);
    if (!marker) {
        return;
    }

    instance.map.setView(marker.getLatLng(), Math.max(instance.map.getZoom(), 14));
    marker.openPopup();
}

/// A pin in the colour the caller asked for, so a name in the list and its pin on the map are tied
/// together by something the reader can see without clicking anything. Colours arrive as whatever CSS
/// the caller wrote - a var(--accent) included, which resolves here because the pin is in the page.
///
/// Leaflet's own default icon is a fixed image, so a coloured one has to be drawn: a divIcon is a plain
/// element the stylesheet can shape - see .map-pin in app.css.
function iconFor(color) {
    if (!color) {
        return {};
    }

    // Narrowed to what a colour is made of before it goes into markup. Every caller here is Orbit's own
    // code, so this guards against a mistake rather than an attacker - but a colour is never the place
    // to find out that string interpolation into HTML is how injection happens.
    const safeColor = String(color).replace(/[^a-zA-Z0-9 ,.%()#/-]/g, '');

    return {
        icon: L.divIcon({
            className: 'map-pin-icon',
            html: `<span class="map-pin" style="background:${safeColor}"></span>`,
            iconSize: [18, 18],
            iconAnchor: [9, 18],
            popupAnchor: [0, -16]
        })
    };
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
