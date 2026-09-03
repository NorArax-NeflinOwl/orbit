// Wraps Leaflet (loaded from a CDN in index.html) so CalendarEventEditor.razor can open an
// interactive map, let the user click a point, and report the picked coordinates back to .NET -
// Blazor WebAssembly has no built-in mapping component, and Leaflet needs a real DOM element plus its
// own click-event wiring that only plain JS can provide.

const mapInstancesByElementId = new Map();

// The one pin on each map, kept here rather than in initializeMapPicker's closure so that moveMarker
// can reach it too - a click and a found address have to move the same pin, not add a second one.
const markersByElementId = new Map();

// Warsaw - used as the map's starting view only when the event has no location yet.
const defaultCenter = [52.2297, 21.0122];

export function initializeMapPicker(elementId, dotNetHelper, initialLatitude, initialLongitude) {
    disposeMapPicker(elementId);

    const hasInitialPosition = initialLatitude !== null && initialLongitude !== null;
    const startPosition = hasInitialPosition ? [initialLatitude, initialLongitude] : defaultCenter;
    const map = L.map(elementId).setView(startPosition, hasInitialPosition ? 15 : 6);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(map);

    if (hasInitialPosition) {
        markersByElementId.set(elementId, L.marker(startPosition).addTo(map));
    }

    map.on('click', event => {
        // The pin is deliberately not moved here. A map is dragged and zoomed by pressing it, so a
        // press is easy to make by accident, and one that moved the pin rewrote where something happens
        // before anybody had agreed to it. What a press does is ask - see LocationPickerOverlay, which
        // moves the pin itself once the question below it is answered.

        // The coordinates reported to .NET are wrapped first: the tile layer repeats the world horizontally, and Leaflet keeps panning past the
        // antimeridian in the same continuous coordinate space rather than wrapping - a click two
        // worlds east of Warsaw arrives here as longitude 741, not 21. CalendarEvent rejects anything
        // outside -180..180, so sending it unwrapped turned an ordinary map click into a failed save.
        const { lat, lng } = event.latlng.wrap();
        dotNetHelper.invokeMethodAsync('OnMapLocationPicked', lat, lng);
    });

    mapInstancesByElementId.set(elementId, map);
}

// Puts the pin somewhere the reader named rather than clicked - see LocationPickerOverlay's address
// search. The map moves with it: a pin dropped outside the visible area is a pin nobody can check.
// Nothing is reported back to .NET here, because .NET is what asked for this in the first place - it
// already knows both the coordinates and the address they came from.
export function moveMarker(elementId, latitude, longitude) {
    const map = mapInstancesByElementId.get(elementId);
    if (!map) {
        return;
    }

    const position = [latitude, longitude];
    let marker = markersByElementId.get(elementId);
    if (marker) {
        marker.setLatLng(position);
    } else {
        marker = L.marker(position).addTo(map);
        markersByElementId.set(elementId, marker);
    }

    map.setView(position, 16);
}

export function disposeMapPicker(elementId) {
    const map = mapInstancesByElementId.get(elementId);
    if (map) {
        map.remove();
        mapInstancesByElementId.delete(elementId);
    }

    // Removing the map takes its layers with it, so the marker only has to be forgotten here - left
    // behind, it would be handed to the next map opened on the same element, where it does not belong.
    markersByElementId.delete(elementId);
}
