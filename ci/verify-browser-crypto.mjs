// Runs Orbit.Web's own e2eeChat.js in a real browser and fails unless the encryption it promises
// actually holds.
//
// The gap this closes: everything in e2eeChat.js is Web Crypto and IndexedDB, and bUnit executes
// neither - so the one file the entire chat's confidentiality rests on had no automated coverage at
// all. The .NET side is pinned against vectors generated from this file (tests/Orbit.Mobile.Tests/
// Crypto), which proves the two agree; it does not prove this file is right, only that Orbit.Mobile
// matches whatever it does.
//
// It serves wwwroot itself rather than booting the whole Blazor app: the module is a plain ES module
// with no dependency on Blazor, and 127.0.0.1 is a secure context, which is all crypto.subtle and
// IndexedDB require.
//
// Usage: node ci/verify-browser-crypto.mjs [wwwrootPath]
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

// Only ever asked for one module and one blank page, but path traversal is refused anyway - a test
// server that will read any file on the machine is a habit worth not forming.
const server = createServer(async (request, response) => {
    const requestedPath = normalize(decodeURIComponent(new URL(request.url, "http://localhost").pathname));
    if (requestedPath === "/" || requestedPath === "/index.html") {
        response.writeHead(200, { "content-type": "text/html" });
        response.end("<!doctype html><title>crypto harness</title>");
        return;
    }

    const filePath = join(wwwroot, requestedPath);
    if (!filePath.startsWith(wwwroot)) {
        response.writeHead(403).end();
        return;
    }

    try {
        const body = await readFile(filePath);
        response.writeHead(200, { "content-type": contentTypes[extname(filePath)] ?? "application/octet-stream" });
        response.end(body);
    } catch {
        response.writeHead(404).end();
    }
});

await new Promise((ready) => server.listen(0, "127.0.0.1", ready));
const origin = `http://127.0.0.1:${server.address().port}`;

const browser = await chromium.launch();
const page = await browser.newPage();

const failures = [];
page.on("pageerror", (error) => failures.push(`Uncaught page error: ${error.message}`));

await page.goto(`${origin}/`);

