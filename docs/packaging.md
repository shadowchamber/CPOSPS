# Packaging CPOSPS (NuGet)

## Package identity

| Property | Value |
|----------|-------|
| PackageId | **CPOSPS** |
| Target | `netstandard2.0` |
| Project | `src/CPOSPS/CPOSPS.csproj` |

## What is / is not in the nupkg

**Included:** CPOSPS managed assemblies and symbols (snupkg), README, LICENSE.

**Not included:** Microsoft Dynamics Commerce SDK DLLs. Those arrive as NuGet `PackageReference` dependencies that consumers restore from the **dynamics365-commerce** private feed.

## Local pack

```powershell
dotnet restore CPOSPS.sln --configfile nuget.config
dotnet pack src/CPOSPS/CPOSPS.csproj -c Release -o artifacts --no-restore
# or with restore:
dotnet pack src/CPOSPS/CPOSPS.csproj -c Release -o artifacts
```

## Publish to nuget.org

CI: [`.github/workflows/release.yml`](../.github/workflows/release.yml)

- Triggers: tag `v*` or `workflow_dispatch`
- Secret: `NUGET_API_KEY`
- Push URL: `https://api.nuget.org/v3/index.json`

Manual:

```powershell
dotnet nuget push artifacts\CPOSPS.*.nupkg `
  --api-key $env:NUGET_API_KEY `
  --source https://api.nuget.org/v3/index.json `
  --skip-duplicate
```

## Version pins

Pinned in the csproj from the IIGCore `HardwareStation.PosTerminalWeb` `packages.lock.json`:

- `Microsoft.Dynamics.Commerce.Sdk.HardwareAndPeripherals` 9.59.26286.5
- `Newtonsoft.Json` 13.0.3
- `Microsoft.Win32.Registry` 5.0.0

Bump versions deliberately when aligning to a newer Commerce SDK.
