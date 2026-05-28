#!/usr/bin/env pwsh
# unrelease.ps1 - Undo the last release (removes tag and reverts commit)
# WARNING: Only use this if the release has NOT been pushed to remote
# Fail fast on any error

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Write-Host "Checking for release to undo..." -ForegroundColor Cyan

# Get the last commit message
$lastCommit = git log -1 --pretty=%s 2>$null
if ($lastCommit -notmatch '^Release v(\d+\.\d+\.\d+)$') {
    Write-Error @"
ERROR: Last commit is not a release commit.

Last commit message: $lastCommit
Expected pattern: Release vX.Y.Z

Cannot undo - the last commit must be a release commit.
"@
    exit 1
}

$version = $Matches[1]
Write-Host "Found release v$version" -ForegroundColor Yellow

# Check if pushed to remote
$remoteStatus = git status -sb 2>$null | Select-String -Pattern '\[.*ahead'
if (-not $remoteStatus) {
    # Check if tag exists on remote
    $remoteTag = git ls-remote --tags origin "v$version" 2>$null
    if ($remoteTag) {
        Write-Error @"
ERROR: Release v$version has been pushed to remote.

Cannot safely undo a pushed release. To fix:
1. Contact team members about the issue
2. Consider creating a new patch release instead
3. Or use: git push --delete origin v$version (DANGEROUS - coordinate with team first)
"@
        exit 1
    }
}

Write-Host ""
Write-Host "Removing the following:" -ForegroundColor Yellow
Write-Host "  - Tag: v$version" -ForegroundColor Yellow
Write-Host "  - Last commit: $lastCommit" -ForegroundColor Yellow
Write-Host ""

# Remove tag
Write-Host "Removing tag v$version..." -ForegroundColor Cyan
git tag -d "v$version"

# Reset last commit
Write-Host "Reverting last commit..." -ForegroundColor Cyan
git reset --soft HEAD~1
git restore --staged .

# Remove package if exists
$packagePath = Join-Path $projectRoot "dist\WobblyLifeHeadTracking-v$version.zip"
if (Test-Path $packagePath) {
    Remove-Item $packagePath -Force
    Write-Host "Removed package: $packagePath" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Release v$version has been undone." -ForegroundColor Green
Write-Host "Files have been unstaged. Review and make corrections as needed." -ForegroundColor Cyan
