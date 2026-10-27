# Game artwork

No game covers are shipped with the source repository or portable package. Cached images are private runtime data, excluded from Git.

Select a game, then use one of these controls:

- **LOAD ARTWORK** reads standard images from the selected game files. PSP ISO images use `PSP_GAME/PIC1.PNG`, falling back to `PSP_GAME/ICON0.PNG`. PS3 disc folders use the same filenames in `PS3_GAME`. Original game files are opened read-only. PS2 discs have no equivalent standard cover location. CSO and CHD artwork extraction is not supported.
- **CHOOSE IMAGE** copies a PNG or JPEG selected by the user into private `Data/Artwork`. The original image is preserved. Files are limited to 12 MB and 32 million pixels, with a maximum dimension of 8192.
- **REMOVE ARTWORK** clears the selection while preserving the source file and cached copies.
- **Open this game's page** opens the corresponding SteamGridDB game page in the user's browser. Downloads are manual. No SteamGridDB API, API key, login automation or scraping is used by the application.

Embedded game artwork is usually a background or icon, not the original retail box cover. The layout preserves the image's aspect ratio. An absent or invalid cached image uses a text-only placeholder with the game's number or short title.

Verified game-page links, checked on 2026-10-27:

| Game | SteamGridDB page |
| --- | --- |
| God of War (2005) | https://www.steamgriddb.com/game/36454 |
| God of War II | https://www.steamgriddb.com/game/35531 |
| Chains of Olympus | https://www.steamgriddb.com/game/36385 |
| Ghost of Sparta | https://www.steamgriddb.com/game/4561 |
| God of War III | https://www.steamgriddb.com/game/38217 |
| Ascension | https://www.steamgriddb.com/game/5248417 |

These links identify a source where users may find artwork. They do not grant redistribution rights. Check the source's terms and the rights applicable to any image before sharing it. Public screenshots in this repository use original geometric art and generated fixtures only.

The PSP embedded-artwork convention is also used by [PPSSPP's official game information reader](https://github.com/hrydgard/ppsspp/blob/master/UI/GameInfoCache.cpp).
