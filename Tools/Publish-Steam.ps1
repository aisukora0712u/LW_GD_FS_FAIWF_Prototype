[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$ContentRoot,
    [Parameter(Mandatory = $true)] [string]$Description,
    [string]$Branch = "internal"
)

$ErrorActionPreference = "Stop"
$required = @("STEAMCMD_PATH", "STEAM_APP_ID", "STEAM_DEPOT_ID", "STEAM_USERNAME")
foreach ($name in $required) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
        throw "Required environment variable is missing: $name"
    }
}

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$content = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $ContentRoot))
$template = Join-Path $projectRoot "Build/Steam/app_build.vdf.template"
$generated = Join-Path ([System.IO.Path]::GetTempPath()) ("steam-build-" + [Guid]::NewGuid().ToString("N") + ".vdf")
try {
    $vdf = (Get-Content -LiteralPath $template -Raw) `
        -replace "__APP_ID__", $env:STEAM_APP_ID `
        -replace "__DEPOT_ID__", $env:STEAM_DEPOT_ID `
        -replace "__CONTENT_ROOT__", $content.Replace("\", "\\") `
        -replace "__DESCRIPTION__", $Description.Replace('"', "'") `
        -replace "__BRANCH__", $Branch
    Set-Content -LiteralPath $generated -Value $vdf -Encoding ascii

    # Runner provisioning must restore an authenticated Steam config.vdf. Passwords are never command-line arguments.
    & $env:STEAMCMD_PATH +login $env:STEAM_USERNAME +run_app_build $generated +quit
    if ($LASTEXITCODE -ne 0) {
        throw "SteamCMD upload failed with exit code $LASTEXITCODE."
    }
}
finally {
    if (Test-Path -LiteralPath $generated) {
        Remove-Item -LiteralPath $generated -Force
    }
}
