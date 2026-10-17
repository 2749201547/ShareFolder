$ErrorActionPreference = "Stop"

. "$PSScriptRoot\helpers\get-arch.ps1"
$arch = Get-Arch
$portableArch = switch ($arch) {
    "amd64" { "x64" }
    "arm64" { "arm64" }
    default { throw "Unknown architecture: $arch" }
}
$releaseDir = ".\release"
$distName = "1.0-irongoo-Portable-$portableArch"
$distDir = ".\dist"

if (-not (Test-Path (Join-Path $distDir "irongoo.exe"))) {
    Write-Error "dist\irongoo.exe missing. Run build-dist.ps1 -Variant Portable first."
    exit 1
}

# Publish PortableInstaller beside the app (do not wipe dist)
$piOut = Join-Path $env:TEMP "irongoo-portable-installer-$portableArch"
if (Test-Path $piOut) {
    Remove-Item $piOut -Recurse -Force
}
dotnet publish -c Release -p:DebugType=None -p:DebugSymbols=false -p:SelfContained=true -o $piOut src/PortableInstaller/PortableInstaller.csproj
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build PortableInstaller. Exiting."
    exit $LASTEXITCODE
}
Copy-Item (Join-Path $piOut "PortableInstaller.exe") (Join-Path $distDir "PortableInstaller.exe") -Force
Remove-Item $piOut -Recurse -Force

if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir | Out-Null
}

# Strip any device identity left behind by local testing before zipping
. "$PSScriptRoot\helpers\clear-package-runtime-state.ps1"
Clear-PackageRuntimeState -Root $distDir -Portable

$zipPath = Join-Path $releaseDir "$distName.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Creating $distName.zip"
if (Test-Path $distName) {
    Remove-Item $distName -Recurse -Force
}
Move-Item -Path $distDir -Destination $distName -Force
try {
    Compress-Archive -Path $distName -DestinationPath $zipPath -Force
}
finally {
    Move-Item -Path $distName -Destination $distDir -Force
}

Write-Host "Portable package ready: $zipPath"
Get-Item $zipPath | Format-List FullName, Length, LastWriteTime
