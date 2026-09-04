[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "Invoke-Unity.ps1") `
    -UnityArguments @("-executeMethod", "Game.Editor.ProjectValidator.ValidateBatchMode") `
    -LogFile "Logs/validation.log"
