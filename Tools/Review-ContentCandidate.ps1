[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CandidateDirectory,
    [ValidateRange(1, 100000)]
    [int]$Samples = 200,
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $CandidateDirectory "review-report.json"
}

& (Join-Path $PSScriptRoot "Invoke-Unity.ps1") `
    -UnityArguments @(
        "-executeMethod", "Game.Editor.ContentCandidateBatch.Review",
        "-candidateDirectory", $CandidateDirectory,
        "-candidateSamples", $Samples.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "-candidateReviewOutput", $Output
    ) `
    -LogFile "Logs/content-candidate-review.log"
