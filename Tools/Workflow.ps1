[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("check", "bootstrap", "build-development", "build-release")]
    [string]$Command = "check"
)

$ErrorActionPreference = "Stop"
switch ($Command) {
    "bootstrap" { & (Join-Path $PSScriptRoot "Bootstrap.ps1") }
    "check" {
        & (Join-Path $PSScriptRoot "Verify-Repository.ps1")
        & (Join-Path $PSScriptRoot "Test-TaskEvidence.Tests.ps1")
        & (Join-Path $PSScriptRoot "ConvertTo-DraftCandidate.Tests.ps1")
        & (Join-Path $PSScriptRoot "Run-UnityTests.ps1") -Platform EditMode
        & (Join-Path $PSScriptRoot "Validate-Project.ps1")
        & (Join-Path $PSScriptRoot "Run-BalanceSimulation.ps1") -Samples 200 -StartSeed 1 -EnforceGate
        & (Join-Path $PSScriptRoot "Run-UnityTests.ps1") -Platform PlayMode
    }
    "build-development" { & (Join-Path $PSScriptRoot "Build-Windows.ps1") -Configuration Development }
    "build-release" { & (Join-Path $PSScriptRoot "Build-Windows.ps1") -Configuration Release -Channel local-release }
}
