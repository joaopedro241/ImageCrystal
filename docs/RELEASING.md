# Publishing ImageCrystal

## Repository

The public repository is [joaopedro241/ImageCrystal](https://github.com/joaopedro241/ImageCrystal), a GitHub fork of `d2phap/ImageGlass`. Its default branch is `main`; `develop` is the untouched upstream branch copied during fork creation. Do not replace it with v9 code or synchronize v10 into main.

The existing `v9.7.0` tag and its release assets identify the previous release. Preserve them. Version 9.8.0 adds photo-session saving; prepare a separate release after testing/review.

## Build the exact committed release

After dependency restore, run `./Build-Release.ps1 -SkipRestore`. Output:

- `artifacts/releases/ImageCrystal-9.8.0-win-x64/`
- `artifacts/releases/ImageCrystal-9.8.0-win-x64.zip`
- `artifacts/releases/ImageCrystal-9.8.0-source.zip`
- `artifacts/releases/SHA256SUMS.txt`

The script requires clean committed source, runs the checks, verifies the binary version, packages only the Git tree as source and excludes private files. Retain all licenses and notices. Check normal JPEG/PNG, ICC/alpha, zoom and replacing images. Do not claim every genuine HEIC variant or TIFF path was tested.

## Publish after review

1. Commit reviewed source to main and build it with Build-Release.ps1. Create an annotated tag at that exact packaged commit: `git tag -a v9.8.0 <sourceCommit> -m "ImageCrystal 9.8.0"`.
2. Push **main and that tag only** to origin. Do not mirror/all/force-push upstream branches.
3. Verify main remains the default branch and Issues/private vulnerability reporting remain enabled.
4. Create a draft release for v9.8.0 using docs/RELEASE-NOTES-9.8.0.md.
5. Attach the portable ZIP, matching source ZIP and hashes. The local shared SHA256SUMS.txt is regenerated for the current build; do not replace the checksums already uploaded for an older release. GitHub's automatic tagged source archive is an additional source download.
6. Verify links and archive contents, then publish the reviewed release.

Local preparation does not push, create a public tag or publish a release. There is no automatic publishing workflow in this version.

Every official release uses an annotated `vMAJOR.MINOR.PATCH` tag. Verify the tag's commit equals RELEASE-MANIFEST.json before uploading. Published tags and release files must remain tied to that source; use a new version for later fixes.
