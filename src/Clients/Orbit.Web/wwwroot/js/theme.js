// Theme persistence/application - the interactive (post-load) counterpart to the anti-flash inline
// script in index.html, which duplicates the resolution logic so it can run before this module (or even
// Blazor itself) is available. Keep the two in sync if this logic ever changes.
const STORAGE_KEY = 'orbit-theme';

/// Returns the stored preference ('light' | 'dark'), or null if unset (meaning: follow the OS).
export function getStoredTheme() {
    return localStorage.getItem(STORAGE_KEY);
}

/// Persists the user's explicit choice. Pass null to clear it (follow the OS again).
export function setStoredTheme(value) {
    if (value === null || value === undefined) {
        localStorage.removeItem(STORAGE_KEY);
    } else {
        localStorage.setItem(STORAGE_KEY, value);
    }
}

export function systemPrefersDark() {
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
}

/// Resolves value ('light' | 'dark' | null-for-system) against the OS preference and stamps the
/// result onto <html data-theme="...">, which every CSS token in app.css is keyed off.
export function applyTheme(value) {
    const dark = value === 'dark' || (value !== 'light' && systemPrefersDark());
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
}
