#!/usr/bin/env pwsh
# Deploy built mod to Wobbly Life BepInEx plugins folder
# Usage: deploy.ps1 [Configuration]
# Example: deploy.ps1 Release

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Import shared game detection module
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$modulePath = Join-Path $projectRoot "cameraunlock-core\powershell\GamePathDetection.psm1"

if (-not (Test-Path $modulePath)) {
    Write-Host "ERROR: CameraUnlock.Core module not found at: $modulePath" -ForegroundColor Red
    Write-Host "Run 'git submodule update --init --recursive' to fetch it." -ForegroundColor Yellow
    exit 1
}

Import-Module $modulePath -Force

$gameId = 'wobbly-life'
$config = Get-GameConfig -GameId $gameId
$StateFileName = ".headtracking-state.json"

$vendorDir = Join-Path $projectRoot "vendor\bepinex"
$vendorZip = Join-Path $vendorDir "BepInEx_win_x64.zip"

if (-not (Test-Path $vendorZip)) {
    Write-Host "ERROR: Vendored BepInEx not found at: $vendorZip" -ForegroundColor Red
    Write-Host "Run 'pixi run update-deps' to fetch it." -ForegroundColor Yellow
    exit 1
}

$vendorReadme = Get-Content (Join-Path $vendorDir "README.md") -Raw
$versionMatch = [regex]::Match($vendorReadme, '(?m)^- Tag: `v([^`]+)`')
if (-not $versionMatch.Success) {
    Write-Host "ERROR: Could not read the BepInEx tag from $vendorDir\README.md" -ForegroundColor Red
    exit 1
}
$BepInExVersion = $versionMatch.Groups[1].Value

# Every file BepInEx needs to actually start. Checking one marker (or just
# that BepInEx\core\ exists) is not enough: a half-deleted loader keeps
# BepInEx.dll while doorstop_config.ini and the Cecil/MonoMod assemblies go
# missing, so Doorstop loads with no target, the chainloader never runs, and
# a marker-only check reports the loader as installed and skips the repair.
$LoaderFiles = @(
    "doorstop_config.ini"
    "winhttp.dll"
    "BepInEx\core\BepInEx.dll"
    "BepInEx\core\BepInEx.Preloader.dll"
    "BepInEx\core\0Harmony.dll"
    "BepInEx\core\Mono.Cecil.dll"
    "BepInEx\core\MonoMod.Utils.dll"
    "BepInEx\core\MonoMod.RuntimeDetour.dll"
)

# Find game installation
$gamePath = Find-GamePath -GameId $gameId

if (-not $gamePath) {
    Write-GameNotFoundError -GameName 'Wobbly Life' -EnvVar $config.EnvVar -SteamFolder $config.SteamFolder
    exit 1
}

Write-Host "Found Wobbly Life at: $gamePath" -ForegroundColor Green

# Track whether we install BepInEx
$installedBepInEx = $false

$missingLoaderFiles = @($LoaderFiles | Where-Object { -not (Test-Path (Join-Path $gamePath $_)) })

if ($missingLoaderFiles.Count -gt 0) {
    $loaderAbsent = -not (Test-Path (Join-Path $gamePath "BepInEx\core\BepInEx.dll"))

    if ($loaderAbsent) {
        Write-Host "BepInEx not found. Installing $BepInExVersion from the vendored copy..." -ForegroundColor Yellow
    } else {
        Write-Host "BepInEx is installed but incomplete - repairing from the vendored copy." -ForegroundColor Yellow
        foreach ($f in $missingLoaderFiles) {
            Write-Host "  missing: $f" -ForegroundColor Gray
        }
    }

    # The vendored zip carries no BepInEx\config or BepInEx\plugins entries,
    # so extracting over an existing install restores the loader without
    # touching user config or other plugins.
    Expand-Archive -Path $vendorZip -DestinationPath $gamePath -Force

    $stillMissing = @($LoaderFiles | Where-Object { -not (Test-Path (Join-Path $gamePath $_)) })
    if ($stillMissing.Count -gt 0) {
        Write-Host "ERROR: BepInEx is still incomplete after extracting $vendorZip" -ForegroundColor Red
        foreach ($f in $stillMissing) {
            Write-Host "  missing: $f" -ForegroundColor Red
        }
        exit 1
    }

    # Repairing someone else's loader must not transfer ownership of it, so
    # only claim it when nothing was there to begin with.
    $installedBepInEx = $loaderAbsent
    Write-Host "  BepInEx $BepInExVersion ready." -ForegroundColor Green
}

