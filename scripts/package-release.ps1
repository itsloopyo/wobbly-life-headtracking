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
}

foreach ($source in $RequiredNotices.Values) {
    $path = Join-Path $projectDir $source
    if (-not (Test-Path $path)) {
        throw "Required licence file missing: $source. Both release ZIPs redistribute binaries whose licences require this notice to accompany them; refusing to package without it."
    }
}

function Add-NoticesToZip {
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][hashtable]$Extra
    )

    $archive = [System.IO.Compression.ZipFile]::Open($ZipPath, 'Update')
    try {
        $existing = @($archive.Entries | ForEach-Object { $_.FullName })
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
        $names = @($archive.Entries | ForEach-Object { $_.FullName })
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

Write-Host "NexusMods ZIP:" -ForegroundColor Cyan
Add-NoticesToZip -ZipPath $zips.NexusZip -Extra $nexusExtra

Write-Host ""
Write-Host "Verifying..." -ForegroundColor Cyan
$expected = @($RequiredNotices.Keys)
Assert-NoticesInZip -ZipPath $zips.GithubZip -Expected $expected
Assert-NoticesInZip -ZipPath $zips.NexusZip -Expected $expected

Write-Host ""
Write-Host "=== Package Complete ===" -ForegroundColor Magenta
