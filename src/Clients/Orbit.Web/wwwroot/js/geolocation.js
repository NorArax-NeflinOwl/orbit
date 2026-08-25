// Reads the browser's own position, once, when the user asks for it. Kept apart from mapPicker.js
// because it is a different thing entirely: that one lets someone point at a place, this one asks the
// device where it is - which needs the user's permission and can be refused.

/// Resolves { latitude, longitude, accuracyMetres } or, when the position can't be had, an { error }
/// naming why in words the page can show as-is. Never throws: a refused permission is an ordinary
/// answer here, not a failure.
export function getCurrentPosition(timeoutMs) {
    return new Promise(resolve => {
        if (!('geolocation' in navigator)) {
            resolve({ error: "This browser can't report a location." });
            return;
        }

        navigator.geolocation.getCurrentPosition(
            position => resolve({
                latitude: position.coords.latitude,
                longitude: position.coords.longitude,
                accuracyMetres: position.coords.accuracy
            }),
            error => resolve({ error: describe(error) }),
            // No maximumAge: recording a location that is actually minutes or hours old would be worse
            // than telling the user it couldn't be taken.
            { enableHighAccuracy: true, timeout: timeoutMs, maximumAge: 0 });
    });
}

function describe(error) {
    switch (error.code) {
        case error.PERMISSION_DENIED:
            return 'Orbit needs permission to read your location - allow it in your browser and try again.';
        case error.POSITION_UNAVAILABLE:
            return "Your device couldn't work out where it is right now.";
        case error.TIMEOUT:
            return 'Getting a position took too long. Try again.';
        default:
            return "Your location couldn't be read.";
    }
}
