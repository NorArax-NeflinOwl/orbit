// Registered by wwwroot/js/pushNotifications.js's requestPermissionAndSubscribe() with the default
// scope (this file lives at the origin root, so its scope is "/", covering every route the Blazor
// Router handles) - this is what lets a push notification arrive, and be clicked to reopen the app, even
// while no Orbit.Web tab is currently open. Kept deliberately minimal: this app has no other use for a
// service worker (no offline caching), so it only ever reacts to push-related events.

self.addEventListener('push', event => {
    // The push payload is plain JSON built by VapidPushNotificationSender in Orbit.Api (title/body/url) -
    // event.data can still be missing entirely (some push services deliver a data-less "wake up and
    // check" ping), so this falls back to a generic notification rather than throwing.
    let payload = {};
    try {
        payload = event.data ? event.data.json() : {};
    } catch {
        payload = {};
    }

    const title = payload.title || 'Orbit';
    const options = {
        body: payload.body || '',
        data: { url: payload.url || '/' }
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = event.notification.data && event.notification.data.url ? event.notification.data.url : '/';

    event.waitUntil((async () => {
        const windowClients = await clients.matchAll({ type: 'window', includeUncontrolled: true });
        // Reuses an Orbit.Web tab that's already open rather than always opening a new one - Blazor's
        // client-side router then takes over navigation to `url` once that tab is focused, same as any
        // in-app link click.
        for (const client of windowClients) {
            if (new URL(client.url).origin === self.location.origin && 'focus' in client) {
                await client.focus();
                if ('navigate' in client) {
                    await client.navigate(url);
                }
                return;
            }
        }

        if (clients.openWindow) {
            await clients.openWindow(url);
        }
    })());
});
