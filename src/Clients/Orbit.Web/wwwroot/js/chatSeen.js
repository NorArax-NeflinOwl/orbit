// What of a chat thread is actually in front of somebody, for ChatSeenProbe. This file only answers
// questions about the page; which messages that makes read, and when the server is told, is decided in
// C# (ChatReadState), where it can be tested.

// Visible is not enough: a window left open on a second screen is visible, and nobody is at it.
export function isInFront() {
    return document.visibilityState === 'visible' && document.hasFocus();
}

// The id of the newest message whose end is on screen inside the list, or null. Its end rather than
// any part of it: a sliver of a message at the bottom edge has not been read, and a message taller than
// the list has been read once its last line has come into view. The pixel of slack is for fractional
// device-pixel offsets, which leave a list scrolled fully down reporting a hair short.
export function newestInView(list) {
    if (!list || typeof list.getBoundingClientRect !== 'function') {
        return null;
    }

    const bounds = list.getBoundingClientRect();
    const messages = list.querySelectorAll('[data-message-id]');
    for (let index = messages.length - 1; index >= 0; index--) {
        const rect = messages[index].getBoundingClientRect();
        if (rect.height > 0 && rect.bottom <= bounds.bottom + 1 && rect.bottom > bounds.top) {
            return messages[index].dataset.messageId;
        }
    }

    return null;
}

// Calls back whenever what is seen may have changed - the list scrolled, the window got focus, the tab
// came to the front - at most once every quarter of a second, since a scroll fires on every frame.
// Keyed by the caller rather than kept on the element, so a thread whose list has already left the
// page can still take its window and document listeners away with it.
const observers = new Map();
const SETTLE_MILLISECONDS = 250;

export function observe(key, list, dotNetRef) {
    unobserve(key);
    if (!list || typeof list.addEventListener !== 'function') {
        return;
    }

    let pending = 0;
    const notify = () => {
        if (pending) {
            return;
        }
        pending = setTimeout(() => {
            pending = 0;
            dotNetRef.invokeMethodAsync('OnThreadSeenMayHaveChanged').catch(() => { });
        }, SETTLE_MILLISECONDS);
    };

    list.addEventListener('scroll', notify, { passive: true });
    window.addEventListener('focus', notify);
    document.addEventListener('visibilitychange', notify);
    observers.set(key, () => {
        clearTimeout(pending);
        list.removeEventListener('scroll', notify);
        window.removeEventListener('focus', notify);
        document.removeEventListener('visibilitychange', notify);
    });
}

export function unobserve(key) {
    const stop = observers.get(key);
    if (stop) {
        stop();
        observers.delete(key);
    }
}
