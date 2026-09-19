// Runs Orbit.Web's own menuAnchor.js in a real browser and fails unless a panel lands where it should.
//
// The gap this closes: every line of that module is `getBoundingClientRect` and `window.innerWidth`,
// which bUnit has neither of - its DOM measures everything as zero - so the whole of "where does this
// panel go" was reachable by no test. It is not decoration either. A dropdown inside a scroller is
// clipped however high its z-index, which is why these panels are `position: fixed` at all, and fixed
// means every edge of them is arithmetic done here.
//
// It was a visible fault twice. A rule that anchored a panel to the *other* edge crushed it to nothing
// on the editor rail; and on 2026-09-18 the tag browser, which hangs off an "Add tag" button as wide as
// those two words, came out about ninety pixels across on a phone - names ellipsised away and a sideways
// scrollbar - reported with a picture of it. The floor added for that (`minimumWidth`) is checked here,
// including the thing a floor must not do: stick out past the window it was meant to fit in.
//
// Usage: node ci/verify-menu-anchor.mjs [wwwrootPath]
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
        response.end("<!doctype html><title>menu anchor harness</title>"
            // No stylesheet is loaded, so the harness states the two things the real page's CSS states
            // and this module's arithmetic assumes: a box measures as the width it is told, and the page
            // itself does not push anything around.
            + "<style>*{box-sizing:border-box;margin:0}body{padding:0}</style>");
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

// A phone-sized window, because that is where the fault was seen and where the floor matters. The two
// checks about a window too narrow for the floor set their own.
await page.setViewportSize({ width: 390, height: 780 });
await page.goto(`${origin}/`);

const results = await page.evaluate(async () => {
    const anchor = await import("./js/menuAnchor.js");

    // The gap this module keeps between a panel and what it belongs to, and the margin it keeps from
    // the window's edges. Written out rather than imported because it is not exported - if it changes
    // there, these numbers have to be changed here on purpose.
    const gap = 4;
    const results = [];

    const check = (name, run) => {
        try {
            const detail = run();
            results.push({ name, passed: detail === true, detail: detail === true ? "" : String(detail) });
        } catch (error) {
            results.push({ name, passed: false, detail: `threw: ${error.message}` });
        }
    };

    /// One field with its panel drawn as a sibling after it, which is the arrangement this module
    /// exists for: the panel is not around the box, so "underneath" is a position only measurement
    /// finds. Handed back measured, and taken off the page again by the caller.
    const lay = ({ fieldWidth, panelHeight = 200, left = 20, top = 100 }) => {
        const holder = document.createElement("div");
        holder.style.position = "absolute";
        holder.style.left = `${left}px`;
        holder.style.top = `${top}px`;
        document.body.appendChild(holder);

        const field = document.createElement("button");
        field.className = "the-field";
        field.style.width = `${fieldWidth}px`;
        field.style.height = "30px";
        holder.appendChild(field);

        const panel = document.createElement("div");
        panel.style.height = `${panelHeight}px`;
        holder.appendChild(panel);

        return {
            field, panel, holder,
            done: () => holder.remove(),
        };
    };

    check("a panel takes the width of the field it hangs off", () => {
        const laid = lay({ fieldWidth: 300 });
        anchor.anchorToField(laid.panel, ".the-field", 240);
        const width = laid.panel.getBoundingClientRect().width;
        laid.done();
        // The floor is below the field, so it does not apply: a list of completions wider than the box
        // it completes reads as being about something else.
        return width === 300 || `the panel came out ${width}px wide beside a 300px field`;
    });

    check("a panel hanging off a narrow button is not squeezed below its floor", () => {
        // The fault reported on 2026-09-18: "Add tag" is as wide as those two words, and the browser
        // hanging off it held rows of a tick, a name, a count and two colour buttons.
        const laid = lay({ fieldWidth: 90 });
        anchor.anchorToField(laid.panel, ".the-field", 240);
        const width = laid.panel.getBoundingClientRect().width;
        laid.done();
        return width === 240 || `the panel came out ${width}px wide off a 90px button`;
    });

    check("a panel asked for no floor is exactly its field's width", () => {
        // What the name suggestions pass, and what must not change: they are a list hanging off the box
        // being typed into, and a floor would make them wider than what they complete.
        const laid = lay({ fieldWidth: 90 });
        anchor.anchorToField(laid.panel, ".the-field");
        const width = laid.panel.getBoundingClientRect().width;
        laid.done();
        return width === 90 || `the panel came out ${width}px wide with no floor asked for`;
    });

    check("a panel never hangs off the right of the window", () => {
        const laid = lay({ fieldWidth: 90, left: window.innerWidth - 100 });
        anchor.anchorToField(laid.panel, ".the-field", 240);
        const box = laid.panel.getBoundingClientRect();
        laid.done();
        return box.right <= window.innerWidth - gap + 0.5
            || `its right edge is at ${box.right} in a window ${window.innerWidth} wide`;
    });

    check("a panel with no room below opens above its field", () => {
        const laid = lay({ fieldWidth: 300, panelHeight: 300, top: window.innerHeight - 60 });
        const fieldTop = laid.field.getBoundingClientRect().top;
        anchor.anchorToField(laid.panel, ".the-field", 240);
        const box = laid.panel.getBoundingClientRect();
        laid.done();
        return box.bottom <= fieldTop + 0.5
            || `the panel's bottom is at ${box.bottom} and the field's top at ${fieldTop}`;
    });

    check("a menu is pulled back on screen rather than off the right edge", () => {
        // anchorToTrigger's own rule, and the one that is easy to lose: the menu is aligned to the
        // trigger's right edge, which puts it off the page for a trigger near it.
        const holder = document.createElement("div");
        holder.style.position = "absolute";
        holder.style.left = `${window.innerWidth - 40}px`;
        holder.style.top = "100px";
        document.body.appendChild(holder);

        const trigger = document.createElement("button");
        trigger.className = "the-trigger";
        trigger.style.width = "30px";
        trigger.style.height = "30px";
        holder.appendChild(trigger);

        const dropdown = document.createElement("div");
        dropdown.style.width = "220px";
        dropdown.style.height = "150px";
        holder.appendChild(dropdown);

        anchor.anchorToTrigger(dropdown, ".the-trigger");
        const box = dropdown.getBoundingClientRect();
        holder.remove();
        return box.right <= window.innerWidth - gap + 0.5 && box.left >= gap - 0.5
            || `the menu sits from ${box.left} to ${box.right} in a window ${window.innerWidth} wide`;
    });

    return results;
});

