// Wraps the browser's Push API and Notification permission so PushNotificationManager (Orbit.Web) never
// has to deal with the browser APIs directly - see service-worker.js for the other half (receiving and
// showing a notification once it arrives).

const serviceWorkerUrl = '/service-worker.js';

function base64UrlToUint8Array(base64Url) {
    const padding = '='.repeat((4 - (base64Url.length % 4)) % 4);
    const base64 = (base64Url + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = atob(base64);
    return Uint8Array.from([...rawData].map(character => character.charCodeAt(0)));
}

function arrayBufferToBase64(buffer) {
    return btoa(String.fromCharCode(...new Uint8Array(buffer)));
}

/// True when this browser supports everything push notifications need - service workers, the Push API,
/// and the Notification API. Checked before showing any "enable push notifications" control, since a
/// browser lacking any one of these can never complete a subscription.
export function isSupported() {
    return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
}

/// The browser's current notification permission - 'default' (never asked), 'granted' or 'denied'.
export function getPermissionState() {
    return Notification.permission;
}

async function getExistingSubscription() {
    const registration = await navigator.serviceWorker.getRegistration(serviceWorkerUrl);
    return registration ? registration.pushManager.getSubscription() : null;
}

/// The endpoint of this browser's current push subscription, or null if it never subscribed (or the
/// service worker isn't registered yet) - used on page load to show accurate enabled/disabled state
/// without prompting for permission again.
export async function getExistingSubscriptionEndpoint() {
    const subscription = await getExistingSubscription();
    return subscription ? subscription.endpoint : null;
}

/// Asks the user for notification permission and, once granted, subscribes this browser to push via the
/// given VAPID public key. Returns null if the user denies (or has already denied) permission; otherwise
/// returns the subscription's endpoint and keys for PushNotificationApiClient to hand to Orbit.Api.
export async function requestPermissionAndSubscribe(vapidPublicKeyBase64Url) {
    const permission = await Notification.requestPermission();
    if (permission !== 'granted') {
        return null;
    }

    const registration = await navigator.serviceWorker.register(serviceWorkerUrl);
    await navigator.serviceWorker.ready;

    // A subscription created under a different VAPID key than the one Orbit.Api currently has
    // configured (e.g. an old subscription from before Vapid:PublicKeyBase64Url was set, or from
    // pointing this browser at a different environment) can't just be replaced: subscribing again
    // while it's still active throws "InvalidStateError: ... a subscription with a different
    // applicationServerKey already exists" - and getExistingSubscriptionEndpoint doesn't distinguish
    // that from a healthy subscription, so the "Włącz" button silently fails every time instead of
    // just recovering. Clearing it first avoids the conflict outright, whatever caused it.
    const existingSubscription = await registration.pushManager.getSubscription();
    if (existingSubscription) {
        await existingSubscription.unsubscribe();
    }

    const subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: base64UrlToUint8Array(vapidPublicKeyBase64Url)
    });

    return {
        endpoint: subscription.endpoint,
        p256dhBase64: arrayBufferToBase64(subscription.getKey('p256dh')),
        authBase64: arrayBufferToBase64(subscription.getKey('auth'))
    };
}

/// Cancels this browser's push subscription, if any. Returns the endpoint that was removed (so the
/// caller can tell Orbit.Api to forget it too), or null if there was nothing to unsubscribe.
export async function unsubscribe() {
    const subscription = await getExistingSubscription();
    if (!subscription) {
        return null;
    }

    const endpoint = subscription.endpoint;
    await subscription.unsubscribe();
    return endpoint;
}
