# CPOSPS browser extension

Side panel for **CPOSPS** (Chrome POS Peripheral Simulator). Talks to a Dynamics 365 Commerce **Hardware Station** PosTerminal API and shows peripheral controls (scanner, drawers, MSR, PIN, scale, printer, light, signature, coin, remote order, wedge, payment terminal, Station API).

## Load unpacked

1. Chrome: `chrome://extensions` → Developer mode → **Load unpacked**  
   Edge: `edge://extensions` → Developer mode → **Load unpacked**
2. Select folder: `extension/` (this directory)
3. Click the extension action (toolbar) to open the **side panel**, or press **Alt+P**. Confirm under `chrome://extensions/shortcuts` (command **Open CPOSPS side panel**).

## API target

**Default:** `https://localhost:450/HardwareStation`

Change via extension **Options** (gear in panel). Saving a non-localhost host requests optional host permission for that origin.

Uses:

- `POST {base}/api/state`
- `POST {base}/api/ui` (body includes `action`)
- `POST {base}/api/station` (body includes `route`)

`host_permissions` cover localhost and 127.0.0.1 (HTTP/HTTPS on port 450). Other hosts: Options → optional host permission.

Legacy lab defaults (e.g. old SIM URLs or `erp-iig-ude-016`) are migrated to localhost automatically.

## Side panel left / right

Chrome/Edge choose panel side in browser settings (Appearance → Side panel), not in the extension.

## Chrome Web Store

See [docs/chrome-web-store.md](../docs/chrome-web-store.md) for listing checklist, privacy page, screenshots, zip pack, and **GitHub Actions publish** (`Publish Extension` workflow).

```powershell
pwsh -File extension/scripts/pack-extension.ps1
# → artifacts/cposps-extension.zip
```

Privacy policy: [privacy.html](privacy.html) (public listing copy: `docs/privacy.html` → GitHub Pages).

## Notes

- Same device state as the web terminal (shared `SimulatorEngine` store on the server).
- Self-signed HTTPS must already be trusted in the browser profile or fetches fail.
- No POS iframe — devices only.
- Requires Hardware Station with the **CPOSPS** NuGet library (or equivalent PosTerminal.Web HWS extension) installed.
