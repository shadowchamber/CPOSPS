import { createRequire } from "node:module";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const requireFromIig = createRequire("C:/_sources/DROP-CDRO/D365Commerce/src/IIGCore/package.json");
const { chromium } = requireFromIig("playwright");
const extensionPath = path.resolve(__dirname, "..").split("\\").join("/");
const userDataDir = fs.mkdtempSync(path.join(os.tmpdir(), "ptw-ext-mig-"));

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
  // Seed legacy value then reload
  await panel.evaluate(async () => {
    await chrome.storage.sync.set({ apiBase: "https://erp-iig-ude-016:446/SIM" });
  });
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