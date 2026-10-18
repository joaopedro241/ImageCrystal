# ImageCrystal 9.7.0

First ImageCrystal release: an independent Windows continuation of **ImageGlass 9.6.1.807**, retaining WinForms, Direct2D/WIC, ImageMagick, ICC management and high-quality bicubic zoom.

This release reduces unnecessary temporary allocations, fixes native-image ownership during replacement, makes neighbour preloading predictable with a default of zero, removes updater/startup-boost/polling work and uses detected formats for misleading extensions. It also streams JPEG motion detection, reuses eligible JPEG/PNG header reads and toolbar icons, and provides quieter defaults with independent branding.

See the full [changelog](https://github.com/joaopedro241/ImageCrystal/blob/v9.7.0/CHANGELOG.md) and [testing notes](https://github.com/joaopedro241/ImageCrystal/blob/v9.7.0/TESTING.md). Full-resolution CPU/GPU storage remains; savings vary and there is no consistent measured startup speedup. No v10/Avalonia or new HDR pipeline is included.

## Downloads

- ImageCrystal-9.7.0-win-x64.zip: extract completely and run ImageCrystal.exe; no installer or separate .NET needed.
- ImageCrystal-9.7.0-source.zip: matching committed source and build instructions.
- SHA256SUMS.txt: checksums for both ZIPs.

Windows x64 only. WebView2 is needed for graphical Settings/About and its SVG renderer. Personal preferences are not bundled/imported. Install future releases manually; update checks are removed.

GNU GPL v3, with existing per-file grants and third-party licenses preserved. Original author: **Duong Dieu Phap and contributors**. ImageCrystal maintainer: **xenon**. This is an independent fork, not an official ImageGlass release.
