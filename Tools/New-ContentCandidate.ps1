[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z][a-z0-9_-]*(\.[a-z][a-z0-9_-]*)+$')]
    [string]$CandidateId,
    [Parameter(Mandatory = $true)]
    [string]$TargetContentVersion,
    [Parameter(Mandatory = $true)]
    [string]$Generator,
    [Parameter(Mandatory = $true)]
    [string]$Model,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$PromptDigest,
    [Parameter(Mandatory = $true)]
    [string]$Source,
    [Parameter(Mandatory = $true)]
    [string]$License,
    [Parameter(Mandatory = $true)]
    [string]$Owner,
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot 'ConvertTo-DraftCandidate.ps1')
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
if (-not $outputPath.StartsWith($projectRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must stay inside the project workspace."
}
if (Test-Path -LiteralPath $outputPath) {
    throw "Candidate directory already exists: $outputPath"
}

$contentPath = Join-Path $projectRoot "Assets/_Game/Content/Source/vertical-slice.json"
$englishPath = Join-Path $projectRoot "Assets/_Game/Content/Localization/vertical-slice.en.json"
$chinesePath = Join-Path $projectRoot "Assets/_Game/Content/Localization/vertical-slice.zh-Hans.json"
$active = Get-Content -LiteralPath $contentPath -Raw | ConvertFrom-Json
if ($active.status -ne "approved") {
    throw "Active content must be approved before creating a candidate."
}
if ($TargetContentVersion -eq $active.contentVersion) {
    throw "TargetContentVersion must differ from the active contentVersion."
}

New-Item -ItemType Directory -Path $outputPath | Out-Null
try {
    $files = @(
        @{ Source = $contentPath; Destination = "content.json"; VersionField = 'contentVersion' },
        @{ Source = $englishPath; Destination = "localization.en.json"; VersionField = 'localizationVersion' },
        @{ Source = $chinesePath; Destination = "localization.zh-Hans.json"; VersionField = 'localizationVersion' }
    )
    foreach ($file in $files) {
        $text = Get-Content -LiteralPath $file.Source -Raw
        $text = ConvertTo-DraftCandidate -Text $text -VersionField $file.VersionField -BaseVersion $active.contentVersion -TargetVersion $TargetContentVersion
        Set-Content -LiteralPath (Join-Path $outputPath $file.Destination) -Value $text -Encoding utf8NoBOM -NoNewline
    }

    $provenance = [ordered]@{
        schemaVersion = 1
        candidateId = $CandidateId
        baseContentVersion = $active.contentVersion
        targetContentVersion = $TargetContentVersion
        generator = $Generator
        model = $Model
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O", [System.Globalization.CultureInfo]::InvariantCulture)
        promptDigest = $PromptDigest.ToLowerInvariant()
        source = $Source
        license = $License
        owner = $Owner
    }
    $provenance | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath "provenance.json") -Encoding utf8NoBOM
}
catch {
    if (Test-Path -LiteralPath $outputPath) {
        Remove-Item -LiteralPath $outputPath -Recurse -Force
    }
    throw
}

Write-Output "Created isolated draft candidate: $outputPath"
