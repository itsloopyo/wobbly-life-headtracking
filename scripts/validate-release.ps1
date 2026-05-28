#!/usr/bin/env pwsh
# validate-release.ps1 - Validate project is ready for release
# Fail fast on any error

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$errors = @()

Write-Host "Validating release readiness..." -ForegroundColor Cyan
Write-Host ""

# Check csproj version
$csprojPath = Join-Path $projectRoot "src\WobblyLifeHeadTracking\WobblyLifeHeadTracking.csproj"
if (-not (Test-Path $csprojPath)) {
    $errors += "WobblyLifeHeadTracking.csproj not found"
}
else {
    $csprojContent = Get-Content $csprojPath -Raw
    $versionMatch = [regex]::Match($csprojContent, '<Version>([^<]+)</Version>')
    if (-not $versionMatch.Success) {
        $errors += "No <Version> found in WobblyLifeHeadTracking.csproj"
    }
    else {
        $version = $versionMatch.Groups[1].Value
        Write-Host "[OK] Project version: $version" -ForegroundColor Green
    }
}

# Check plugin version matches csproj
$pluginPath = Join-Path $projectRoot "src\WobblyLifeHeadTracking\WobblyLifeHeadTrackingPlugin.cs"
if (-not (Test-Path $pluginPath)) {
    $errors += "WobblyLifeHeadTrackingPlugin.cs not found"
}
else {
    $pluginContent = Get-Content $pluginPath -Raw
    $pluginVersionMatch = [regex]::Match($pluginContent, 'PluginVersion\s*=\s*"([^"]+)"')
    if ($pluginVersionMatch.Success) {
        $pluginVersion = $pluginVersionMatch.Groups[1].Value
        if ($version -and $pluginVersion -ne $version) {
            $errors += "Plugin version ($pluginVersion) does not match csproj version ($version)"
        }
        else {
            Write-Host "[OK] Plugin version matches: $pluginVersion" -ForegroundColor Green
        }
    }
}

# Check pixi.toml version matches csproj
$pixiPath = Join-Path $projectRoot "pixi.toml"
if (-not (Test-Path $pixiPath)) {
    $errors += "pixi.toml not found"
}
else {
    $pixiContent = Get-Content $pixiPath -Raw
    $pixiVersionMatch = [regex]::Match($pixiContent, 'version\s*=\s*"([^"]+)"')
    if ($pixiVersionMatch.Success) {
        $pixiVersion = $pixiVersionMatch.Groups[1].Value
        if ($version -and $pixiVersion -ne $version) {
            $errors += "pixi.toml version ($pixiVersion) does not match csproj version ($version)"
        }
        else {
            Write-Host "[OK] pixi.toml version matches: $pixiVersion" -ForegroundColor Green
        }
    }
}

# Check README exists
$readmePath = Join-Path $projectRoot "README.md"
if (-not (Test-Path $readmePath)) {
    $errors += "README.md not found"
}
else {
    Write-Host "[OK] README.md exists" -ForegroundColor Green
}

# Check no uncommitted changes
$gitStatus = git status --porcelain 2>$null
if ($gitStatus) {
    $errors += "Uncommitted changes found - commit or stash before release"
}
else {
    Write-Host "[OK] Working directory clean" -ForegroundColor Green
}

# Check on main branch
$currentBranch = git rev-parse --abbrev-ref HEAD 2>$null
if ($currentBranch -ne "main") {
    $errors += "Must be on 'main' branch to release (currently on '$currentBranch')"
}
else {
    Write-Host "[OK] On main branch" -ForegroundColor Green
}

Write-Host ""

if ($errors.Count -gt 0) {
    Write-Host "Validation FAILED with $($errors.Count) error(s):" -ForegroundColor Red
    foreach ($err in $errors) {
        Write-Host "  - $err" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Validation PASSED - ready for release" -ForegroundColor Green
