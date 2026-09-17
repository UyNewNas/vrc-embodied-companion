[CmdletBinding()]
param(
    [string]$UnityPath,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$expectedUnityVersion = '2022.3.22f1'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path

if ($env:OS -ne 'Windows_NT') {
    throw 'The real Unity smoke host must be Windows. This preflight does not certify Linux/macOS hosts.'
}

$arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
if ($arch -ne 'X64') {
    throw "The self-hosted smoke workflow requires x64 Windows. Detected architecture: $arch"
}

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $UnityPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$expectedUnityVersion\Editor\Unity.exe"
}
$UnityPath = [Environment]::ExpandEnvironmentVariables($UnityPath)
if (-not (Test-Path $UnityPath -PathType Leaf)) {
    throw "Unity $expectedUnityVersion was not found at '$UnityPath'. Install and activate the exact editor first, or pass -UnityPath."
}
$UnityPath = (Resolve-Path $UnityPath).Path

$reported = (& $UnityPath -version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unity -version failed with exit code $LASTEXITCODE: $reported"
}
if ($reported -notmatch '(?m)\b2022\.3\.22f1\b') {
    throw "Unity version mismatch. Expected $expectedUnityVersion, reported: $reported"
}

$projectVersionPath = Join-Path $repoRoot 'World\ProjectSettings\ProjectVersion.txt'
$projectVersionText = Get-Content $projectVersionPath -Raw
if ($projectVersionText -notmatch '(?m)^m_EditorVersion:\s*2022\.3\.22f1\s*$') {
    throw "World/ProjectSettings/ProjectVersion.txt is not pinned to $expectedUnityVersion."
}

$smokeRunner = Join-Path $repoRoot 'World\Tools\Invoke-UnitySmoke.ps1'
if (-not (Test-Path $smokeRunner -PathType Leaf)) {
    throw "Missing smoke runner at $smokeRunner"
}
& $smokeRunner -UnityPath $UnityPath -ValidateOnly
if ($LASTEXITCODE -ne 0) {
    throw "Invoke-UnitySmoke.ps1 -ValidateOnly failed with exit code $LASTEXITCODE."
}

$gitVersion = $null
try {
    $gitVersion = (git --version 2>&1 | Out-String).Trim()
} catch {
    $gitVersion = 'unavailable'
}

$dotnetVersion = $null
try {
    $dotnetVersion = (dotnet --version 2>&1 | Out-String).Trim()
} catch {
    $dotnetVersion = 'unavailable (workflow installs .NET 8 before VPM use)'
}

$result = [ordered]@{
    schema = 'unity-smoke-host-preflight.v0.1'
    checked_at_utc = [DateTimeOffset]::UtcNow.ToString('o')
    windows = $true
    os_architecture = $arch
    unity_path = $UnityPath
    unity_reported_version = $reported
    expected_unity_version = $expectedUnityVersion
    world_contract_valid = $true
    git_version = $gitVersion
    dotnet_version = $dotnetVersion
    unity_license_verified = $false
    unity_license_note = 'Unity -version does not prove that the Editor license can open the project. License activation remains a real-smoke runtime gate.'
    required_runner_labels = @('self-hosted', 'windows', 'x64', 'unity-2022.3.22f1')
}

$json = $result | ConvertTo-Json -Depth 4
Write-Host $json

if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    if (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath = Join-Path $repoRoot $OutputPath
    }
    $parent = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
    $json | Set-Content -Path $OutputPath -Encoding utf8
    Write-Host "Preflight evidence: $OutputPath"
}

Write-Host 'Unity smoke host preflight PASSED.'
Write-Host 'This does not claim Unity license activation, project import/compile, VRChat SDK validation, or Build & Test success.'
