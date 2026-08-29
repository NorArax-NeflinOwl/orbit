// The accent colour's hue, persisted and applied - the counterpart to theme.js, and the interactive
// half of the anti-flash inline script in index.html, which duplicates the read so it can run before
// this module (or Blazor itself) is available. Keep the two in sync if this logic ever changes.
const STORAGE_KEY = 'orbit-accent-hue';

/// Returns the stored hue as a string, or null when the reader has never chosen one.
export function getStoredAccentHue() {
    return localStorage.getItem(STORAGE_KEY);
}

/// Persists the reader's choice. Pass null to clear it (back to the colour app.css ships with).
export function setStoredAccentHue(value) {
    if (value === null || value === undefined) {
        localStorage.removeItem(STORAGE_KEY);
    } else {
        localStorage.setItem(STORAGE_KEY, value);
    }
}

/// Stamps the hue onto <html>, where it overrides app.css's own --accent-hue and so repaints every
/// accent token in both themes at once. Clearing it hands the page back to the stylesheet's default.
export function applyAccentHue(value) {
    if (value === null || value === undefined) {
        document.documentElement.style.removeProperty('--accent-hue');
    } else {
        document.documentElement.style.setProperty('--accent-hue', value);
    }
}
