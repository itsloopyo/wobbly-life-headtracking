#!/usr/bin/env pwsh
#Requires -Version 5.1
# Thin wrapper: calls shared packaging script with Wobbly Life values.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

& "$projectDir/cameraunlock-core/scripts/package-bepinex-mod.ps1" `
    -ModName "WobblyLifeHeadTracking" `
    -CsprojPath "src/WobblyLifeHeadTracking/WobblyLifeHeadTracking.csproj" `
    -BuildOutputDir "src/WobblyLifeHeadTracking/bin/Release/net472" `
    -ModDlls @("WobblyLifeHeadTracking.dll","CameraUnlock.Core.dll","CameraUnlock.Core.Unity.dll") `
    -ProjectRoot $projectDir
