# Changelog

## [Unreleased]

### Changed

- replace `Smoothing.SmoothingFactor` and `Position.Smoothing` with `Smoothing.LocalSmoothing` (default 0.0) and `Smoothing.RemoteSmoothing` (default 0.15), selected per connection from the packet source address and covering both rotation and position
- remove the hidden 0.15 baseline smoothing floor, so a tracker running on this PC now gets zero-latency tracking by default
