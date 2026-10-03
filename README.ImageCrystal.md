# ImageCrystal

An independent Windows image viewer based strictly on ImageGlass **v9 tag 9.6.1.807**, retaining WinForms, Direct2D/WIC, ICC colour management and the existing zoom/rendering path. This checkout contains the first local test changes and has not been published to GitHub.

Changes:

- Release temporary ImageMagick images promptly after conversion, including error/cancellation paths. Materialize animation frames before releasing their decoder.
- Replace ImageBooster with **Neighbour preloading**, default 0. Zero schedules no neighbour decode or metadata jobs. Positive counts preload that many images in each direction, with finite cancellable batches and no idle timer. Cache batches use image identities rather than mutable list indices.
- Remove update services, updater windows, startup checks, settings and notifications. Block HTTP/HTTPS resources in embedded WebView2 content and remove automatic tracking parameters from browser links.
- Remove the deletion polling thread. The optional filesystem watcher uses events; default off. Dispose replaced watchers correctly.
- Remove forced full garbage collections and delayed heap compaction after image changes/unloads, including slideshow. The runtime collects managed objects normally; native decoders are released explicitly. Managed memory can remain allocated longer, while avoiding those induced pauses.
- Preserve folder/Explorer order snapshots and prune missing entries when navigating, including cached ones. Keep normal errors for inaccessible or corrupt files. Remove an entry after user deletion only if the shell operation succeeds; reload on cancellation/failure.
- Use ImageCrystal branding, independent instance/startup/association/protocol identifiers, original crystal artwork, and separate portable defaults. Gallery is hidden by default; its architecture is unchanged.

Deferred: alternate decoders/precision, tiled or viewport decoding, GPU eviction, shared processes, memory-budget caches, gallery redesign and broader rendering changes. Full-resolution decoding and GPU storage remain, so large images still need substantially more memory than their compressed files.

## Build and test

Windows x64, .NET 10 SDK, Node.js/npm, and Visual Studio with **.NET desktop development** and a Windows SDK are required. Use full Visual Studio MSBuild because upstream uses a COM reference. Dependencies retain upstream versions and npm lockfile.

```powershell
.\Build-Test.ps1
# Later builds with dependencies already restored:
.\Build-Test.ps1 -SkipRestore
```

Output: `artifacts\ImageCrystal-test\ImageCrystal.exe`, self-contained and runnable without an installer. The script also builds the local Web UI and runs regression checks against the final app binaries. TESTING.md describes manual checks. Runtime WebView2 is optional for Settings/About/SVG as in v9.

Cached incremental compiles on this machine took roughly 3–10 seconds; a full frontend/portable build takes longer. Initial dependency/toolchain setup is the slow part. These are observations, not guaranteed build times.

## Attribution and future publication

Original [ImageGlass](https://github.com/d2phap/ImageGlass) code and notices are retained. See LICENSE and NOTICE.ImageCrystal.txt. ImageCrystal uses a new name/logo. When publishing binaries, publish the corresponding modified source and build instructions, preserve GPL/third-party notices, and identify the upstream version. Public release is deferred until local testing.
