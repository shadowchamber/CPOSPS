/**
 * Load PosTerminal.Extension in Playwright, open the side panel, verify HWS API link.
 * Usage: node scripts/verify-extension-api.mjs
 */
import { createRequire } from "node:module";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const iigRoot = "C:/_sources/DROP-CDRO/D365Commerce/src/IIGCore";
const requireFromIig = createRequire(path.join(iigRoot, "package.json"));
const { chromium } = requireFromIig("playwright");

const extensionPath = path.resolve(__dirname, "..").split("\\").join("/");
const manifestPath = path.join(extensionPath, "manifest.json");
if (!fs.existsSync(manifestPath)) {
  console.error(`Missing manifest: ${manifestPath}`);
  process.exit(1);
}

const expectedBase = "https://erp-iig-ude-016:450/HardwareStation";
const userDataDir = fs.mkdtempSync(path.join(os.tmpdir(), "ptw-ext-"));

const context = await chromium.launchPersistentContext(userDataDir, {
  headless: false,
  ignoreHTTPSErrors: true,
  args: [
    `--disable-extensions-except=${extensionPath}`,
    `--load-extension=${extensionPath}`,
  ],
  ignoreDefaultArgs: ["--disable-extensions"],
});

try {
  let worker = context.serviceWorkers()[0];
  if (!worker) {
    worker = await context.waitForEvent("serviceworker", { timeout: 30000 });
  }
  const extId = new URL(worker.url()).host;
  console.log(`extension id: ${extId}`);
  console.log(`api base expected: ${expectedBase}`);

  const panel = await context.newPage();
  await panel.goto(`chrome-extension://${extId}/sidepanel.html`, {
    waitUntil: "domcontentloaded",
    timeout: 30000,
  });

  await panel.waitForFunction(
    () => {
      const el = document.getElementById("link-status");
      const text = (el && el.textContent) || "";
      return text.startsWith("Linked") || text.startsWith("API offline");
    },
    null,
    { timeout: 20000 },
  );

  const status = (await panel.locator("#link-status").textContent()) || "";
  const label = (await panel.locator("#api-label").textContent()) || "";
  console.log(`api label: ${label}`);
  console.log(`link status: ${status}`);

  if (!label.includes("450/HardwareStation")) {
    throw new Error(`API base not updated in panel: ${label}`);
  }
  if (!/^Linked · \d+/.test(status)) {
    throw new Error(`Extension did not link to HWS API with revision: ${status}`);
  }

  console.log("VERIFY OK");
  process.exitCode = 0;
} catch (error) {
  console.error(`VERIFY FAIL: ${error instanceof Error ? error.message : error}`);
  process.exitCode = 1;
} finally {
  await context.close().catch(() => {});
}
