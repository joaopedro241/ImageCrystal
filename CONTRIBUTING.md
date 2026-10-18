# Contributing to ImageCrystal

ImageCrystal continues the Windows ImageGlass v9 codebase. Report fork-specific problems here, not to the original author's support channels.

Base focused branches and pull requests on `main`. Explain the concrete problem, resulting behavior and relevant verification. Keep unrelated changes separate. Follow [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

Preserve image quality, ICC processing and deterministic native-resource disposal. Avoid persistent polling and unbounded caches. Zero neighbour preloading must remain zero. New automatic network requests or update prompts do not fit current goals.

Use synthetic, redistributable test images. Do not commit personal photos, preferences, machine paths, binary builds or caches. New decoder paths should cover pixels, transparency, orientation and lifetime as applicable; documentation changes do not need unrelated tests.

Retain copyright and dependency notices. New original contributions are submitted under GPL-3.0-or-later. No separate ImageCrystal CLA or copyright assignment is required. Historical upstream agreements under `docs/upstream/` are retained for reference only.
