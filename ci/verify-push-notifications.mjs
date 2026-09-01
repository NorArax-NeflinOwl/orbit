// Runs Orbit.Web's own service-worker.js and pushNotifications.js in a real browser and fails unless
// they do what push depends on.
//
// The gap this closes: both files are browser APIs end to end - a service worker's push event, the
// Notification permission, the Push API - and bUnit executes none of them, so the two files standing
// between a notification being sent and a person seeing it had no automated coverage at all. They are
// also the parts nobody notices breaking: a push that silently shows nothing looks exactly like a push
// that was never sent.
//
// The push event is delivered for real, through Chrome DevTools' ServiceWorker.deliverPushMessage, so
// this exercises the registered worker rather than a copy of its source with a fake `self` around it.
// What that cannot reach is noted at the bottom.
//
// It serves wwwroot itself rather than booting Blazor: neither file depends on it, and 127.0.0.1 is a
// secure context, which is all service workers and the Push API require.
//
// Usage: node ci/verify-push-notifications.mjs [wwwrootPath]
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize, resolve } from "node:path";
import { chromium } from "playwright";

const wwwroot = resolve(process.argv[2] ?? "src/Clients/Orbit.Web/wwwroot");

const contentTypes = {
    ".js": "text/javascript",
    ".html": "text/html",
    ".css": "text/css",
    ".json": "application/json",
};

const server = createServer(async (request, response) => {
    const requestedPath = normalize(decodeURIComponent(new URL(request.url, "http://localhost").pathname));
    if (requestedPath === "/" || requestedPath === "/index.html") {
        response.writeHead(200, { "content-type": "text/html" });
        response.end("<!doctype html><title>push harness</title>");
        return;
    }

    const filePath = join(wwwroot, requestedPath);
    // A test server that will read any file on the machine is a habit worth not forming.
    if (!filePath.startsWith(wwwroot)) {
        response.writeHead(403).end();
        return;
    }

    try {
        const body = await readFile(filePath);
        response.writeHead(200, {
            "content-type": contentTypes[extname(filePath)] ?? "application/octet-stream",
            // Lets a worker served from anywhere claim the whole origin. It is served from the root
            // here anyway; the header keeps that true if the layout ever changes.
            "service-worker-allowed": "/",
        });
        response.end(body);
    } catch {
        response.writeHead(404).end();
    }
});

await new Promise((ready) => server.listen(0, "127.0.0.1", ready));
const origin = `http://127.0.0.1:${server.address().port}`;

// The full browser, not Playwright's default headless shell: the shell has no notification service at
// all, so Notification.permission there is "denied" no matter what is granted, and every check below
// would fail for a reason that has nothing to do with Orbit. Both are installed by the same
// `playwright install chromium` the workflow already runs.
const browser = await chromium.launch({ channel: "chromium" });
const results = [];
const check = async (name, run) => {
    try {
        const detail = await run();
        results.push({ name, passed: detail === true, detail: detail === true ? "" : String(detail) });
    } catch (error) {
        results.push({ name, passed: false, detail: `threw: ${error.message}` });
    }
};

// ---- service-worker.js, driven by real push events -------------------------------------------------

const notified = await browser.newContext();
// Granted for this origin by name: a permission granted for "everywhere" is not what a browser
// actually stores, and showNotification is refused without it - which reads as the worker doing
// nothing rather than as a permission problem.
await notified.grantPermissions(["notifications"], { origin });
const page = await notified.newPage();
const pageErrors = [];
page.on("pageerror", (error) => pageErrors.push(`Uncaught page error: ${error.message}`));
await page.goto(`${origin}/`);

const registrationId = await registerTheWorker(page, notified);

/// Delivers one push and hands back the notifications the worker showed for it.
async function pushAndRead(cdp, data) {
    await page.evaluate(async () => {
        const registration = await navigator.serviceWorker.ready;
        for (const notification of await registration.getNotifications()) {
            notification.close();
        }
    });

    await cdp.send("ServiceWorker.deliverPushMessage", { origin, registrationId, data });

    // The worker shows the notification inside event.waitUntil, so it is not there the instant the
    // call returns. Polled rather than slept on: a fixed wait is either flaky or slow.
    for (let attempt = 0; attempt < 50; attempt++) {
        const shown = await page.evaluate(async () => {
            const registration = await navigator.serviceWorker.ready;
            return (await registration.getNotifications()).map((notification) => ({
                title: notification.title,
                body: notification.body,
                data: notification.data,
            }));
        });

        if (shown.length > 0) {
            return shown;
        }

        await new Promise((wait) => setTimeout(wait, 100));
    }

    return [];
}

const cdp = await notified.newCDPSession(page);
await cdp.send("ServiceWorker.enable");

await check("a push is shown, with what it said", async () => {
    const [shown] = await pushAndRead(cdp, JSON.stringify({
        title: "New message", body: "Ala wrote to you", url: "/chat/abc",
    }));

    if (!shown) {
        return "the worker showed nothing at all";
    }

    return (shown.title === "New message" && shown.body === "Ala wrote to you" && shown.data?.url === "/chat/abc")
        || `showed ${JSON.stringify(shown)}`;
});

