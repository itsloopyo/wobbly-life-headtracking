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

# BepInEx version and download URL
$BepInExVersion = "5.4.23.4"
$BepInExUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$BepInExVersion/BepInEx_win_x64_$BepInExVersion.zip"

# Find game installation
$gamePath = Find-GamePath -GameId $gameId

if (-not $gamePath) {
    Write-GameNotFoundError -GameName 'Wobbly Life' -EnvVar $config.EnvVar -SteamFolder $config.SteamFolder
    exit 1
}

Write-Host "Found Wobbly Life at: $gamePath" -ForegroundColor Green

# Track whether we install BepInEx
$installedBepInEx = $false

# Install BepInEx if missing
$bepinexCorePath = Join-Path $gamePath "BepInEx\core"
if (-not (Test-Path $bepinexCorePath)) {
    Write-Host "BepInEx not found. Installing BepInEx $BepInExVersion..." -ForegroundColor Yellow

    $tempZip = Join-Path $env:TEMP "BepInEx_$BepInExVersion.zip"

    Write-Host "  Downloading from $BepInExUrl..." -ForegroundColor Gray
    try {
        Invoke-WebRequest -Uri $BepInExUrl -OutFile $tempZip -UseBasicParsing
    } catch {
        Write-Host "ERROR: Failed to download BepInEx: $_" -ForegroundColor Red
        exit 1
    }

    Write-Host "  Extracting to $gamePath..." -ForegroundColor Gray
    try {
        Expand-Archive -Path $tempZip -DestinationPath $gamePath -Force
    } catch {
        Write-Host "ERROR: Failed to extract BepInEx: $_" -ForegroundColor Red
        exit 1
    }

    Remove-Item $tempZip -Force -ErrorAction SilentlyContinue

    $installedBepInEx = $true
    Write-Host "  BepInEx installed successfully!" -ForegroundColor Green
    Write-Host "  NOTE: Run Wobbly Life once to let BepInEx initialize before testing mods." -ForegroundColor Yellow
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