// Everything below runs inside the page, because that is the only place Web Crypto and IndexedDB exist.
// It returns a list of {name, passed, detail} rather than throwing on the first problem, so one broken
// case does not hide the rest.
const results = await page.evaluate(async () => {
    const crypto = await import("./js/e2eeChat.js");

    const alice = "11111111-1111-1111-1111-111111111111";
    const bob = "22222222-2222-2222-2222-222222222222";
    const results = [];

    const check = async (name, run) => {
        try {
            const detail = await run();
            results.push({ name, passed: detail === true, detail: detail === true ? "" : String(detail) });
        } catch (error) {
            results.push({ name, passed: false, detail: `threw: ${error.message}` });
        }
    };

    await check("a browser with no key says so before one is made", async () =>
        (await crypto.hasOwnPrivateKey(alice)) === false || "hasOwnPrivateKey answered true for a fresh browser");

    await check("a key is generated once and then reused", async () => {
        const first = await crypto.ensureOwnPublicKey(alice);
        const second = await crypto.ensureOwnPublicKey(alice);
        if (first !== second) {
            return "ensureOwnPublicKey generated a second key pair for the same user";
        }
        return (await crypto.hasOwnPrivateKey(alice)) === true || "the generated private key was not persisted";
    });

    await check("two accounts in one browser do not share a key", async () => {
        const aliceKey = await crypto.ensureOwnPublicKey(alice);
        const bobKey = await crypto.ensureOwnPublicKey(bob);
        // Records are namespaced by user id precisely so one account signing in does not overwrite or
        // inherit another's key - which would let either read the other's messages.
        return aliceKey !== bobKey || "both accounts were handed the same public key";
    });

    await check("a message encrypted for somebody comes back as what was written", async () => {
        const aliceKey = await crypto.ensureOwnPublicKey(alice);
        const bobKey = await crypto.ensureOwnPublicKey(bob);
        const written = "the same text, there and back — z polskimi znakami";

        const sealed = await crypto.encryptMessage(alice, bobKey, written);
        const opened = await crypto.decryptMessage(bob, aliceKey, sealed.ciphertextBase64, sealed.nonceBase64);
        return opened === written || `decrypted to ${JSON.stringify(opened)}`;
    });

    await check("the ciphertext is not the message", async () => {
        const bobKey = await crypto.ensureOwnPublicKey(bob);
        const sealed = await crypto.encryptMessage(alice, bobKey, "meet me at six");
        return !atob(sealed.ciphertextBase64).includes("meet me at six") || "the plaintext survived into the ciphertext";
    });

    await check("every message gets its own nonce", async () => {
        const bobKey = await crypto.ensureOwnPublicKey(bob);
        const first = await crypto.encryptMessage(alice, bobKey, "same words");
        const second = await crypto.encryptMessage(alice, bobKey, "same words");
        // A repeated nonce under one AES-GCM key is a catastrophic failure, not a cosmetic one, and the
        // same words twice is exactly how it would show up.
        if (first.nonceBase64 === second.nonceBase64) {
            return "two messages were encrypted under the same nonce";
        }
        return first.ciphertextBase64 !== second.ciphertextBase64 || "identical text produced identical ciphertext";
    });

    await check("a tampered message does not open", async () => {
        const aliceKey = await crypto.ensureOwnPublicKey(alice);
        const bobKey = await crypto.ensureOwnPublicKey(bob);
        const sealed = await crypto.encryptMessage(alice, bobKey, "transfer 100");

        const bytes = Uint8Array.from(atob(sealed.ciphertextBase64), (character) => character.charCodeAt(0));
        bytes[0] ^= 0xff;
        const tampered = btoa(String.fromCharCode(...bytes));

        // AES-GCM authenticates as well as encrypts, and this is what says so: a null here is the
        // placeholder the chat window draws instead of showing something nobody wrote.
        const opened = await crypto.decryptMessage(bob, aliceKey, tampered, sealed.nonceBase64);
        return opened === null || `a tampered message opened as ${JSON.stringify(opened)}`;
    });

    await check("somebody else's key does not open the message", async () => {
        const bobKey = await crypto.ensureOwnPublicKey(bob);
        const stranger = "33333333-3333-3333-3333-333333333333";
        const strangerKey = await crypto.ensureOwnPublicKey(stranger);

        const sealed = await crypto.encryptMessage(alice, bobKey, "not for you");
        const opened = await crypto.decryptMessage(bob, strangerKey, sealed.ciphertextBase64, sealed.nonceBase64);
        return opened === null || `a stranger's key opened it as ${JSON.stringify(opened)}`;
    });

    await check("a note sealed for yourself opens for yourself", async () => {
        await crypto.ensureOwnPublicKey(alice);
        const sealed = await crypto.encryptForSelf(alice, "a private note");
        const opened = await crypto.decryptForSelf(alice, sealed.ciphertextBase64, sealed.nonceBase64);
        return opened === "a private note" || `decrypted to ${JSON.stringify(opened)}`;
    });

    await check("a private note does not open for anybody else", async () => {
        await crypto.ensureOwnPublicKey(alice);
        const sealed = await crypto.encryptForSelf(alice, "a private note");
        const opened = await crypto.decryptForSelf(bob, sealed.ciphertextBase64, sealed.nonceBase64);
        return opened === null || `another account read it as ${JSON.stringify(opened)}`;
    });

    await check("a key backed up under a password comes back under that password", async () => {
        const restorer = "44444444-4444-4444-4444-444444444444";
        const original = await crypto.ensureOwnPublicKey(restorer);
        const wrapped = await crypto.wrapOwnPrivateKeyWithPassword(restorer, "correct horse battery staple");
        if (wrapped === null) {
            return "the key could not be exported for backup";
        }
        if (atob(wrapped.ciphertextBase64).includes("\"d\"")) {
            return "the backup carries the private key in the clear";
        }

        const restored = await crypto.restoreOwnPrivateKeyFromBackup(restorer, "correct horse battery staple", wrapped);
        return restored === original || `restored a different public key: ${restored}`;
    });

    await check("the wrong password restores nothing", async () => {
        const restorer = "55555555-5555-5555-5555-555555555555";
        await crypto.ensureOwnPublicKey(restorer);
        const wrapped = await crypto.wrapOwnPrivateKeyWithPassword(restorer, "the real password");

        // Null rather than a throw, because the caller's answer to a wrong password is to generate a
        // fresh key pair - a rejected promise there would take the sign-in down instead.
        const restored = await crypto.restoreOwnPrivateKeyFromBackup(restorer, "not the real password", wrapped);
        return restored === null || "a wrong password restored a key anyway";
    });

    await check("a restored key opens what was written to the old one", async () => {
        const restorer = "66666666-6666-6666-6666-666666666666";
        const restorerKey = await crypto.ensureOwnPublicKey(restorer);
        const senderKey = await crypto.ensureOwnPublicKey(alice);
        const sealed = await crypto.encryptMessage(alice, restorerKey, "written before the new device");

        const wrapped = await crypto.wrapOwnPrivateKeyWithPassword(restorer, "a password");
        await crypto.restoreOwnPrivateKeyFromBackup(restorer, "a password", wrapped);

        // The whole point of the backup: signing in somewhere new has to reach the conversation that
        // already exists, not start an unreadable one.
        const opened = await crypto.decryptMessage(restorer, senderKey, sealed.ciphertextBase64, sealed.nonceBase64);
        return opened === "written before the new device" || `decrypted to ${JSON.stringify(opened)}`;
    });

    return results;
});

// Reloading is the point of this one: the key has to be in IndexedDB rather than in a module-level
// variable, or every refresh would silently strand the conversation.
await page.reload();
const survivedReload = await page.evaluate(async () => {
    const crypto = await import("./js/e2eeChat.js");
    return crypto.hasOwnPrivateKey("11111111-1111-1111-1111-111111111111");
});
results.push({
    name: "a key outlives a page reload",
    passed: survivedReload === true,
    detail: survivedReload === true ? "" : "the key was gone after a reload - it is not reaching IndexedDB",
});

await browser.close();
server.close();

for (const result of results) {
    console.log(`${result.passed ? "ok  " : "FAIL"}  ${result.name}${result.detail ? ` - ${result.detail}` : ""}`);
}

const failed = results.filter((result) => !result.passed);
if (failed.length > 0 || failures.length > 0) {
    for (const failure of failures) {
        console.error(failure);
    }
    console.error(`\n${failed.length} of ${results.length} browser crypto checks failed.`);
    process.exit(1);
}

console.log(`\nAll ${results.length} browser crypto checks passed.`);
