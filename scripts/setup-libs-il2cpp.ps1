#!/usr/bin/env pwsh
# Populate src/WobblyLifeHeadTracking.Il2Cpp/libs/ with the Unity reference
# assemblies CameraUnlock.Core.Unity's HintPath references resolve to when it is
# built for the IL2CPP (Xbox Game Pass) plugin.
#
# These are Unity's PUBLISHED reference assemblies from the unityengine.modules
# NuGet package, not the generated stubs the Mono plugin builds against, and the
# difference is not cosmetic. In the stubs, UnityEngine.dll declares Quaternion
# itself; in real Unity it type-forwards to UnityEngine.CoreModule. Il2CppInterop
# generates its assemblies to the real shape, so a Core.Unity built against the
# stubs throws at load:
#
#   TypeLoadException: Could not load type 'UnityEngine.Quaternion' from
#   assembly 'UnityEngine, Version=0.0.0.0'
#
# Sources are REPO FILES ONLY (the NuGet cache the csproj's PackageReferences
# fill), never a game install: a contributor who owns the game and a CI runner
# that does not must compile against byte-identical references.
#
# UnityEngine.UI (UGUI) is deliberately absent. It is not on NuGet and ships only
# inside a built Unity game, so there is no copy of it we may redistribute.
# Directory.Build.targets drops the submodule's reference to it, along with the
# files that consume it, for this build only.
#
# Run order: dotnet restore -> this script -> dotnet build.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$libsPath    = Join-Path $projectRoot "src/WobblyLifeHeadTracking.Il2Cpp/libs"

# Must match the PackageReference version in WobblyLifeHeadTracking.Il2Cpp.csproj,
# which in turn matches the Unity the game ships (2022.3.62f2 on both stores).
$unityVersion = '2022.3.62'

New-Item -ItemType Directory -Path $libsPath -Force | Out-Null
Get-ChildItem $libsPath -Filter '*.dll' -File | Remove-Item -Force

$nugetRoot = (& dotnet nuget locals global-packages -l) -replace '^global-packages: ', ''
if (-not (Test-Path $nugetRoot)) {
    throw "NuGet global-packages root not found: $nugetRoot. Run 'dotnet restore' first."
}

$unityModulesDir = Join-Path $nugetRoot "unityengine.modules/$unityVersion/lib/netstandard2.0"
if (-not (Test-Path $unityModulesDir)) {
    throw "UnityEngine.Modules $unityVersion not found at $unityModulesDir. Did 'dotnet restore' complete?"
}
Copy-Item (Join-Path $unityModulesDir '*.dll') $libsPath -Force

$dlls = Get-ChildItem $libsPath -Filter '*.dll'
Write-Host "Populated $($dlls.Count) Unity $unityVersion reference assemblies in $libsPath" -ForegroundColor Green
