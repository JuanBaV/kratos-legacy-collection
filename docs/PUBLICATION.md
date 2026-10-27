# Publication checklist

The owner has added a GitHub remote. The development workflow has performed no push or public release. Publication requires the owner's explicit authorization.

Before each commit, run `scripts/Verify-Repository.ps1 -Staged`, inspect `git diff --cached --stat` and review each staged filename. The scanner is a useful guard, not a proof of licensing or absence of secrets. Inspect image contents separately. Keep private installation data and release builds outside Git.

Do not stage game images, extracted PS3 disc contents, EBOOT files, BIOS, firmware, personal saves, memory cards, emulator installations, movies, third-party mods or private-path configuration. `.gitignore` and the scanner block common binary and runtime paths. Raw compressed archives are excluded.

Review trademark and naming risks before publication. Kratos is also a protected entertainment character name in this context; using a mythology reference is not automatic clearance. The launcher title must not imply an official Sony collection. Obtain qualified review if needed, and be prepared to rename before public release. No legal clearance is claimed.

The source license covers original code and original geometric art. Emulator names and descriptive game titles identify compatibility; no official logos or game assets are distributed. Third-party enhancements remain user supplied until express permission and precise compatibility are documented. Preserve Microsoft's notices with self-contained runtimes.

Public screenshots must use empty libraries or legal generated fixtures and original artwork. Check that no usernames, absolute personal paths, private filenames or third-party game art are visible. Settings and diagnostic logs may contain private paths and must not be uploaded by default.
