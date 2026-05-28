#!/usr/bin/env pwsh
# Revert to vanilla (unmodded) game
# Removes HeadTracking mod, and BepInEx ONLY if we installed it
# Usage: pixi run vanilla

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Import shared game detection module
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$modulePath = Join-Path $projectRoot "..\cameraunlock-core\powershell\GamePathDetection.psm1"

if (-not (Test-Path $modulePath)) {
    Write-Host "ERROR: CameraUnlock.Core module not found at: $modulePath" -ForegroundColor Red
    Write-Host "Make sure cameraunlock-core is present in the parent directory." -ForegroundColor Yellow
    exit 1
}

Import-Module $modulePath -Force

$gameId = 'WobblyLife'
$config = Get-GameConfig -GameId $gameId
$StateFileName = ".headtracking-state.json"

# Find game installation
$gamePath = Find-GamePath -GameId $gameId

if (-not $gamePath) {
    Write-GameNotFoundError -GameName 'Wobbly Life' -EnvVar $config.EnvVar -SteamFolder $config.SteamFolder
    exit 1
}

Write-Host "Reverting to vanilla (unmodded) game..." -ForegroundColor Cyan
Write-Host "  Game path: $gamePath" -ForegroundColor Gray
Write-Host ""

# Read state file
$stateFile = Join-Path $gamePath $StateFileName
$frameworkInstalledByUs = $false

if (Test-Path $stateFile) {
    try {
        $state = Get-Content $stateFile -Raw | ConvertFrom-Json
        $frameworkInstalledByUs = $state.framework.installed_by_us
        Write-Host "  Found state file - respecting installation history" -ForegroundColor Gray
    } catch {
        Write-Host "  Warning: Could not read state file, assuming full removal" -ForegroundColor Yellow
        $frameworkInstalledByUs = $true
    }
} else {
    Write-Host "  No state file found - will remove everything" -ForegroundColor Yellow
    $frameworkInstalledByUs = $true
}

$removed = $false

# Remove HeadTracking mod files
$pluginsPath = Join-Path $gamePath "BepInEx\plugins"
$modFiles = @(
    "WobblyLifeHeadTracking.dll",
    "WobblyLifeHeadTracking.pdb",
    "CameraUnlock.Core.dll",
    "CameraUnlock.Core.Unity.dll",
    "manifest.json"
)

foreach ($file in $modFiles) {
    $path = Join-Path $pluginsPath $file
    if (Test-Path $path) {
        Remove-Item $path -Force
        Write-Host "  Removed: BepInEx\plugins\$file" -ForegroundColor Green
        $removed = $true
    }
}

# Only remove BepInEx if we installed it
if ($frameworkInstalledByUs) {
    $bepinexDir = Join-Path $gamePath "BepInEx"
    if (Test-Path $bepinexDir) {
        Remove-Item $bepinexDir -Recurse -Force
        Write-Host "  Removed: BepInEx\ (entire folder)" -ForegroundColor Green
        $removed = $true
    }

    $doorstopFiles = @("winhttp.dll", "doorstop_config.ini", ".doorstop_version")
    foreach ($file in $doorstopFiles) {
        $path = Join-Path $gamePath $file
        if (Test-Path $path) {
            Remove-Item $path -Force
            Write-Host "  Removed: $file" -ForegroundColor Green
            $removed = $true
        }
    }
} else {
    Write-Host "  BepInEx preserved (was not installed by us)" -ForegroundColor Cyan
}

# Remove state file
if (Test-Path $stateFile) {
    Remove-Item $stateFile -Force
    Write-Host "  Removed: $StateFileName" -ForegroundColor Gray
}

if (-not $removed) {
    Write-Host "  No mod files found - game is already vanilla" -ForegroundColor Yellow
}

Write-Host ""
if ($frameworkInstalledByUs) {
    Write-Host "Game is now completely vanilla (unmodded)" -ForegroundColor Cyan
} else {
    Write-Host "HeadTracking removed, BepInEx preserved for other mods" -ForegroundColor Cyan
}
