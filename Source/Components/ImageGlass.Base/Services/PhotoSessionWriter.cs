// ImageCrystal, copyright (C) 2026 xenon. SPDX-License-Identifier: GPL-3.0-or-later
using ImageGlass.Base.WinApi;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ImageGlass.Base.Services;

public sealed record PhotoSessionResult(string Directory, int PathCount, int ShortcutCount,
    int MissingCount, int ShortcutErrorCount, int CaptureWarningCount);

public static class PhotoSessionWriter
{
    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "ImageCrystal Sessions");

    /// <summary>Use a short-lived STA for shell COM work; never retain a worker while idle.</summary>
    public static Task<PhotoSessionResult> SaveAsync(PhotoSessionCapture capture, string? root = null,
        DateTime? timestamp = null, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<PhotoSessionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(Save(capture, root ?? DefaultRoot, timestamp ?? DateTime.Now, cancellationToken)); }
            catch (OperationCanceledException) { completion.SetCanceled(cancellationToken); }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true, Name = "Save photo session" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static PhotoSessionResult Save(PhotoSessionCapture capture, string root, DateTime timestamp,
        CancellationToken cancellationToken)
    {
        var paths = capture.Paths.Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (paths.Length == 0) throw new InvalidOperationException("No open photos with saved file paths were found.");
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(root);
        var name = $"{timestamp.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture)} - {paths.Length} Photos";
        var session = CreateSessionDirectory(root, name);
        var missing = new List<string>();
        var failures = new List<string>();
        var shortcutCount = 0;
        var utf8 = new UTF8Encoding(false);
        // Write the complete manifest first, preserving even paths that have disappeared.
        File.WriteAllLines(Path.Combine(session, "0 - paths.txt"), paths, utf8);
        if (capture.Warnings.Length > 0)
            File.WriteAllLines(Path.Combine(session, "0 - capture-errors.txt"), capture.Warnings, utf8);
        foreach (var photo in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(photo)) { missing.Add(photo); continue; }
            var safeName = string.Concat(Path.GetFileNameWithoutExtension(photo)
                .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var shortcut = UniqueChildPath(session, safeName + ".lnk");
            try { CreateShortcut(shortcut, photo); shortcutCount++; }
            catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                failures.Add($"{photo}\t{ex.Message}");
            }
        }
        if (missing.Count > 0) File.WriteAllLines(Path.Combine(session, "0 - missing-files.txt"), missing, utf8);
        if (failures.Count > 0) File.WriteAllLines(Path.Combine(session, "0 - shortcut-errors.txt"), failures, utf8);
        return new(session, paths.Length, shortcutCount, missing.Count, failures.Count, capture.Warnings.Length);
    }

    private static string UniqueChildPath(string directory, string name)
    {
        var candidate = Path.Combine(directory, name);
        for (var index = 2; File.Exists(candidate) || Directory.Exists(candidate); index++)
            candidate = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(name)} ({index}){Path.GetExtension(name)}");
        return candidate;
    }

    private static string CreateSessionDirectory(string root, string name)
    {
        // Atomic creation prevents two viewer windows from reusing the same session folder.
        for (var index = 1; ; index++)
        {
            var candidate = Path.Combine(root, index == 1 ? name : $"{name} ({index})");
            if (CreateDirectory(candidate, IntPtr.Zero)) return candidate;
            var error = Marshal.GetLastWin32Error();
            if (error != 183) throw new Win32Exception(error);
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectory(string path, IntPtr securityAttributes);

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        // The existing shared shortcut helper swallows failures and overrides the icon.
        // Keep the organizer's target/working-directory behavior and report failures here.
        var link = (IShellLinkW)new ShellLinkCom();
        try
        {
            link.SetPath(targetPath);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetPath) ?? "");
            ((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Save(shortcutPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }
}

