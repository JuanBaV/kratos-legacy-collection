# Local release candidate report

Date: 2026-10-27. Version: 0.1.0. This is a working Windows desktop application and a local release candidate. Public publication and remaining hardware acceptance remain separate steps.

## Architecture and technology

C# and .NET 10 LTS, WPF, MVVM, Windows 10/11 x64 and Git. There are no third-party application NuGet dependencies. The framework-independent Core project contains catalog, discovery, identity readers, emulator adapters, profiles, persistence, portable transfers, enhancement ownership and backup services. The App project contains WPF views, view models and Windows dialogs. Two executable test projects provide generated-content regression cases and STA WPF checks.

Storage uses atomic JSON replacement with a preceding settings backup. Managed paths are relative when inside the collection. Transfers require a new destination, check free space and FAT32 limits, copy and compare SHA-256 values, then publish staging. Extracted content is constrained to managed destinations. Long operations are asynchronous, expose busy feedback and support cancellation. Errors produce English messages and local diagnostics.

## Delivered functionality

| Component | State |
| --- | --- |
| First-Time Setup | Five resumable steps; opens on a clean collection; accepts partial or empty setup. Official downloads and native configuration guidance are integrated. |
| Library | Six independent entries, including Ascension. Each game has its own readiness, identity, path and settings. Missing titles do not block others. |
| Sorting | Two persistent controls use the owner's exact chronological and release sequences, preserving the selected game. |
| Detection | Bounded read-only emulator discovery, executable validation, file versions, ISO9660/SYSTEM.CNF and PARAM.SFO inspection. Compressed images require manual identity confirmation. |
| Launching | Separate PCSX2, PPSSPP and RPCS3 argument adapters, shell-free process startup and actionable failures. |
| Graphics | Original, Enhanced, Ultra HD and custom preferences per game. Opt-in private PCSX2 2.6+ overlays, PPSSPP temporary append-config sessions and copied RPCS3 YAML. Unsupported backends/layouts are rejected. |
| Portable storage | Relative paths, simulated collection relocation, verified PCSX2/PPSSPP preparation and explicit copy operations. RPCS3 uses an existing installation. |
| Texture packs | ZIP inspection for three identified PCSX2/PPSSPP layouts; safe staging, compatibility checks, credits, optional source/version, backups, enable, disable, conflict protection and uninstall. |
| Patches | User-supplied PNACH or PSP cheat import. PS2 serial/ELF CRC and PSP identity/revision/hash/emulator-range checks. RPCS3 uses its PPU-aware native patch manager. Unavailable controls say so. |
| Artwork | Read-only PSP ISO / PS3 folder image extraction, manual PNG/JPEG import and per-game SteamGridDB links. No SteamGridDB API or bundled commercial covers. |
| Backup | Launcher-settings export/restore with a before-restore backup. Enhancement rollback is tracked separately. Emulator saves require their own backup. |
| Presentation | English interface, original Greek geometric artwork, requested text-only card placeholders, keyboard focus styling, optional restrained motion and scrollable layouts. Requested slogans were removed. |
| Packaging | Self-contained Windows x64 folder, project license and matching Microsoft runtime notices. No emulator, game, BIOS, firmware or third-party pack is included. |

## Emulator and personal collection verification

PCSX2 2.6.3 and PPSSPP 1.20.4 were found locally. The initial private SSD settings referenced four verified PS2/PSP images in their original locations and reused the existing texture directories and graphics/controller files. These packs were not falsely registered as launcher-installed mods.

Using isolated copies and the launch service, God of War (2005), Chains of Olympus and Ghost of Sparta reached their main menus. God of War II reached its initial display confirmation. The owner assisted with startup menus. This is boot evidence, not a full gameplay, controller or performance certification. The original PCSX2 INI and PPSSPP system/controller INI hashes remained unchanged.

A second, small PPSSPP copy tested a 2x temporary profile. Chains of Olympus reached the menu with all four copied system INIs unchanged after normal shutdown. That particular copy deliberately contained no HD texture files. PPSSPP profile sessions disable settings saves while restoring automatic boot; in-emulator settings/control edits during those sessions are temporary.

Completed emulator test copies and helper builds were deleted. The full PPSSPP and PCSX2 clones alone recovered approximately 15.46 GB. Original games, saves, configurations, BIOS and texture directories were preserved. Private settings and extracted artwork remain outside the public repository.

