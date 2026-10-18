# Development

## Layout

```text
ImageCrystal/                 Git repository root
  Source/                     WinForms application, components and igcmd
  Setup/Assets/               Themes, language packs and portable assets
  Tools/                      Inherited source/build tools
  tests/SmokeTests/           Checks against the compiled application
  docs/                       Policies and release instructions
    upstream/                 Unmodified historical upstream documents
  Build-Test.ps1              Restore, build and check the portable app
  Build-Release.ps1           Package the app, source and checksums
  VERSION                     ImageCrystal release version
  LICENSE                     GNU GPL v3 text
  NOTICE.ImageCrystal.txt      Attribution and modification notice
  THIRD-PARTY-NOTICES.md        Dependency license overview
  reference/ImageGlass/       Ignored upstream Git checkout at the base tag
  local/                     Ignored settings backups and investigations
  artifacts/                 Ignored builds, fixtures and dependencies
```

Retain the inherited Source/Setup/Tools layout to simplify comparisons. Internal `ImageGlass.*` namespaces and assembly names remain for compatibility; the user-facing app is ImageCrystal.

The reference is a separate upstream Git checkout, not a submodule or part of source archives. It is detached at tag `9.6.1.807`; its origin points to `d2phap/ImageGlass`. Keep it untouched and fetch upstream separately when needed. Do not merge v10 automatically.

## Git and versions

- `main`: ImageCrystal development/release source; use short feature branches.
- `origin`: the future `joaopedro241/ImageCrystal` fork.
- `upstream`: `d2phap/ImageGlass`, with local pushing disabled.
- `9.6.1.807`: unchanged upstream base tag.
- `v9.7.0`: planned first release tag, created after final review.

Preserve upstream ancestry and authorship. The imported base is a shallow v9 tag checkout; fetch additional upstream history explicitly when required. Do not reinitialize the repository or force-push over copied upstream branches. Set the online default branch to our `main` after publication.

Use semantic versions from 9.7.0. Update VERSION, app/helper project versions, notices and changelog together. These are independent ImageCrystal versions.

## Build

Install Visual Studio 2026 with .NET desktop development and a Windows SDK, .NET 10 SDK and Node.js 22+ with npm. `global.json` permits newer compatible .NET 10 SDK feature bands. The SDK selects the bundled runtime patch; the current candidate uses .NET 10.0.13.

Run `./Build-Test.ps1`. It restores dependencies, builds Web UI assets, invokes full VS MSBuild for COM references, publishes self-contained win-x64 output, copies portable assets and runs the harness. Subsequent builds may use `-SkipRestore`.

`-SkipTests` is for development only; Build-Release.ps1 always checks the app. Distribution must exclude user configs, thumbnail caches, logs, probe/test DLLs and photos. ARM64 is inherited but not claimed as validated for the first release. WebView2 is needed for Settings/About and its SVG renderer, not ordinary raster decoding.

See TESTING.md and MEMORY-DIAGNOSTICS.md. Normal launches do not enable probes or diagnostic forced GC.
