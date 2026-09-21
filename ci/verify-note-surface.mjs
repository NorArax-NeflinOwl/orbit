// Runs Orbit.Web's own checklistTextEditor.js in a real browser and fails unless a note survives being
// drawn and read back.
//
// The gap this closes has a name: on 2026-09-16 a rule drawn across a note (NoteSeparatorLine) was being
// stored as an empty line, because extractLines - the read of the surface that every keystroke and every
// save makes - read a table and a picture back and not a separator. Nothing failed. The note simply lost
// the rule at the next read, which the user saw as "Android does not read separators made in the
// browser" and "a separator added on Orbit.Maui does not show in Orbit.Web" (issue #294), two reports of
// one fault that no test in this repository could have caught: the whole of it is in a browser module,
// and bUnit executes no JavaScript at all.
//
// So this is the round trip, and one key pressed on an element's line (see the second half). Every kind
// of line the editor knows goes in through
// initialize, and getLinesAsJson reads the surface back; a line that comes back saying something
// different is the failure. It serves wwwroot itself rather than booting Blazor, the way
// verify-browser-crypto.mjs does and for the same reason: the module is a plain ES module with no
// dependency on Blazor, and one blank page is all it needs.
//
// Usage: node ci/verify-note-surface.mjs [wwwrootPath]
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
        response.end("<!doctype html><title>note surface harness</title>");
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

// The field names are the ones Orbit.Core.Notes.NoteContentLine serialises to - "marks", not
// "allMarks", which is a derived view and never travels. A name this module does not know reads as the
// default, so getting one wrong here would make a passing check that proves nothing.
const results = await page.evaluate(async () => {
    const surface = await import("./js/checklistTextEditor.js");

    const written = [
        { name: "ordinary writing", line: { text: "A plain line", isChecklistItem: false, isChecked: false, style: "Body" } },
        { name: "a heading", line: { text: "A heading", isChecklistItem: false, isChecked: false, style: "Heading" } },
        { name: "a subheading", line: { text: "A subheading", isChecklistItem: false, isChecked: false, style: "Subheading" } },
        { name: "a line of a list", line: { text: "A bulleted line", isChecklistItem: false, isChecked: false, style: "Bulleted" } },
        { name: "a numbered line", line: { text: "A numbered line", isChecklistItem: false, isChecked: false, style: "Numbered" } },
        { name: "an errand nobody has ticked", line: { text: "Buy flour", isChecklistItem: true, isChecked: false, style: "Body" } },
        { name: "a ticked errand", line: { text: "Buy milk", isChecklistItem: true, isChecked: true, style: "Body" } },
        { name: "an errand crossed out", line: { text: "Buy yeast", isChecklistItem: true, isChecked: false, isFailed: true, style: "Body" } },
        {
            name: "words with a mark on them",
            line: {
                text: "bold here", isChecklistItem: false, isChecked: false, style: "Body",
                marks: [{ start: 0, length: 4, mark: "Bold" }],
            },
        },
        {
            name: "a table",
            line: {
                text: "", isChecklistItem: false, isChecked: false, style: "Body",
                table: { rows: [{ cells: [{ text: "a" }, { text: "b" }] }, { cells: [{ text: "c" }, { text: "d" }] }] },
            },
        },
        {
            name: "a picture",
            line: {
                text: "", isChecklistItem: false, isChecked: false, style: "Body",
                picture: {
                    pictureId: "77777777-7777-7777-7777-777777777777",
                    contentType: "image/png", widthPixels: 320, heightPixels: 200,
                },
            },
        },
        {
            // The two that #294 was about. A rule carries what was written on it when it was made and
            // never works it out again - see NoteSeparatorLine.Stamp - so the stamp has to survive the
            // read as exactly the words it went in as, and an undated rule has to stay undated rather
            // than picking up today.
            name: "a rule with the moment it was drawn on it",
            line: { text: "", isChecklistItem: false, isChecked: false, style: "Body", separator: { stamp: "19.09.2026 01:20" } },
        },
        { name: "writing under the dated rule", line: { text: "after the dated rule", isChecklistItem: false, isChecked: false, style: "Body" } },
        {
            name: "a rule with nothing written on it",
            line: { text: "", isChecklistItem: false, isChecked: false, style: "Body", separator: { stamp: "" } },
        },
        { name: "writing under the plain rule", line: { text: "after the plain rule", isChecklistItem: false, isChecked: false, style: "Body" } },
    ];

    const container = document.createElement("div");
    container.setAttribute("contenteditable", "true");
    document.body.appendChild(container);

    // Blazor's end of it, stubbed. Nothing here presses a key, but the surface listens for the caret
    // moving and asks the component about a picture's URL while drawing one, and a null helper would
    // make those listeners throw instead of the check reporting what it found. Null for a picture is a
    // real answer - the component gives it for a picture it cannot hand over - and the figure is drawn
    // either way, which is what the read reads.
    const blazor = {
        invokeMethod: () => [],
        invokeMethodAsync: () => Promise.resolve(null),
    };
    surface.initialize(container, blazor, JSON.stringify(written.map((each) => each.line)), {});
    const readBack = JSON.parse(surface.getLinesAsJson(container));
    surface.dispose(container);
    container.remove();

    const results = [{
        name: "every line comes back",
        passed: readBack.length === written.length,
        detail: readBack.length === written.length ? "" : `${written.length} lines went in and ${readBack.length} came back`,
    }];

    const sameShape = (mine, theirs) => JSON.stringify(mine ?? null) === JSON.stringify(theirs ?? null);

    written.forEach(({ name, line }, index) => {
        const back = readBack[index] ?? {};
        const wrong = [];

        if (back.text !== line.text) {
            wrong.push(`text came back as ${JSON.stringify(back.text)}`);
        }
        // Read case-insensitively, the way NoteLineStyles.Read reads it off the wire.
        if ((back.style ?? "").toLowerCase() !== (line.style ?? "Body").toLowerCase()) {
            wrong.push(`style came back as ${JSON.stringify(back.style)}`);
        }
        if (Boolean(back.isChecklistItem) !== Boolean(line.isChecklistItem)) {
            wrong.push(`isChecklistItem came back as ${back.isChecklistItem}`);
        }
        if (Boolean(back.isChecked) !== Boolean(line.isChecked)) {
            wrong.push(`isChecked came back as ${back.isChecked}`);
        }
        if (Boolean(back.isFailed) !== Boolean(line.isFailed)) {
            wrong.push(`isFailed came back as ${back.isFailed}`);
        }
        if (!sameShape(back.separator, line.separator)) {
            wrong.push(`separator came back as ${JSON.stringify(back.separator ?? null)}`);
        }
        if (Boolean(back.table) !== Boolean(line.table)) {
            wrong.push(`table came back as ${JSON.stringify(back.table ?? null)}`);
        }
        if (!sameShape(back.picture, line.picture)) {
            wrong.push(`picture came back as ${JSON.stringify(back.picture ?? null)}`);
        }
        if ((back.marks ?? []).length !== (line.marks ?? []).length) {
            wrong.push(`marks came back as ${JSON.stringify(back.marks ?? [])}`);
        }

        results.push({ name: `${name} survives being read back`, passed: wrong.length === 0, detail: wrong.join("; ") });
    });

    return results;
});

