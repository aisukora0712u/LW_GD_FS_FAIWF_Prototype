[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CandidateDirectory,
    [Parameter(Mandatory = $true)]
    [string]$Approval,
    [ValidateRange(1, 100000)]
    [int]$Samples = 200
)

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "Invoke-Unity.ps1") `
    -UnityArguments @(
        "-executeMethod", "Game.Editor.ContentCandidateBatch.Promote",
        "-candidateDirectory", $CandidateDirectory,
        "-candidateApproval", $Approval,
        "-candidateSamples", $Samples.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    ) `
    -LogFile "Logs/content-candidate-promotion.log"
