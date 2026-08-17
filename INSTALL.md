# Wobbly Life Head Tracking - Installation Guide

## Requirements

- **Wobbly Life** (Steam)
- **Windows 10/11** (64-bit)
- **[OpenTrack](https://github.com/opentrack/opentrack)** or an OpenTrack-compatible head tracking app (smartphone, webcam, or dedicated hardware)

## Installation

### Automatic Installation (Recommended)

1. Extract all files from the zip to a folder
2. Double-click **install.cmd**
3. The installer will:
   - Find your Wobbly Life installation automatically
   - Install BepInEx if not present
   - Deploy mod DLLs to BepInEx/plugins

If the installer can't find your game, see [Custom Game Path](#custom-game-path) below.

### Manual Installation

1. Download [BepInEx 5.4.x win_x64](https://github.com/BepInEx/BepInEx/releases) and extract to your Wobbly Life folder:
   - Default: `C:\Program Files (x86)\Steam\steamapps\common\Wobbly Life`
2. Run Wobbly Life once to initialize BepInEx, then close the game
3. Copy these files to `BepInEx/plugins/`:
   - `WobblyLifeHeadTracking.dll`
   - `CameraUnlock.Core.dll`
   - `CameraUnlock.Core.Unity.dll`
   - `CameraUnlock.Core.Unity.BepInEx.dll`

### Custom Game Path

If your game is installed in a non-standard location:

1. Set the environment variable and run the installer:
   ```
   set WOBBLY_LIFE_PATH=D:\Games\Wobbly Life
   install.cmd
   ```
   Or pass the path directly: `install.cmd "D:\Games\Wobbly Life"`

## Setting Up OpenTrack

1. Download and install [OpenTrack](https://github.com/opentrack/opentrack/releases)
2. Configure your tracker (Input):
   - For webcam: Select "neuralnet tracker"
   - For phone app: Select "UDP over network"
3. Configure output:
   - Select **UDP over network**
   - Host: `127.0.0.1`
   - Port: `4242`
4. Click **Start** to begin tracking
5. Launch Wobbly Life

### Phone App Setup

This mod includes built-in smoothing to handle network jitter, so if your tracking app already provides a filtered signal, you can send directly from your phone to the mod on port 4242 without needing OpenTrack on PC.

1. Install an OpenTrack-compatible head tracking app from your phone's app store
2. Configure your phone app to send to your PC's IP address on port 4242 (run `ipconfig` to find it, e.g. `192.168.1.100`)
3. Set the protocol to OpenTrack/UDP
4. Start tracking

**With OpenTrack (optional):** If you want curve mapping or visual preview, route through OpenTrack by setting its Input to "UDP over network" and Output to port 4242.

### Multiplayer Ports

For couch co-op, each player needs their own tracker on a separate port. Ports are configurable in the config file:

| Player | Default UDP Port | Config Entry |
|--------|-----------------|--------------|
| Player 1 | 4242 | `Player1Port` |
| Player 2 | 4243 | `Player2Port` |
| Player 3 | 4244 | `Player3Port` |
| Player 4 | 4245 | `Player4Port` |

## Controls

| Action | Nav cluster | Chord |
|--------|-------------|-------|
| Recenter view | **Home** | **Ctrl+Shift+T** |
| Toggle head tracking | **End** | **Ctrl+Shift+Y** |
| Cycle tracking mode | **Page Up** | **Ctrl+Shift+G** |
| Toggle yaw mode | **Page Down** | **Ctrl+Shift+H** |

## Verifying Installation

1. Start OpenTrack and enable tracking
2. Launch Wobbly Life
3. Once in-game, move your head - the camera should follow
4. Press **Home** to recenter if needed

Check `BepInEx/LogOutput.log` for status messages.

## Configuration

After first run, a config file is created at:
`BepInEx/config/com.cameraunlock.wobblylife.headtracking.cfg`

```ini
[Network]
Player1Port = 4242
Player2Port = 4243
Player3Port = 4244
Player4Port = 4245

[Sensitivity]
YawSensitivity = 1.0
PitchSensitivity = 1.0
RollSensitivity = 1.0

[Smoothing]
LocalSmoothing = 0.0
RemoteSmoothing = 0.15

[General]
WorldSpaceYaw = true

[Controls]
EnableOnStartup = true
ToggleKey = End
RecenterKey = Home
PositionToggleKey = PageUp
YawModeKey = PageDown

[Position]
SensitivityX = 1.0
SensitivityY = 1.0
SensitivityZ = 1.0
LimitX = 0.30
LimitY = 0.15
LimitYDown = 0.05
LimitZ = 0.40

[GameState]
DisableInMenus = true
DisableWhenPaused = true
```

`LocalSmoothing` applies when the tracker runs on this machine, `RemoteSmoothing`
when it is a phone or other network device. Both cover rotation and position, and
the value is picked per connection from the packet source address.

## Troubleshooting

### Mod not loading

- Check that `winhttp.dll` and `doorstop_config.ini` exist in the game folder
- Verify the DLL is in `BepInEx/plugins/`
- Check `BepInEx/LogOutput.log` for errors

### Camera not responding

1. Verify OpenTrack is running and tracking is active
2. Check UDP output is set to `127.0.0.1:4242`
3. Press **End** to make sure tracking is enabled
4. Press **Home** to recenter
5. Check Windows Firewall isn't blocking UDP on port 4242

### Camera jittering

1. Increase `RemoteSmoothing` (phone/network tracker) or `LocalSmoothing` (tracker on this PC) in config (try 0.5-0.7)
2. Enable Accela filter in OpenTrack
3. Improve lighting for webcam-based tracking

### Wrong movement direction

- In OpenTrack, adjust the curves or invert axes under Options > Mapping

## Uninstalling

### Automatic

Double-click **uninstall.cmd**

### Manual

#### Remove Mod Only (Keep BepInEx)

Delete from `BepInEx/plugins/`:
- `WobblyLifeHeadTracking.dll`
- `CameraUnlock.Core.dll`
- `CameraUnlock.Core.Unity.dll`
- `CameraUnlock.Core.Unity.BepInEx.dll`

#### Complete Removal

Delete these from the game folder:
- `BepInEx/` folder
- `winhttp.dll`
- `doorstop_config.ini`
