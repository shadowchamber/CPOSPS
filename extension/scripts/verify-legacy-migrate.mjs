/**
 * Seed a legacy SIM apiBase and confirm migration to localhost Hardware Station.
 * Usage:
 *   set PLAYWRIGHT_NODE_MODULES=<dir that contains playwright>
 *   node scripts/verify-legacy-migrate.mjs
 */
import { createRequire } from "node:module";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const playwrightRoot = process.env.PLAYWRIGHT_NODE_MODULES;
if (!playwrightRoot) {
  console.error("Set PLAYWRIGHT_NODE_MODULES to a folder that contains playwright (node_modules parent or package root).");
  process.exit(1);
}
const requireFrom = createRequire(path.join(playwrightRoot, "package.json"));
const { chromium } = requireFrom("playwright");
const extensionPath = path.resolve(__dirname, "..").split("\\").join("/");
const userDataDir = fs.mkdtempSync(path.join(os.tmpdir(), "cposps-ext-mig-"));
const legacyBase = process.env.CPOSPS_LEGACY_API_BASE || "https://127.0.0.1:446/SIM";

const context = await chromium.launchPersistentContext(userDataDir, {
  headless: false,
  ignoreHTTPSErrors: true,
  args: [`--disable-extensions-except=${extensionPath}`, `--load-extension=${extensionPath}`],
  ignoreDefaultArgs: ["--disable-extensions"],
});

try {
  let worker = context.serviceWorkers()[0] || await context.waitForEvent("serviceworker", { timeout: 30000 });
  const extId = new URL(worker.url()).host;
  const panel = await context.newPage();
  await panel.goto(`chrome-extension://${extId}/sidepanel.html`, { waitUntil: "domcontentloaded" });
  await panel.evaluate(async (base) => {
    await chrome.storage.sync.set({ apiBase: base });
  }, legacyBase);
  await panel.reload({ waitUntil: "domcontentloaded" });
  await panel.waitForFunction(() => {
    const el = document.getElementById("api-label");
    const st = document.getElementById("link-status");
    return el && st && el.textContent && st.textContent &&
      (st.textContent.startsWith("Linked") || st.textContent.startsWith("API offline"));
  }, null, { timeout: 20000 });
  const label = await panel.locator("#api-label").textContent();
  const status = await panel.locator("#link-status").textContent();
  console.log("label=", label);
  console.log("status=", status);
  if (!label.includes("450/HardwareStation")) throw new Error("legacy SIM not migrated: " + label);
  if (!/^Linked · \d+/.test(status || "")) throw new Error("not linked: " + status);
  console.log("VERIFY OK");
} catch (e) {
  console.error("VERIFY FAIL", e);
  process.exitCode = 1;
} finally {
  await context.close().catch(() => {});
}
