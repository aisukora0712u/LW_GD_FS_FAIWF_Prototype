[CmdletBinding()]
param([string]$Specification, [string]$Evidence)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-TaskEvidence {
    param([hashtable]$Spec, [hashtable]$Record, [string]$Root)
    if ($Spec.schemaVersion -ne 1 -or $Record.schemaVersion -ne 1) { throw 'Unsupported schemaVersion.' }
    foreach ($key in @('taskId', 'inputVersion', 'owner')) {
        if ([string]::IsNullOrWhiteSpace($Spec[$key]) -or $Spec[$key] -ne $Record[$key]) { throw "Missing or mismatched $key." }
    }
    $ids = @($Spec.acceptanceIds)
    if ($ids.Count -eq 0 -or @($ids | Sort-Object -Unique).Count -ne $ids.Count) { throw 'Acceptance IDs must be nonempty and unique.' }
    foreach ($id in $ids) { if ($id -cnotmatch '^AC-[0-9]+$') { throw 'Invalid acceptance ID.' } }
    $results = @($Record.results)
    if ($results.Count -ne $ids.Count) { throw 'Acceptance coverage mismatch.' }
    $seen = @{}
    $rootPath = [IO.Path]::GetFullPath($Root)
    foreach ($result in $results) {
        if ($result.id -notin $ids -or $seen.ContainsKey($result.id)) { throw 'Unknown or duplicate result ID.' }
        $seen[$result.id] = $true
        if ($result.status -cne 'passed') { throw "Unverified or failed criterion: $($result.id)" }
        foreach ($key in @('method', 'verifiedAtUtc')) {
            if ([string]::IsNullOrWhiteSpace($result[$key])) { throw "Missing $key." }
        }
        $date = [DateTimeOffset]::MinValue
        if ($result.verifiedAtUtc -notmatch 'Z$' -or -not [DateTimeOffset]::TryParse($result.verifiedAtUtc, [ref]$date) -or $date -gt [DateTimeOffset]::UtcNow) { throw 'Invalid verification time.' }
        $artifacts = @($result.artifacts)
        if ($artifacts.Count -eq 0) { throw 'Evidence artifacts are required.' }
        foreach ($artifact in $artifacts) {
            $relative = [string]$artifact.path
            if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or $relative.Contains(':')) { throw 'Evidence path must be repository-relative.' }
            $path = [IO.Path]::GetFullPath((Join-Path $rootPath $relative))
            if (-not $path.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence path escapes repository.' }
            $cursor = $path
            while ($cursor -ne $rootPath) {
                $item = Get-Item -LiteralPath $cursor -Force
                if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked evidence paths are not allowed.' }
                $cursor = Split-Path -Parent $cursor
            }
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'Evidence must be a file.' }
            if ($artifact.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $artifact.sha256) { throw 'Evidence digest mismatch.' }
        }
    }
    if ($Spec.requiresHumanApproval -isnot [bool]) { throw 'requiresHumanApproval must be boolean.' }
    if ($Spec.requiresHumanApproval) {
        # This is a record-integrity gate, not identity authentication or authorization.
        if ($Record.approval.status -cne 'approved' -or [string]::IsNullOrWhiteSpace($Record.approval.reviewer) -or
            [string]::IsNullOrWhiteSpace($Record.approval.reference)) { throw 'Human approval record is pending or missing.' }
    }
    'Task evidence integrity passed; semantic review and approval authenticity remain human responsibilities.'
}

if ($MyInvocation.InvocationName -ne '.') {
    if (-not $Specification -or -not $Evidence) { throw 'Provide -Specification and -Evidence JSON files.' }
    $root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    Assert-TaskEvidence -Spec (Get-Content -LiteralPath $Specification -Raw | ConvertFrom-Json -AsHashtable) `
        -Record (Get-Content -LiteralPath $Evidence -Raw | ConvertFrom-Json -AsHashtable) -Root $root
}
