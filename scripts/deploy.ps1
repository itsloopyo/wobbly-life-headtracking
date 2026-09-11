#!/usr/bin/env pwsh
# Deploy the built mod to every Wobbly Life installation on this machine.
# Usage: deploy.ps1 [Configuration]
# Example: deploy.ps1 Release
#
# The game ships two different builds and they need different payloads. The
# Steam copy is Mono and takes the BepInEx 5 plugin; the Xbox Game Pass copy is
# IL2CPP and takes the BepInEx 6 one. Which is which is decided per install by
# looking for GameAssembly.dll, not by which store the path came from: the
# scripting backend is what the loader has to match, and a store could change
# backend in a patch without the path moving.

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

$corePs = Join-Path $projectRoot "cameraunlock-core\powershell"
if (-not (Test-Path (Join-Path $corePs "GamePathDetection.psm1"))) {
    Write-Host "ERROR: CameraUnlock.Core PowerShell modules not found at: $corePs" -ForegroundColor Red
    Write-Host "Run 'git submodule update --init --recursive' to fetch them." -ForegroundColor Yellow
    exit 1
}

Import-Module (Join-Path $corePs "GamePathDetection.psm1")
Import-Module (Join-Path $corePs "DevDeploy.psm1")

$gameId = 'wobbly-life'
$gameName = 'Wobbly Life'
$StateFileName = ".headtracking-state.json"

$ModDll = "WobblyLifeHeadTracking.dll"
$ExtraDlls = @("CameraUnlock.Core.dll", "CameraUnlock.Core.Unity.dll")

# One entry per scripting backend: the vendored loader, the build output, and the
# loader files whose absence means a repair is due.
$Payloads = @{
    Mono = @{
        Label        = 'Mono / BepInEx 5'
        MajorVersion = 5
        VendorDir    = Join-Path $projectRoot "vendor\bepinex"
        VendorZip    = Join-Path $projectRoot "vendor\bepinex\BepInEx_win_x64.zip"
        BuildDir     = Join-Path $projectRoot "src\WobblyLifeHeadTracking\bin\$Configuration\net472"
        # Every file BepInEx needs to actually start. Checking one marker (or just
        # that BepInEx\core\ exists) is not enough: a half-deleted loader keeps
        # BepInEx.dll while doorstop_config.ini and the Cecil/MonoMod assemblies go
        # missing, so Doorstop loads with no target, the chainloader never runs, and
        # a marker-only check reports the loader as installed and skips the repair.
        LoaderFiles  = @(
            "doorstop_config.ini"
            "winhttp.dll"
            "BepInEx\core\BepInEx.dll"
            "BepInEx\core\BepInEx.Preloader.dll"
            "BepInEx\core\0Harmony.dll"
            "BepInEx\core\Mono.Cecil.dll"
            "BepInEx\core\MonoMod.Utils.dll"
            "BepInEx\core\MonoMod.RuntimeDetour.dll"
        )
    }
    Il2Cpp = @{
        Label        = 'IL2CPP / BepInEx 6'
        MajorVersion = 6
        VendorDir    = Join-Path $projectRoot "vendor\bepinex-il2cpp"
        VendorZip    = Join-Path $projectRoot "vendor\bepinex-il2cpp\BepInEx_UnityIL2CPP_x64.zip"
        BuildDir     = Join-Path $projectRoot "src\WobblyLifeHeadTracking.Il2Cpp\bin\$Configuration\net6.0"
        # BepInEx 6 replaces the Mono preloader with a .NET host in dotnet\, so an
        # install missing that is as dead as a 5 install missing Cecil.
        LoaderFiles  = @(
            "doorstop_config.ini"
            "winhttp.dll"
            "BepInEx\core\BepInEx.Core.dll"
            "BepInEx\core\BepInEx.Unity.IL2CPP.dll"
            "BepInEx\core\Il2CppInterop.Runtime.dll"
            "BepInEx\core\0Harmony.dll"
            "dotnet\coreclr.dll"
            "dotnet\System.Private.CoreLib.dll"
        )
    }
}

function Get-VendoredLoaderVersion {
    param([Parameter(Mandatory)][string]$VendorDir)

    $readme = Get-Content (Join-Path $VendorDir "README.md") -Raw
    $match = [regex]::Match($readme, '(?m)^- Tag: `v?([^`]+)`')
    if (-not $match.Success) {
        throw "Could not read the BepInEx tag from $VendorDir\README.md"
    }
    return $match.Groups[1].Value
}

# Mono keeps its managed assemblies in <Game>_Data\Managed; IL2CPP compiles them
# into GameAssembly.dll and ships il2cpp_data instead. Either marker alone is
# enough, and they are never both present.
function Get-ScriptingBackend {
    param([Parameter(Mandatory)][string]$GamePath)

    if (Test-Path -LiteralPath (Join-Path $GamePath "GameAssembly.dll")) { return 'Il2Cpp' }
    return 'Mono'
}

# Find-AllGamePaths rather than Find-GamePath: owning the game on two stores is
# ordinary here, and a deploy that picks one silently leaves the other running
# whatever build was last dropped into it.
$targets = @(Find-AllGamePaths -GameId $gameId)
if ($targets.Count -eq 0) {
    $config = Get-GameConfig -GameId $gameId
    Write-GameNotFoundError -GameName $gameName -EnvVar $config.EnvVar -SteamFolder $config.SteamFolder
    exit 1
}

Write-Host "Found $($targets.Count) installation$(if ($targets.Count -ne 1) { 's' }) of ${gameName}:" -ForegroundColor Cyan
$targets | ForEach-Object { Write-Host "  $_" -ForegroundColor Cyan }

