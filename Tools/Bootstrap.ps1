[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
Push-Location $projectRoot
try {
    git lfs install --local
    if ($LASTEXITCODE -ne 0) { throw "Git LFS is required." }
    git lfs pull
    if ($LASTEXITCODE -ne 0) { throw "Git LFS pull failed." }

    & (Join-Path $PSScriptRoot "Invoke-Unity.ps1") `
        -UnityArguments @("-executeMethod", "Game.Editor.ProjectScaffolder.CreateInitialAssets") `
        -LogFile "Logs/bootstrap.log"
    & (Join-Path $PSScriptRoot "Verify-Repository.ps1")
    & (Join-Path $PSScriptRoot "Run-UnityTests.ps1") -Platform EditMode
    Write-Output "Workstation bootstrap and verification completed."
}
finally {
    Pop-Location
}
