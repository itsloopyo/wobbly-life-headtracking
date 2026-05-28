#!/usr/bin/env pwsh
#Requires -Version 5.1
<#
.SYNOPSIS
    Automated release workflow for Wobbly Life Head Tracking mod.

.DESCRIPTION
    This script:
    1. Updates version in csproj, plugin, and pixi.toml
    2. Commits the version change
    3. Creates and pushes a git tag to trigger CI release

.PARAMETER Version
    The version to release (e.g., "1.0.0", "1.2.3")

.EXAMPLE
    pixi run release 1.0.0

.NOTES
    Run via: pixi run release <version>
#>
param(
    [Parameter(Position=0)]
    [string]$Version = "",
    [switch]$AllowDirty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $Version) {
    Write-Error "Usage: pixi run release <major|minor|patch|nightly|X.Y.Z>"
    exit 1
}

if ($Version -eq 'nightly') {
    & (Join-Path $PSScriptRoot 'release-nightly.ps1') -AllowDirty:$AllowDirty
    exit $LASTEXITCODE
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$csprojPath = Join-Path $projectDir "src\WobblyLifeHeadTracking\WobblyLifeHeadTracking.csproj"
$pluginPath = Join-Path $projectDir "src\WobblyLifeHeadTracking\WobblyLifeHeadTrackingPlugin.cs"
$pixiPath = Join-Path $projectDir "pixi.toml"

Import-Module (Join-Path $projectDir "cameraunlock-core\powershell\ReleaseWorkflow.psm1") -Force

# Function to get current version from csproj
function Get-CurrentVersion {
    $content = Get-Content $csprojPath -Raw
    if ($content -match '<Version>([^<]+)</Version>') {
        return $matches[1]
    }
    return $null
}

# Function to set version in csproj
function Set-CsprojVersion {
    param([string]$NewVersion)
    $content = Get-Content $csprojPath -Raw
    $content = $content -replace '<Version>[^<]+</Version>', "<Version>$NewVersion</Version>"
    $content | Set-Content $csprojPath -NoNewline
}

# Function to set version in plugin
function Set-PluginVersion {
    param([string]$NewVersion)
    $content = Get-Content $pluginPath -Raw
    $content = $content -replace 'PluginVersion\s*=\s*"[^"]+"', "PluginVersion = `"$NewVersion`""
    $content | Set-Content $pluginPath -NoNewline
}

# Function to set version in pixi.toml
function Set-PixiVersion {
    param([string]$NewVersion)
    $content = Get-Content $pixiPath -Raw
    $content = $content -replace 'version\s*=\s*"[^"]+"', "version = `"$NewVersion`""
    $content | Set-Content $pixiPath -NoNewline
}

Write-Host "=== Wobbly Life Head Tracking Release ===" -ForegroundColor Cyan
Write-Host ""

$currentVersion = Get-CurrentVersion

# If no version provided, show current and exit
if ([string]::IsNullOrWhiteSpace($Version)) {
    Write-Host "Current version: " -NoNewline -ForegroundColor Yellow
    Write-Host $currentVersion -ForegroundColor White
    Write-Host ""
    Write-Host "Usage: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release <version>" -ForegroundColor White
    Write-Host ""
    Write-Host "Example: " -NoNewline -ForegroundColor Yellow
    Write-Host "pixi run release 1.1.0" -ForegroundColor White
    exit 0
}

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Host "Error: Invalid version format '$Version'" -ForegroundColor Red
    Write-Host "Use semantic versioning: X.Y.Z (e.g., 1.0.0, 1.2.3)" -ForegroundColor Yellow
    exit 1
}

# Check if we're on main branch
$currentBranch = git rev-parse --abbrev-ref HEAD
if ($currentBranch -ne "main") {
    Write-Host "Error: Must be on 'main' branch to release (currently on '$currentBranch')" -ForegroundColor Red
    exit 1
}

# Check for uncommitted changes
$status = git status --porcelain
if ($status) {
    Write-Host "Error: Working directory has uncommitted changes" -ForegroundColor Red
    Write-Host "Please commit or stash changes before releasing" -ForegroundColor Yellow
    exit 1
}

# Check if tag already exists
$tagName = "v$Version"
$existingTag = git tag -l $tagName
if ($existingTag) {
    Write-Host "Error: Tag '$tagName' already exists" -ForegroundColor Red
    exit 1
}

Write-Host "Current version: $currentVersion" -ForegroundColor Gray
Write-Host "New version:     $Version" -ForegroundColor Green
Write-Host ""

# Step 1: Update csproj version
Write-Host "Updating csproj version to $Version..." -ForegroundColor Cyan
Set-CsprojVersion $Version

# Step 2: Update plugin version
Write-Host "Updating plugin version to $Version..." -ForegroundColor Cyan
Set-PluginVersion $Version

# Step 3: Update pixi.toml version
Write-Host "Updating pixi.toml version to $Version..." -ForegroundColor Cyan
Set-PixiVersion $Version

# Step 4: Build release to verify version compiles
Write-Host "Building release..." -ForegroundColor Cyan
& pixi run build
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed - aborting release" -ForegroundColor Red
    exit 1
}

# Step 5: Generate CHANGELOG
Write-Host "Generating CHANGELOG from commits..." -ForegroundColor Cyan
$changelogPath = Join-Path $projectDir "CHANGELOG.md"
New-ChangelogFromCommits -ChangelogPath $changelogPath -Version $Version -ArtifactPaths @("src/")

# Step 5: Commit
Write-Host "Committing version change..." -ForegroundColor Cyan
git add $csprojPath $pluginPath $pixiPath $changelogPath
git commit -m "Release v$Version"

# Step 5: Create tag
Write-Host "Creating tag $tagName..." -ForegroundColor Cyan
git tag $tagName

# Step 6: Push
Write-Host "Pushing to GitHub..." -ForegroundColor Cyan
git push origin main
git push origin $tagName

Write-Host ""
Write-Host "Release $tagName initiated!" -ForegroundColor Green
Write-Host ""
Write-Host "The GitHub Actions release workflow will now:" -ForegroundColor Yellow
Write-Host "  - Build the release" -ForegroundColor White
Write-Host "  - Generate changelog from commits" -ForegroundColor White
Write-Host "  - Create GitHub release with artifacts" -ForegroundColor White
Write-Host ""
Write-Host "Watch progress at:" -ForegroundColor Yellow
Write-Host "  https://github.com/itsloopyo/wobbly-life-headtracking/actions" -ForegroundColor Cyan
