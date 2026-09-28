# Changelog

## [Unreleased]

### Added

- Support for the Xbox Game Pass / Microsoft Store copy of the game. It is a
  different build from the Steam one - IL2CPP rather than Mono - so it needs a
  different mod binary and a different version of BepInEx. One download covers
  both: `install.cmd` and the Lopari launcher each work out which build they are
  installing into and deploy the matching pair. Own the game on both stores and
  the installer does both.
- The mod DLLs for the Game Pass build ship in `plugins-il2cpp/` and its loader
  in `vendor/bepinex-il2cpp/`, alongside the existing Steam payload. The Nexus
  archive remains the Mono payload only. Use the installer ZIP for Game Pass.
- Ship the licence text of every third-party binary the release ZIPs carry, as
  `licenses/` in both the installer and Nexus ZIPs and reproduced verbatim in
  THIRD-PARTY-NOTICES.md. Previously the ZIPs deployed `CameraUnlock.Core.dll`
  with no notice for its copyright holder, and bundled BepInEx's archive, which
  also contains HarmonyX, Mono.Cecil and MonoMod, with only BepInEx's own LGPL
  text beside it.

### Changed

- Packaging now builds the Nexus ZIP that `release.yml` expects, and refuses to
  produce either ZIP when a required licence file is missing rather than warning
  and carrying on.
- Credit HarmonyX rather than Harmony for the `0Harmony.dll` that BepInEx ships,
  and credit MonoMod and Mono.Cecil alongside it.
- Removed recentring from the mod. The `Home` / `Ctrl+Shift+T` hotkey and the
  `[Controls] RecenterKey` entry are gone and the tracker pose is applied as
  sent. Every tracker app centres itself, so a mod-side centre sat in series
  with the tracker's own and the two drifted apart. Centre in your tracker app
  instead: OpenTrack's Center bind, or the CENTER button in Headcam.
- replace `Smoothing.SmoothingFactor` and `Position.Smoothing` with `Smoothing.LocalSmoothing` (default 0.0) and `Smoothing.RemoteSmoothing` (default 0.15), selected per connection from the packet source address and covering both rotation and position
- remove the hidden 0.15 baseline smoothing floor, so a tracker running on this PC now gets zero-latency tracking by default
- Settings move to `BepInEx\config\CameraUnlock.ini`. Earlier versions of the mod kept these settings in `com.cameraunlock.wobblylife.headtracking.cfg`, in the same folder. The first time this version starts and finds no `CameraUnlock.ini`, it reads your settings from `com.cameraunlock.wobblylife.headtracking.cfg` and writes them into `CameraUnlock.ini`. It never changes `com.cameraunlock.wobblylife.headtracking.cfg`, and does not read it again while `CameraUnlock.ini` exists.
- A setting that the defaults the README shows set to `default` is written as `default` when you never changed it from the default earlier versions used, because `com.cameraunlock.wobblylife.headtracking.cfg` does not hold it or holds that default. It then follows `Defaults.ini`, so it takes the value `Defaults.ini` gives it, or the built-in value where `Defaults.ini` gives none, which can differ from the default earlier versions used. A setting you changed is written with the value imported for it, or as `default` where that value equals its default at that start.
- `RotationEnabled` and `PositionEnabled` are one setting here, the tracking mode, so both are written as `default` or neither is.
- The built-in vertical lean limits are the fleet's: `PositionLimitY=0.2` and `PositionLimitYDown=0.2`, where earlier versions used 0.15 up and 0.05 down. A limit you never changed follows `Defaults.ini`, so with the built-in values you can raise and lower your head further than before. Set `PositionLimitY=0.15` and `PositionLimitYDown=0.05` in `CameraUnlock.ini` to keep the old range for this game.
- Settings are renamed to the fleet's names: `[Network] Player1Port` is `UdpPort`, `LimitX`, `LimitY`, `LimitYDown` and `LimitZ` are `PositionLimitX`, `PositionLimitY`, `PositionLimitYDown` and `PositionLimitZ`, `[Controls] EnableOnStartup` is under `[General]`, and the hotkeys are under `[Hotkeys]`, with `PositionToggleKey` now `CycleTrackingModeKey`. `Player2Port` to `Player4Port` stay under `[Network]` and `DisableInMenus` and `DisableWhenPaused` under `[GameState]`. `PositionLimitZBack` is new and sets how far leaning back can move the view, 0.1 by default as before.
- The tracking mode (rotation and position, rotation only, position only) and the yaw mode are saved to `CameraUnlock.ini` the moment a hotkey changes them, and the next start begins in the saved modes. Turning head tracking on or off with End is not saved: the mod starts with head tracking on or off as `EnableOnStartup` says.
- A change made to the settings file while the game runs takes effect at the next start. Earlier versions applied an edit to the `.cfg`, or a change in ConfigurationManager, while the game ran.
- Comments, and keys the mod never read, are not carried over. Nor are these, where your old file had them:
  - A sensitivity (`[Sensitivity] YawSensitivity`, `PitchSensitivity`, `RollSensitivity`, `[Position] SensitivityX`, `SensitivityY`, `SensitivityZ`) you changed from its default. Set these in your tracker instead.
  - A hotkey set to Ctrl, Shift or Alt on its own. That key goes down before the key of any chord made with it, so the hotkey is left unbound, and it keeps its Ctrl+Shift chord.
  - A hotkey set to a number that is not a key code Unity names. The hotkey is left unbound, the log says so, and it keeps its Ctrl+Shift chord.
- An older version of the mod reads `com.cameraunlock.wobblylife.headtracking.cfg` and never reads `CameraUnlock.ini`, so a setting you change after updating is not in `com.cameraunlock.wobblylife.headtracking.cfg`.
- Deleting only `CameraUnlock.ini` makes the next start read `com.cameraunlock.wobblylife.headtracking.cfg` again. To go back to the defaults, replace everything in `CameraUnlock.ini` with the defaults the README shows. Every setting they set to `default` then follows `Defaults.ini`.
- BepInEx's ConfigurationManager no longer lists these settings. Edit `BepInEx\config\CameraUnlock.ini` with any text editor.
- Hotkeys are written as key names, and each hotkey lists every key that triggers it, the Ctrl+Shift chord included: `ToggleKey=End, Ctrl+Shift+Y`. The chords can now be rebound or removed like any other key.
- A hotkey bound to a plain key no longer fires while Ctrl and Shift are both held, so Ctrl+Shift with that key reaches only a binding that names the chord.
- On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini`, reads your settings from `com.cameraunlock.wobblylife.headtracking.cfg` again at every start while there is no `CameraUnlock.ini`, and a change made in game lasts until the game closes.
- `uninstall.cmd` leaves `BepInEx\config\CameraUnlock.ini` and `com.cameraunlock.wobblylife.headtracking.cfg` in place.

### Added

- A setting set to `default` in `CameraUnlock.ini` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it, and neither do earlier versions of this mod. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.
- `Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.
- When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that.

### Removed

- The sensitivity settings. Set these in your tracker app instead.
- With these settings at their shipped defaults the camera moves as it did before.