// The second half: a key pressed with the caret on an element's line. The caret is left exactly there
// after a picture or a rule is put in (domPoint puts it on the line, before the element, because neither
// has a place for words), so the very next key lands on it. The browser would type into the element's
// own line - drawn beside it, and dropped by the next read, since that line is read as the element
// alone. That is what happened to a rule until 2026-09-21: its line was missing from the guard the
// picture had. What is checked is the handoff, not what C# does with it: the words must not reach the
// element's line, and they must be handed to C# as a replace, which NoteSurfaceEdits.Replace answers
// by putting them under the element.
const onAnElement = [
    {
        name: "a picture",
        line: {
            text: "", isChecklistItem: false, isChecked: false, style: "Body",
            picture: { pictureId: "77777777-7777-7777-7777-777777777777", contentType: "image/png", widthPixels: 320, heightPixels: 200 },
        },
    },
    { name: "a dated rule", line: { text: "", isChecklistItem: false, isChecked: false, style: "Body", separator: { stamp: "21.09.2026 21:18" } } },
    { name: "a plain rule", line: { text: "", isChecklistItem: false, isChecked: false, style: "Body", separator: { stamp: "" } } },
];

for (const { name, line } of onAnElement) {
    await page.evaluate(async (element) => {
        const surface = await import("./js/checklistTextEditor.js");
        const container = document.createElement("div");
        container.setAttribute("contenteditable", "true");
        document.body.replaceChildren(container);

        // Blazor's end, recording what it is asked and answering nothing - so whatever reaches the
        // element's line got there by the browser typing it, not by an answer being drawn.
        window.asked = [];
        const blazor = {
            invokeMethod: (method, json) => {
                if (method === "Edit") {
                    window.asked.push(JSON.parse(json));
                }
                return null;
            },
            invokeMethodAsync: () => Promise.resolve(null),
        };
        const writing = { text: "Before", isChecklistItem: false, isChecked: false, style: "Body" };
        surface.initialize(container, blazor, JSON.stringify([writing, element]), {});
        window.surfaceUnderTest = { surface, container };

        container.focus();
        const range = document.createRange();
        range.setStart(container.children[1], 0);
        range.collapse(true);
        window.getSelection().removeAllRanges();
        window.getSelection().addRange(range);
    }, line);

    await page.keyboard.type("B");

    results.push(await page.evaluate((name) => {
        const { surface, container } = window.surfaceUnderTest;
        const elementLine = container.children[1];
        const stray = Array.from(elementLine.childNodes)
            .filter((node) => node.nodeType === Node.TEXT_NODE && node.textContent.length > 0)
            .map((node) => node.textContent)
            .join("");
        const handedOver = window.asked.some((request) => request.command === "replace" && request.text === "B");
        surface.dispose(container);
        container.remove();

        const wrong = [];
        if (stray) {
            wrong.push(`the browser typed ${JSON.stringify(stray)} into the element's own line`);
        }
        if (!handedOver) {
            wrong.push(`the key was not handed to C# as a replace (asked: ${JSON.stringify(window.asked.map((request) => request.command))})`);
        }
        return { name: `a key pressed on ${name}'s line goes to C#`, passed: wrong.length === 0, detail: wrong.join("; ") };
    }, name));
}

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
    console.error(`\n${failed.length} of ${results.length} note surface checks failed.`);
    process.exit(1);
}

console.log(`\nAll ${results.length} note surface checks passed.`);
