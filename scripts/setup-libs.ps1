#!/usr/bin/env pwsh
# setup-libs.ps1 - Copy game assemblies to src/WobblyLifeHeadTracking/libs for compilation
# Uses shared CameraUnlock modules for game detection

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

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

$gameId = 'wobbly-life'
$config = Get-GameConfig -GameId $gameId

# Find game installation
$gamePath = Find-GamePath -GameId $gameId

if (-not $gamePath) {
    Write-GameNotFoundError -GameName 'Wobbly Life' -EnvVar $config.EnvVar -SteamFolder $config.SteamFolder
    exit 1
}

Write-Host "Found Wobbly Life at: $gamePath" -ForegroundColor Green

# Verify game data directory exists
$managedPath = Join-Path $gamePath "Wobbly Life_Data\Managed"
if (-not (Test-Path $managedPath)) {
    Write-Host "ERROR: Game data directory not found at: $managedPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "The game installation may be corrupted. Try:" -ForegroundColor Yellow
    Write-Host "1. Right-click Wobbly Life in Steam" -ForegroundColor Gray
    Write-Host "2. Properties -> Local Files -> Verify integrity of game files" -ForegroundColor Gray
    Write-Host "3. Run 'pixi run setup' again" -ForegroundColor Gray
    exit 1
}

# Destination must match UnityEnginePath in Directory.Build.props / .csproj
$libPath = Join-Path $projectRoot "src\WobblyLifeHeadTracking\libs"
if (-not (Test-Path $libPath)) {
    New-Item -ItemType Directory -Path $libPath -Force | Out-Null
    Write-Host "Created libs directory" -ForegroundColor Cyan
}

# Check for BepInEx installation
$bepinexCorePath = Join-Path $gamePath "BepInEx\core"
$bepinexInstalled = Test-Path $bepinexCorePath

if (-not $bepinexInstalled) {
    Write-Host "NOTE: BepInEx not installed in game folder." -ForegroundColor Yellow
    Write-Host "Will download BepInEx DLLs for compilation." -ForegroundColor Yellow
    Write-Host ""

    # Download BepInEx for compilation
    $bepinexVersion = "5.4.23.4"
    $bepinexUrl = "https://github.com/BepInEx/BepInEx/releases/download/v$bepinexVersion/BepInEx_win_x64_$bepinexVersion.zip"
    $tempZip = Join-Path $env:TEMP "BepInEx_$bepinexVersion.zip"
    $tempExtract = Join-Path $env:TEMP "BepInEx_extract"

    Write-Host "Downloading BepInEx $bepinexVersion..." -ForegroundColor Cyan
    try {
        Invoke-WebRequest -Uri $bepinexUrl -OutFile $tempZip -UseBasicParsing
    } catch {
        Write-Host "ERROR: Failed to download BepInEx: $_" -ForegroundColor Red
        exit 1
    }

    # Extract to temp folder
    if (Test-Path $tempExtract) {
        Remove-Item $tempExtract -Recurse -Force
    }
    Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force

    $bepinexCorePath = Join-Path $tempExtract "BepInEx\core"
}

# BepInEx DLLs
$bepinexDlls = @(
    "BepInEx.dll",
    "0Harmony.dll"
)

# Copy BepInEx DLLs
foreach ($dll in $bepinexDlls) {
    $sourcePath = Join-Path $bepinexCorePath $dll
    $destPath = Join-Path $libPath $dll

    if (-not (Test-Path $sourcePath)) {
        Write-Host "ERROR: BepInEx DLL not found: $sourcePath" -ForegroundColor Red
        exit 1
    }

    Copy-Item -Path $sourcePath -Destination $destPath -Force
    Write-Host "  Copied: $dll (BepInEx)" -ForegroundColor Gray
}

# Required game DLLs for compilation
$requiredDlls = @(
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.IMGUIModule.dll",
    "UnityEngine.InputLegacyModule.dll",
    "UnityEngine.PhysicsModule.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.UI.dll",
    "UnityEngine.UIModule.dll"
)

# Copy each required DLL
$copyCount = 0
foreach ($dll in $requiredDlls) {
    $sourcePath = Join-Path $managedPath $dll
    $destPath = Join-Path $libPath $dll

    if (-not (Test-Path $sourcePath)) {
        Write-Host "ERROR: Required DLL not found: $sourcePath" -ForegroundColor Red
        Write-Host ""
        Write-Host "The game installation may be corrupted or outdated. Try:" -ForegroundColor Yellow
        Write-Host "1. Right-click Wobbly Life in Steam" -ForegroundColor Gray
        Write-Host "2. Properties -> Local Files -> Verify integrity of game files" -ForegroundColor Gray
        Write-Host "3. Run 'pixi run setup' again" -ForegroundColor Gray
        exit 1
    }

    Copy-Item -Path $sourcePath -Destination $destPath -Force
    $copyCount++
    Write-Host "  Copied: $dll" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Successfully copied $($copyCount + 2) DLLs to $libPath" -ForegroundColor Green
Write-Host "You can now build with: pixi run build" -ForegroundColor Cyan
