// How a group checklist opens - as the tree of linked lists it is or as one flat run of items, in what
// order, and whether the panel pricing it against an inventory is in the way. Kept in localStorage per
// list, the same category as the dashboard's pinned cards (see dashboardPins.js): it is how one person
// reads one page on one device, and it says nothing about the lists themselves.
const STORAGE_KEY = 'orbit-checklist-views';

function read() {
    try {
        const stored = JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? '{}');
        return stored && typeof stored === 'object' ? stored : {};
    } catch {
        return {};
    }
}

/// { view, order, isStockCheckHidden, stockOrder } for this list, or null when it has never had
/// anything saved for it.
export function getSavedReading(taskListId) {
    const saved = read()[taskListId];
    // Before an order could be chosen, what was stored was the view's name on its own.
    if (saved === 'flat' || saved === 'tree') {
        return { view: saved, order: 'as-arranged', isStockCheckHidden: false, stockOrder: 'as-counted' };
    }

    if (saved && typeof saved === 'object' && typeof saved.view === 'string') {
        return {
            view: saved.view,
            order: typeof saved.order === 'string' ? saved.order : 'as-arranged',
            isStockCheckHidden: saved.isStockCheckHidden === true,
            stockOrder: typeof saved.stockOrder === 'string' ? saved.stockOrder : 'as-counted'
        };
    }

    return null;
}

export function saveReading(taskListId, view, order, isStockCheckHidden, stockOrder) {
    const stored = read();
    stored[taskListId] = { view, order, isStockCheckHidden, stockOrder };
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
}
