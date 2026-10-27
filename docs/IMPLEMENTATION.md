# Implementation plan and acceptance boundaries

The public repository contains source and original presentation assets only. Runtime data, imported content, discovery reports, backups and builds stay outside version control. No pushes or public releases are authorized.

1. Foundation: .NET 10 LTS, WPF, a framework-independent core, MVVM commands, atomic settings and portable path resolution.
2. Setup: resumable five-step wizard, bounded read-only discovery, manual executable validation, official links and emulator-owned configuration.
3. Library: six independent entries, image/disc metadata inspection, conservative compatibility, launch plans using argument lists.
4. Portability: relative paths inside the collection, copy-only verified transfers, emulator copy preparation and free-space checks.
5. Mods: local ZIP inspection, multiple documented layouts, isolated staging, ownership manifests, reversible install/disable/uninstall and conflict detection. 7Z is rejected unless a safe supported implementation is available.
6. Patches: user-supplied version-specific files, strict identity checks, emulator-native patch workflows and individual graphics profiles. No guessed patch codes.
7. Visuals: original Greek architectural motifs, DPI-aware WPF, keyboard focus, motion preference and compact layout.
8. Personal installation: read-only inventory, isolated SSD smoke tests and an owner-authorized removable-drive migration with individual copy verification. PCSX2 and PPSSPP use portable copies; RPCS3 remains on the PC. Existing files and original PC installations are retained. Transfer and final portable launch acceptance are still in progress.
9. Verification: dependency-free executable regression suite, real WPF smoke checks, build/publish and English documentation. Commercial-game boot and hardware calibration require user-owned content and remain explicitly manual.

## Architecture

`Core` owns catalog, storage, discovery, game inspection, launch planning, copy operations and enhancement transactions. `App` owns Windows dialogs, WPF rendering and view models. `Tests` uses legal generated fixtures and fake processes, without commercial game dependencies. Emulator adapters use documented command lines; configuration requiring BIOS, firmware or controller calibration opens the emulator's own UI. Existing emulator files are never silently edited.

## Publication review

The chosen name is not a trademark clearance. Before publication, review game and emulator names, the Kratos character name, Sony/Santa Monica marks, third-party mod permissions and screenshot contents. No game assets or third-party packs are bundled. Author-provided links and metadata are only catalogued when verified; unknown versions, rights and compatibility remain unknown.
