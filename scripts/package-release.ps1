#!/usr/bin/env pwsh
#Requires -Version 5.1
# Thin wrapper: calls shared packaging script with Wobbly Life values, then
# stages the licence texts the shared packager does not know about.
#
# The staging below is deliberately inlined rather than pushed into
# cameraunlock-core: this repo pins a submodule commit, so a fix landed in core
# would not reach this mod's packager until the pointer moves, and a licence
# obligation cannot wait on that. It also throws rather than skipping - the
# shared packager copies LICENSE behind a Test-Path guard that only warns, which
# turns a compliance failure into a green build.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir

$monoProject = [xml](Get-Content (Join-Path $projectDir 'src/WobblyLifeHeadTracking/WobblyLifeHeadTracking.csproj') -Raw)
$il2cppProject = [xml](Get-Content (Join-Path $projectDir 'src/WobblyLifeHeadTracking.Il2Cpp/WobblyLifeHeadTracking.Il2Cpp.csproj') -Raw)
$version = $monoProject.SelectSingleNode('//Version').InnerText
if ($il2cppProject.SelectSingleNode('//Version').InnerText -ne $version) {
    throw 'Mono and IL2CPP project versions do not match.'
}
foreach ($name in @('WobblyLifeHeadTracking', 'WobblyLifeHeadTracking.Il2Cpp')) {
    $plugin = Get-Content (Join-Path $projectDir "src/$name/WobblyLifeHeadTrackingPlugin.cs") -Raw
    if ([regex]::Match($plugin, 'PluginVersion\s*=\s*"([^"]+)"').Groups[1].Value -ne $version) {
        throw "$name plugin version does not match project version $version."
    }
}

# Every file that must reach a user alongside a binary we redistribute.
# ZIP-relative path => repo-relative source.
$RequiredNotices = [ordered]@{
    'LICENSE'                                  = 'LICENSE'
    'THIRD-PARTY-NOTICES.md'                   = 'THIRD-PARTY-NOTICES.md'
    'licenses/README.md'                       = 'licenses/README.md'
    'licenses/cameraunlock-core-LICENSE.txt'   = 'licenses/cameraunlock-core-LICENSE.txt'
    'licenses/BepInEx-LICENSE.txt'             = 'licenses/BepInEx-LICENSE.txt'
    'licenses/HarmonyX-LICENSE.txt'            = 'licenses/HarmonyX-LICENSE.txt'
    'licenses/Harmony-LICENSE.txt'             = 'licenses/Harmony-LICENSE.txt'
    'licenses/Mono.Cecil-LICENSE.txt'          = 'licenses/Mono.Cecil-LICENSE.txt'
    'licenses/MonoMod-LICENSE.txt'             = 'licenses/MonoMod-LICENSE.txt'
    # Additional to the BepInEx 5 archive: these ship only inside
    # vendor/bepinex-il2cpp/, which the installer ZIP carries for the IL2CPP
    # (Xbox Game Pass) build of the game. See licenses/README.md for which
    # binary each one covers.
    'licenses/Il2CppInterop-LICENSE.txt'          = 'licenses/Il2CppInterop-LICENSE.txt'
    'licenses/Cpp2IL-LICENSE.txt'                 = 'licenses/Cpp2IL-LICENSE.txt'
    'licenses/Disarm-LICENSE.txt'                 = 'licenses/Disarm-LICENSE.txt'
    'licenses/AsmResolver-LICENSE.txt'            = 'licenses/AsmResolver-LICENSE.txt'
    'licenses/AssetRipper.CIL-LICENSE.txt'        = 'licenses/AssetRipper.CIL-LICENSE.txt'
    'licenses/AssetRipper.Primitives-LICENSE.txt' = 'licenses/AssetRipper.Primitives-LICENSE.txt'
    'licenses/Iced-LICENSE.txt'                   = 'licenses/Iced-LICENSE.txt'
    'licenses/Capstone.NET-LICENSE.txt'           = 'licenses/Capstone.NET-LICENSE.txt'
    'licenses/Dobby-LICENSE.txt'                  = 'licenses/Dobby-LICENSE.txt'
    'licenses/SemanticVersioning-LICENSE.txt'     = 'licenses/SemanticVersioning-LICENSE.txt'
    'licenses/dotnet-runtime-LICENSE.txt'         = 'licenses/dotnet-runtime-LICENSE.txt'
}

foreach ($source in $RequiredNotices.Values) {
    $path = Join-Path $projectDir $source
    if (-not (Test-Path $path)) {
        throw "Required licence file missing: $source. Both release ZIPs redistribute binaries whose licences require this notice to accompany them; refusing to package without it."
    }
}

