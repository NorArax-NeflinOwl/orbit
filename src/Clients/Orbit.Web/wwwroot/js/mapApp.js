// Hands a position to whatever map app this device already has.
//
// The one thing on the map screen that has to work when Orbit's own map cannot help: a reader who keeps
// third parties out gets no tiles (see mapTiles.js), and one who never gave Orbit their location gets a
// map that shows pins and nothing about where they stand. On a phone the map app knows both, and knows
// how to get them there.
//
// Not a Google Maps URL, which is what the calendar hands over for a place: this opens an app already on
// the device rather than sending anybody to a third party - the same choice the phone app made (see
// Orbit.Maui's MapPage.OpenInPhoneMapsAsync).

// iOS does not handle geo:. Everything else - Android, and a desktop with a map app registered - does.
// The iPad reports itself as a Mac with a touch screen, which is the second half of this.
function isApple() {
    return /iPad|iPhone|iPod/.test(navigator.userAgent)
        || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
}

/// The address to hand over, kept pure so both branches can be read off without opening anything.
/// A label is what the pin is called once it arrives - somebody's name, here.
export function urlFor(apple, latitude, longitude, label) {
    const point = `${latitude},${longitude}`;
    const name = encodeURIComponent(label ?? '');
    // Apple Maps' own scheme rather than maps.apple.com: a URL would be a request to Apple before the
    // app ever opened, and this screen is the one that must not add one.
    return apple ? `maps://?ll=${point}&q=${name}` : `geo:${point}?q=${point}(${name})`;
}

export function openPosition(latitude, longitude, label) {
    window.location.href = urlFor(isApple(), latitude, longitude, label);
}
