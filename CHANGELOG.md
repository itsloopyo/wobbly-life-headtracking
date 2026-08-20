# Changelog

## [Unreleased]

### Changed

- Removed recentring from the mod. The `Home` / `Ctrl+Shift+T` hotkey and the
  `[Controls] RecenterKey` entry are gone and the tracker pose is applied as
  sent. Every tracker app centres itself, so a mod-side centre sat in series
  with the tracker's own and the two drifted apart. Centre in your tracker app
  instead: OpenTrack's Center bind, or the CENTER button in Headcam.
- replace `Smoothing.SmoothingFactor` and `Position.Smoothing` with `Smoothing.LocalSmoothing` (default 0.0) and `Smoothing.RemoteSmoothing` (default 0.15), selected per connection from the packet source address and covering both rotation and position
- remove the hidden 0.15 baseline smoothing floor, so a tracker running on this PC now gets zero-latency tracking by default
