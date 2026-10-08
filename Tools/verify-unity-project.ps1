[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedUnityVersion,

    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'
if (-not $ProjectPath) {
    $ProjectPath = Split-Path -Parent $PSScriptRoot
}
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$versionFile = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
$manifestFile = Join-Path $projectRoot 'Packages/manifest.json'
$lockFile = Join-Path $projectRoot 'Packages/packages-lock.json'

foreach ($requiredFile in @($versionFile, $manifestFile, $lockFile)) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required Unity project file is missing: $requiredFile"
    }
}

$versionLine = Get-Content -LiteralPath $versionFile |
    Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } |
    Select-Object -First 1

if (-not $versionLine) {
    throw "Could not read m_EditorVersion from $versionFile"
}

$actualVersion = ($versionLine -replace '^m_EditorVersion:\s*', '').Trim()
if ($actualVersion -ne $ExpectedUnityVersion) {
    throw "Unity version mismatch. Expected '$ExpectedUnityVersion', found '$actualVersion'."
}

$assetRoot = Join-Path $projectRoot 'Assets'
$missingMeta = [System.Collections.Generic.List[string]]::new()
$orphanMeta = [System.Collections.Generic.List[string]]::new()

Get-ChildItem -LiteralPath $assetRoot -Recurse -Force |
    Where-Object { $_.Name -notlike '*.meta' } |
    ForEach-Object {
        if (-not (Test-Path -LiteralPath ($_.FullName + '.meta') -PathType Leaf)) {
            $missingMeta.Add($_.FullName.Substring($projectRoot.Length + 1))
        }
    }

Get-ChildItem -LiteralPath $assetRoot -Recurse -Force -File -Filter '*.meta' |
    ForEach-Object {
        $assetPath = $_.FullName.Substring(0, $_.FullName.Length - '.meta'.Length)
        if (-not (Test-Path -LiteralPath $assetPath)) {
            $orphanMeta.Add($_.FullName.Substring($projectRoot.Length + 1))
        }
    }

if ($missingMeta.Count -gt 0 -or $orphanMeta.Count -gt 0) {
    $details = @()
    if ($missingMeta.Count -gt 0) {
        $details += "Assets without .meta:`n$($missingMeta -join "`n")"
    }
    if ($orphanMeta.Count -gt 0) {
        $details += "Orphan .meta files:`n$($orphanMeta -join "`n")"
    }
    throw ($details -join "`n`n")
}

Write-Host "Unity project integrity verified for $actualVersion."
