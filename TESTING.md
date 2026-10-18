# Testing ImageCrystal 9.7

Run `./Build-Test.ps1` to build the portable application and execute the regression harness against those binaries. `Build-Release.ps1` always runs the same checks and rejects mismatched versions or private files in release output.

## Automated coverage

The test7 implementation passed 117 checks. The final release build logs its own result. Coverage includes JPEG/PNG pixel readability after native disposal, ICC processing, alpha, EXIF rotation, bounded bitmap export, WIC ownership, thumbnails, resize/base64/clipboard paths, animation frames, zero/one-neighbour preloading, cancellation/list mutations, missing-file navigation, order preservation, motion-video buffer boundaries, header reuse/replaced files, mislabeled extensions and toolbar icon reuse/disposal.

JPEG data named .png or .heic and PNG data named .jpg are synthetic regression cases. Two user-supplied JPEGs with misleading extensions also decoded successfully. These do not establish compatibility with every genuine HEIC variant. The library reports HEIC decoding support.

## Manual release checks

1. Launch the portable app empty and with your usual JPEG/PNG files. Check ICC colors, transparency, EXIF rotation, fit/100%, bicubic zoom and panning.
2. Drop a large image, a small image and the large image again into one window. Test unload, clipboard paste and closing, including files from different folders.
3. At neighbour preloading 0, only the requested image should decode. At 1, one neighbour per direction is eligible; returning to 0 releases neighbours. File-name snapshots still exist at zero.
4. Use disposable copies for Delete/Shift+Delete and cancellation. A failed or canceled shell deletion must retain the entry. Shell confirmation cancellation needs manual coverage.
5. Remove neighbours externally and navigate forward/back. Missing files should be skipped, including cached entries; all-missing folders must terminate navigation. Check Explorer order when opening from Explorer.
6. Check PNG alpha, GIF animation, native TIFF frames, real HEIC/AVIF/JXL/RAW samples and motion photos if those formats matter to you. Optional tools and all decoder variants are not exhaustively covered by the smoke suite.
7. Change toolbar size, switch light/dark theme and move between monitors with different scaling. Check About/version/credits, graphical Settings and License links with WebView2 installed.
8. Compare memory using the same file/settings and distinguish private working set, total working set, private commit and GPU memory. Idle CPU should not be driven by deleted polling loops. Large decoded CPU/GPU images remain allocated by design.

## Known limitations

- Full-resolution CPU and GPU image storage remains; compressed file size is not decoded memory size.
- A pre-existing Magick multipage TIFF roundtrip color issue was observed in older and current prototypes. Synthetic native WIC red/blue frame rendering passed; not every TIFF decoding/export path is validated.
- Upstream v9 does not automatically reapply the monitor color profile when moving the window between monitors.
- Graphical Settings/About and WebView2 SVG rendering require WebView2. ARM64 is not validated for the first release.
- Memory improvements do not imply a consistent startup/loading speedup. The short empty-startup/large-JPEG comparison showed no reliable gain.

Report dimensions, format, relevant settings and reproduction steps. Use safe shareable samples and review diagnostic logs for personal paths before attaching them.
