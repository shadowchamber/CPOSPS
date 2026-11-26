const DEFAULT_API_BASE = "https://localhost:450/HardwareStation";

let lastWindowId = chrome.windows.WINDOW_ID_NONE;
let panelOpen = false;

function isLegacyApiBase(base) {
  return !base || /erp-iig-ude-016/i.test(base) || /:446\/SIM\/?$/i.test(base) || /\/SIM\/?$/i.test(base) || /localhost:5058\/?$/i.test(base) || /127\.0\.0\.1:5058\/?$/i.test(base);
}

async function ensureApiBase() {
  const { apiBase } = await chrome.storage.sync.get("apiBase");
  if (isLegacyApiBase(apiBase)) {
    await chrome.storage.sync.set({ apiBase: DEFAULT_API_BASE });
  }
}

async function ensurePanelBehavior() {
  await chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true });
}

async function rememberFocusedWindow() {
  try {
    const win = await chrome.windows.getLastFocused({ populate: false });
    if (win?.id != null) lastWindowId = win.id;
  } catch (_) {
    /* ignore */
  }
}

chrome.runtime.onInstalled.addListener(async () => {
  await ensureApiBase();
  await ensurePanelBehavior();
  await rememberFocusedWindow();
});

chrome.runtime.onStartup.addListener(async () => {
  await ensureApiBase();
  await ensurePanelBehavior();
  await rememberFocusedWindow();
});

chrome.windows.onFocusChanged.addListener((windowId) => {
  if (windowId !== chrome.windows.WINDOW_ID_NONE) lastWindowId = windowId;
});

// Side panel page holds this port while it is open (including user closing via X).
chrome.runtime.onConnect.addListener((port) => {
  if (port.name !== "sidepanel") return;
  panelOpen = true;
  port.onDisconnect.addListener(() => {
    panelOpen = false;
  });
});

// open/close must run in the same turn as the command (no await before it).
chrome.commands.onCommand.addListener((command) => {
  if (command !== "open-pos-devices") return;
  const windowId = lastWindowId;
  if (windowId === chrome.windows.WINDOW_ID_NONE) {
    console.error("open-pos-devices: no focused window cached yet");
    return;
  }

  if (panelOpen) {
    if (typeof chrome.sidePanel.close !== "function") {
      console.error("open-pos-devices: sidePanel.close() not supported in this browser");
      return;
    }
    chrome.sidePanel.close({ windowId }).catch((err) => {
      console.error("open-pos-devices close failed", err);
    });
    return;
  }

  chrome.sidePanel.open({ windowId }).catch((err) => {
    console.error("open-pos-devices open failed", err);
  });
});

rememberFocusedWindow();
ensureApiBase();
