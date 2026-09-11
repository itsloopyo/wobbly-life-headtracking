param([Parameter(Mandatory)][string]$PackagePath, [switch]$Interactive)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = Join-Path ([IO.Path]::GetTempPath()) ('wobbly-installer-' + [guid]::NewGuid().ToString('N'))
$package = Join-Path $root 'package'
[IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $PackagePath), $package)
$mono = Join-Path $root 'Mono! copy'
$il2cpp = Join-Path $root 'IL2CPP copy'
$alias = Join-Path $root 'Mono junction'
foreach ($path in @($mono, $il2cpp)) {
    New-Item -ItemType Directory -Path $path | Out-Null
    [IO.File]::WriteAllText((Join-Path $path 'WobblyFixture.exe'), 'fixture')
}
[IO.File]::WriteAllText((Join-Path $il2cpp 'GameAssembly.dll'), 'fixture')
New-Item -ItemType Junction -Path $alias -Target $mono | Out-Null
$fixtureId = 'wobbly-installer-fixture'
$gameData = @{
    schema_version = 1
    games = @{
        $fixtureId = @{
            display_name = 'Wobbly Installer Fixture'
            executable_relpath = 'WobblyFixture.exe'
            xbox_paths = @($mono, $il2cpp, $alias)
        }
    }
}
$encoding = [Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText((Join-Path $package 'shared/games.json'), ($gameData | ConvertTo-Json -Depth 10), $encoding)
foreach ($script in @('install.cmd', 'uninstall.cmd')) {
    $path = Join-Path $package $script
    $body = [IO.File]::ReadAllText($path).Replace('set "GAME_ID=wobbly-life"', ('set "GAME_ID=' + $fixtureId + '"'))
    [IO.File]::WriteAllText($path, $body, $encoding)
}
$install = Join-Path $package 'install.cmd'
$uninstall = Join-Path $package 'uninstall.cmd'

if ($Interactive) {
    & $install
} else {
    & $env:ComSpec /d /v:on /c ('""' + $install + '" /y"')
}
if ($LASTEXITCODE -ne 0) { throw 'Multi-install invocation failed.' }
foreach ($entry in @(@{ Path = $mono; Source = 'plugins'; Marker = 'BepInEx.dll' }, @{ Path = $il2cpp; Source = 'plugins-il2cpp'; Marker = 'BepInEx.Unity.IL2CPP.dll' })) {
    if (-not (Test-Path -LiteralPath (Join-Path $entry.Path ('BepInEx/core/' + $entry.Marker)))) {
        throw "Wrong loader in $($entry.Path)"
    }
    foreach ($dll in @('WobblyLifeHeadTracking.dll', 'CameraUnlock.Core.dll', 'CameraUnlock.Core.Unity.dll')) {
        $expected = (Get-FileHash -LiteralPath (Join-Path $package ($entry.Source + '/' + $dll))).Hash
        $actual = (Get-FileHash -LiteralPath (Join-Path $entry.Path ('BepInEx/plugins/' + $dll))).Hash
        if ($expected -ne $actual) { throw "Wrong $dll in $($entry.Path)" }
    }
    $configDir = Join-Path $entry.Path 'BepInEx/config'
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $configDir 'player.cfg'), 'preserve me')
}

& $env:ComSpec /d /v:on /c ('""' + $install + '" /y"')
if ($LASTEXITCODE -ne 0) { throw 'Reinstall failed.' }
foreach ($path in @($mono, $il2cpp)) {
    if ([IO.File]::ReadAllText((Join-Path $path 'BepInEx/config/player.cfg')) -ne 'preserve me') { throw 'Reinstall changed user config.' }
}

$il2cppPlugin = Join-Path $il2cpp 'BepInEx/plugins/WobblyLifeHeadTracking.dll'
[IO.File]::Delete($il2cppPlugin)
& $install $mono /y
if ($LASTEXITCODE -ne 0) { throw 'Explicit-path install failed.' }
if (Test-Path -LiteralPath $il2cppPlugin) { throw 'Explicit-path install touched a different copy.' }

[IO.File]::WriteAllText((Join-Path $mono 'GameAssembly.dll'), 'changed backend')
& $env:ComSpec /d /v:on /c ('""' + $install + '" /y"')
if ($LASTEXITCODE -ne 1) { throw 'A failed target must fail the overall install.' }
if (-not (Test-Path -LiteralPath $il2cppPlugin)) { throw 'The second target was skipped.' }
[IO.File]::Delete((Join-Path $mono 'GameAssembly.dll'))

New-Item -ItemType Directory -Path (Join-Path $mono 'dotnet') | Out-Null
[IO.File]::WriteAllText((Join-Path $mono 'dotnet/coreclr.dll'), 'game runtime')

foreach ($path in @($mono, $il2cpp)) {
    & $uninstall $path /y /force
    if ($LASTEXITCODE -ne 0) { throw "Uninstall failed for $path" }
    if (Test-Path -LiteralPath (Join-Path $path 'BepInEx/plugins/WobblyLifeHeadTracking.dll')) { throw 'Uninstall left the plugin.' }
    if ($path -eq $il2cpp -and (Test-Path -LiteralPath (Join-Path $path 'dotnet'))) { throw 'Uninstall left the private IL2CPP runtime.' }
    if (-not (Test-Path -LiteralPath (Join-Path $path 'WobblyFixture.exe'))) { throw 'Uninstall removed the game executable.' }
}
if ([IO.File]::ReadAllText((Join-Path $mono 'dotnet/coreclr.dll')) -ne 'game runtime') { throw 'Mono uninstall removed a game-owned runtime.' }
Write-Host "PASS: multi-install, backend selection, reinstall, config preservation, partial failure, uninstall. Fixtures: $root"
