[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
Push-Location $projectRoot
try {
    $required = @(
        "Packages/manifest.json",
        "Packages/packages-lock.json",
        "ProjectSettings/ProjectVersion.txt",
        "Content/Schema/game-content-v7.schema.json",
        "Content/Schema/localization-v1.schema.json",
        "Content/Schema/content-candidate-provenance-v1.schema.json",
        "Content/Schema/content-candidate-approval-v1.schema.json",
        "Content/Candidates/README.md",
        "Tools/Review-ContentCandidate.ps1",
        "Tools/New-ContentCandidate.ps1",
        "Tools/Promote-ContentCandidate.ps1",
        "Assets/_Game/Content/Source/vertical-slice.json",
        "Assets/_Game/Content/Localization/vertical-slice.en.json",
        "Assets/_Game/Content/Localization/vertical-slice.zh-Hans.json",
        "Tools/Run-BalanceSimulation.ps1",
        "Docs/ADR/002-versioned-data-driven-run-content.md",
        "Docs/RFC/005-card-upgrades-and-balance-simulation.md",
        ".gitattributes",
        ".gitignore"
    )
    foreach ($path in $required) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Required repository file is missing: $path"
        }
    }

    $lfsExtensions = @(".fbx", ".blend", ".ma", ".mb", ".psd", ".kra", ".tif", ".tiff", ".exr", ".wav", ".flac", ".mp4", ".mov")
    $tracked = @(git ls-files --cached --others --exclude-standard)
    $lfsFiles = @(git lfs ls-files -n 2>$null)
    foreach ($path in $tracked) {
        $extension = [System.IO.Path]::GetExtension($path).ToLowerInvariant()
        if ($lfsExtensions -contains $extension -and $lfsFiles -notcontains $path) {
            throw "Binary asset is not tracked by Git LFS: $path"
        }

        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $item = Get-Item -LiteralPath $path
            if ($item.Length -gt 10MB -and $lfsFiles -notcontains $path) {
                throw "File larger than 10 MB is not tracked by Git LFS: $path"
            }
        }
    }

    $forbidden = @("steam_appid.txt", ".env")
    foreach ($name in $forbidden) {
        if ($tracked | Where-Object { [System.IO.Path]::GetFileName($_) -eq $name }) {
            throw "Forbidden secret/runtime file is tracked: $name"
        }
    }

    $jsonFiles = @(
        "Packages/manifest.json",
        "Packages/packages-lock.json",
        "Content/Schema/game-content-v7.schema.json",
        "Content/Schema/localization-v1.schema.json",
        "Content/Schema/content-candidate-provenance-v1.schema.json",
        "Content/Schema/content-candidate-approval-v1.schema.json",
        "Assets/_Game/Content/Source/vertical-slice.json",
        "Assets/_Game/Content/Localization/vertical-slice.en.json",
        "Assets/_Game/Content/Localization/vertical-slice.zh-Hans.json"
    )
    foreach ($jsonFile in $jsonFiles) {
        try {
            Get-Content -LiteralPath $jsonFile -Raw | ConvertFrom-Json | Out-Null
        }
        catch {
            throw "Invalid JSON in ${jsonFile}: $($_.Exception.Message)"
        }
    }

    git rev-parse --verify HEAD 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $largeLfsPointers = git lfs fsck 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Git LFS validation failed: $largeLfsPointers"
        }
    }

    Write-Output "Repository policy checks passed."
}
finally {
    Pop-Location
}
