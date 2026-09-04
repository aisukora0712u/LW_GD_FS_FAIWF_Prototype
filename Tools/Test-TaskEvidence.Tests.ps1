$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Test-TaskEvidence.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$spec = @{ schemaVersion=1; taskId='task.fixture'; inputVersion='fixture.1'; owner='fixture'; acceptanceIds=@('AC-01'); requiresHumanApproval=$true }
$record = @{
    schemaVersion=1; taskId='task.fixture'; inputVersion='fixture.1'; owner='fixture'
    results=@(@{ id='AC-01'; status='passed'; method='fixture-only'; verifiedAtUtc='2026-01-01T00:00:00Z'
        artifacts=@(@{ path='Tools/Test-TaskEvidence.ps1'; sha256=(Get-FileHash (Join-Path $PSScriptRoot 'Test-TaskEvidence.ps1')).Hash }) })
    approval=@{ status='approved'; reviewer='synthetic-test-reviewer'; reference='synthetic fixture, not a real approval' }
}
Assert-TaskEvidence $spec $record $root | Out-Null
$cases = @(
    { param($r) $r.results=@() },
    { param($r) $r.results[0].id='AC-99' },
    { param($r) $r.results[0].status='unverified' },
    { param($r) $r.results[0].artifacts[0].sha256=('0' * 64) },
    { param($r) $r.results[0].artifacts[0].path='../outside.txt' },
    { param($r) $r.results[0].verifiedAtUtc='2099-01-01T00:00:00Z' },
    { param($r) $r.inputVersion='changed' },
    { param($r) $r.approval.status='pending' },
    { param($r) $r.results[0].artifacts=@() }
)
foreach ($mutate in $cases) {
    $copy = $record | ConvertTo-Json -Depth 12 | ConvertFrom-Json -AsHashtable
    & $mutate $copy
    $rejected = $false
    try { Assert-TaskEvidence $spec $copy $root | Out-Null } catch { $rejected=$true }
    if (-not $rejected) { throw 'Invalid evidence was accepted.' }
}
Write-Output 'Task evidence tests passed: 1 valid fixture and 9 rejection cases.'
