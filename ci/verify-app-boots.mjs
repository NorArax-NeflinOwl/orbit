// Loads Orbit in a real browser and fails unless the app actually started.
//
// The gap this closes: nginx serves index.html for every request, so a dead client still answers 200,
// and Container Apps reports the revision Healthy because nginx itself is fine. Both existing gates
// passed while the deployed app rendered nothing but "Loading…" - see the dependency cycle in PR #70.
// Nothing short of booting it in a browser can tell the difference.
//
// Usage: node ci/verify-app-boots.mjs <url> [timeoutMs]
import { chromium } from "playwright";

const url = process.argv[2];
const timeoutMs = Number(process.argv[3] ?? 60_000);

if (!url) {
    console.error("usage: node ci/verify-app-boots.mjs <url> [timeoutMs]");
    process.exit(2);
}

const browser = await chromium.launch();
const page = await browser.newPage();

// Collected rather than asserted on directly: a page can boot despite a noisy console, and the
// failure message is far more useful with them than without.
const consoleErrors = [];
const pageErrors = [];
page.on("console", (message) => {
    if (message.type() === "error") {
        consoleErrors.push(message.text());
    }
});
page.on("pageerror", (error) => pageErrors.push(error.message));

const fail = async (reason) => {
    console.error(`::error::${reason}`);
    if (pageErrors.length > 0) {
        console.error("--- unhandled errors ---");
        pageErrors.forEach((error) => console.error(`  ${error}`));
    }
    if (consoleErrors.length > 0) {
        console.error("--- console errors ---");
        consoleErrors.slice(0, 10).forEach((error) => console.error(`  ${error}`));
    }
    await browser.close();
    process.exit(1);
};

try {
    await page.goto(url, { waitUntil: "domcontentloaded", timeout: timeoutMs });
} catch (error) {
    await fail(`${url} could not be loaded at all: ${error.message}`);
}

// index.html ships #app holding the start screen (`.app-boot` - the mark, the name and a spinner);
// Blazor replaces that content once it has started. Waiting for it to go is the difference between
// "the server answered" and "the app runs".
//
// The start screen is what this looks for rather than the words in #app: it used to wait for the text
// to stop being exactly "Loading…", which was true the moment that placeholder grew any markup of its
// own. A check that a nicer loading screen can satisfy is a check that would have passed while the
// deployed app rendered nothing at all - the very failure this exists for, see PR #70.
try {
    await page.waitForFunction(
        () => {
            const app = document.querySelector("#app");
            return app !== null && app.children.length > 0 && app.querySelector(".app-boot") === null;
        },
        { timeout: timeoutMs },
    );
} catch {
    await fail(
        `${url} served a page, but the app never started - #app still shows the start screen ` +
        `after ${timeoutMs}ms. This is what a broken service graph, a failed module load, or a bad ` +
        `boot config looks like from outside.`,
    );
}

// The app's own "something went wrong" bar. Hidden by default and shown by Blazor's error handler, so a
// visible one means the app started and then fell over - still not a deployable state.
if (await page.locator("#blazor-error-ui").isVisible()) {
    await fail(`${url} started and then hit an unhandled error - the error bar is showing.`);
}

if (pageErrors.length > 0) {
    await fail(`${url} started but raised ${pageErrors.length} unhandled error(s).`);
}

const rendered = (await page.locator("#app").innerText()).trim().replace(/\s+/g, " ").slice(0, 80);
console.log(`${url} booted. #app renders: "${rendered}"`);

await browser.close();
