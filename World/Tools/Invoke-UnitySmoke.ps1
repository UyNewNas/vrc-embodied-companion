[CmdletBinding()]
param(
    [string]$UnityPath,
    [string]$ProjectPath,
    [string]$LogPath,
    [string]$SummaryPath,
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot 'World'
} elseif (-not [System.IO.Path]::IsPathRooted($ProjectPath)) {
    $ProjectPath = Join-Path $repoRoot $ProjectPath
}
$ProjectPath = (Resolve-Path $ProjectPath).Path

$projectVersionPath = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path $projectVersionPath -PathType Leaf)) {
    throw "Missing Unity ProjectVersion.txt at $projectVersionPath"
}

$projectVersionText = Get-Content $projectVersionPath -Raw
$versionMatch = [regex]::Match($projectVersionText, '(?m)^m_EditorVersion:\s*(?<version>[^\r\n]+)\s*$')
if (-not $versionMatch.Success) {
    throw "Could not parse m_EditorVersion from $projectVersionPath"
}
$unityVersion = $versionMatch.Groups['version'].Value.Trim()
if ($unityVersion -ne '2022.3.22f1') {
    throw "World project requests Unity $unityVersion, but the reviewed VRChat baseline is 2022.3.22f1. Update the baseline intentionally before running this smoke check."
}

$bootstrapPath = Join-Path $ProjectPath 'Assets\Editor\CompanionMinimalWorldBootstrap.cs'
if (-not (Test-Path $bootstrapPath -PathType Leaf)) {
    throw "Missing bootstrap source at $bootstrapPath"
}
$bootstrapText = Get-Content $bootstrapPath -Raw
$executeMethod = 'CompanionMinimalWorldBootstrap.CreateSaveReopenAndVerifyMinimalWorld'
if ($bootstrapText -notmatch [regex]::Escape('public static void CreateSaveReopenAndVerifyMinimalWorld()')) {
    throw "Bootstrap source no longer exposes the expected execute method: $executeMethod"
}

$successMarker = 'VRC_COMPANION_UNITY_SMOKE_PASS:v1'
if (-not $bootstrapText.Contains($successMarker)) {
    throw "Bootstrap source no longer emits the stable Unity smoke success marker: $successMarker"
}

if ($ValidateOnly) {
    Write-Host 'Unity smoke runner contract OK'
    Write-Host "Project: $ProjectPath"
    Write-Host "Unity: $unityVersion"
    Write-Host "Execute method: $executeMethod"
    Write-Host "Success marker: $successMarker"
    return
}

$candidates = New-Object System.Collections.Generic.List[string]
if (-not [string]::IsNullOrWhiteSpace($UnityPath)) {
    $candidates.Add($UnityPath)
}
if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EDITOR)) {
    $candidates.Add($env:UNITY_EDITOR)
}
if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
    $candidates.Add((Join-Path $env:ProgramFiles "Unity\Hub\Editor\$unityVersion\Editor\Unity.exe"))
}

$unityExe = $null
foreach ($candidate in $candidates) {
    if ([string]::IsNullOrWhiteSpace($candidate)) {
        continue
    }
    $expanded = [Environment]::ExpandEnvironmentVariables($candidate)
    if (Test-Path $expanded -PathType Leaf) {
        $unityExe = (Resolve-Path $expanded).Path
        break
    }
}

if ($null -eq $unityExe) {
    $searched = ($candidates | ForEach-Object { "  - $_" }) -join [Environment]::NewLine
    throw "Unity $unityVersion was not found. Install the exact editor via VCC/Unity Hub or pass -UnityPath. Searched:`n$searched"
}

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $ProjectPath 'Logs\unity-smoke.log'
} elseif (-not [System.IO.Path]::IsPathRooted($LogPath)) {
    $LogPath = Join-Path $repoRoot $LogPath
}

if ([string]::IsNullOrWhiteSpace($SummaryPath)) {
    $SummaryPath = Join-Path $ProjectPath 'Logs\unity-smoke-summary.json'
} elseif (-not [System.IO.Path]::IsPathRooted($SummaryPath)) {
    $SummaryPath = Join-Path $repoRoot $SummaryPath
}

$logDirectory = Split-Path -Parent $LogPath
$summaryDirectory = Split-Path -Parent $SummaryPath
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $summaryDirectory | Out-Null

$startedAt = [DateTimeOffset]::UtcNow
$unityArgs = @(
    '-batchmode',
    '-nographics',
    '-quit',
    '-projectPath', $ProjectPath,
    '-executeMethod', $executeMethod,
    '-logFile', $LogPath
)

Write-Host "Running Unity $unityVersion smoke verification"
Write-Host "Unity: $unityExe"
Write-Host "Project: $ProjectPath"
Write-Host "Log: $LogPath"

& $unityExe @unityArgs
$exitCode = $LASTEXITCODE
$finishedAt = [DateTimeOffset]::UtcNow

$logText = if (Test-Path $LogPath -PathType Leaf) { Get-Content $LogPath -Raw } else { '' }
$markerSeen = $logText.Contains($successMarker)
$knownBatchmodeOpenSceneCrash = ($logText -match 'CollectManagedImportDependencyGetters') -and ($logText -match 'OpenScene')
$scenePath = Join-Path $ProjectPath 'Assets\Scenes\CompanionMinimal.unity'
$buildSettingsPath = Join-Path $ProjectPath 'ProjectSettings\EditorBuildSettings.asset'
$sceneExists = Test-Path $scenePath -PathType Leaf
$buildSettingsExists = Test-Path $buildSettingsPath -PathType Leaf
$passed = ($exitCode -eq 0) -and $markerSeen -and $sceneExists -and $buildSettingsExists

$summary = [ordered]@{
    schema = 'world-unity-smoke.v0.1'
    started_at_utc = $startedAt.ToString('o')
    finished_at_utc = $finishedAt.ToString('o')
    unity_version = $unityVersion
    unity_path = $unityExe
    project_path = $ProjectPath
    execute_method = $executeMethod
    exit_code = $exitCode
    success_marker_seen = $markerSeen
    scene_exists = $sceneExists
    editor_build_settings_exists = $buildSettingsExists
    known_uum_57742_signature = $knownBatchmodeOpenSceneCrash
    passed = $passed
    log_path = $LogPath
}
$summary | ConvertTo-Json -Depth 4 | Set-Content -Path $SummaryPath -Encoding utf8

Write-Host "Evidence summary: $SummaryPath"
if ($passed) {
    Write-Host 'Unity import/compile/bootstrap/save/reopen smoke verification PASSED.'
    Write-Host 'This is not VRChat SDK validation or Build & Test evidence.'
    return
}

if ($knownBatchmodeOpenSceneCrash) {
    throw "Unity batchmode hit the known OpenScene crash signature associated with UUM-57742. Keep the log as engine-limit evidence and run the same Create, Save, Reopen and Verify command in interactive Unity 2022.3.22f1."
}

if ($exitCode -ne 0) {
    throw "Unity smoke verification failed with exit code $exitCode. Inspect $LogPath and $SummaryPath."
}

throw "Unity exited successfully but the verifier evidence was incomplete (marker=$markerSeen, scene=$sceneExists, buildSettings=$buildSettingsExists). Inspect $LogPath and $SummaryPath."
