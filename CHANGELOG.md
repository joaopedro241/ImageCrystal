# Changelog

ImageCrystal has independent release numbers. Upstream ImageGlass releases are not ImageCrystal releases.

## 9.7.0 — first ImageCrystal release

Based on ImageGlass **9.6.1.807**, commit `1650ca9f3b8209d4a69a353f804ff6f7c42fc9de`.

### Memory and ownership

- Export ImageMagick pixels directly to WIC in blocks of at most 64 KiB, avoiding the full-image managed export array and ordinary-viewing WPF bitmap bridge.
- Dispose temporary native Magick images/collections after conversion, including failures and cancellation. Materialize animation frames before disposing their decoder collection.
- Transfer WIC ownership correctly so displayed pixels survive disposal of temporary decoder containers.
- Dispose replaced directly opened photos after renderer replacement and on unload, clipboard replacement and form disposal.
- Give temporary previews and selected GIF/TIFF frame wrappers explicit owners; clean up canceled/failed preview setup.
- Dispose JPEG XR, thumbnail, resize, base64 and clipboard/export intermediates. Resize output owns pixels independently of encoded streams.
- Stream JPEG motion detection with a 64 KiB buffer and boundary overlap; allocate only an identified video tail.
- Remove forced full managed GC and delayed LOH compaction from normal image changes, unload and slideshow.

### Preloading, idle work and navigation

- Replace ImageBooster with Neighbour preloading, default 0. Zero schedules no neighbour decode/metadata jobs and releases cached neighbours.
- Define positive counts per direction. Use finite, serial, cancellable batches with size/dimension limits and image identities that survive list changes.
- Await actual decode completion instead of polling the cache.
- Remove the image-deletion polling thread. Optional filesystem watching uses events, is off by default and disposes replaced watchers.
- Preserve folder/Explorer order snapshots, skip missing entries including cached ones, and terminate navigation when all entries are missing.
- Update the list after viewer deletion only if the operation succeeds; retain/reload entries after cancellation or failure.
- Remove Startup Boost, including UI and command-line controls.

### Loading and rendering work

- Reuse foreground metadata for direct image decoding.
- Reuse successful single-frame JPG/JPEG/JFIF/PNG header facts; validate file size/write time and keep special decoder/multi-frame/WIC threshold fallbacks.
- Skip preview header reads only when metadata establishes no relevant profile exists.
- Carry detected image formats into asynchronous single/multi-frame decoding, fixing JPEG data named .png/.heic and PNG data named .jpg.
- Reuse current toolbar icons for unchanged name/theme/dark-mode/pixel-size keys. Serialize overlapping updates and dispose owned replacements while preserving borrowed theme ownership.

### Defaults and identity

- Default preloading to 0, folder monitoring off, gallery hidden, welcome image off and opening the last image off.
- Default enlargement/reduction to high-quality bicubic interpolation.
- Default to current-monitor ICC processing, including untagged images.
- Remove update services, automatic checks, windows, prompts and settings.
- Block remote HTTP/HTTPS resources in embedded WebView2 pages and remove automatic browser-link tracking parameters. Explicit help links remain.
- Introduce ImageCrystal branding, original crystal artwork, independent protocol/instance/association identifiers and separate configuration identity.
- Credit xenon and Duong Dieu Phap (original author) in Language settings, preserving other language contributors. Route fork support links to this repository.

### Development and packaging

- Add a self-contained Windows x64 portable build and compiled-app regression harness.
- Add opt-in local memory probes, weak resource tracking and finite diagnostic snapshots. Ordinary launches do not enable probes or forced diagnostic GC.
- Prepare versioned portable/source ZIPs, SHA-256 checksums, source/build instructions and licensing notices.
- Retain upstream ancestry and a separately ignored upstream reference checkout. Exclude photos, personal preferences, investigations, caches and builds from Git.

### Validation and limits

The test7 implementation passed 117 checks; the final release build records its own result. Tests cover JPEG/PNG ICC pixels, alpha, EXIF rotation, WIC/native ownership, animation, navigation, preloading, icon lifetimes and misleading extensions. Supplied mislabeled JPEG files decoded successfully.

Full-resolution CPU/GPU image storage remains. There is no universal memory-reduction percentage or consistent measured startup speedup. No v10 code, new HDR pipeline, shared-window process, viewport decoding, GPU eviction or gallery redesign is included. OpenCL is unchanged. A pre-existing Magick multipage TIFF roundtrip color issue remains documented in TESTING.md.
