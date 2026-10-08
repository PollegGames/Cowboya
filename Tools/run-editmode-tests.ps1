[CmdletBinding()]
param(
    [string]$UnityPath,
    [string]$ProjectPath,
    [string]$ArtifactsPath = 'Logs/CI'
)

$ErrorActionPreference = 'Stop'
if (-not $ProjectPath) {
    $ProjectPath = Split-Path -Parent $PSScriptRoot
}
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$versionFile = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
$unityVersion = ((Get-Content -LiteralPath $versionFile |
    Where-Object { $_ -match '^m_EditorVersion:' } |
    Select-Object -First 1) -replace '^m_EditorVersion:\s*', '').Trim()

if (-not $UnityPath) {
    $command = Get-Command unity -ErrorAction SilentlyContinue
    if ($command) {
        $UnityPath = $command.Source
    } else {
        $UnityPath = "C:/Program Files/Unity/Hub/Editor/$unityVersion/Editor/Unity.exe"
    }
}

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity $unityVersion was not found at '$UnityPath'. Pass -UnityPath explicitly."
}

$artifactRoot = if ([System.IO.Path]::IsPathRooted($ArtifactsPath)) {
    $ArtifactsPath
} else {
    Join-Path $projectRoot $ArtifactsPath
}
New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

$resultsPath = Join-Path $artifactRoot 'editmode-results.xml'
$logPath = Join-Path $artifactRoot 'editmode.log'
$quotedProjectRoot = '"' + $projectRoot + '"'
$quotedResultsPath = '"' + $resultsPath + '"'
$quotedLogPath = '"' + $logPath + '"'
$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $quotedProjectRoot,
    '-runTests',
    '-testPlatform', 'EditMode',
    '-testResults', $quotedResultsPath,
    '-logFile', $quotedLogPath
)

$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -Wait -PassThru
if (-not (Test-Path -LiteralPath $resultsPath -PathType Leaf)) {
    throw "Unity did not produce '$resultsPath'. See '$logPath'."
}

if ($process.ExitCode -ne 0) {
    Write-Error "Edit Mode tests failed with exit code $($process.ExitCode). Results: $resultsPath"
}

Write-Host "Edit Mode tests passed. Results: $resultsPath"
