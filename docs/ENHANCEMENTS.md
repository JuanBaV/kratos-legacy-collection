# Enhancements and source review

Research date: 2026-10-27. No third-party enhancement content is redistributed.

## Texture formats

The importer supports three identified ZIP layouts. Additional layouts should be added to `TexturePackInspector` with an independently generated regression fixture.

```text
PCSX2:   any-wrapper/SCUS-97399/replacements/*.png or *.dds
PPSSPP:  any-wrapper/UCUS98653/textures.ini and texture files
PPSSPP:  textures.ini at archive root, with this Game ID in its metadata
```

The selected game's ID must match. PCSX2 textures are installed in `textures/<serial-with-hyphen>`, PPSSPP textures in `PSP/TEXTURES/<ID-without-hyphen>`. Confirm these are the emulator's configured folders before enabling. Custom emulator folder overrides require matching data selection or manual installation through the emulator.

ZIP extraction rejects traversal, absolute paths, alternate streams, reserved Windows names, links, duplicate names, unknown binary/script content and excessive expansion. Extraction goes to a private payload first. Enabling requires consent before writing to the emulator; previous files are backed up. An ownership manifest prevents enabled enhancements from overwriting one another. Disable restores previous files, and uninstall removes only the managed payload and owned installation files. External edits cause a conflict and are preserved.

Do not use archive names to infer compatibility. Root PPSSPP packs without an identity marker must be repackaged with their verified Game ID folder. 7Z is explicitly unsupported in this build; use a trusted extractor manually and make a ZIP of texture content only. Unknown scripts are never executed.

PCSX2 layout references come from its texture folder conventions and current configuration source. PPSSPP installation behavior and ID directories are documented in its [official texture guide](https://www.ppsspp.org/docs/reference/use-texture-replacement/) and [texture format reference](https://www.ppsspp.org/docs/reference/texture-replacement/).

## Modder Espartano

The current creator channel is [Modder Espartano](https://www.youtube.com/@modderespartano), resolved directly from the original [God of War II publication dated 2025-05-11](https://www.youtube.com/watch?v=0wsqJFCK15Y). Its public description links to [this Discord invitation](https://discord.com/invite/dmgWJtZDsB). On 2026-10-27, the public invitation displayed the server name MODDER ESPARTANO. No account was created and no server was joined. The publication alone does not establish the precise version, supported serial/CRC, archive layout or redistribution permission of a downloaded pack.

| Item | Verified state |
| --- | --- |
| Creator attribution | Original public video identifies Modder Espartano. |
| God of War II fan modification | Original publication found. No archive inspected. |
| Versions, precise regions and IDs | Not independently established from an authorized archive. |
| Other games' specific creator packages | Not accepted into a verified compatibility catalog. Third-party references are insufficient. |
| Current official Discord invitation | Verified in the creator's original description and public server invitation on 2026-10-27. Embedded link can be updated if it changes. |
| Redistribution license | No express redistribution permission established. No pack is bundled. |

The creator button opens the verified invitation in the user's browser. A replacement link entered in Settings requires confirmation of its original source. Downloads remain manual; no Discord login, server scraping or private access is automated. A valid invitation today is not a guarantee that it will remain valid indefinitely.

Existing personal texture directories cannot establish authorship or license merely by their folder names. Preserve their original credits. Importer support means the content layout can be processed, not that a creator endorses the launcher.

## Frame rate and display patches

The PS2 titles do not generally require a generic 60 FPS code for NTSC gameplay. The [PCSX2 God of War compatibility page](https://wiki.pcsx2.net/God_of_War) records 60 FPS operation. Region, hardware and emulation speed still matter. PPSSPP timing hacks and frame-rate cheats must not be treated as interchangeable.

PCSX2 distributes a [native patch database](https://github.com/PCSX2/pcsx2_patches); use its per-game UI for built-in patches. No patch code from it is copied into this repository. If importing a PNACH, supply the exact ELF CRC and confirm the patch's source and behavior. The filename must identify both the serial and CRC. PSP cheat files must have the selected `_S` Game ID and a compatibility descriptor beside the INI. A marker match alone cannot validate arbitrary code.

For a PSP file named `patch.ini`, create `patch.ini.klc.json` using information verified from its original source. The following is a schema example, not a real patch or compatibility claim:

```json
{
  "GameId": "UCUS98653",
  "Revision": "1.00",
  "MinEmulatorVersion": "1.20.4",
  "MaxEmulatorVersion": "1.20.4",
  "Sha256": "REPLACE_WITH_THE_ACTUAL_PATCH_SHA256",
  "Source": "https://example.org/original-author-publication",
  "Author": "REPLACE_WITH_THE_REAL_AUTHOR"
}
```

Import rejects a mismatched revision, file hash or emulator range. The same conditions are checked again on enable and launch. Changing game versions requires disabling old enhancements first. This descriptor records compatibility evidence; it is not a digital signature or an endorsement. Use `Get-FileHash patch.ini -Algorithm SHA256` to obtain the file hash.

God of War III and Ascension use RPCS3's patch manager for exact PPU/version matching. This launcher does not ship unverified PS3 patch YAML. Hardware requirements and game-specific emulator changes can affect results. Consult [RPCS3 compatibility](https://rpcs3.net/compatibility) and its own patch dialog.

Widescreen support distinguishes output aspect from actual expanded view. Enable native in-game widescreen where available, then set matching output. Use a verified game-specific display patch for games that need one. Disabling launcher-installed patches restores the preceding files but cannot remove effects already embedded in an emulator save state.
