// Parses every Mermaid diagram in info/uml/ and fails on any that would not draw.
//
// The gap this closes: the diagrams have no other check. Nothing in the solution reads them, so a
// broken one is found by whoever opens the page on GitHub - where it renders as an error box rather
// than as nothing, which is worse than a missing diagram. Mermaid's own parser is the only honest
// judge; reading the source and thinking it looks right is a different test that always passes.
//
// It parses rather than renders, deliberately. Rendering would need a size to be right about, and
// "readable" is not a thing a script can assert - see info/uml/README.md, which says to look at a
// diagram that has grown rather than trust this exit code.
//
// README.md is skipped: it is the index, holds no diagrams, and quotes this file's own extraction
// pattern, which would otherwise match itself.
//
// Usage: node ci/verify-diagrams.mjs [directory]
import { readFileSync, readdirSync } from "node:fs";
import { join, resolve } from "node:path";
import { chromium } from "playwright";

const directory = resolve(process.argv[2] ?? "info/uml");

const blocks = readdirSync(directory)
  .filter(file => file.endsWith(".md") && file !== "README.md")
  .flatMap(file =>
    [...readFileSync(join(directory, file), "utf8").matchAll(/```mermaid\n([\s\S]*?)```/g)]
      .map((match, index) => ({ name: `${file} #${index + 1}`, code: match[1] })));

if (blocks.length === 0) {
  console.error(`No diagrams found in ${directory}. That is almost certainly a broken path.`);
  process.exit(1);
}

const browser = await chromium.launch();
try {
  const page = await browser.newPage();
  await page.setContent("<body>");
  await page.addScriptTag({ url: "https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.min.js" });

  let failed = 0;
  for (const block of blocks) {
    const error = await page.evaluate(async code => {
      try {
        await mermaid.parse(code);
        return null;
      } catch (thrown) {
        return String(thrown?.message ?? thrown);
      }
    }, block.code);

    if (error) {
      failed++;
      console.error(`FAIL ${block.name}: ${error.split("\n")[0]}`);
    } else {
      console.log(`OK   ${block.name}`);
    }
  }

  console.log(`${blocks.length} diagrams, ${failed} failed`);
  process.exit(failed ? 1 : 0);
} finally {
  await browser.close();
}
