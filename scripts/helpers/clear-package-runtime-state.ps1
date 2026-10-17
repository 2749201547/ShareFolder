# Removes Syncthing/device identity and other machine-local runtime state from a
# package tree so each install generates its own Device ID.
function Clear-PackageRuntimeState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [switch]$Portable
    )

    if (-not (Test-Path -LiteralPath $Root)) {
        Write-Warning "Package root not found: $Root"
        return
    }

    $identityNames = @(
        "cert.pem",
        "key.pem",
        "https-cert.pem",
        "https-key.pem",
        "syncthing.lock",
        "csrftokens.txt"
    )

    $removed = 0
    foreach ($name in $identityNames) {
        Get-ChildItem -LiteralPath $Root -Filter $name -Recurse -File -Force -ErrorAction SilentlyContinue |
            ForEach-Object {
                Remove-Item -LiteralPath $_.FullName -Force
                $removed++
                Write-Host "Removed package identity file: $($_.FullName)"
            }
    }

    # Syncthing config / DB belong to a specific device — never ship them
    $syncthingHomes = @()
    $portableHome = Join-Path $Root "data\syncthing"
    if (Test-Path -LiteralPath $portableHome) {
        $syncthingHomes += $portableHome
    }
    Get-ChildItem -LiteralPath $Root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq "syncthing" -and $_.Parent.Name -eq "data" } |
        ForEach-Object { $syncthingHomes += $_.FullName }

    foreach ($home in ($syncthingHomes | Select-Object -Unique)) {
        Get-ChildItem -LiteralPath $home -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne "gui" } |
            ForEach-Object {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force
                $removed++
                Write-Host "Removed package Syncthing state: $($_.FullName)"
            }
    }

    # CEF cache / logs must not travel with the package
    foreach ($rel in @("data\cef", "logs", "data\config-backups")) {
        $path = Join-Path $Root $rel
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
            Write-Host "Removed package runtime folder: $path"
        }
    }

    if ($Portable) {
        $dataDir = Join-Path $Root "data"
        if (-not (Test-Path -LiteralPath $dataDir)) {
            New-Item -ItemType Directory -Path $dataDir | Out-Null
        }
        # First launch deletes this marker and regenerates cert.pem / key.pem
        $marker = Join-Path $dataDir ".irongoo-new-install"
        Set-Content -LiteralPath $marker -Value "fresh" -Encoding ascii -NoNewline
        Write-Host "Wrote fresh-install marker: $marker"
    }

    Write-Host "Cleared package runtime/identity state under $Root (removed $removed item(s))"
}
