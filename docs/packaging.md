# Packaging CPOSPS (NuGet)

## Package identity

| Property | Value |
|----------|-------|
| PackageId | **CPOSPS** |
| Target | `netstandard2.0` |
| Project | `src/CPOSPS/CPOSPS.csproj` |
| Feed | [nuget.org](https://www.nuget.org/packages/CPOSPS) |

## What is / is not in the nupkg

**Included**

- CPOSPS managed assemblies and symbols (`.snupkg`)
- README, LICENSE
- `buildTransitive/CPOSPS.targets` — promotes `CPOSPS.dll` to `HardwareStationAssembly` so host `extensions.config` composition includes it (no consumer-side Promote MSBuild hack)

**Not included**

- Microsoft Dynamics Commerce SDK DLLs. Those arrive as NuGet `PackageReference` dependencies that consumers restore from the **dynamics365-commerce** private feed.

## Local pack

```powershell
dotnet restore CPOSPS.sln --configfile nuget.config
dotnet pack src/CPOSPS/CPOSPS.csproj -c Release -o artifacts
```

## Publish with GitHub Actions (recommended)

Workflow: [`.github/workflows/release.yml`](../.github/workflows/release.yml)

| Item | Detail |
|------|--------|
| Triggers | Git tag `v*` **or** Actions → **Release** → **Run workflow** |
| Runner | `windows-latest` |
| Auth | NuGet Trusted Publishing (OIDC) via `NuGet/login@v1` — no long-lived API key in secrets |
| GitHub Environment | `production` (must exist; matches nuget.org trusted-publishing policy) |
| Secret | `NUGET_USER` — nuget.org **profile name** (not email) |
| Push | `https://api.nuget.org/v3/index.json` |

### One-time setup

1. On nuget.org: enable **Trusted Publishing** for package **CPOSPS**, repository `shadowchamber/CPOSPS`, workflow `release.yml`, environment `production`.
2. In GitHub repo **Settings → Environments**: create `production`.
3. In GitHub repo **Settings → Secrets**: set `NUGET_USER` to your nuget.org username.
4. Dynamics feed for restore: CI uses [`nuget.config`](../nuget.config). If the private feed needs auth in Actions, add a PAT secret and wire it in the Restore step (see comment in `ci.yml`).

### Publish a new version

1. Bump `<Version>` in `src/CPOSPS/CPOSPS.csproj` (and commit on `main`).
2. Tag and push:

```powershell
git tag v1.1.0
git push origin v1.1.0
```

Or **Actions → Release → Run workflow** and set version override (e.g. `1.1.0`).

3. Confirm the **Release** run is green and [nuget.org/packages/CPOSPS](https://www.nuget.org/packages/CPOSPS) shows the new version.

### Manual push (optional)

```powershell
dotnet nuget push artifacts\CPOSPS.*.nupkg `
    --api-key $env:NUGET_API_KEY `
    --source https://api.nuget.org/v3/index.json `
    --skip-duplicate
```

## Consume from a Hardware Station host (e.g. IIGCore)

1. `nuget.config`: restore **CPOSPS** from **nuget.org** (map package id `CPOSPS` to that source). Keep **dynamics365-commerce** for `Microsoft.Dynamics.Commerce.*`.
2. Central version / PackageReference:

```xml
<PackageVersion Include="CPOSPS" Version="1.1.0" />
<!-- in Hardware Station csproj: -->
<PackageReference Include="CPOSPS" />
```

3. Do **not** add a local `artifacts` folder source or a host-side Promote target — `buildTransitive/CPOSPS.targets` handles composition.
4. Build/install the Hardware Station (or Store Commerce) extension as usual; confirm `extensions.config` lists `CPOSPS`.

## Version pins (dependencies)

Pinned in the csproj:

- `Microsoft.Dynamics.Commerce.Sdk.HardwareAndPeripherals` **9.59.26286.5**
- `Newtonsoft.Json` **13.0.3**

Bump deliberately when aligning to a newer Commerce SDK.
