// Runs Orbit.Web's own locationMap.js against a real Leaflet map and fails unless a refresh of the pins
// does what it says.
//
// The gap this closes is `updateLocations`, which is what the map's Refresh button actually calls. It is
// deliberately *not* a redraw - the pan and the zoom somebody pressed it from are kept, a pin that moved
// is moved in place so an open popup they pressed to read survives, a pin that arrived is added and one
// that is gone is taken off. Every one of those decisions is invisible from outside: a refresh that
// silently did nothing would look exactly like a refresh with nothing new behind it, which is how
// "Refresh on the map does nothing" (2026-09-18, issue #293) was reported and why it could not be
// reproduced from the code. bUnit reaches none of it - Leaflet is a browser library and the markers are
// DOM nodes it positions.
//
// The tiles are stubbed rather than fetched. They are the one third-party request Orbit makes, they are
// nothing to do with which pins are on the map, and a harness that needs the internet is a harness that
// fails for the wrong reason - see mapTiles.js, which is the seam that makes stubbing them honest.
//
// Usage: node ci/verify-map-markers.mjs [wwwrootPath]
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize, resolve } from "node:path";
import { chromium } from "playwright";

const wwwroot = resolve(process.argv[2] ?? "src/Clients/Orbit.Web/wwwroot");

const contentTypes = {
    ".js": "text/javascript",
    ".html": "text/html",
    ".css": "text/css",
    ".png": "image/png",
    ".json": "application/json",
};

// The map needs Leaflet the way index.html loads it - a classic script tag putting `L` on the window -
// its stylesheet, and a container with a real size, because Leaflet measures its container once at
// creation and lays the tile grid out from that.
const harness = `<!doctype html><title>map markers harness</title>
<link rel="stylesheet" href="/vendor/leaflet/leaflet.css" />
<style>*{box-sizing:border-box;margin:0}#map{width:600px;height:400px}</style>
<div id="map"></div>
<script src="/vendor/leaflet/leaflet.js"></script>`;

// Only ever asked for the module, Leaflet and one page, but path traversal is refused anyway - a test
// server that will read any file on the machine is a habit worth not forming.
const server = createServer(async (request, response) => {
    const requestedPath = normalize(decodeURIComponent(new URL(request.url, "http://localhost").pathname));
    if (requestedPath === "/" || requestedPath === "/index.html") {
        response.writeHead(200, { "content-type": "text/html" });
        response.end(harness);
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

await page.setViewportSize({ width: 900, height: 700 });
await page.goto(`${origin}/`);

const results = await page.evaluate(async () => {
    const maps = await import("./js/locationMap.js");

    // What mapTiles.js hands the map, minus the request. Orbit asks for the tiles rather than adding
    // them precisely so a reader who refused third-party requests gets a map without them - so a map
    // with none is a state the page already supports, not a state invented for this check.
    window.OrbitMapTiles = { addTo: () => {} };

    const results = [];
    const check = async (name, run) => {
        try {
            const detail = await run();
            results.push({ name, passed: detail === true, detail: detail === true ? "" : String(detail) });
        } catch (error) {
            results.push({ name, passed: false, detail: `threw: ${error.message}` });
        }
    };

    const pins = () => [...document.querySelectorAll("#map .leaflet-marker-icon")];
    const whereEachPinIs = () => pins().map((pin) => pin.style.transform);
    const ala = { key: "ala", latitude: 52.2297, longitude: 21.0122, label: "Ala" };
    const bogdan = { key: "bogdan", latitude: 50.0647, longitude: 19.9450, label: "Bogdan" };

    await check("every point handed in gets a pin", async () => {
        await maps.showLocations("map", [ala, bogdan]);
        return pins().length === 2 || `${pins().length} pins for two points`;
    });

    await check("a point that moved moves the pin it already had", async () => {
        await maps.showLocations("map", [ala, bogdan]);
        const before = pins();
        const placesBefore = whereEachPinIs();

        maps.updateLocations("map", [{ ...ala, latitude: 54.3520, longitude: 18.6466 }, bogdan]);

        const after = pins();
        if (after.length !== 2) {
            return `${after.length} pins after the refresh`;
        }
        // The same nodes, not new ones: a replaced marker is a new element, and everything the reader
        // had done to the old one - an open popup, most of all - goes with it.
        if (!before.every((pin) => after.includes(pin))) {
            return "the refresh replaced the markers rather than moving them";
        }
        return whereEachPinIs().join("|") !== placesBefore.join("|")
            || "the pin did not move, though the point did";
    });

    await check("a popup somebody opened survives the refresh", async () => {
        await maps.showLocations("map", [ala, bogdan]);
        maps.focusOn("map", "ala");
        const opened = document.querySelector("#map .leaflet-popup");
        if (!opened) {
            return "no popup opened to begin with";
        }

        maps.updateLocations("map", [{ ...ala, latitude: 54.3520, longitude: 18.6466 }, bogdan]);

        const still = document.querySelector("#map .leaflet-popup");
        if (!still) {
            return "the popup was closed by the refresh";
        }
        if (!(still.textContent || "").includes("Ala")) {
            return `the popup now reads ${JSON.stringify(still.textContent)}`;
        }
        // The pin it belongs to has to be the only Ala on the map. A refresh that replaced the marker
        // instead of moving it leaves the old one behind still holding this popup open, which looks
        // like the popup surviving and is a second pin for a person who is in one place.
        return pins().length === 2 || `the popup survived, but onto ${pins().length} pins for two people`;
    });

    await check("a point that has arrived gets a pin of its own", async () => {
        await maps.showLocations("map", [ala]);
        maps.updateLocations("map", [ala, bogdan]);
        return pins().length === 2 || `${pins().length} pins after one arrived`;
    });

    await check("a point that is gone has its pin taken off", async () => {
        await maps.showLocations("map", [ala, bogdan]);
        maps.updateLocations("map", [ala]);
        return pins().length === 1 || `${pins().length} pins after one went away`;
    });

    await check("two points with no key of their own are told apart by where they are", async () => {
        // A point without a key is keyed by its coordinates. Two of them collapsing into one pin would
        // be a place quietly missing from the map, with nothing to say so.
        await maps.showLocations("map", [
            { latitude: 52.2297, longitude: 21.0122, label: "One" },
            { latitude: 50.0647, longitude: 19.9450, label: "Another" },
        ]);
        return pins().length === 2 || `${pins().length} pins for two unkeyed points`;
    });

    await check("a refresh of a map that is not there does nothing rather than throwing", () => {
        maps.updateLocations("no-such-map", [ala]);
        return true;
    });

    maps.dispose("map");
    return results;
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
    console.error(`\n${failed.length} of ${results.length} map marker checks failed.`);
    process.exit(1);
}

console.log(`\nAll ${results.length} map marker checks passed.`);
