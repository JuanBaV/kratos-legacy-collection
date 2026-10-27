# Emulator setup

Select official Windows builds, including the modern Qt edition of PCSX2. The launcher opens download pages but does not install or redistribute emulators. Do not run executables from a texture archive.

| Emulator | Download | Configuration |
| --- | --- | --- |
| PCSX2 | https://pcsx2.net/download/ | Supply your own BIOS through PCSX2's wizard. Check graphics, controllers, memory cards, save-state and texture folders. |
| PPSSPP | https://www.ppsspp.org/download/ | Check controls, graphics and the memory-stick directory. The selected launcher data folder must contain `PSP`. |
| RPCS3 | https://rpcs3.net/download | Follow [Quickstart](https://rpcs3.net/quickstart), obtain firmware from PlayStation.com, then use File > Install Firmware. Set up Pads and game folders. |

Only mark an emulator configured after checking it yourself. Detection proves the executable exists and has an MZ header; it is not a signature, trust or full compatibility check. File versions are reported when present.

## PCSX2

The Qt command line uses `-batch`, optional `-fullscreen`, `--` and one game-path argument. On PCSX2 2.6+, `-gamecfg` supplies a private game overlay and `-datapath` selects configured data. Portable copies use `-portable`. See [official command-line documentation](https://pcsx2.net/docs/advanced/cli/).

Per-game overlays copy an existing serial/CRC game configuration when its identity is known, then replace only selected graphics keys. Existing emulator configurations remain intact. Original texture-replacement and cheat settings are retained unless an enabled enhancement requires them on. For widescreen, select the native in-game option or a verified built-in patch; a 16:9 output aspect by itself does not expand the field of view.

An ELF CRC must come from PCSX2's game information. It must not be guessed from a filename or substituted with a whole-image CRC. PCSX2 memory cards are persistent saves; save states are tied to emulator versions and may retain patch effects. See [general configuration](https://pcsx2.net/docs/configuration/general/).

## PPSSPP

The launcher passes one image path, optional `--fullscreen` and a private `--appendconfig` file. Profile launches also pass Windows' `-s` to prevent PPSSPP from saving appended settings over existing configuration. The appended `General/AutoRun = True` restores automatic boot, which `-s` otherwise disables. Settings and controller edits made inside PPSSPP during a profile session are temporary; game saves still use the configured memory stick. Renderer choices affect the launch profile. The memory-stick folder contains `PSP/SYSTEM`, `PSP/SAVEDATA`, `PSP/PPSSPP_STATE`, `PSP/Cheats` and `PSP/TEXTURES`.

PPSSPP 1.21 adds command-line options that older releases lack; this launcher uses the supported Windows no-save and append-config mechanisms. See [official CLI documentation](https://www.ppsspp.org/docs/reference/command-line/) and the [configuration implementation](https://github.com/hrydgard/ppsspp/blob/v1.20.4/Core/Config.cpp) before changing flags. Confirm a copied installation actually uses its local memory stick, particularly if the old configuration redirects it elsewhere.

## RPCS3

PS3 games use an extracted disc folder with `PS3_GAME/PARAM.SFO` and `PS3_GAME/USRDIR/EBOOT.BIN`. Select either the disc root or its PS3_GAME folder. The launcher supplies EBOOT.BIN with `--no-gui`. Firmware, controller setup and per-game compatibility remain RPCS3 responsibilities.

RPCS3 supports its own per-game configurations and patch manager. Patch selection depends on exact game version and PPU hash, so the launcher opens the emulator instead of guessing patch entries. When enabled, the launch profile copies the current game's custom configuration, or its global configuration if no custom file exists. It adjusts resolution scale (maximum 800%) and an explicitly selected Vulkan/OpenGL renderer using the [official configuration keys](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Emu/system_config.h), preserving unrelated fixes. The copy is supplied with `--config`; originals remain unchanged. Unsupported YAML layouts are rejected. CLI behavior is verified against the [official source](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/rpcs3.cpp).

## Portable copies

Choose a new parent directory and CREATE PORTABLE COPY. PCSX2 and PPSSPP binaries and configured data are copied and checked. No existing destination is reused. Conflicts stop the operation, and staging is cleaned up. Source files, saves, memory cards, mods and controller profiles stay in place.

Copying a BIOS or firmware is not a download entitlement. Review your own files and local rules. A BIOS configured outside PCSX2's data folder is not automatically gathered; reselect your own BIOS in the copied emulator. Hardware-dependent renderer and controller bindings must be checked on each PC. Always keep a separate backup of important saves.
