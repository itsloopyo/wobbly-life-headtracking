[CmdletBinding()]
param([switch]$AllowDirty)
$ErrorActionPreference = 'Stop'
$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Import-Module (Join-Path $ProjectRoot 'cameraunlock-core\powershell\NightlyRelease.psm1') -Force

$csprojPath = Join-Path $ProjectRoot 'src\WobblyLifeHeadTracking\WobblyLifeHeadTracking.csproj'
$match = Select-String -Path $csprojPath -Pattern '<Version>([^<]+)</Version>' | Select-Object -First 1
if (-not $match) {
    throw "Could not extract <Version> from $csprojPath"
}
$version = $match.Matches[0].Groups[1].Value

Publish-NightlyBuild `
    -ModId 'wobbly-life' `
    -ModName 'WobblyLifeHeadTracking' `
    -Version $version `
    -ProjectRoot $ProjectRoot `
    -AllowDirty:$AllowDirty
