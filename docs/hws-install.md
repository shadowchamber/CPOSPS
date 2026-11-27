# Hardware Station install overview

## What CPOSPS provides to HWS

Hardware Station extension with the PosTerminal.Web engine **in-process**, served on the Hardware Station HTTP surface (`IController`).

| Surface | How |
|--|--|
| PosTerminal API | Controllers under `RoutePrefix("api")` |
| Device controllers | `PTWLightDevice`, coin dispenser, remote order display, … |
| Peripheral hosts (`PTW*` names) | In-process `SimulatorEngine.Station(...)` via `PtwProfile*` |

## Typical install path (host product)

1. Publish or restore **CPOSPS** from nuget.org (see [packaging.md](packaging.md) — GitHub Actions **Release** workflow).
2. `PackageReference` `CPOSPS` from your Hardware Station extension project (do **not** copy Microsoft Commerce DLLs into your own nupkg; do **not** use a local artifacts feed or host-side Promote MSBuild).
3. Deploy alongside your Store Commerce / Hardware Station extension package using your normal installer flow.
4. Confirm `extensions.config` composition includes `CPOSPS`.
5. Confirm the HWS HTTPS endpoint (default port **450**):

   `https://localhost:450/HardwareStation/`

6. Optionally point the **CPOSPS** browser extension at that base URL.

## Runtime folders

| Variable | Default |
|----------|---------|
| `PosTerminalWeb__ContentRoot` | `%ProgramData%\IIGCore\PosTerminalWeb` |
| `PosTerminalWeb__OposFolder` | `{ContentRoot}/App_Data/opos` |
| `PosTerminalWeb__WatchOpos` | `true` |

Ensure the content root exists and is writable by the Hardware Station service account if lab fixture files are used.

## Self-signed HTTPS

Local HWS often uses a self-signed certificate. Trust it in the OS / browser profile; otherwise the extension and tools cannot call `https://localhost:450/...`.

## Related docs

- [Usage](usage.md)
- [Packaging](packaging.md)
- [Extension README](../extension/README.md)
