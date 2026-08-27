// Which parts of the dashboard a person has put away. Kept in localStorage rather than on the server
// because this is page layout on one device, in the same category as the pins beside it (see
// dashboardPins.js) and the theme (see theme.js).
//
// What is stored is what is hidden, not what is shown: a card added to the dashboard later then appears
// for everybody rather than staying invisible to whoever saved a layout before it existed.
const STORAGE_KEY = 'orbit-dashboard-hidden-cards';

/// Returns the hidden card keys, or an empty array when nothing is hidden or the stored value is unusable.
export function getHiddenCards() {
    try {
        const stored = JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? '[]');
        return Array.isArray(stored) ? stored.filter(key => typeof key === 'string') : [];
    } catch {
        return [];
    }
}

export function setHiddenCards(keys) {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(keys ?? []));
}

// What each card is filtered down to, by card key - see DashboardCardFilter. A card showing everything
// is simply absent, so the stored shape stays as small as what the reader actually changed.
const FILTER_KEY = 'orbit-dashboard-card-filters';

export function getCardFilters() {
    try {
        const stored = JSON.parse(window.localStorage.getItem(FILTER_KEY) ?? '{}');
        return stored && typeof stored === 'object' && !Array.isArray(stored) ? stored : {};
    } catch {
        return {};
    }
}

export function setCardFilters(filters) {
    window.localStorage.setItem(FILTER_KEY, JSON.stringify(filters ?? {}));
}
