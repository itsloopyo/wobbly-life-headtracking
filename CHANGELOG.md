# Changelog

## [Unreleased]

### Added

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
