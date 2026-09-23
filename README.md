# Wobbly Life Head Tracking

![Wobbly Life running with this mod](https://raw.githubusercontent.com/itsloopyo/wobbly-life-headtracking/main/assets/readme-clip.gif)

An unofficial head tracking mod for Wobbly Life that moves the camera with your head while your mouse or controller keeps aiming, driven by OpenTrack over UDP, with no VR headset required.

> [!CAUTION]
> ## Experimental prototype - expect missing core features
>
> This is **not** a finished mod.
>
> Current builds may only test whether head tracking can drive the camera. Bug fixes and core features like decoupled look/aim, independent reticle behavior, correct shot direction, off-screen reticle support, movement handling, and comfort tuning may be missing at this early stage of development.

## Features

- **Decoupled look and aim** - head tracking moves the camera; aim stays on your mouse/controller
- **6DOF positional tracking** - lean and peek with head position
- **Works with any OpenTrack compatible tracker** - free options available for PC, iOS and Android
- **4-player couch co-op** - each player runs their own tracker on a separate UDP port

## Requirements

- Wobbly Life on [Steam](https://store.steampowered.com/app/1211020/Wobbly_Life/) or [Xbox Game Pass](https://www.xbox.com/games/store/wobbly-life/9ns86bq33spx)
- [OpenTrack](https://github.com/opentrack/opentrack) or a compatible head tracking app (smartphone, webcam, or dedicated hardware)
- Windows

The two stores ship different builds of the game - the Steam one runs Mono and
the Xbox Game Pass one runs IL2CPP - so they need different versions of the mod and
different versions of BepInEx. One download covers both; the installer and the
Lopari launcher each work out which one your copy is and deploy the matching
pair. Own it on both and the installer does both.

## Installation

### Lopari

Once this mod is available in Lopari, download [Lopari](https://lopari.app), choose **Wobbly Life**, and click
**Play with head tracking**.

### Standalone Installer

1. Download the latest release from the [Releases page](https://github.com/itsloopyo/wobbly-life-headtracking/releases)
2. Extract the ZIP anywhere
3. Double-click `install.cmd`
4. Configure OpenTrack to output UDP to `127.0.0.1:4242`
5. Launch Wobbly Life

The installer finds the game itself: the Steam library, and the Xbox Game Pass
install under `XboxGames\Wobbly Life\Content`. If it can't find the game:
- Set the `WOBBLY_LIFE_PATH` environment variable to your game folder, or
- Run from command prompt: `install.cmd "D:\Games\Wobbly Life"`

On Xbox Game Pass, the first launch needs an internet connection and takes longer
while BepInEx 6 downloads Unity reference libraries and generates interop
assemblies. These are cached in the game folder. Check `BepInEx/LogOutput.log`
for progress or errors.

### Manual Installation

Everything below is already in the release ZIP - this is what `install.cmd`
does, if you would rather do it by hand. Which half applies depends on your
copy: look in the game folder for `GameAssembly.dll`.

**Steam (no `GameAssembly.dll`):**

1. Extract `vendor/bepinex/BepInEx_win_x64.zip` into your Wobbly Life folder
2. Run Wobbly Life once to initialise BepInEx, then close the game
3. Copy the three DLLs from `plugins/` into `BepInEx/plugins/`:
   - `WobblyLifeHeadTracking.dll`
   - `CameraUnlock.Core.dll`
   - `CameraUnlock.Core.Unity.dll`

**Xbox Game Pass (`GameAssembly.dll` present):**

1. Extract `vendor/bepinex-il2cpp/BepInEx_UnityIL2CPP_x64.zip` into
   `XboxGames\Wobbly Life\Content`
2. Run Wobbly Life once to initialise BepInEx, then close the game
3. Copy the three DLLs from `plugins-il2cpp/` into `BepInEx/plugins/`

The two `plugins/` folders hold different builds of the same three files. Mixing
them does nothing visible: the game starts, the mod never loads, and
`BepInEx/LogOutput.log` is the only place that says so.

The Nexus archive contains the Mono payload only. For the Xbox Game Pass build,
use the installer ZIP or the manual steps above.

## Setting Up OpenTrack

The mod listens for OpenTrack pose data on UDP port `4242`, on every network
interface. One datagram is six little-endian 64-bit floats in the order
`x, y, z, yaw, pitch, roll`: position in centimetres, rotation in degrees, 48
bytes in total. Anything that sends that to that port drives the view.
OpenTrack's **UDP over network** output sends exactly this, and the steps below
set it up.

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Pick a tracker under **Input**, using the notes below.
3. Set **Output** to **UDP over network**, host `127.0.0.1`, port `4242`.
4. Press **Start**. Tracking and the game can start in either order.

### Webcam

OpenTrack ships a `neuralnet tracker` input that reads a plain webcam. Select it
under **Input**, pick your camera in its settings, and use the output settings
above. How well it tracks depends on your camera and your lighting, so try it
before buying anything.

### Phone

A phone app can reach the mod directly, with no OpenTrack on the PC, if it sends
the datagram described above. Point it at this PC's IP address (run `ipconfig`
to find it) on port `4242`. Not every phone tracker speaks this protocol, so
check yours for an OpenTrack or UDP output option first. [Headcam](https://headcam.app)
sends it, and I wrote it so decent tracking is free for anyone who already owns
a phone.

Sending direct works when the app filters its own signal on the device. The
mod's smoothing is sized to take the edge off a clean signal rather than to
rescue a noisy one, so a raw feed sent direct will jitter. If it does, point the
app at OpenTrack's **UDP over network** *input* on some other port, say 5252,
and let OpenTrack's filters and curves clean it up before its output forwards to
`127.0.0.1:4242`.

Anything arriving from outside `127.0.0.0/8` counts as a remote connection and
is smoothed with `RemoteSmoothing` rather than `LocalSmoothing`. That includes a
tracker on this very PC that sends to the machine's own LAN address, because the
mod reads the source address and not the machine.

### Headset or other hardware

If your device has an OpenTrack input driver, select it under **Input** and use
the same output settings. OpenTrack's own **Input** list is the authority on
what it can read; the mod only ever sees what OpenTrack sends.

### Centring

Centring belongs to your tracker. The mod subtracts no centre of its own: it
applies the pose it receives exactly as it arrives, so a stream of zeros holds
the view where the game itself puts it. Press the centre control in your tracker
(OpenTrack's **Center** bind, or the CENTER button in Headcam) and the tracker
zeroes its own output, which leaves the view centred with the mod doing nothing.

That is why there is no centre hotkey here and nothing to re-centre in game. Two
centres in series would drift apart, because each side re-centres at moments the
other cannot see, and you would end up pressing twice to centre once. If the
view sits off to one side, centre it in the tracker.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action              | Nav-cluster | Chord           |
|---------------------|-------------|-----------------|
| Toggle tracking     | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode     | `Page Down` | `Ctrl+Shift+H`  |

There is no recenter key. The mod applies the pose your tracker sends as-is, so centre it in the tracker app: OpenTrack's Center bind, or the CENTER button in Headcam.

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay
2. Positional tracking disabled, rotational tracking enabled
3. Rotational tracking disabled, positional tracking enabled
4. Back to normal

The nav-cluster keys are configurable in the config file; the chord alternatives are fixed and useful on keyboards without a nav cluster (e.g. tenkeyless / laptops).

## Configuration

The mod creates a config file at `BepInEx/config/com.cameraunlock.wobblylife.headtracking.cfg` on first run. Edit it to customize:

A comment has to sit on its own line. BepInEx splits each line at the first `=`
and takes everything after it as the value, so a trailing `# note` becomes part
of the value, the conversion fails, and the entry silently keeps its default -
the only trace is a line in `BepInEx/LogOutput.log`. Put explanations above the
key, never after it.

```ini
[Network]
# UDP port for Player 1 (1024-65535)
Player1Port = 4242
# UDP port for Player 2
Player2Port = 4243
# UDP port for Player 3
Player3Port = 4244
# UDP port for Player 4
Player4Port = 4245

[Sensitivity]
# Horizontal rotation (0.0-3.0)
YawSensitivity = 1.0
# Vertical rotation (0.0-3.0)
PitchSensitivity = 1.0
# Head tilt (0.0-3.0)
RollSensitivity = 1.0

[Smoothing]
# Smoothing when the tracker runs on this machine (0.0-1.0)
LocalSmoothing = 0.0
# Smoothing when the tracker is a remote network device (0.0-1.0)
RemoteSmoothing = 0.15

[General]
# true = horizon-locked yaw (default); false = camera-local
WorldSpaceYaw = true

[Controls]
EnableOnStartup = true
ToggleKey = End
PositionToggleKey = PageUp
# Toggle world-locked vs camera-local yaw
YawModeKey = PageDown

[Position]
# Lateral sensitivity (0.0-5.0)
SensitivityX = 1.0
# Vertical sensitivity (0.0-5.0)
SensitivityY = 1.0
# Depth sensitivity (0.0-5.0)
SensitivityZ = 1.0
# Max lateral offset in meters
LimitX = 0.30
# Max upward offset in meters
LimitY = 0.15
# Max downward offset in meters
LimitYDown = 0.05
# Max depth offset in meters
LimitZ = 0.40

[GameState]
DisableInMenus = true
DisableWhenPaused = true
```

Smoothing covers both rotation and position. Which of the two values applies is
decided per connection from the packet source address: a tracker running on this
PC uses `LocalSmoothing`, a phone or other network device uses `RemoteSmoothing`.
Each player is judged independently, and switching takes effect without
restarting the game.

## Troubleshooting

**Mod not loading:**
- Ensure BepInEx is installed (the installer handles this automatically)
- Check that `winhttp.dll` exists in the game folder (installed by BepInEx)
- Check `BepInEx/LogOutput.log` for errors
- Look for "Wobbly Life Head Tracking" in the log

**Nothing at all in `BepInEx/LogOutput.log`, or no log file:**
- The loader and the game disagree about which build this is. `GameAssembly.dll`
  in the game folder means the IL2CPP build, which needs BepInEx 6
  (`BepInEx/core/BepInEx.Unity.IL2CPP.dll` and a `dotnet` folder beside the
  game); without it, BepInEx 5 (`BepInEx/core/BepInEx.Preloader.dll`). Remove an
  incompatible loader with its installer, preserving your plugins and config,
  then run `install.cmd` again.
- If you own the game on both stores, check you modded the copy you are
  launching. The Xbox app and Steam each start their own.

**Xbox Game Pass: the game hangs on a black screen the first time:**
- BepInEx 6 generates interop assemblies and downloads Unity libraries on the
  first launch. Check `BepInEx/LogOutput.log` for progress or errors.

**No tracking response:**
- Verify OpenTrack is running and outputting data
- Check UDP port matches (default 4242)
- Press **End** to enable tracking; if the view is off-centre, centre it in your tracker app
- Check firewall isn't blocking UDP port 4242

**A config edit had no effect:**
- Make sure nothing follows the value on the line. A trailing `# comment` is read as part of the value, the entry falls back to its default, and the game gives no sign of it. `BepInEx/LogOutput.log` records the failed conversion.

**Camera jittering:**
- Increase `RemoteSmoothing` (phone/network tracker) or `LocalSmoothing` (tracker on this PC) in config (try 0.3-0.5), with nothing after the value on the line
- Improve lighting for webcam tracking
- Reduce sensitivity in your tracking software

**Yaw feels wrong when looking up or down at extreme angles:**
- Try toggling between world-locked and camera-local yaw with `Page Down`. World-locked (default) is horizon-stable; camera-local follows the camera's current up-axis.

**Wrong rotation axes:**
- Check your tracker's axis mapping in OpenTrack
- Use OpenTrack's "Options" > "Mapping" to swap or invert axes

## Updating

Download the new release and run `install.cmd` again.

## Uninstalling

Run `uninstall.cmd` from the release folder. This removes the mod DLLs. BepInEx is only removed if it was originally installed by this mod. To force-remove BepInEx:

```
uninstall.cmd /force
```

## Building from Source

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (any recent version)
- [pixi](https://pixi.sh) task runner
- Wobbly Life installed (for Unity/BepInEx DLL references)

### Build

```bash
git clone --recurse-submodules https://github.com/itsloopyo/wobbly-life-headtracking.git
cd wobbly-life-headtracking

# Build and install to game
pixi run install

# Build only
pixi run build

# Package for release
pixi run package
```

### Available Tasks

| Task | Description |
|------|-------------|
| `pixi run build` | Build the mod (Release configuration) |
| `pixi run install` | Build and install to game directory |
| `pixi run uninstall` | Remove the mod from the game |
| `pixi run uninstall -- --force` | Remove the mod and BepInEx |
| `pixi run package` | Create release ZIP |
| `pixi run clean` | Clean build artifacts |
| `pixi run release` | Version bump, build, tag, and push |

## Community & Support

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

## License

MIT - see [LICENSE](LICENSE). Copyright (c) 2026 itsloopyo.

## Credits

- [RubberBandGames](https://rubberbandgames.itch.io/) - Wobbly Life
- [BepInEx](https://github.com/BepInEx/BepInEx) - Unity modding framework
- [OpenTrack](https://github.com/opentrack/opentrack) - Head tracking software
- [HarmonyX](https://github.com/BepInEx/HarmonyX) - Runtime patching library shipped inside BepInEx, a fork of [Harmony](https://github.com/pardeike/Harmony) by Andreas Pardeike
- [MonoMod](https://github.com/MonoMod/MonoMod) and [Mono.Cecil](https://github.com/jbevain/cecil) - IL tooling HarmonyX is built on

Full licence texts for everything the release ZIPs redistribute are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and ship as separate files under `licenses/` in both ZIPs.

## Disclaimer

This mod is not affiliated with, endorsed by, or supported by RubberBandGames. "Wobbly Life" is a trademark of RubberBandGames. Use this mod at your own risk; no warranty is provided.
