// Where the bubble behind a field's "?" actually goes.
//
// CSS hangs it off the mark and stops there, which is enough until the mark is near an edge: inside the
// editor rail's menu, a bubble anchored to the mark's left corner ran 70px past the right of the window
// and the last third of every sentence was unreadable. Nothing in CSS can say "and stay inside the
// window" - anchor positioning would, and only one browser has it - so the coordinates are measured.
//
// One listener on the document rather than one per mark, and no Blazor interop at all: there are eight
// of these on a form, they come and go with every render, and a page that imported a module for each
// would spend more on the wiring than on the thing being wired. Hover, focus and a press all arrive
// here, which between them is every way in - see FieldHint.razor on why a press is one of them.
(function () {
    const GAP_PIXELS = 6;
    const EDGE_PIXELS = 8;

    function place(mark) {
        const bubble = mark.nextElementSibling;
        if (!bubble || !bubble.classList.contains('field-hint-bubble')) {
            return;
        }

        // Cleared first, so the measurement reads the bubble's natural size rather than the size the
        // last placement left it at - the same reason menuAnchor.js clears before it measures.
        bubble.style.position = 'fixed';
        bubble.style.left = 'auto';
        bubble.style.right = 'auto';
        bubble.style.top = 'auto';
        bubble.style.bottom = 'auto';

        const markBox = mark.getBoundingClientRect();
        const bubbleBox = bubble.getBoundingClientRect();

        // Above the mark where there is room, below it where there is not: near the top of a window the
        // upward one is the half that goes missing, and a hint nobody can read is a hint nobody has.
        const fitsAbove = markBox.top - GAP_PIXELS - bubbleBox.height >= EDGE_PIXELS;
        const top = fitsAbove
            ? markBox.top - GAP_PIXELS - bubbleBox.height
            : markBox.bottom + GAP_PIXELS;

        // Lined up with the mark, then pulled back inside the window - both edges, since a form can put
        // one of these anywhere across the page.
        const left = Math.min(
            Math.max(EDGE_PIXELS, markBox.left),
            Math.max(EDGE_PIXELS, window.innerWidth - bubbleBox.width - EDGE_PIXELS));

        bubble.style.top = `${top}px`;
        bubble.style.left = `${left}px`;
    }

    function onAsked(event) {
        const mark = event.target instanceof Element ? event.target.closest('.field-hint-mark') : null;
        if (mark) {
            place(mark);
        }
    }

    // Capture, so a mark inside something that stops these events still answers - the map panel and the
    // editor rail both stop clicks from reaching the row behind them.
    document.addEventListener('pointerover', onAsked, true);
    document.addEventListener('focusin', onAsked, true);
    document.addEventListener('click', onAsked, true);
})();
