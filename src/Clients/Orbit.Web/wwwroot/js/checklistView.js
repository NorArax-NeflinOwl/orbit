// Which way a group checklist opens - as the tree of linked lists it is, or as one flat run of items.
// Kept in localStorage per list, the same category as the dashboard's pinned cards (see dashboardPins.js):
// it is how one person reads one page on one device, and it says nothing about the lists themselves.
const STORAGE_KEY = 'orbit-checklist-views';

function read() {
    try {
        const stored = JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? '{}');
        return stored && typeof stored === 'object' ? stored : {};
    } catch {
        return {};
    }
}

/// 'flat', 'tree', or null when this list has never had a view saved for it.
export function getSavedView(taskListId) {
    const view = read()[taskListId];
    return view === 'flat' || view === 'tree' ? view : null;
}

export function saveView(taskListId, view) {
    const stored = read();
    stored[taskListId] = view;
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
}
