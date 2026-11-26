# Chrome Web Store listing checklist (CPOSPS)

Dashboard: [Chrome Web Store Developer Dashboard](https://chrome.google.com/webstore/devconsole)

## Product

- **Name:** CPOSPS
- **Short description:** Side panel for Hardware Station PosTerminal peripherals (scanner, drawers, MSR, PIN, scale, printer, light, and more).
- **Default API base:** `https://localhost:450/HardwareStation`
- **Privacy policy URL (listing):** host `docs/privacy.html` (or `extension/privacy.html`) on a public HTTPS URL you control, then paste that URL in the listing.

## Privacy

- Ship `extension/privacy.html` inside the zip; host a public copy for the Store listing field.
- Declare: no account login; only Hardware Station API base URL in `chrome.storage.sync`; no analytics / no third-party trackers by default.
- Host permissions: localhost / 127.0.0.1 by default; optional host permission when the user sets another API base.

## Screenshots / icons

- Screenshots: put **real** captures of the loaded extension in `extension/screenshots/` (1280×800 or 640×400), then upload those in the dashboard. Do not use mock/generated images.
- Icons: `extension/icons/icon{16,32,48,128}.png` (bundled in the zip).

## Store description draft

```
CPOSPS (Chrome POS Peripheral Simulator) is a side-panel extension for Chrome and Edge that talks to a Dynamics 365 Commerce Hardware Station hosting the PosTerminal API.

Use it to drive simulated peripherals: barcode scanner, cash drawers, MSR, PIN pad, scale, printer/receipt, lane light, signature, coin dispenser, remote order display, keyboard wedge, and payment terminal helpers.

Default endpoint: https://localhost:450/HardwareStation
Change the API base under Options. Other hosts request optional host permission when you save.

Requires a running Hardware Station with the CPOSPS / PosTerminal.Web library installed. Self-signed HTTPS certificates must be trusted in the browser.
```

## Local zip pack

```powershell
pwsh -File extension/scripts/pack-extension.ps1
# → artifacts/cposps-extension.zip
```

CI also packs this zip on every CI run (artifact `cposps-extension`).

## Publish from GitHub Actions

Workflow: [`.github/workflows/publish-extension.yml`](../.github/workflows/publish-extension.yml)

| Trigger | Behavior |
|---------|----------|
| `workflow_dispatch` | Pack + **upload** draft. Set `publish_live=true` to also **publish**. |
| Tag `extension-v*` | Pack + upload + **publish**. |

### One-time Google OAuth (API access)

Chrome Web Store API has **no** GitHub OIDC — long-lived OAuth secrets are required.

1. Google Cloud project → enable **Chrome Web Store API**.
2. Create OAuth **Desktop** client → save Client ID + Client Secret.  
   Guide: [chrome-webstore-upload-keys](https://github.com/fregante/chrome-webstore-upload-keys).
3. Locally: `npx chrome-webstore-upload-keys` → get Refresh Token (browser consent as the same Google account that owns the dashboard).
4. In the developer dashboard, create the CPOSPS item if it does not exist yet; note the **extension / item ID** and your **publisher ID** (from the dashboard URL path).

### GitHub secrets

Repo → Settings → Secrets and variables → Actions:

| Secret | Value |
|--------|--------|
| `CHROME_PUBLISHER_ID` | Publisher UUID from your dashboard URL |
| `CHROME_EXTENSION_ID` | Extension / item ID from the dashboard item |
| `CHROME_CLIENT_ID` | OAuth client id |
| `CHROME_CLIENT_SECRET` | OAuth client secret |
| `CHROME_REFRESH_TOKEN` | Refresh token from `chrome-webstore-upload-keys` |

### First publish flow

1. Set the five secrets above.
2. Host a public privacy policy URL and set it on the listing.
3. In the dashboard: create listing, upload real screenshots, set privacy URL, single-purpose + permissions justification.
4. Actions → **Publish Extension** → Run workflow (upload only first), confirm package appears as draft.
5. Re-run with `publish_live=true`, or push tag `extension-v1.0.0` (align with `manifest.json` version when you care about version alignment).

Bump `extension/manifest.json` `version` before each Store upload.

## Listing checklist

- [ ] Manifest name is **CPOSPS**
- [ ] Icons 16/32/48/128 present
- [ ] Privacy policy URL set (public HTTPS you control)
- [ ] Screenshots uploaded from `extension/screenshots/` (real captures)
- [ ] Single purpose justified (POS peripheral simulation UI)
- [ ] Host / optional host permissions explained in listing
- [ ] GitHub secrets set; test `Publish Extension` upload
- [ ] Version bumped in `manifest.json` before each upload
- [ ] Tested Load unpacked + Store package on Chrome and Edge

## Related

- [extension/README.md](../extension/README.md)
- [usage.md](usage.md)
