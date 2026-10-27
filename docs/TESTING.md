# Verification

Run `scripts/Build.ps1` on Windows with the .NET 10 SDK. The executable core suite and WPF checks return nonzero exit codes on failure. `dotnet test` is not the entry point for these dependency-free runners.

The core suite uses generated ISO9660 directories, PARAM.SFO records, tiny PNGs, no-op patch descriptors, ZIPs and dummy MZ headers. It starts a real argument-capture test process; it does not pretend that dummy executables emulate a console. No commercial content is needed in CI.

WPF checks instantiate the real application resources and window on an STA dispatcher, exercise view-model command routes, render the interface at 900x650, 1280x860 and 1600x900, inspect binding diagnostics and validate logical focus. File dialogs, external browser launches and confirmation answers are injected test boundaries. These checks are not a claim that every native Windows dialog was manually clicked.

The latest local run on 2026-10-27 passed 50/50 core cases and 80 distinct WPF assertions, excluding repeated command-availability checks. Release compilation completed with zero warnings and zero errors. The self-contained Windows x64 folder was published locally; no GitHub workflow was run remotely.

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
| Drive-letter change | Collection-relative and removable-volume paths resolve against simulated moved roots; unsafe volume paths are rejected. | Physical USB reconnection on another letter and PC. |
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
| Library wheel and loading state | Wheel input over a real card scrolls the outer viewport; a disabled-card render retains a dark or transparent background. | Confirm wheel scrolling and loading appearance in the native package. |

## Private integration checks

On 2026-10-27, the detector identified four user-provided PS2/PSP images by internal metadata. Artwork was extracted from both PSP images without writing to the originals.

A verified private PPSSPP copy with copied configurations, saves and textures booted Chains of Olympus and Ghost of Sparta to their main menus using launch plans produced by the core service. This proves initial boot for those specific local images and emulator version, not full playthroughs, controller calibration or compatibility of other releases. Original PPSSPP configuration and controller-file SHA-256 values remained unchanged after the tests. The temporary emulator copy was then removed at the owner's request.

A separate minimal PPSSPP 1.20.4 copy tested the temporary launch profile with `-s`, appended `AutoRun = True` and 2x resolution. Chains of Olympus reached its main menu. All four copied system INIs remained byte-identical after normal shutdown. This small copy intentionally contained no HD textures and was removed after the test.

A copied PCSX2 2.6.3 installation with copied user data and a private copy of the owner's BIOS booted God of War (2005) to its main menu. God of War II reached its initial display-settings confirmation. The owner assisted with emulator/game startup menus. The original PCSX2 INI SHA-256 remained unchanged. The temporary PCSX2 tree was removed after verification, recovering 11,863,354,427 bytes.

God of War III (BCES00510) booted from its existing removable-drive disc folder through the launch service and the owner's PC installation of RPCS3 0.0.43. The owner-provided European update was installed first; installed metadata and the boot log confirmed version 01.03. The log also confirmed the owner's native custom configuration. The owner assisted with brightness calibration and confirmed the main menu. No RPCS3 copy is required on the removable drive.

At the owner's request, only the custom profile's Vulkan `Exclusive Fullscreen Mode` was changed from `Automatic` to `Disable` before restarting. The next boot log confirmed that value and `VK_FULL_SCREEN_EXCLUSIVE_DISALLOWED_EXT`. Windows HDR settings were not changed. Controller configuration was performed by the owner afterward and is not an automated test result.

The owner-authorized copy and file audit to removable storage completed. PCSX2 and PPSSPP use copied executables, local data folders, user-provided BIOS where needed, saves and existing texture replacements. The existing PC RPCS3 remains external. All original PC emulator installations and collection files were retained. The pre-existing USB disc/movie inventory passed size/timestamp checks; critical disc metadata and boot files also passed SHA-256 checks. This was not a full re-hash of the existing movie or disc folder.

The real USB launcher displayed five ready titles and started God of War (2005), God of War II, Chains of Olympus and Ghost of Sparta through their individual PLAY controls. All four reached their main menus using the USB emulator copies. Modified texture-pack menus were visible in both PSP games and God of War II. Twenty-six copied save, memory-card and save-state files still matched their transfer hashes after these tests, as did their original source files. This verifies menu boot and preservation, not full gameplay, controller calibration or every texture replacement.

An actual native wheel-scrolling failure and light background during loading were reproduced with WPF checks before replacing the library's redundant inner scrolling template. Both focused checks then passed. The header includes the owner-requested credit and an original seven-size Spartan shield icon, decoded by the asset generator.

Physical drive-letter changes, another PC, controller acceptance and extended gameplay remain manual items. Ascension has no user-provided game files and has not been booted. No public screenshot or test fixture includes personal or commercial game content.

The owner later explicitly authorized deletion of the migrated PC game originals. A private whitelist selected 38,030 files, totaling 31,137,928,089 logical bytes. All 7,759 valuable files had freshly verified SHA-256 counterparts on USB before deletion; remaining entries were generated game diagnostics and an empty obsolete lock file. Individual deletion receipts and source-absence checks covered the entire list. Installed emulators, active RPCS3 data, shared BIOS/memory cards and global settings remained outside that list. After deletion, the real USB launcher again booted God of War (2005) and Ghost of Sparta to their main menus. Ghost of Sparta displayed the modified texture-pack menu. Both owned sessions were closed. All 26 portable save/card/state hashes, 39 protected PC file hashes, 111 pre-existing USB file checks and 3,504 unrelated source texture-file size/timestamp checks passed afterward. No new PS3 or full gameplay test is claimed by this cleanup.