$pluginsPath = Join-Path $gamePath "BepInEx\plugins"
if (-not (Test-Path $pluginsPath)) {
    Write-Host "Creating BepInEx plugins folder..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $pluginsPath -Force | Out-Null
}

$buildPath = Join-Path $projectRoot "src\WobblyLifeHeadTracking\bin\$Configuration\net472"

# Validate build output exists
if (-not (Test-Path $buildPath)) {
    Write-Host "ERROR: Build output not found at $buildPath" -ForegroundColor Red
    Write-Host "Please run 'pixi run build' first" -ForegroundColor Yellow
    exit 1
}

# Read version from csproj
$csprojPath = Join-Path $projectRoot "src\WobblyLifeHeadTracking\WobblyLifeHeadTracking.csproj"
$csprojContent = Get-Content $csprojPath -Raw
$versionMatch = [regex]::Match($csprojContent, '<Version>([^<]+)</Version>')
$modVersion = if ($versionMatch.Success) { $versionMatch.Groups[1].Value } else { "unknown" }

Write-Host "Deploying WobblyLifeHeadTracking ($Configuration) to BepInEx..." -ForegroundColor Green
Write-Host "  Source: $buildPath" -ForegroundColor Gray
Write-Host "  Target: $pluginsPath" -ForegroundColor Gray

# Copy DLLs
$dllsToCopy = @(
    "WobblyLifeHeadTracking.dll",
    "CameraUnlock.Core.dll",
    "CameraUnlock.Core.Unity.dll"
)

foreach ($dll in $dllsToCopy) {
    $sourcePath = Join-Path $buildPath $dll
    if (Test-Path $sourcePath) {
        Copy-Item $sourcePath $pluginsPath -Force
        Write-Host "  Copied: $dll" -ForegroundColor Gray
    } else {
        Write-Host "WARNING: $dll not found in build output" -ForegroundColor Yellow
    }
}

# Copy PDB for debugging (optional)
$pdbPath = Join-Path $buildPath "WobblyLifeHeadTracking.pdb"
if (Test-Path $pdbPath) {
    Copy-Item $pdbPath $pluginsPath -Force -ErrorAction SilentlyContinue
}

# Create/update state file
$stateFile = Join-Path $gamePath $StateFileName
$existingState = $null

if (Test-Path $stateFile) {
    try {
        $existingState = Get-Content $stateFile -Raw | ConvertFrom-Json
    } catch {
        # State file corrupted, will be recreated
    }
}

# Determine if we should track BepInEx installation
$frameworkInstalledByUs = $installedBepInEx
if ($existingState -and $existingState.framework.installed_by_us) {
    # Preserve existing state if we installed it before
    $frameworkInstalledByUs = $true
}

$state = @{
    framework = @{
        installed_by_us = $frameworkInstalledByUs
        type = "BepInEx"
        version = $BepInExVersion
        architecture = "x64"
    }
    mod = @{
        name = "WobblyLifeHeadTracking"
        version = $modVersion
        deployed_at = (Get-Date).ToString("o")
    }
}

$state | ConvertTo-Json -Depth 4 | Set-Content $stateFile -Encoding UTF8

Write-Host "" -ForegroundColor Green
Write-Host "[OK] Deployment complete!" -ForegroundColor Green
Write-Host "DLL location: $pluginsPath\WobblyLifeHeadTracking.dll" -ForegroundColor Cyan
Write-Host "" -ForegroundColor Green
Write-Host "Launch Wobbly Life to test your changes." -ForegroundColor Yellow
Write-Host "  - Press End to toggle tracking on/off" -ForegroundColor Gray
Write-Host "  - Press Home to recenter view" -ForegroundColor Gray
