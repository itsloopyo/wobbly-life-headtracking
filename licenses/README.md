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

The installer ZIP carries a second loader archive,
`vendor/bepinex-il2cpp/BepInEx_UnityIL2CPP_x64.zip`, for the IL2CPP (Xbox Game
Pass) build of the game. It is BepInEx again, so `BepInEx-LICENSE.txt` covers
the loader itself and `HarmonyX`, `Harmony`, `Mono.Cecil` and `MonoMod` above
cover the parts it shares with the BepInEx 5 archive. The rest of what is inside
it is new, and each entry below is a component that archive ships as its own
binary.

| File | Component | Why it ships |
|------|-----------|--------------|
| `Il2CppInterop-LICENSE.txt` | Il2CppInterop (LGPL-3.0) | `BepInEx/core/Il2CppInterop.*.dll`. The managed-to-IL2CPP bridge the mod's camera code reaches the game through. LGPL-3.0, not the LGPL-2.1 of BepInEx itself, so it needs its own text. |
| `Cpp2IL-LICENSE.txt` | Cpp2IL (MIT, Copyright (c) 2020 Sam Byass) | `BepInEx/core/Cpp2IL.Core.dll`, `LibCpp2IL.dll`, `StableNameDotNet.dll`, `WasmDisassembler.dll`. Recovers type information from `GameAssembly.dll` on first launch. |
| `Disarm-LICENSE.txt` | Disarm (MIT, Copyright (c) 2025 Sam Byass) | `BepInEx/core/Disarm.dll`, the ARM64 disassembler Cpp2IL uses. |
| `AsmResolver-LICENSE.txt` | AsmResolver (MIT) | `BepInEx/core/AsmResolver*.dll`, PE and .NET metadata handling. |
| `AssetRipper.CIL-LICENSE.txt` | AssetRipper.CIL (MIT, Copyright (c) 2024 ds5678) | `BepInEx/core/AssetRipper.CIL.dll`. A separate project from the AssetRipper application, and MIT where that is not. |
| `AssetRipper.Primitives-LICENSE.txt` | AssetRipper.Primitives (MIT, Copyright (c) 2022 ds5678) | `BepInEx/core/AssetRipper.Primitives.dll`. Same split as above. |
| `Iced-LICENSE.txt` | Iced (MIT) | `BepInEx/core/Iced.dll`, the x86 disassembler Cpp2IL uses. |
| `Capstone.NET-LICENSE.txt` | Capstone.NET (MIT, Copyright (c) Ahmed Garhy) | `BepInEx/core/Gee.External.Capstone.dll`. Shipped by BepInEx from the `AssetRipper.Gee.External.Capstone` fork. |
| `Dobby-LICENSE.txt` | Dobby (Apache-2.0) | `BepInEx/core/dobby.dll`, the native hooking library. Apache-2.0 requires the licence and the NOTICE of any modifications to accompany it. |
| `SemanticVersioning-LICENSE.txt` | SemanticVersioning (MIT, Copyright (c) Adam Reeve) | `BepInEx/core/SemanticVersioning.dll`. |
| `dotnet-runtime-LICENSE.txt` | .NET runtime (MIT, Copyright (c) .NET Foundation and Contributors) | The whole `dotnet/` directory that archive installs beside the game. BepInEx 6 hosts its plugins on .NET 6 rather than the game's Mono runtime, so it carries one. |

The "why it ships" column describes the installer ZIP, which is the one that
carries both BepInEx archives. The Nexus ZIP holds only the three mod DLLs, so
`cameraunlock-core-LICENSE.txt` is the entry it strictly needs; the rest travel
with it as well so both ZIPs carry one identical, complete set rather than two
that have to be kept in step.

Fetched from each project's own repository, not transcribed. Re-fetch from the
upstream URLs recorded in `../THIRD-PARTY-NOTICES.md` rather than editing these
by hand.
