# Pack CPOSPS Chrome extension zip for Chrome Web Store / CI.
# Usage (repo root): pwsh -File extension/scripts/pack-extension.ps1
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Set-Location $root

$outDir = Join-Path $root 'artifacts'
$zip = Join-Path $outDir 'cposps-extension.zip'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
if (Test-Path $zip) { Remove-Item $zip -Force }

$stage = Join-Path $outDir 'extension-stage'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

$files = @(
  'manifest.json',
  'background.js',
  'options.html', 'options.js', 'options.css',
  'sidepanel.html', 'sidepanel.js', 'sidepanel.css',
  'privacy.html'
)
foreach ($f in $files) {
  $src = Join-Path $root "extension\$f"
  if (-not (Test-Path $src)) { throw "Missing required file: extension\$f" }
  Copy-Item $src (Join-Path $stage $f)
}
Copy-Item (Join-Path $root 'extension\icons') (Join-Path $stage 'icons') -Recurse

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Remove-Item $stage -Recurse -Force

$m = Get-Content (Join-Path $root 'extension\manifest.json') -Raw | ConvertFrom-Json
Write-Host "Packed $zip (manifest $($m.name) v$($m.version))"
Write-Output $zip
