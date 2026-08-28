# Licence texts shipped with the binaries

Every file here is a verbatim copy of the upstream licence for a component this
mod redistributes. The packager stages this whole directory into both release
ZIPs, because MIT and LGPL-2.1 both require the notice to travel with the
binary and a `THIRD-PARTY-NOTICES.md` entry alone is not the text.

| File | Component | Why it ships |
|------|-----------|--------------|
| `cameraunlock-core-LICENSE.txt` | cameraunlock-core (MIT, Copyright (c) 2026 CameraUnlock) | Compiled into `CameraUnlock.Core.dll` / `CameraUnlock.Core.Unity.dll`, which both ZIPs deploy. A different copyright holder from the mod's own `LICENSE`, so that file does not cover it. |
| `BepInEx-LICENSE.txt` | BepInEx 5.4.23.5 (LGPL-2.1) | The installer ZIP carries `vendor/bepinex/BepInEx_win_x64.zip`. Also present beside the archive at `vendor/bepinex/LICENSE`. |
| `HarmonyX-LICENSE.txt` | HarmonyX 2.9.0 (MIT, Copyright (c) 2020 BepInEx) | `BepInEx/core/0Harmony.dll` inside that archive. |
| `Harmony-LICENSE.txt` | Harmony 2 (MIT, Copyright (c) 2017 Andreas Pardeike) | HarmonyX is a fork of Harmony 2 and carries its code, so Pardeike's notice travels with the same binary. |
| `Mono.Cecil-LICENSE.txt` | Mono.Cecil 0.10.4 (MIT, Jb Evain and Novell, Inc.) | `BepInEx/core/Mono.Cecil*.dll` inside that archive. Two copyright holders in one file; both are reproduced. |
| `MonoMod-LICENSE.txt` | MonoMod 22.01.29.01 (MIT, Copyright (c) 2015 - 2020 0x0ade) | `BepInEx/core/MonoMod.*.dll` inside that archive. |

The "why it ships" column describes the installer ZIP, which is the one that
carries the BepInEx archive. The Nexus ZIP holds only the three mod DLLs, so
`cameraunlock-core-LICENSE.txt` is the entry it strictly needs; the rest travel
with it as well so both ZIPs carry one identical, complete set rather than two
that have to be kept in step.

Fetched from each project's own repository, not transcribed. Re-fetch from the
upstream URLs recorded in `../THIRD-PARTY-NOTICES.md` rather than editing these
by hand.