The owner subsequently authorized physical migration to removable storage. Copying, individual SHA-256 transfer verification, portable configuration, preservation checks and a file audit completed. The real USB launcher then booted all four PS2/PSP titles to their main menus through their separate PLAY controls and copied emulators. Native texture-replacement settings were retained; modified menus were visible in both PSP titles and God of War II. Twenty-six copied saves, memory cards and save states still matched their transfer hashes after the tests. Original PC installations and collection files were retained for owner acceptance. Existing PS3 disc content and an unrelated movie remain outside the application folder and repository.

After explicit owner acceptance, a private file-level cleanup removed the four source images, their identified texture packs, game-specific data, redundant artwork and generated diagnostic dumps. Fresh SHA-256 comparisons verified USB counterparts for every valuable selected file before deletion. All installed PC emulators, active RPCS3 dependencies, shared BIOS and memory cards, global settings and unrelated games were retained. God of War (2005) and Ghost of Sparta then reached their menus again through the USB launcher with the PC game copies absent. Ghost of Sparta's modified texture-pack menu remained visible. All 26 portable save/card/state hashes still matched afterward. This cleanup is a private maintenance operation, not an automatic launcher feature.

God of War III BCES00510 was launched from its existing removable-drive folder using the owner's PC RPCS3 0.0.43 installation. The matching owner-provided European update was installed first; metadata and the boot log confirmed 01.03. The native custom configuration was loaded. After changing its exclusive-fullscreen option to Disable, a new boot reached the main menu with owner assistance. No global Windows HDR setting was changed. The game was closed after the boot check; prolonged gameplay and performance have not been certified. Details and limits are recorded in [Testing](TESTING.md).

## Verification results

- Release build: success, zero warnings and zero errors.
- Core regression suite: 50/50 cases passed using legal generated fixtures, including collection-relative and removable-volume drive changes.
- WPF suite: 80 distinct assertions passed; repeated command-availability assertions are excluded from this count. Three rendered sizes, navigation and command routes, binding diagnostics, sorting, artwork decoding, library wheel routing and the loading-state background were checked.
- Self-contained package: started natively; welcome, five-step setup, empty library, sorting and native executable-picker opening/cancellation were exercised. Duplicate collection opening produces an English error.
- Native dialogs and external services are injected boundaries in automated tests; no claim is made that every OS dialog or third-party emulator feature was manually exercised.
- [UI delivery gate](UI-VERIFICATION.md) records presentation evidence. [Testing](TESTING.md) distinguishes automated coverage from remaining hardware and real-game checks.

## Remaining acceptance and limits

Ascension boot, full gameplay, actual performance, controller acceptance, per-monitor DPI changes, another PC and physical USB disconnection/reconnection remain manual acceptance items. The launcher does not promise Ultra HD performance on arbitrary hardware.

No authorized Modder Espartano archive was obtained or inspected. The creator channel, original God of War II publication and public Discord invitation were verified, but exact pack versions, regions, archive layouts and redistribution permissions remain unestablished. No invented package catalog or unverified patch code is shipped. 7Z extraction is not implemented; ZIP is explicitly supported. Native game widescreen and output stretching are distinguished.

## Build, run and GitHub preparation

On Windows with the .NET 10 SDK:

```powershell
./scripts/Build.ps1 -Publish
./artifacts/portable-win-x64/KratosLegacyCollection.exe
```

Keep the complete published folder together. A clean copy opens setup. The owner's separate local shortcut points to the private removable-drive collection; private paths are never part of public defaults. See the [README](../README.md) for manual build commands and data-location behavior.

The local repository uses branch `main`. The owner added a GitHub remote; the development workflow has performed no push or public release. Incremental commits include:

- `61809ef` feat: initialize launcher, independent library and enhancement services
- `ba97a51` feat: add safe artwork extraction, graphics profiles and patch compatibility
- `3bc5b87` feat: refine library controls and verify portable workflows
- `344d66d` fix: show only supported per-game graphics controls
- `8b5f7d9` docs: prepare local release candidate and verification evidence
- `7c3f3b8` feat: preserve removable-volume game paths across drive changes

Documentation and verification are committed separately. Branch names and commit messages have no agent suffix. Staged filenames and content were scanned before commits; public screenshots contain original presentation only. Source is MIT licensed, and README/About contain the non-affiliation notice. The Windows CI workflow is prepared but has not run remotely. No public release, GitHub push or trademark clearance is claimed. Review naming and third-party rights before authorizing publication.
