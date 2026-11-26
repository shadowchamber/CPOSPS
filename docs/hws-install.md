# Hardware Station install overview

Adapted from the original HardwareStation.PosTerminalWeb README.

## What CPOSPS provides to HWS

Hardware Station extension with the PosTerminal.Web engine **in-process**, served on the Hardware Station HTTP surface (`IController`).

| Surface | How |
|--|--|
| PosTerminal API | Controllers under `RoutePrefix("api")` |
| Device controllers | Light, coin dispenser, remote order display, … |
| Peripheral handlers (`PTW*`) | In-process `SimulatorEngine.Station(...)` |

## Typical install path (host product)

1. Build or restore **CPOSPS** against a machine that can reach the Dynamics Commerce private feed.
2. Reference `CPOSPS` from your Hardware Station extension / installer project (do **not** copy Microsoft Commerce DLLs into your own nupkg).
3. Deploy alongside your Store Commerce / Hardware Station extension package using your normal Scale Unit / Hardware Station installer flow.
4. Confirm the HWS HTTPS endpoint (default port **450**):

   `https://localhost:450/HardwareStation/`

5. Optionally point the **CPOSPS** browser extension at that base URL.

## Runtime folders

| Variable | Default |
|----------|---------|
| `PosTerminalWeb__ContentRoot` | `%ProgramData%\IIGCore\PosTerminalWeb` |
| `PosTerminalWeb__OposFolder` | `{ContentRoot}/App_Data/opos` |
| `PosTerminalWeb__WatchOpos` | `true` |

Ensure the content root exists and is writable by the Hardware Station service account if OPOS simulation files are used.

## Self-signed HTTPS

Local HWS often uses a self-signed certificate. Trust it in the OS / browser profile; otherwise the extension and tools cannot call `https://localhost:450/...`.

## Related docs

- [Usage](usage.md)
- [Packaging](packaging.md)
- [Extension README](../extension/README.md)
