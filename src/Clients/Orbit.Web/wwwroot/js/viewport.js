// Plain classic script (not an ES module) so a single global function is available for interop calls
// that don't need a whole module import - mirrors the mobile breakpoint already used throughout
// app.css (@media (max-width: 680px)), kept here as the one place both CSS and Blazor code check it.
window.OrbitViewport = {
    isMobile: () => window.matchMedia('(max-width: 680px)').matches
};

// What the window is actually showing, which on a phone is not what CSS thinks. An on-screen keyboard
// covers the bottom of the window without changing innerHeight - and on iOS it does not change 100dvh
// either - so a page sized to the window puts its own last row, the one being typed into, underneath
// the keyboard. The visual viewport is the part still visible, and it is the only thing that knows.
//
// Published as a variable rather than applied to anything: what to do with the room is each page's
// own question, and only the chat has an answer worth having (see .main-content-page:has(.chat-layout)).
// A browser without visualViewport - none that matters today, and every desktop where this changes
// nothing - leaves the variable unset, and every rule reading it falls back to the window.
//
// The keyboard is called open past a threshold rather than at any shrink at all: the address bar
// sliding away is a visual-viewport change too, and it is not something to rearrange a page for.
(function () {
    const viewport = window.visualViewport;
    if (!viewport) {
        return;
    }

    // Enough to be a keyboard rather than a browser's own chrome coming and going.
    const keyboardThreshold = 140;

    function report() {
        const root = document.documentElement;
        root.style.setProperty('--visual-viewport-height', `${Math.round(viewport.height)}px`);
        const covered = window.innerHeight - viewport.height;
        if (covered > keyboardThreshold) {
            root.setAttribute('data-keyboard', 'open');
        } else {
            root.removeAttribute('data-keyboard');
        }
    }

    viewport.addEventListener('resize', report);
    // Scrolling the visual viewport is what iOS does instead of resizing it in some states, so the
    // height read on resize alone goes stale.
    viewport.addEventListener('scroll', report);
    report();
})();