# The IL2CPP payload, which the shared packager knows nothing about: it stages
# one build output into plugins/ and one vendor/<loader>/ directory, and this
# mod ships two of each. The launcher picks between them at install time from
# the variants block in launcher-manifest.json, so both have to be in the ZIP
# and both have to sit exactly where that manifest says.
$Il2CppBuildDir = 'src/WobblyLifeHeadTracking.Il2Cpp/bin/Release/net6.0'
$Il2CppPayload = [ordered]@{
    'plugins-il2cpp/WobblyLifeHeadTracking.dll' = "$Il2CppBuildDir/WobblyLifeHeadTracking.dll"
    'plugins-il2cpp/CameraUnlock.Core.dll'      = "$Il2CppBuildDir/CameraUnlock.Core.dll"
    'plugins-il2cpp/CameraUnlock.Core.Unity.dll' = "$Il2CppBuildDir/CameraUnlock.Core.Unity.dll"
    'vendor/bepinex-il2cpp/BepInEx_UnityIL2CPP_x64.zip' = 'vendor/bepinex-il2cpp/BepInEx_UnityIL2CPP_x64.zip'
    'vendor/bepinex-il2cpp/LICENSE'             = 'vendor/bepinex-il2cpp/LICENSE'
    'vendor/bepinex-il2cpp/README.md'           = 'vendor/bepinex-il2cpp/README.md'
}

foreach ($source in $Il2CppPayload.Values) {
    $path = Join-Path $projectDir $source
    if (-not (Test-Path $path)) {
        throw "IL2CPP payload file missing: $source. The Xbox Game Pass build of the game needs it, and launcher-manifest.json names it, so a ZIP without it fails to install on that copy. Run 'pixi run build' first."
    }
}

function Add-NoticesToZip {
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][hashtable]$Extra
    )

    $archive = [System.IO.Compression.ZipFile]::Open($ZipPath, 'Update')
    try {
        $existing = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        foreach ($entryName in $Extra.Keys) {
            if ($existing -contains $entryName) { continue }
            $sourcePath = Join-Path $projectDir $Extra[$entryName]
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive, $sourcePath, $entryName) | Out-Null
            Write-Host "  + $entryName" -ForegroundColor Green
        }
    } finally {
        $archive.Dispose()
    }
}

function Assert-NoticesInZip {
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][string[]]$Expected
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $names = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    } finally {
        $archive.Dispose()
    }

    $missing = @($Expected | Where-Object { $names -notcontains $_ })
    if ($missing.Count -gt 0) {
        throw "$(Split-Path -Leaf $ZipPath) is missing required notices: $($missing -join ', ')"
    }
    Write-Host "  $(Split-Path -Leaf $ZipPath): all $($Expected.Count) notice files present" -ForegroundColor Green
}

$zips = & "$projectDir/cameraunlock-core/scripts/package-bepinex-mod.ps1" `
    -ModName "WobblyLifeHeadTracking" `
    -CsprojPath "src/WobblyLifeHeadTracking/WobblyLifeHeadTracking.csproj" `
    -BuildOutputDir "src/WobblyLifeHeadTracking/bin/Release/net472" `
    -ModDlls @("WobblyLifeHeadTracking.dll","CameraUnlock.Core.dll","CameraUnlock.Core.Unity.dll") `
    -ProjectRoot $projectDir `
    -CreateNexusZip

Write-Host ""
Write-Host "=== Staging licence notices ===" -ForegroundColor Magenta
Write-Host ""

# The Nexus ZIP is a binary distribution and carries the same notice obligations
# as the installer ZIP. The shared packager builds it from the mod DLLs alone.
$nexusExtra = @{}
foreach ($k in $RequiredNotices.Keys) { $nexusExtra[$k] = $RequiredNotices[$k] }
$nexusExtra['README.md'] = 'README.md'

$installerExtra = @{}
foreach ($k in $RequiredNotices.Keys) {
    if ($k -like 'licenses/*') { $installerExtra[$k] = $RequiredNotices[$k] }
}

Write-Host "Installer ZIP:" -ForegroundColor Cyan
Add-NoticesToZip -ZipPath $zips.GithubZip -Extra $installerExtra

# Installer ZIP only. The Nexus ZIP is the Mono payload alone - a mod manager
# deploys into one fixed subtree and cannot reach a Game Pass install, so
# shipping the IL2CPP DLLs there would only give a Game Pass user a download
# that looks right and does nothing. The README sends them to the installer.
Write-Host "Installer ZIP (IL2CPP payload):" -ForegroundColor Cyan
$il2cppExtra = @{}
foreach ($k in $Il2CppPayload.Keys) { $il2cppExtra[$k] = $Il2CppPayload[$k] }
Add-NoticesToZip -ZipPath $zips.GithubZip -Extra $il2cppExtra

Write-Host "NexusMods ZIP:" -ForegroundColor Cyan
Add-NoticesToZip -ZipPath $zips.NexusZip -Extra $nexusExtra

Write-Host ""
Write-Host "Verifying..." -ForegroundColor Cyan
$expected = @($RequiredNotices.Keys)
Assert-NoticesInZip -ZipPath $zips.GithubZip -Expected $expected
Assert-NoticesInZip -ZipPath $zips.NexusZip -Expected $expected
Assert-NoticesInZip -ZipPath $zips.GithubZip -Expected @($Il2CppPayload.Keys)

& node (Join-Path $projectDir 'cameraunlock-core/scripts/validate-manifest.mjs') $zips.GithubZip
if ($LASTEXITCODE -ne 0) { throw 'Installer manifest validation failed.' }

Write-Host ""
Write-Host "=== Package Complete ===" -ForegroundColor Magenta
