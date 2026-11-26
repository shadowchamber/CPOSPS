# Chrome Web Store listing checklist (CPOSPS)

## Product

- **Name:** CPOSPS
- **Short description:** Side panel for Hardware Station PosTerminal peripherals (scanner, drawers, MSR, PIN, scale, printer, light, and more).
- **Default API base:** `https://localhost:450/HardwareStation`

## Privacy

- Host a privacy policy page. Ship `extension/privacy.html` with the zip or host it publicly and link it in the Store listing.
- Declare: no account login in the extension; only the Hardware Station API base URL is stored (`chrome.storage.sync`); no analytics / no third-party trackers by default.
- Host permissions: localhost / 127.0.0.1 by default; optional host permission requested when the user sets another API base.

## Screenshots

1. Put PNG/JPEG screenshots in `extension/screenshots/` (1280×800 or 640×400 recommended).
2. Capture at least: side panel overview, Options API base, connected to localhost HWS.
3. Do not include personal data or production credentials.

## Store description draft

```
CPOSPS (Chrome POS Peripheral Simulator) is a side-panel extension for Chrome and Edge that talks to a Dynamics 365 Commerce Hardware Station hosting the PosTerminal API.

Use it to drive simulated peripherals: barcode scanner, cash drawers, MSR, PIN pad, scale, printer/receipt, lane light, signature, coin dispenser, remote order display, keyboard wedge, and payment terminal helpers.

Default endpoint: https://localhost:450/HardwareStation
Change the API base under Options. Other hosts request optional host permission when you save.

Requires a running Hardware Station with the CPOSPS / PosTerminal.Web library installed. Self-signed HTTPS certificates must be trusted in the browser.
```

## Zip pack steps

```powershell
# From repo root
$zip = "artifacts\cposps-extension.zip"
New-Item -ItemType Directory -Force -Path artifacts | Out-Null
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path @(
  'extension\manifest.json',
  'extension\background.js',
  'extension\options.html','extension\options.js','extension\options.css',
  'extension\sidepanel.html','extension\sidepanel.js','extension\sidepanel.css',
  'extension\privacy.html',
  'extension\icons',
  'extension\README.md'
) -DestinationPath $zip
Write-Host "Packed $zip"
```

Upload that zip in [Chrome Developer Dashboard](https://chrome.google.com/webstore/devconsole).

## Listing checklist

- [ ] Manifest name is **CPOSPS**
- [ ] Icons 16/32/48/128 present
- [ ] Privacy policy URL set
- [ ] Screenshots uploaded (folder `extension/screenshots/`)
- [ ] Single purpose justified (POS peripheral simulation UI)
- [ ] Host / optional host permissions explained in listing
- [ ] Version bumped in `manifest.json` before each upload
- [ ] Tested Load unpacked + Store package on Chrome and Edge

## Related

- [extension/README.md](../extension/README.md)
- [usage.md](usage.md)