// The floor must give way to the window: a minimum that does not fit is a panel with its right-hand
// half off the screen, which is the same fault it was added to cure. Its own viewport, narrower than
// the floor, so this is the only place the arithmetic can be seen.
await page.setViewportSize({ width: 200, height: 600 });
const squeezed = await page.evaluate(async () => {
    const anchor = await import("./js/menuAnchor.js");

    const holder = document.createElement("div");
    holder.style.position = "absolute";
    holder.style.left = "10px";
    holder.style.top = "50px";
    document.body.appendChild(holder);

    const field = document.createElement("button");
    field.className = "the-field";
    field.style.width = "90px";
    field.style.height = "30px";
    holder.appendChild(field);

    const panel = document.createElement("div");
    panel.style.height = "150px";
    holder.appendChild(panel);

    anchor.anchorToField(panel, ".the-field", 240);
    const box = panel.getBoundingClientRect();
    holder.remove();
    return { width: box.width, right: box.right, windowWidth: window.innerWidth };
});

results.push({
    name: "a floor wider than the window gives way to the window",
    passed: squeezed.width === squeezed.windowWidth - 8 && squeezed.right <= squeezed.windowWidth - 4 + 0.5,
    detail: squeezed.width === squeezed.windowWidth - 8 && squeezed.right <= squeezed.windowWidth - 4 + 0.5
        ? ""
        : `a 240px floor in a ${squeezed.windowWidth}px window came out ${squeezed.width}px wide, ending at ${squeezed.right}`,
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
    console.error(`\n${failed.length} of ${results.length} menu anchor checks failed.`);
    process.exit(1);
}

console.log(`\nAll ${results.length} menu anchor checks passed.`);
