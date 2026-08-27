// Which dashboard cards a person has pinned to the top of their page. Kept in localStorage rather than
// on the server because this is page layout on one device, in the same category as the theme (see
// theme.js) - nothing here is content, and nothing here is worth a row in the database.
const STORAGE_KEY = 'orbit-dashboard-pins';

/// Returns the pinned card keys, or an empty array when nothing is pinned or the stored value is unusable.
export function getPinnedCards() {
    try {
        const stored = JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? '[]');
        return Array.isArray(stored) ? stored.filter(key => typeof key === 'string') : [];
    } catch {
        return [];
    }
}

export function setPinnedCards(keys) {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(keys ?? []));
}
