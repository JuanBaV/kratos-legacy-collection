# Local release candidate report

Date: 2026-10-27. Version: 0.1.0. This is a working Windows desktop application and a local release candidate. Public publication and physical removable-drive acceptance remain separate steps.

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

PCSX2 2.6.3 and PPSSPP 1.20.4 were found locally. RPCS3 0.0.43 was discovered; PS3 boot has not been tested with real content. Private SSD settings reference the four verified PS2/PSP images in their original locations. Existing texture directories and graphics/controller files remain there. They are not falsely registered as launcher-installed mods.

Using isolated copies and the launch service, God of War (2005), Chains of Olympus and Ghost of Sparta reached their main menus. God of War II reached its initial display confirmation. The owner assisted with startup menus. This is boot evidence, not a full gameplay, controller or performance certification. The original PCSX2 INI and PPSSPP system/controller INI hashes remained unchanged.

A second, small PPSSPP copy tested a 2x temporary profile. Chains of Olympus reached the menu with all four copied system INIs unchanged after normal shutdown. That particular copy deliberately contained no HD texture files. PPSSPP profile sessions disable settings saves while restoring automatic boot; in-emulator settings/control edits during those sessions are temporary.

Completed emulator test copies and helper builds were deleted. The full PPSSPP and PCSX2 clones alone recovered approximately 15.46 GB. Original games, saves, configurations, BIOS and texture directories were preserved. Private settings and extracted artwork remain outside the public repository.

BIOCHIP is connected, but physical integration is deferred at the owner's request. No migration or writes to it were performed. Its remaining space was not measured in this acceptance pass. Its existing game folder and supercut are outside the application and repository.

## Verification results

- Release build: success, zero warnings and zero errors.
- Core regression suite: 49/49 cases passed using legal generated fixtures.
- WPF suite: 78 distinct assertions passed; repeated command-availability assertions are excluded from this count. Three rendered sizes, navigation and command routes, binding diagnostics, sorting and artwork decoding were checked.
- Self-contained package: started natively; welcome, five-step setup, empty library, sorting and native executable-picker opening/cancellation were exercised. Duplicate collection opening produces an English error.
- Native dialogs and external services are injected boundaries in automated tests; no claim is made that every OS dialog or third-party emulator feature was manually exercised.
- [UI delivery gate](UI-VERIFICATION.md) records presentation evidence. [Testing](TESTING.md) distinguishes automated coverage from remaining hardware and real-game checks.

## Remaining acceptance and limits

Real God of War III / Ascension boot, full gameplay, actual performance, controller calibration, per-monitor DPI changes, another PC and physical USB disconnection/reconnection remain manual acceptance items. The launcher does not promise Ultra HD performance on arbitrary hardware.

No authorized Modder Espartano archive was obtained or inspected. The creator channel, original God of War II publication and public Discord invitation were verified, but exact pack versions, regions, archive layouts and redistribution permissions remain unestablished. No invented package catalog or unverified patch code is shipped. 7Z extraction is not implemented; ZIP is explicitly supported. Native game widescreen and output stretching are distinguished.

## Build, run and GitHub preparation

On Windows with the .NET 10 SDK:

```powershell
./scripts/Build.ps1 -Publish
./artifacts/portable-win-x64/KratosLegacyCollection.exe
```

Keep the complete published folder together. A clean copy opens setup. The owner's separate local shortcut supplies a private collection root; private paths are never part of public defaults. See the [README](../README.md) for manual build commands and data-location behavior.

The local repository uses branch `main`, with no remote or push. Incremental commits include:

- `61809ef` feat: initialize launcher, independent library and enhancement services
- `ba97a51` feat: add safe artwork extraction, graphics profiles and patch compatibility
- `3bc5b87` feat: refine library controls and verify portable workflows
- `344d66d` fix: show only supported per-game graphics controls

Documentation and verification are committed separately. Branch names and commit messages have no agent suffix. Staged filenames and content were scanned before commits; public screenshots contain original presentation only. Source is MIT licensed, and README/About contain the non-affiliation notice. The Windows CI workflow is prepared but has not run remotely. No public release, GitHub push or trademark clearance is claimed. Review naming and third-party rights before authorizing publication.
