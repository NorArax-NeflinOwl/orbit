// Wraps Leaflet (loaded from a CDN in index.html) so CalendarEventEditor.razor can open an
// interactive map, let the user click a point, and report the picked coordinates back to .NET -
// Blazor WebAssembly has no built-in mapping component, and Leaflet needs a real DOM element plus its
// own click-event wiring that only plain JS can provide.

const mapInstancesByElementId = new Map();

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

    let marker = hasInitialPosition ? L.marker(startPosition).addTo(map) : null;

    map.on('click', event => {
        if (marker) {
            marker.setLatLng(event.latlng);
        } else {
            marker = L.marker(event.latlng).addTo(map);
        }

        // The marker stays where the click landed, but the coordinates reported to .NET are wrapped
        // first: the tile layer repeats the world horizontally, and Leaflet keeps panning past the
        // antimeridian in the same continuous coordinate space rather than wrapping - a click two
        // worlds east of Warsaw arrives here as longitude 741, not 21. CalendarEvent rejects anything
        // outside -180..180, so sending it unwrapped turned an ordinary map click into a failed save.
        const { lat, lng } = event.latlng.wrap();
        dotNetHelper.invokeMethodAsync('OnMapLocationPicked', lat, lng);
    });

    mapInstancesByElementId.set(elementId, map);
}

export function disposeMapPicker(elementId) {
    const map = mapInstancesByElementId.get(elementId);
    if (map) {
        map.remove();
        mapInstancesByElementId.delete(elementId);
    }
}
