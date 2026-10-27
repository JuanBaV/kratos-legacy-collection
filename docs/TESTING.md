# Verification

Run `scripts/Build.ps1` on Windows with the .NET 10 SDK. The executable core suite and WPF checks return nonzero exit codes on failure. `dotnet test` is not the entry point for these dependency-free runners.

The core suite uses generated ISO9660 directories, PARAM.SFO records, tiny PNGs, no-op patch descriptors, ZIPs and dummy MZ headers. It starts a real argument-capture test process; it does not pretend that dummy executables emulate a console. No commercial content is needed in CI.

WPF checks instantiate the real application resources and window on an STA dispatcher, exercise view-model command routes, render the interface at 900x650, 1280x860 and 1600x900, inspect binding diagnostics and validate logical focus. File dialogs, external browser launches and confirmation answers are injected test boundaries. These checks are not a claim that every native Windows dialog was manually clicked.

The final local run on 2026-10-27 passed 49/49 core cases and 78 distinct WPF assertions, excluding repeated command-availability checks. Release compilation completed with zero warnings and zero errors. The self-contained Windows x64 folder was published locally; no GitHub workflow was run remotely.

## Acceptance matrix

| Requirement | Automated coverage | Additional manual acceptance |
| --- | --- | --- |
| First start without emulators | Six empty entries and resumable five-step setup. | Start the packaged EXE on a clean machine. |
| Emulator detection | Bounded search of generated installations and PE/name checks. | Search actual user-selected locations and confirm versions. |
| Manual executable selection | Validation rejects wrong names and invalid MZ headers; picker route checked. | Select an official emulator in the Windows file dialog. |
| Partial configuration | One game can be ready while the other five are absent. | Verify one configured emulator on another PC. |
| Empty library | Setup completes and all six cards remain accessible. | Native keyboard and high-contrast OS settings. |
| Single-game library | Readiness remains independent. | Boot the owned game with the real emulator. |
| All game launch adapters | Six separate argument plans, shell-free process transport and spaces preserved. | Real boot and gameplay for each owned release. |
| Wrong game or incompatible image | Invalid ISO/SFO bounds, mismatched IDs, invalid compressed headers. | Modified or less-common regional dumps need review. |
| Drive-letter change | Relative paths resolve against a simulated moved root. | Physical USB reconnection on another letter and PC. |
| Valid textures | PCSX2 serial/replacements and both PPSSPP layouts. | Check visual replacements in the emulator. |
| Incompatible textures | Wrong IDs, ambiguous layouts, scripts, unsafe paths and expansion limits rejected. | Author-specific archives require their actual instructions. |
| Enable/disable/uninstall | Existing files backed up; overlapping mods and external edits rejected. | Crash/disconnect recovery on removable storage. |
| Backup restoration | Settings round trip with a before-restore backup; corrupt data preserved. | Keep a separate backup of emulator saves. |
| Patches | Serial/CRC checks, PSP revision/hash/version descriptor, enable and disable. | Confirm actual gameplay effects and RPCS3 PPU patch choice. |
| Storage errors | Insufficient-space and cancellation paths; existing destinations refused. | Full drive, write-protected USB, lost permissions and unplug during write. |
| Offline operation | Setup, local library, profiles and artwork need no network. | Browser links naturally require connectivity. |
| Original preservation | Copy hashes, immutable ISO artwork reads, source image hashes and restore checks. | Confirm actual collection checksums before a migration. |
| Persistence | Atomic settings, setup progress, game settings and library ordering. | Restart packaged application after configuration. |
| Resolutions | Three actual WPF renders, no horizontal content overflow. | Per-monitor DPI transitions at 125%, 150% and 200%. |
| English UI | Application copy and validation messages are English; binding routes exercised. | User filenames and third-party emulator UI retain their own language. |
| Artwork | Embedded PSP/PS3 extraction, PNG/JPEG safety limits, real WPF image decoding and manual import. | Review source rights before publishing user images. |
| Sorting | Exact requested chronological and release sequences, selection preservation and saved preference. | Click the two library controls in the packaged build. |

## Private integration checks

On 2026-10-27, the detector identified four user-provided PS2/PSP images by internal metadata. Artwork was extracted from both PSP images without writing to the originals.

A verified private PPSSPP copy with copied configurations, saves and textures booted Chains of Olympus and Ghost of Sparta to their main menus using launch plans produced by the core service. This proves initial boot for those specific local images and emulator version, not full playthroughs, controller calibration or compatibility of other releases. Original PPSSPP configuration and controller-file SHA-256 values remained unchanged after the tests. The temporary emulator copy was then removed at the owner's request.

A separate minimal PPSSPP 1.20.4 copy tested the temporary launch profile with `-s`, appended `AutoRun = True` and 2x resolution. Chains of Olympus reached its main menu. All four copied system INIs remained byte-identical after normal shutdown. This small copy intentionally contained no HD textures and was removed after the test.

A copied PCSX2 2.6.3 installation with copied user data and a private copy of the owner's BIOS booted God of War (2005) to its main menu. God of War II reached its initial display-settings confirmation. The owner assisted with emulator/game startup menus. The original PCSX2 INI SHA-256 remained unchanged. The temporary PCSX2 tree was removed after verification, recovering 11,863,354,427 bytes.

God of War III and Ascension require separate real-content acceptance. Physical BIOCHIP integration is deferred by the owner. No public screenshot or test fixture includes personal or commercial game content.
