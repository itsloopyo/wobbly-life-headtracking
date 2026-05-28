#!/usr/bin/env pwsh
# Build the mod with correct Unity reference paths
# Usage: build.ps1 [Configuration]

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libPath = Join-Path $projectRoot "lib"
$csproj = Join-Path $projectRoot "src\WobblyLifeHeadTracking\WobblyLifeHeadTracking.csproj"

dotnet build $csproj -c $Configuration "-p:UnityEnginePath=$libPath"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
