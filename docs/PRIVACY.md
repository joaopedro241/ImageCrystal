# Privacy and network behavior

ImageCrystal stores settings and optional thumbnail caches locally. Updater services, automatic checks and update prompts have been removed. There is no ImageCrystal analytics upload or automatic submission of memory logs.

Embedded WebView2 pages block remote HTTP/HTTPS resources. Clicking an explicit project/help/upstream-documentation link can open your browser and contact that site. Windows, WebView2 and external tools have their own behavior; this is not a promise that those components never access the network.

Memory diagnostics are disabled by default. When enabled, local logs may include full image paths, counters and dimensions. Review them before sharing. Local photos, preferences, diagnostics and caches are excluded from the Git/source release.

ImageCrystal is independent of ImageGlass. Upstream Store, donation and privacy policies do not describe this fork; upstream credits remain attribution.

Saving a photo session writes shortcuts and absolute paths to `%USERPROFILE%\Documents\ImageCrystal Sessions`. Same-user local named pipes exchange current image paths only when requested; they do not contact a remote server. Reports can contain private paths and process IDs. Session folders are created only by the explicit menu action and are not uploaded automatically. See PHOTO-SESSIONS.md.
