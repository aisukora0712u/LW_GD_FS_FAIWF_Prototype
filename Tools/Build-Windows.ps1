[CmdletBinding()]
param(
    [ValidateSet("Development", "Release")]
    [string]$Configuration = "Development",
    [string]$Version = "0.1.0",
    [string]$BuildNumber = "local",
    [string]$Channel = "development",
    [string]$Commit = "working-tree",
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = "BuildOutput/$Configuration/Game.exe"
}

$method = if ($Configuration -eq "Release") { "Game.Editor.BuildCommand.BuildRelease" } else { "Game.Editor.BuildCommand.BuildDevelopment" }
& (Join-Path $PSScriptRoot "Invoke-Unity.ps1") `
    -UnityArguments @(
        "-executeMethod", $method,
        "-outputPath", $OutputPath,
        "-buildVersion", $Version,
        "-buildNumber", $BuildNumber,
        "-channel", $Channel,
        "-commit", $Commit
    ) `
    -LogFile "Logs/build-$Configuration.log"
