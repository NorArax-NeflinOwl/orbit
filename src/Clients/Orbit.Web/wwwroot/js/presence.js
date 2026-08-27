// Whether this tab is actually in front of somebody. PresenceService stops sending heartbeats while it
// is not, which is what makes a person fade from available to away without them having to say so.
export function isPageVisible() {
    return document.visibilityState === 'visible';
}
