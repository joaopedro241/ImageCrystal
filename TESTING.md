# ImageCrystal test 1

Run **ImageCrystal.exe** directly from this folder. No installer or .NET installation is needed: this is a self-contained Windows x64 build. WebView2 remains an optional system dependency for graphical Settings, About and SVG viewing, as in upstream v9. Raster viewing does not need it.

This test starts with separate portable preferences (`igconfig.json` beside the executable), zero neighbour preloading, folder monitoring off, gallery hidden, the current monitor's colour profile, and high-quality bicubic scaling. It does not import your installed ImageGlass preferences. The original renderer remains in use.

Please try:

1. Open JPEGs, including the 4000×6000 and 8K examples. Check ICC-managed colour, zoom, pan, fit and 100% viewing.
2. Open your usual number of separate windows. Compare **Private working set**, **Commit size**, CPU while idle, and GPU dedicated/shared memory against v9 using the same images/settings. Compressed JPEG size is not decoded pixel size. Full-resolution CPU and GPU images still consume memory; this first pass does not introduce tiled decoding or GPU eviction.
3. In Settings → Image, check **Neighbour preloading**. At 0, navigation decodes only the requested image. At 1, it preloads one image in each direction. Switching back to 0 releases neighbours. Large-folder filename snapshots still exist.
4. Use copied test images to try Delete (Recycle Bin), Shift+Delete, confirmation cancellation, and deleting the last image. Cancellation or failed deletion must leave the entry available. Shell cancellation is not covered by the automated smoke suite.
5. Open a folder, delete some neighbouring files externally, then navigate forward/backward. Missing entries should be skipped without an error. Try all entries missing and navigation with wraparound disabled.
6. Open from Explorer with a nonalphabetical sort order and enable the existing Explorer-order option. The initial snapshot order should be preserved. Folder monitoring is optional, off by default; enable it to test create/rename/delete notifications. With it off, newly added files need an explicit folder reload.
7. Try a GIF, multipage TIFF, transparent PNG, local SVG, clipboard and save/crop. Embedded HTTP/HTTPS assets in WebView2 are blocked; an SVG that depends on remote assets may display incompletely. Explicit help links open your normal browser. The Edge runtime's own external behaviour is not certified by this test.

The updater, startup update requests, update notifications and update UI were removed. No public GitHub repository or release was created yet.

Forced full garbage collections after image changes were also removed. Managed memory may remain allocated longer before normal collection; compare CPU pauses and commit as well as the Task Manager memory number.

Automated tests cover decoder disposal, readable JPEG/WIC pixels, PNG alpha, thumbnails, native GIF and Magick animation, TIFF, zero/one neighbour behaviour, changing to zero, cancellation, changing the list during preloading, permanent deletion of a generated fixture, missing-file navigation, all-missing termination and preservation of snapshot order.

Please report the image dimensions, format, preloading setting and the steps for any regression. This is the first local test build, not a finished release.
