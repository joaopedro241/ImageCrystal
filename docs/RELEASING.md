# Publishing ImageCrystal

## Create the real fork

Use [GitHub's fork form](https://github.com/d2phap/ImageGlass/fork), not an empty new repository:

1. Owner: `joaopedro241`.
2. Repository name: `ImageCrystal`.
3. Description: `Windows continuation of ImageGlass v9: WinForms, Direct2D, ICC color management and lower memory overhead.`
4. Copy all branches if desired; our source is based on the v9 tag regardless.
5. Create fork and verify it reports parent `d2phap/ImageGlass`.

The connected GitHub tools do not expose fork creation and GitHub CLI is not installed. This is the small manual browser step. A renamed fork keeps its fork relationship. Our prepared local main branch can be pushed without recloning.

## Build the exact committed release

After dependency restore, run `./Build-Release.ps1 -SkipRestore`. Output:

- `artifacts/releases/ImageCrystal-9.7.0-win-x64/`
- `artifacts/releases/ImageCrystal-9.7.0-win-x64.zip`
- `artifacts/releases/ImageCrystal-9.7.0-source.zip`
- `artifacts/releases/SHA256SUMS.txt`

The script requires clean committed source, runs the checks, verifies the binary version, packages only the Git tree as source and excludes private files. Retain all licenses and notices. Check normal JPEG/PNG, ICC/alpha, zoom and replacing images. Do not claim every genuine HEIC variant or TIFF path was tested.

## Publish after review

1. Commit reviewed source to main, then create `v9.7.0` at that exact commit.
2. Push **main and that tag only** to origin. Do not mirror/all/force-push upstream branches.
3. Set the GitHub fork's default branch to main; enable Issues and private vulnerability reporting if available.
4. Create a draft release for v9.7.0 using docs/RELEASE-NOTES-9.7.0.md.
5. Attach the portable ZIP, matching source ZIP and hashes. GitHub's automatic tagged source archive is an additional source download.
6. Verify links and archive contents, then publish the reviewed release.

Local preparation does not push, create a public tag or publish a release. There is no automatic publishing workflow in this version.