await check("a push carrying nothing still says something", async () => {
    // Some push services deliver a data-less "wake up and check" ping. Showing nothing would be a
    // notification the user was told about by their phone and cannot find.
    const [shown] = await pushAndRead(cdp, "");
    if (!shown) {
        return "a data-less push showed nothing";
    }

    return (shown.title === "Orbit" && shown.data?.url === "/") || `showed ${JSON.stringify(shown)}`;
});

await check("a push that is not JSON does not take the worker down", async () => {
    const [shown] = await pushAndRead(cdp, "this is not json");
    if (!shown) {
        return "a malformed push showed nothing";
    }

    return shown.title === "Orbit" || `showed ${JSON.stringify(shown)}`;
});

await check("a push with no url still leads somewhere", async () => {
    const [shown] = await pushAndRead(cdp, JSON.stringify({ title: "Overdue task", body: "Buy milk" }));
    if (!shown) {
        return "the worker showed nothing";
    }

    // Clicking it navigates to data.url, so an absent one has to become the app's own root rather
    // than undefined - see service-worker.js's notificationclick.
    return shown.data?.url === "/" || `data.url was ${JSON.stringify(shown.data)}`;
});

// ---- pushNotifications.js --------------------------------------------------------------------------

await check("a browser with all three pieces reports itself supported", async () =>
    (await page.evaluate(async () => (await import("./js/pushNotifications.js")).isSupported())) === true
    || "isSupported answered false in a browser that has service workers, push and notifications");

await check("the permission it reports is the browser's own", async () =>
    (await page.evaluate(async () => (await import("./js/pushNotifications.js")).getPermissionState())) === "granted"
    || "getPermissionState disagreed with a browser that has been granted permission");

await check("a browser that never subscribed has no endpoint to report", async () =>
    (await page.evaluate(async () => (await import("./js/pushNotifications.js")).getExistingSubscriptionEndpoint())) === null
    || "getExistingSubscriptionEndpoint invented an endpoint");

await check("unsubscribing when there is nothing to unsubscribe answers null", async () =>
    (await page.evaluate(async () => (await import("./js/pushNotifications.js")).unsubscribe())) === null
    || "unsubscribe claimed to have removed something");

// Its own context: permission is granted per origin per context, and this one has to be refused.
const refused = await browser.newContext();
const refusedPage = await refused.newPage();
await refused.grantPermissions([]);
await refusedPage.goto(`${origin}/`);

await check("a refusal answers null rather than half-subscribing", async () => {
    // The branch that decides whether somebody who said no gets a button that does nothing forever.
    const answer = await refusedPage.evaluate(async () => {
        const push = await import("./js/pushNotifications.js");
        return push.requestPermissionAndSubscribe("BFakeKeyThatIsNeverUsedBecausePermissionIsRefused");
    });

    return answer === null || `answered ${JSON.stringify(answer)} for a browser that refused permission`;
});

await check("a refused browser still reports its permission honestly", async () =>
    (await refusedPage.evaluate(async () => (await import("./js/pushNotifications.js")).getPermissionState())) !== "granted"
    || "getPermissionState claimed permission a browser had refused");

await browser.close();
server.close();

for (const result of results) {
    console.log(`${result.passed ? "ok  " : "FAIL"}  ${result.name}${result.detail ? ` - ${result.detail}` : ""}`);
}

const failed = results.filter((result) => !result.passed);
if (failed.length > 0 || pageErrors.length > 0) {
    for (const pageError of pageErrors) {
        console.error(pageError);
    }
    console.error(`\n${failed.length} of ${results.length} push notification checks failed.`);
    process.exit(1);
}

console.log(`\nAll ${results.length} push notification checks passed.`);

// What this still does not reach:
//
// - `notificationclick` in service-worker.js - reopening or focusing a tab and navigating it to the
//   notification's url. There is no way to raise a real click on a system notification from outside the
//   operating system, and CDP has no command for it, so the branch that decides whether a notification
//   reuses an open Orbit tab or opens a new one is checked by hand.
// - `requestPermissionAndSubscribe` all the way through. It needs a push service to subscribe against;
//   headless Chromium has none, so what is covered here is the refusal, not the subscription. The
//   endpoint and keys it hands back are covered on the C# side instead, in PushNotificationManagerTests.

/// Registers the worker and waits for it to be the activated one, which is the only state a push can
/// be delivered to. Answers the registration id ServiceWorker.deliverPushMessage is addressed by.
async function registerTheWorker(page, context) {
    await page.evaluate(async () => {
        await navigator.serviceWorker.register("/service-worker.js");
        await navigator.serviceWorker.ready;
    });

    const session = await context.newCDPSession(page);
    await session.send("ServiceWorker.enable");

    return new Promise((found, giveUp) => {
        const waited = setTimeout(() => giveUp(new Error("the service worker never became active")), 15000);
        session.on("ServiceWorker.workerVersionUpdated", ({ versions }) => {
            const active = versions.find((version) => version.status === "activated");
            if (active) {
                clearTimeout(waited);
                found(active.registrationId);
            }
        });
    });
}
