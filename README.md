# ImageCrystal 9.8

<img src="Source/Components/ImageGlass.UI/Resources/DefaultLogo.png" alt="ImageCrystal crystal logo" width="96" />

**Continue the ImageGlass v9 experience on Windows.**

ImageCrystal is an independent Windows image viewer forked from [ImageGlass 9.6.1.807](https://github.com/d2phap/ImageGlass/releases/tag/9.6.1.807), before the Avalonia rewrite. It keeps the familiar WinForms interface, Direct2D rendering, ImageMagick decoding, ICC color management and high-quality bicubic zoom, while reducing unnecessary memory allocations and background work.

The goal is a dependable Windows viewer with predictable resource use. This is a separate project maintained by **xenon**, not an official ImageGlass release. Version 9.8 continues our v9-based line and does not imply upstream endorsement.

## Download and run

Get the portable Windows x64 ZIP from [ImageCrystal Releases](https://github.com/joaopedro241/ImageCrystal/releases). Extract the complete archive into a writable folder and run **ImageCrystal.exe**. No installer or separate .NET installation is required. Keep the supporting files beside the executable.

- Windows 10/11, x64. ARM64 is not part of the first release.
- Microsoft WebView2 is required for graphical Settings, About and the optional WebView2 SVG renderer. Ordinary raster viewing does not require it.
- Portable preferences are stored in `igconfig.json` beside the executable when the folder is writable. Installed ImageGlass preferences are not imported automatically.
- Optional language packs are supplied in `Language/`; English is built in. Some translations may still use upstream terminology.
- Update checking has been removed. Install new versions manually from this repository's releases.

## Save a photo session (new in 9.8)

Right-click a photo and choose **Save photo session**, just above **Exit**. The same action is available under **File**. It saves shortcuts for the photos currently displayed across your open ImageCrystal windows, including minimized windows, without copying the images.

Sessions go to `%USERPROFILE%\Documents\ImageCrystal Sessions\yyyy-MM-dd HH-mm-ss - N Photos\`, with a sorted `0 - paths.txt` manifest. Duplicate paths are included once; colliding shortcut/session names receive numbered suffixes. Missing files and capture/shortcut failures have separate reports.

This native feature needs no PowerShell. Restart older ImageCrystal windows with 9.8 to include them. Empty windows, unsaved clipboard images and still-loading images are excluded; unavailable or loading windows are reported. See [the session guide](docs/PHOTO-SESSIONS.md).

## Improvements since ImageGlass v9

### Memory and image ownership

- Export ImageMagick pixels directly into native WIC storage in blocks of at most 64 KiB. Normal raster viewing avoids the full-image managed export array and WPF bitmap bridge.
- Dispose temporary Magick images/collections, previews, selected-frame wrappers, resize/export resources and decoder intermediates promptly, including error and cancellation paths.
- Release a directly opened photo after replacement, unload or window disposal, addressing retained native image storage after dropping another photo into the same window.
- Make animation-frame and WIC decoder ownership explicit, keeping displayed pixels alive after temporary decoder containers are released.
- Stream JPEG motion-photo detection through a small buffer instead of allocating an array for the whole compressed file.
- Remove forced full garbage collections and delayed heap-compaction calls from image changes, unload and slideshow paths.

### Predictable preloading and background work

- Replace **ImageBooster** with **Neighbour preloading**, default **0**. Zero schedules no neighbour decode/metadata jobs and releases cached neighbours.
- Positive counts mean that many neighbours in each direction. Work runs in finite, serial, cancellable batches, with file-size and dimension limits.
- Remove the deletion polling thread and cache-completion polling. Optional folder monitoring uses events and defaults to off.
- Preserve folder/Explorer order snapshots. Skip externally removed files when navigating; update the list after viewer deletion only when the shell operation succeeds.
- Remove Startup Boost, including settings and command-line controls.

### Loading, rendering and defaults

- Reuse foreground metadata and eligible JPEG/PNG header facts instead of repeating header scans. Changed files, multiple frames and special decoder paths retain their fallbacks.
- Use the detected format for misleading extensions, such as JPEG data named `.png` or `.heic`.
- Reuse each toolbar button's current icon when its name, theme and pixel size are unchanged; dispose replacements instead of retaining old DPI sizes.
- Default both enlargement and reduction to high-quality bicubic interpolation.
- Enable current-monitor ICC processing, including images without an embedded profile, by default.
- Start without a welcome or last-seen image and with the gallery hidden. These remain configurable.

### Independent and quieter

- Remove the updater, automatic checks, update prompts and associated settings.
- Block remote HTTP/HTTPS resources in embedded WebView2 pages and remove tracking parameters added by the app to browser links. Explicit external help links remain available.
- Use original crystal artwork, independent ImageCrystal application/protocol/association identifiers and separate configuration identity.
- Include opt-in local memory diagnostics and a regression harness. Diagnostics stay off during ordinary launches.

See [CHANGELOG.md](CHANGELOG.md) for the complete change list and [TESTING.md](TESTING.md) for verification coverage.

## What remains unchanged

ImageMagick, WinForms, WIC and Direct2D remain central to the app. Full-resolution CPU storage and GPU textures are retained for responsive zoom and panning. A small compressed JPEG can therefore still require much more RAM and VRAM than its file size.

This release does not add viewport-only decoding, a shared process for all windows, GPU eviction, a new HDR pipeline, Avalonia or a gallery redesign. OpenCL is unchanged. Some v9 limitations remain, including automatic monitor-profile changes when moving between displays and the multipage TIFF issue in [TESTING.md](TESTING.md).

Observed memory improvements concern specific workloads and our earlier prototypes, not a universal percentage against upstream. Small loading changes have not demonstrated a consistent empty-startup or large-JPEG speedup.

## Build from source

Use Windows, Visual Studio 2026 with **.NET desktop development** and a Windows SDK, .NET 10 SDK, and Node.js 22+ with npm. Full Visual Studio MSBuild is needed for inherited COM references.

```powershell
# Restore, build the Web UI and portable app, and run the checks:
.\Build-Test.ps1
# Subsequent builds with dependencies already restored:
.\Build-Test.ps1 -SkipRestore
# Checked, versioned portable/source archives and SHA-256 hashes:
.\Build-Release.ps1 -SkipRestore
```

Development output: `artifacts/ImageCrystal-dev/`. Release output: `artifacts/releases/`. Personal settings, photos, caches, investigations and the upstream reference checkout are excluded from Git. See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## License and credits

ImageCrystal is distributed under **GNU GPL version 3**; existing source-file grants allowing v3 or later are preserved. New original ImageCrystal contributions use GPL-3.0-or-later. See [LICENSE](LICENSE), [NOTICE.ImageCrystal.txt](NOTICE.ImageCrystal.txt) and [third-party notices](THIRD-PARTY-NOTICES.md). Distribute binaries with their corresponding modified source and build instructions.

**Original author:** Duong Dieu Phap and the ImageGlass contributors. **ImageCrystal maintainer:** xenon. ImageGlass's name and original logo belong to their owner; this fork uses its own name/artwork and is not affiliated with or endorsed by ImageGlass. The unmodified upstream license is preserved in [docs/upstream/LICENSE](docs/upstream/LICENSE).

Report ImageCrystal problems [in this fork](https://github.com/joaopedro241/ImageCrystal/issues), rather than asking the original author to support our modifications. See [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md) and [privacy notes](docs/PRIVACY.md).
