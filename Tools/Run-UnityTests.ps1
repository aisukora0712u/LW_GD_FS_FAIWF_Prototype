[CmdletBinding()]
param(
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Platform = "EditMode"
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$results = "TestResults/$Platform-results.xml"
$absoluteResults = Join-Path $projectRoot $results
if (Test-Path -LiteralPath $absoluteResults) {
    Remove-Item -LiteralPath $absoluteResults -Force
}
& (Join-Path $PSScriptRoot "Invoke-Unity.ps1") `
    -UnityArguments @("-runTests", "-testPlatform", $Platform, "-testResults", $absoluteResults) `
    -LogFile "Logs/tests-$Platform.log"

if (-not (Test-Path -LiteralPath $absoluteResults)) {
    throw "Unity did not create test results: $results"
}

[xml]$testResults = Get-Content -LiteralPath $absoluteResults
$run = $testResults.'test-run'
if ($run.failed -ne "0" -or $run.result -notlike "Passed*") {
    throw "$Platform tests failed: $($run.passed)/$($run.total) passed. See $results"
}

Write-Output "$Platform tests passed: $($run.passed)/$($run.total)."
