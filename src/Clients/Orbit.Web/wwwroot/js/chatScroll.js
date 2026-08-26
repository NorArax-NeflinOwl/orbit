// Scroll helpers for Chat.razor's message list. Blazor can't read or set scroll offsets on its own, so
// the "jump to newest" button and the scroll-to-bottom-on-open behaviour both go through here.
(function () {
    "use strict";

    // "Near" rather than "exactly at" the bottom: fractional device-pixel scroll offsets mean an element
    // the user sees as fully scrolled down often reports a few pixels short.
    const NEAR_BOTTOM_THRESHOLD_PIXELS = 120;

    function isNearBottom(element) {
        return element.scrollHeight - element.scrollTop - element.clientHeight <= NEAR_BOTTOM_THRESHOLD_PIXELS;
    }

    window.OrbitChatScroll = {
        // Always an instant jump: a smooth animation is silently ignored where reduced motion is in
        // effect, and where it does run it gets cancelled part-way by the poll loop's re-renders.
        scrollToBottom: (element) => {
            if (!element) {
                return;
            }
            element.scrollTop = element.scrollHeight;
        },

        isScrolledNearBottom: (element) => !element || isNearBottom(element),

        // The scroll event doesn't bubble, and Blazor delegates DOM events from the document root, so an
        // @onscroll handler on the message list never fires. Registering the listener directly on the
        // element here and calling back into the component is the way to get it.
        observeScroll: (element, dotNetRef) => {
            if (!element) {
                return;
            }

            const onScroll = () => dotNetRef.invokeMethodAsync("OnMessageListScrolled", isNearBottom(element));
            element.addEventListener("scroll", onScroll, { passive: true });
            element.orbitScrollListener = onScroll;
        },

        unobserveScroll: (element) => {
            if (element && element.orbitScrollListener) {
                element.removeEventListener("scroll", element.orbitScrollListener);
                delete element.orbitScrollListener;
            }
        },

        // Brings the message a reply is quoting into view. Missing is the ordinary case rather than an
        // error: the quote carries its own preview, so it still reads correctly for a message that has
        // since been deleted, or that sits further back than this window has loaded.
        scrollToMessage: (elementId) => {
            const element = document.getElementById(elementId);
            if (element) {
                element.scrollIntoView({ block: "center", behavior: "smooth" });
            }
        }
    };
})();