$report = [System.Collections.Generic.List[object]]::new()

foreach ($gamePath in $targets) {
    $backend = Get-ScriptingBackend -GamePath $gamePath
    $payload = $Payloads[$backend]

    Write-Host ""
    Write-Host "--- $gamePath" -ForegroundColor Cyan
    Write-Host "    $($payload.Label)" -ForegroundColor Cyan

    if (-not (Test-Path $payload.VendorZip)) {
        throw "Vendored BepInEx not found at $($payload.VendorZip). Run 'pixi run update-deps'."
    }
    if (-not (Test-Path $payload.BuildDir)) {
        throw "Build output not found at $($payload.BuildDir). Run 'pixi run build' first."
    }

    $loaderVersion = Get-VendoredLoaderVersion -VendorDir $payload.VendorDir

    $running = @(Get-Process -Name 'Wobbly Life', 'Wobbly Life_EOS' -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) { throw 'Close Wobbly Life before deploying.' }
    $wrongMarkers = if ($backend -eq 'Il2Cpp') {
        @('BepInEx/core/BepInEx.dll', 'BepInEx/core/BepInEx.Unity.Mono.dll')
    } else {
        @('BepInEx/core/BepInEx.Core.dll', 'BepInEx/core/BepInEx.Unity.IL2CPP.dll')
    }
    foreach ($marker in $wrongMarkers) {
        if (Test-Path -LiteralPath (Join-Path $gamePath $marker)) {
            throw "Incompatible loader at $gamePath. Remove it with its installer, preserving plugins and config, then retry."
        }
    }
    $stateFile = Join-Path $gamePath $StateFileName
    $previousOwnership = $false
    if (Test-Path -LiteralPath $stateFile) {
        $existingState = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
        $previousOwnership = $existingState.framework.installed_by_us
    }

    # Repair before deploying. Invoke-DevDeployBepInExToPath installs the loader
    # when BepInEx is absent entirely, but an incomplete one reads as present -
    # see LoaderFiles above.
    $missing = @($payload.LoaderFiles | Where-Object { -not (Test-Path (Join-Path $gamePath $_)) })
    $loaderAbsent = -not (Test-Path (Join-Path $gamePath "BepInEx\core"))

    if ($missing.Count -gt 0 -and -not $loaderAbsent) {
        Write-Host "    BepInEx is installed but incomplete - repairing from the vendored copy." -ForegroundColor Yellow
        foreach ($f in $missing) { Write-Host "      missing: $f" -ForegroundColor Gray }

        # The vendored zips carry no BepInEx\config or BepInEx\plugins entries, so
        # extracting over an existing install restores the loader without touching
        # user config or other plugins.
        Expand-Archive -Path $payload.VendorZip -DestinationPath $gamePath -Force

        $stillMissing = @($payload.LoaderFiles | Where-Object { -not (Test-Path (Join-Path $gamePath $_)) })
        if ($stillMissing.Count -gt 0) {
            foreach ($f in $stillMissing) { Write-Host "      still missing: $f" -ForegroundColor Red }
            throw "BepInEx is still incomplete after extracting $($payload.VendorZip)"
        }
    }

    # -GivenPath pins this call to one install; the loop above owns enumeration.
    $result = Invoke-DevDeployBepInEx `
        -GameId $gameId `
        -GameDisplayName $gameName `
        -GivenPath $gamePath `
        -BuildOutputPath $payload.BuildDir `
        -ModDllName $ModDll `
        -ExtraDlls $ExtraDlls `
        -EnsureLoader `
        -MajorVersion $payload.MajorVersion `
        -VendorZip $payload.VendorZip

    # Read the mod version from whichever project produced this payload.
    $csproj = if ($backend -eq 'Il2Cpp') {
        Join-Path $projectRoot "src\WobblyLifeHeadTracking.Il2Cpp\WobblyLifeHeadTracking.Il2Cpp.csproj"
    } else {
        Join-Path $projectRoot "src\WobblyLifeHeadTracking\WobblyLifeHeadTracking.csproj"
    }
    $versionMatch = [regex]::Match((Get-Content $csproj -Raw), '<Version>([^<]+)</Version>')
    $modVersion = if ($versionMatch.Success) { $versionMatch.Groups[1].Value } else { "unknown" }

    # Ownership of the loader only transfers when nothing was there to begin with,
    # and once claimed it stays claimed across redeploys.
    $stateFile = Join-Path $gamePath $StateFileName
    $frameworkInstalledByUs = $loaderAbsent -or $previousOwnership

    @{
        framework = @{
            installed_by_us = $frameworkInstalledByUs
            type            = "BepInEx"
            version         = $loaderVersion
            architecture    = "x64"
        }
        mod = @{
            name        = "WobblyLifeHeadTracking"
            version     = $modVersion
            deployed_at = (Get-Date).ToString("o")
        }
    } | ConvertTo-Json -Depth 4 | Set-Content $stateFile -Encoding UTF8

    $report.Add([pscustomobject]@{
        Path    = $gamePath
        Backend = $payload.Label
        Loader  = "BepInEx $loaderVersion"
        Plugins = $result.PluginsPath
    })
}

Write-Host ""
Write-Host "[OK] Deployed to $($report.Count) installation$(if ($report.Count -ne 1) { 's' })" -ForegroundColor Green
foreach ($entry in $report) {
    Write-Host "  $($entry.Path)" -ForegroundColor Cyan
    Write-Host "    $($entry.Backend), $($entry.Loader)" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Launch Wobbly Life to test your changes." -ForegroundColor Yellow
Write-Host "  - End / Ctrl+Shift+Y to toggle tracking on/off" -ForegroundColor Gray
