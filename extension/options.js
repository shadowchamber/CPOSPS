const DEFAULT_API_BASE = "https://localhost:450/HardwareStation";

function isLegacyApiBase(base) {
  const b = String(base || "").trim();
  if (!b) return true;
  // Migrate old PosTerminal / SIM / lab host defaults to localhost.
  return (
    /:446\/SIM\/?$/i.test(b) ||
    /\/SIM\/?$/i.test(b) ||
    /localhost:5058\/?$/i.test(b) ||
    /127\.0\.0\.1:5058\/?$/i.test(b)
  );
}

function normalizeBase(base) {
  const cleaned = String(base || DEFAULT_API_BASE).trim().replace(/\/+$/, "");
  return isLegacyApiBase(cleaned) ? DEFAULT_API_BASE : cleaned;
}

const input = document.getElementById("apiBase");
const status = document.getElementById("status");

function showStatus(text, ok) {
  status.hidden = false;
  status.textContent = text;
  status.style.color = ok === false ? "#c44" : "";
}

chrome.storage.sync.get({ apiBase: DEFAULT_API_BASE }, (data) => {
  const next = normalizeBase(data.apiBase);
  input.value = next;
  if (next !== String(data.apiBase || "").trim().replace(/\/+$/, "")) {
    chrome.storage.sync.set({ apiBase: next });
  }
});

document.getElementById("reset").onclick = () => {
  input.value = DEFAULT_API_BASE;
};

document.getElementById("save").onclick = async () => {
  let base = normalizeBase(input.value);
  input.value = base;

  try {
    const origin = new URL(base).origin + "/*";
    const have = await chrome.permissions.contains({ origins: [origin] });
    if (!have) {
      const granted = await chrome.permissions.request({ origins: [origin] });
      if (!granted) {
        showStatus("Permission denied for that host", false);
        return;
      }
    }
  } catch (_) {
    /* invalid URL still saved; fetch will fail clearly in panel */
  }

  await chrome.storage.sync.set({ apiBase: base });
  showStatus("Saved", true);
  setTimeout(() => { status.hidden = true; }, 1500);
};
