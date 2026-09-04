$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ConvertTo-DraftCandidate.ps1')

function Assert-True($condition, [string]$message) {
    if (-not $condition) { throw $message }
}

# Synthetic values intentionally overlap metadata. They are not an approval record.
$fixture = '{"contentVersion":"fixture.1","status":"approved","text":"版本 fixture.1", "nested":{"status":"approved","contentVersion":"fixture.1"},"values":[null,true,false,1.25,9007199254740993]}'
$target = 'fixture."2'
$result = ConvertTo-DraftCandidate $fixture contentVersion fixture.1 $target
$parsed = $result | ConvertFrom-Json
Assert-True ($parsed.contentVersion -ceq $target) 'Target version was not JSON escaped.'
Assert-True ($parsed.status -ceq 'draft') 'Top-level status was not converted.'
Assert-True ($parsed.text -ceq '版本 fixture.1') 'Narrative text was changed.'
Assert-True ($parsed.nested.status -ceq 'approved') 'Nested status was changed.'
Assert-True ($parsed.nested.contentVersion -ceq 'fixture.1') 'Nested version was changed.'
Assert-True ($parsed.values[4] -eq 9007199254740993) 'Integer precision was lost.'
Assert-True ($null -eq $parsed.values[0] -and $parsed.values[1] -eq $true -and $parsed.values[2] -eq $false -and $parsed.values[3] -eq 1.25) 'Scalar values were changed.'

$invalid = @(
    @{ text='[]'; target='fixture.2' },
    @{ text='{"contentVersion":"fixture.1"}'; target='fixture.2' },
    @{ text='{"status":"approved"}'; target='fixture.2' },
    @{ text='{"contentVersion":"fixture.1","status":"approved","status":"approved"}'; target='fixture.2' },
    @{ text='{"contentVersion":"wrong","status":"approved"}'; target='fixture.2' },
    @{ text='{"contentVersion":"fixture.1","status":"draft"}'; target='fixture.2' },
    @{ text='{"contentVersion":42,"status":"approved"}'; target='fixture.2' },
    @{ text=$fixture; target=' ' },
    @{ text=$fixture; target='fixture.1' },
    @{ text='{'; target='fixture.2' }
)
foreach ($case in $invalid) {
    $rejected = $false
    try { ConvertTo-DraftCandidate $case.text contentVersion fixture.1 $case.target | Out-Null } catch { $rejected = $true }
    Assert-True $rejected 'Invalid metadata was accepted.'
}

# Compare every non-metadata property of the actual sources, including localized strings.
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sources = @(
    @{ path='Assets/_Game/Content/Source/vertical-slice.json'; field='contentVersion' },
    @{ path='Assets/_Game/Content/Localization/vertical-slice.en.json'; field='localizationVersion' },
    @{ path='Assets/_Game/Content/Localization/vertical-slice.zh-Hans.json'; field='localizationVersion' }
)
foreach ($source in $sources) {
    $path = Join-Path $root $source.path
    $hash = (Get-FileHash -LiteralPath $path).Hash
    $original = Get-Content -LiteralPath $path -Raw
    $before = $original | ConvertFrom-Json -AsHashtable
    $converted = ConvertTo-DraftCandidate $original $source.field $before[$source.field] 'synthetic-copy.1'
    $after = $converted | ConvertFrom-Json -AsHashtable
    Assert-True ($after[$source.field] -ceq 'synthetic-copy.1' -and $after.status -ceq 'draft') 'Source metadata conversion failed.'
    $after[$source.field] = $before[$source.field]
    $after.status = $before.status
    Assert-True (($after | ConvertTo-Json -Depth 100 -Compress) -ceq ($before | ConvertTo-Json -Depth 100 -Compress)) "Source data changed: $path"
    Assert-True ((Get-FileHash -LiteralPath $path).Hash -ceq $hash) "Active source was written: $path"
}
Write-Output 'Draft copy fidelity passed: synthetic preservation, 10 rejections, 3 read-only source comparisons.'
