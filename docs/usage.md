# CPOSPS usage

## Hardware Station library

CPOSPS hosts a **virtual** PosTerminal.Web engine in-process on the Hardware Station HTTP surface. No ServiceOPOS registry. No real devices.

### Surfaces

| Surface | How |
|--|--|
| Browser / extension API | `POST api/state`, `api/ui`, `api/station` |
| Virtual light HTTP | `PTWLightDevice` (POS calls this when traffic-light device name starts with `PTW`) |
| Virtual peripherals | `PtwProfile*` hosts: hardware profile type stays OPOS, device name `PTW*` → simulator; other names → stock OPOS |

### API base

`https://{host}:450/HardwareStation/`

```http
POST https://localhost:450/HardwareStation/api/state
POST https://localhost:450/HardwareStation/api/ui
     {"action":"drawer","drawer":"main"}
POST https://localhost:450/HardwareStation/api/station
     {"route":"Light/SwitchOff","deviceName":"PTWLight","number":1}
POST https://localhost:450/HardwareStation/PTWLightDevice/GreenLight
     {"DeviceName":"PTWLight"}
```

### Config

| Variable | Default |
|----------|---------|
| `PosTerminalWeb__ContentRoot` | `%ProgramData%\IIGCore\PosTerminalWeb` |
| `PosTerminalWeb__OposFolder` | `{ContentRoot}/App_Data/opos` |
| `PosTerminalWeb__WatchOpos` | `true` (file pipe for lab fixtures; not ServiceOPOS) |

### Consuming the NuGet package

1. Restore **CPOSPS** from GitHub Packages (or nuget.org) — see [packaging.md](packaging.md).
2. `PackageReference` **CPOSPS** from the Hardware Station extension project. The package’s `buildTransitive` targets promote `CPOSPS` into `extensions.config` composition automatically.
3. Use hardware profile **HW_PTW** (or any profile) with device names `PTW*`. Do **not** register those names under ServiceOPOS.

Namespaces: `IIGCore.HardwareStation.PosTerminalWeb.*`.

## Browser extension

1. Load unpacked from `extension/` (Developer mode).
2. Open the side panel (toolbar action or **Alt+P**).
3. Set API base in Options if not using `https://localhost:450/HardwareStation`.

Details: [extension/README.md](../extension/README.md).
