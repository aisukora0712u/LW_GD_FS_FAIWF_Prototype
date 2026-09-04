[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$BuildDirectory,
    [Parameter(Mandatory = $true)] [string]$Version,
    [Parameter(Mandatory = $true)] [string]$BuildNumber,
    [Parameter(Mandatory = $true)] [string]$Commit,
    [string]$OutputPath = "Artifacts/release-manifest.json"
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$absoluteBuild = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $BuildDirectory))
$absoluteOutput = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
if (-not (Test-Path -LiteralPath $absoluteBuild -PathType Container)) {
    throw "Build directory does not exist: $absoluteBuild"
}

$files = Get-ChildItem -LiteralPath $absoluteBuild -File -Recurse | Sort-Object FullName | ForEach-Object {
    [ordered]@{
        path = [System.IO.Path]::GetRelativePath($absoluteBuild, $_.FullName).Replace("\", "/")
        size = $_.Length
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

$manifest = [ordered]@{
    version = $Version
    buildNumber = $BuildNumber
    commit = $Commit
    unityVersion = ((Get-Content (Join-Path $projectRoot "ProjectSettings/ProjectVersion.txt") | Select-String "m_EditorVersion:").Line -split ":", 2)[1].Trim()
    createdAtUtc = [DateTime]::UtcNow.ToString("O")
    files = @($files)
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $absoluteOutput) | Out-Null
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $absoluteOutput -Encoding utf8NoBOM
Write-Output $absoluteOutput
