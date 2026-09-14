// What the two bars across the top of a narrow screen do while the page is read.
//
// On a phone the page has two rows of chrome above it: the app's own bar - the logo, the sections, the
// avatar - and, on a screen that edits something, the editor's bar with Save, Back and its menu. Two
// rows is most of the room a phone has for reading, so the first one goes away while the reader is going
// down the page and comes back the moment they go up. The editor's bar never goes: it is the one thing
// on that screen somebody is reaching for, and a Save that has to be scrolled back to is a Save nobody
// finds.
//
// Plain DOM rather than anything of Blazor's: it changes one class on <body> and one custom property,
// touches nothing the app renders, and has to work from the first paint - before the app has started, on
// the boot screen, and after every navigation without being told one happened.
(function () {
    // Far enough down that the bar does not flicker away on the little bounce a touch scroll starts
    // with, and close enough that it is gone by the time somebody is reading rather than glancing.
    const AWAY_AFTER = 64;

    // How much of a change of direction counts as one. Without it, the pixel of upward drift a phone
    // reports at the end of a flick brings the bar back over the page nobody asked to leave.
    const ENOUGH = 6;

    // Matches the one breakpoint the layout uses for "no room for a column beside the page" - see
    // app.css, where the editor's panel becomes a bar at the same width.
    const NARROW = window.matchMedia('(max-width: 680px)');

    let lastY = window.scrollY;
    let waiting = false;

    /// The app's bar, measured rather than guessed: its height is padding plus whatever the row holds,
    /// which is a different number on a phone from on a laptop and would be a constant to keep in step
    /// with the CSS. What sticks under it reads this - see --topbar-height in app.css.
    function measureTheBar() {
        const bar = document.querySelector('.topbar');
        if (!bar) {
            return;
        }

        const height = Math.round(bar.getBoundingClientRect().height);
        if (height > 0) {
            document.documentElement.style.setProperty('--topbar-height', `${height}px`);
        }
    }

    function readTheScroll() {
        waiting = false;
        const y = window.scrollY;
        const moved = y - lastY;

        // Only ever hidden on a narrow screen, and never while the page is at the top of itself - a bar
        // hidden over an unscrolled page is a bar nobody can get back.
        if (!NARROW.matches || y <= AWAY_AFTER) {
            document.body.classList.remove('topbar-away');
            lastY = y;
            return;
        }

        if (Math.abs(moved) < ENOUGH) {
            return;
        }

        document.body.classList.toggle('topbar-away', moved > 0);
        lastY = y;
    }

    function onScroll() {
        if (waiting) {
            return;
        }

        waiting = true;
        window.requestAnimationFrame(readTheScroll);
    }

    // Passive, because this never answers the scroll - it only reads it, and a listener that might call
    // preventDefault costs the browser the fast path on every touch.
    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', measureTheBar);

    // The bar is built by the app, so it is not there on the first line of this file. Measured again
    // whenever the layout changes shape around it - a navigation, a menu opening, the bar wrapping onto
    // two rows - which is what an observer answers and a one-off measurement does not.
    if (typeof MutationObserver === 'function') {
        new MutationObserver(measureTheBar).observe(document.documentElement, { childList: true, subtree: true });
    }

    // Leaving a narrow screen puts the bar back: the rule that hides it only applies below the
    // breakpoint, and a class left on the body would otherwise be waiting there on the way back.
    NARROW.addEventListener('change', () => {
        document.body.classList.remove('topbar-away');
        measureTheBar();
    });

    measureTheBar();
})();
