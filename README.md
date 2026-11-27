# CPOSPS

**Chrome POS Peripheral Simulator** — Hardware Station PosTerminal.Web engine (in-process) plus a Chrome/Edge side-panel extension for driving simulated POS peripherals.

| Component | Path | Package / product |
|-----------|------|-------------------|
| NuGet library | `src/CPOSPS/` | **[CPOSPS](https://www.nuget.org/packages/CPOSPS)** on nuget.org |
| Browser extension | `extension/` | **CPOSPS** (Chrome Web Store listing name) |

Namespaces remain `IIGCore.HardwareStation.PosTerminalWeb.*` for drop-in compatibility with existing Hardware Station consumers.

## Requirements (NuGet)

- .NET SDK that can build `netstandard2.0`
- Access to the **Dynamics 365 Commerce private feed** for `Microsoft.Dynamics.Commerce.*` packages
- See sample [`nuget.config`](nuget.config) — CPOSPS does **not** redistribute Microsoft Commerce DLLs

Pinned dependency versions:

- `Microsoft.Dynamics.Commerce.Sdk.HardwareAndPeripherals` **9.59.26286.5**
- `Newtonsoft.Json` **13.0.3**

## Quick start (library)

```powershell
dotnet restore CPOSPS.sln --configfile nuget.config
dotnet build CPOSPS.sln -c Release
dotnet pack src/CPOSPS/CPOSPS.csproj -c Release -o artifacts
```

Host projects: `PackageReference` **CPOSPS** from nuget.org. The package ships `buildTransitive/CPOSPS.targets` so `CPOSPS` is composed into Hardware Station `extensions.config` automatically.

## Publish

GitHub Actions **Release** workflow (tag `v*` or `workflow_dispatch`) pushes to nuget.org via NuGet Trusted Publishing (OIDC). Details: [docs/packaging.md](docs/packaging.md).

## Extension

See [`extension/README.md`](extension/README.md). Default API base:

`https://localhost:450/HardwareStation`

## Docs

- [Usage](docs/usage.md)
- [Packaging](docs/packaging.md)
- [Hardware Station install overview](docs/hws-install.md)
- [Chrome Web Store](docs/chrome-web-store.md)

## License

MIT for CPOSPS source. Microsoft Dynamics Commerce SDK packages are licensed separately (see `LICENSE`).
