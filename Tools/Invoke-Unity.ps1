[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string[]]$UnityArguments,
    [string]$LogFile = "Logs/unity-command.log"
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$expectedVersion = ((Get-Content (Join-Path $projectRoot "ProjectSettings/ProjectVersion.txt") | Select-String "m_EditorVersion:").Line -split ":", 2)[1].Trim()
$candidates = @(
    $env:UNITY_EDITOR_PATH,
    "C:\soft\UnityEditors\$expectedVersion\Editor\Unity.exe",
    "C:\Program Files\Unity\Hub\Editor\$expectedVersion\Editor\Unity.exe"
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

$unity = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $unity) {
    throw "Unity $expectedVersion was not found. Set UNITY_EDITOR_PATH to the editor executable."
}

$absoluteLog = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $LogFile))
$logDirectory = Split-Path -Parent $absoluteLog
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

$arguments = @("-batchmode", "-nographics")
if ($UnityArguments -notcontains "-runTests") {
    $arguments += "-quit"
}
$arguments += @("-projectPath", $projectRoot) + $UnityArguments + @("-logFile", $absoluteLog)
$process = Start-Process -FilePath $unity -ArgumentList $arguments -PassThru -WindowStyle Hidden
$process.WaitForExit()
$exitCode = $process.ExitCode
if ($exitCode -ne 0) {
    if (Test-Path -LiteralPath $absoluteLog) {
        Get-Content -LiteralPath $absoluteLog -Tail 200
    }
    throw "Unity command failed with exit code $exitCode. See $absoluteLog"
}

Write-Output "Unity command completed. Log: $absoluteLog"
