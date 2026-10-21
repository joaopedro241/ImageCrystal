# ImageCrystal 9.8.0

Save the photos currently displayed across your open ImageCrystal windows as a session of Windows shortcuts. Choose **Save photo session** from the photo context menu above Exit, or from File.

Sessions are saved under `%USERPROFILE%\Documents\ImageCrystal Sessions`, preserving the organizer's timestamp/count folder names, sorted unique `0 - paths.txt` manifest, numbered filename collisions and missing/failed-shortcut reports. A completion dialog offers Open folder. This feature captures current photos after navigation/replacement, includes minimized windows, uses native C# and needs no PowerShell. No polling, image reloading or photo copying is added.

Restart older windows with 9.8 to include them. Empty and unsaved clipboard views are excluded. Unavailable or loading windows are reported in `0 - capture-errors.txt`. See the [session guide](https://github.com/joaopedro241/ImageCrystal/blob/v9.8.0/docs/PHOTO-SESSIONS.md), [changelog](https://github.com/joaopedro241/ImageCrystal/blob/v9.8.0/CHANGELOG.md) and [testing notes](https://github.com/joaopedro241/ImageCrystal/blob/v9.8.0/TESTING.md).

Windows x64 portable: extract the complete ZIP and run ImageCrystal.exe. No installer or separate .NET installation is required. WebView2 is needed for graphical Settings/About and its SVG renderer. Existing 9.7.0 release files remain unchanged.

Based on ImageGlass 9.6.1.807. GNU GPL v3 with original per-file grants and third-party notices retained. Original author: Duong Dieu Phap and contributors. ImageCrystal maintainer: xenon.
