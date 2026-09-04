[CmdletBinding()]
param(
    [ValidateRange(1, 100000)]
    [int]$Samples = 1000,
    [UInt64]$StartSeed = 1,
    [string]$Output = "Artifacts/balance-report.json",
    [ValidateRange(0, 1000)]
    [int]$MinimumWinRatePermille = 100,
    [ValidateRange(0, 1000)]
    [int]$MaximumWinRatePermille = 900,
    [ValidateRange(0, 100000)]
    [int]$MaximumStalledRuns = 0,
    [switch]$EnforceGate
)

$ErrorActionPreference = "Stop"
$unityArguments = @(
    "-executeMethod", "Game.Editor.BalanceSimulationBatch.Run",
    "-balanceSamples", $Samples.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "-balanceStartSeed", $StartSeed.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "-balanceOutput", $Output,
    "-balanceMinimumWinRatePermille", $MinimumWinRatePermille.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "-balanceMaximumWinRatePermille", $MaximumWinRatePermille.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "-balanceMaximumStalledRuns", $MaximumStalledRuns.ToString([System.Globalization.CultureInfo]::InvariantCulture)
)
if ($EnforceGate) {
    $unityArguments += "-balanceEnforceGate"
}

& (Join-Path $PSScriptRoot "Invoke-Unity.ps1") `
    -UnityArguments $unityArguments `
    -LogFile "Logs/balance-simulation.log"
