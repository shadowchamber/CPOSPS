# CPOSPS usage

## Hardware Station library

CPOSPS hosts the PosTerminal.Web engine **in-process** on the Hardware Station HTTP surface (`IController`).

### Surfaces

| Surface | How |
|--|--|
| PosTerminal API | HWS controllers under `RoutePrefix("api")` |
| Device controllers | `PTWLightDevice`, `PTWCoinDispenser`, `PTWRemoteOrderDisplay`, … |
| Peripheral handlers (`PTW*`) | In-process `SimulatorEngine.Station(...)` |

### API (Hardware Station host)

Base: `https://{host}:450/HardwareStation/`

| Method | Path | Notes |
|--|--|--|
| POST | `api/state` | Same payload as PosTerminal.Web `/api/state` |
| POST | `api/ui` | Body includes `action` plus action fields |
| POST | `api/station` | Body includes `route` plus route fields |

Hardware Station controllers are POST-only (HWS hosting contract). Examples:

```http
POST https://localhost:450/HardwareStation/api/state
POST https://localhost:450/HardwareStation/api/ui
     {"action":"drawer","drawer":"main"}
POST https://localhost:450/HardwareStation/api/station
     {"route":"Light/SwitchOff","deviceName":"PTWLight","number":1}
```

### Config

| Variable | Default |
|----------|---------|
| `PosTerminalWeb__ContentRoot` | `%ProgramData%\IIGCore\PosTerminalWeb` |
| `PosTerminalWeb__OposFolder` | `{ContentRoot}/App_Data/opos` |
| `PosTerminalWeb__WatchOpos` | `true` |

### Consuming the NuGet package

1. Add the Dynamics Commerce private feed (see repo `nuget.config`).
2. `dotnet add package CPOSPS`
3. Ensure your Hardware Station host discovers the MEF/controllers from the CPOSPS assembly (same as referencing the original project).

Namespaces: `IIGCore.HardwareStation.PosTerminalWeb.*`.

## Browser extension

1. Load unpacked from `extension/` (Developer mode).
2. Open the side panel (toolbar action or **Alt+P**).
3. Set API base in Options if not using the default `https://localhost:450/HardwareStation`.

Details: [extension/README.md](../extension/README.md).
