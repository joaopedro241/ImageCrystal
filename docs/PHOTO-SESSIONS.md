# Photo sessions

Available in ImageCrystal 9.8. Right-click any photo and choose **Save photo session**, just above **Exit**, or use **File → Save photo session**.

The command collects the currently displayed file from every ImageCrystal window for your Windows user in the current Windows session, including minimized windows. It creates Windows shortcuts, not copies of the photos. Navigation and dropped replacements are reflected; process launch arguments are not used.

## Saved layout

The fixed root is `%USERPROFILE%\Documents\ImageCrystal Sessions`. This is the literal Documents directory under your user profile, even if Windows' Documents known folder has been redirected elsewhere.

```text
ImageCrystal Sessions/
  2026-10-21 22-35-08 - 43 Photos/
    0 - paths.txt
    beach.lnk
    portrait.lnk
    portrait (2).lnk
```

The timestamp uses local time and `yyyy-MM-dd HH-mm-ss`. The count is the number of unique captured file paths, including paths whose files have since disappeared. Case-insensitive duplicate paths are included once, sorted like the organizer script. Shortcut names drop the image extension and use `(2)`, `(3)`, etc. when necessary. Repeated or concurrent saves in the same second create separate numbered session folders.

`0 - paths.txt` contains the absolute original paths as UTF-8 text. Reports are created only when needed:

- `0 - missing-files.txt`: paths retained in the manifest whose files are missing or inaccessible; no shortcut is created for them.
- `0 - shortcut-errors.txt`: paths for which Windows could not write a shortcut, with the error.
- `0 - capture-errors.txt`: windows that were unavailable or still loading and could not be captured.

The completion dialog shows the number of shortcuts and paths and offers **Open folder**. If nothing has a saved file path, no session folder is created. Unsaved clipboard images and empty/unloaded/error windows are excluded. Photos themselves are never copied, renamed or modified. Shortcuts target the original files, so moving/deleting those files later can break them.

## Scope and resource use

All participating windows must run ImageCrystal 9.8 or newer with this session protocol. Restart older windows after upgrading; they are reported as unavailable, with no fallback to potentially stale launch arguments. ImageGlass windows are not included. Still-loading windows are reported; wait for loading to finish and save again to include them.

This is a native C# feature and does not invoke PowerShell, WMI or a console process. Each viewer has a same-user local named pipe waiting asynchronously; it exchanges only a small path/status response when a save is requested. There is no periodic polling, image decoding or thumbnail generation. Requests have a one-second timeout and bounded concurrency. Shell shortcut writing uses a temporary STA thread and releases COM objects when finished. A save is a best-effort snapshot across windows, not a simultaneous freeze of them. Saving again from a different window is allowed and creates a separate session; repeated clicks in the same window are ignored until its save finishes.

Sessions contain personal absolute paths. They stay local and are not uploaded automatically. No original organizer config or script is modified or required.
