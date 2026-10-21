// ImageCrystal, copyright (C) 2026 xenon. SPDX-License-Identifier: GPL-3.0-or-later
using ImageGlass;
using ImageGlass.Base;
using ImageGlass.Base.Services;
using ImageGlass.Base.WinApi;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using ImageMagick;

internal static partial class SmokeTests
{
    private static void TestPhotoSessionViewer(string fixtures)
    {
        static void Pump(Task task)
        {
            var watch = Stopwatch.StartNew();
            while (!task.IsCompleted && watch.Elapsed < TimeSpan.FromSeconds(15))
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(1);
            }
            if (!task.IsCompleted) throw new TimeoutException("Session viewer test did not finish");
            task.GetAwaiter().GetResult();
            System.Windows.Forms.Application.DoEvents();
        }
        var directory = Path.Combine(fixtures, "session-viewer");
        Directory.CreateDirectory(directory);
        var first = Path.Combine(directory, "first.png");
        var second = Path.Combine(directory, "second.png");
        using (var image = new MagickImage(MagickColors.Red, 24, 20)) image.Write(first);
        using (var image = new MagickImage(MagickColors.Blue, 32, 28)) image.Write(second);
        using var form = new FrmMain();
        Local.FrmMain = form;
        Local.InitImageList(new[] { first, second });
        Local.Metadata = null;
        PhotoSessionSnapshot Read()
        {
            var task = PhotoSessionEndpoint.ReadAsync(Environment.ProcessId);
            Pump(task);
            return task.Result;
        }
        try
        {
            _ = form.Handle;
            Check(form.MnuFile.DropDownItems.Contains(form.MnuSavePhotoSession)
                && form.MnuSavePhotoSession.Text == "Save photo session", "session action appears in File with its language text");
            Pump(form.ViewNextCancellableAsync(0, filePath: first));
            Check(Read().Path == first && !Read().IsLoading, "viewer publishes successfully displayed PNG path");
            Pump(form.ViewNextCancellableAsync(0, filePath: second));
            Check(Read().Path == second && !Read().IsLoading, "viewer publishes replacement PNG rather than original photo");
            typeof(FrmMain).GetMethod("MnuContext_Opening", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { form, new System.ComponentModel.CancelEventArgs() });
            var items = form.MnuContext.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().ToArray();
            Check(items[^2].Name.StartsWith("MnuSavePhotoSession-", StringComparison.Ordinal)
                && items[^1].Name.StartsWith("MnuExit-", StringComparison.Ordinal),
                "session action appears directly above Exit in the photo context menu");
            var renamed = Path.Combine(directory, "renamed.png");
            File.Move(second, renamed, overwrite: true);
            typeof(FrmMain).GetMethod("RenameSessionPhotoPath", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(form, new object[] { second, renamed });
            Check(Read().Path == renamed, "viewer session path follows the rename notification");
            form.IG_Unload();
            Check(Read().Path == null, "viewer unload clears the exported session path");
            using var clipboard = new MagickImage(MagickColors.Green, 10, 10);
            form.LoadClipboardImage(BHelper.ToWicBitmapSource(clipboard));
            Check(Read().Path == null, "viewer excludes unsaved clipboard pixels from sessions");
            FrmMain.ClearClipboardImage();
        }
        finally
        {
            form.Dispose();
            Local.Images.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static int RunPhotoSessionHost()
    {
        using var endpoint = new PhotoSessionEndpoint();
        Console.WriteLine("READY");
        string? command;
        while ((command = Console.ReadLine()) != null && command != "EXIT")
        {
            endpoint.Update(command is "EMPTY" or "LOADING" ? null : command, command == "LOADING");
            Console.WriteLine("UPDATED");
        }
        return 0;
    }

    private static async Task TestPhotoSessionsAsync(string fixtures)
    {
        var directory = Path.Combine(fixtures, "photo-sessions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var first = Path.Combine(directory, "one", "portrait.png");
        var second = Path.Combine(directory, "two", "portrait.jpg");
        var unicode = Path.Combine(directory, "João [test] 照片.png");
        foreach (var filename in new[] { first, second, unicode })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filename)!);
            File.WriteAllText(filename, "shortcut target fixture");
        }
        var missing = Path.Combine(directory, "gone.jpg");
        var output = Path.Combine(directory, "saved");
        var timestamp = new DateTime(2026, 10, 21, 22, 35, 8);
        Process? child = null;
        using var endpoint = new PhotoSessionEndpoint();
        try
        {
            Check((await PhotoSessionEndpoint.ReadAsync(Environment.ProcessId)).Path == null,
                "session endpoint excludes empty windows");
            endpoint.Update(first);
            Check((await PhotoSessionEndpoint.ReadAsync(Environment.ProcessId)).Path == first,
                "session endpoint exposes loaded path");
            endpoint.Update(second);
            Check((await PhotoSessionEndpoint.ReadAsync(Environment.ProcessId)).Path == second,
                "session endpoint follows replacement rather than launch arguments");
            endpoint.Update(null);
            Check((await PhotoSessionEndpoint.ReadAsync(Environment.ProcessId)).Path == null,
                "session endpoint clears unloaded or clipboard photos");

            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true,
            };
            foreach (var argument in new[] { "exec", "--depsfile", Path.Combine(AppContext.BaseDirectory, "ImageCrystal.deps.json"),
                "--runtimeconfig", Path.Combine(AppContext.BaseDirectory, "ImageCrystal.runtimeconfig.json"),
                Assembly.GetExecutingAssembly().Location, "--photo-session-host" }) start.ArgumentList.Add(argument);
            child = Process.Start(start)!;
            Check(await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) == "READY",
                "second process starts dormant session endpoint");
            async Task UpdateChild(string value)
            {
                await child.StandardInput.WriteLineAsync(value);
                await child.StandardInput.FlushAsync();
                Check(await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)) == "UPDATED",
                    "second process updates its current photo");
            }
            await UpdateChild(first);
            endpoint.Update(second);
            var captured = await PhotoSessionEndpoint.CaptureAsync(new[] { Environment.ProcessId, child.Id });
            Check(captured.Paths.Length == 2 && captured.Paths.Contains(first) && captured.Paths.Contains(second)
                && captured.Warnings.Length == 0, "session captures current paths across two processes");
            await UpdateChild(second);
            captured = await PhotoSessionEndpoint.CaptureAsync(new[] { Environment.ProcessId, child.Id });
            Check(captured.Paths.SequenceEqual(new[] { second }), "session removes duplicate paths across windows");
            await UpdateChild("LOADING");
            captured = await PhotoSessionEndpoint.CaptureAsync(new[] { Environment.ProcessId, child.Id });
            Check(captured.Paths.SequenceEqual(new[] { second }) && captured.Warnings.Length == 1,
                "session reports a still-loading window instead of saving a stale path");
            var watch = Stopwatch.StartNew();
            captured = await PhotoSessionEndpoint.CaptureAsync(new[] { int.MaxValue });
            Check(captured.Paths.Length == 0 && captured.Warnings.Length == 1 && watch.Elapsed < TimeSpan.FromSeconds(4),
                "session times out unavailable or old processes and reports incomplete capture");
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                try { await PhotoSessionEndpoint.CaptureAsync(new[] { child.Id }, cancel.Token); Check(false, "capture should cancel"); }
                catch (OperationCanceledException) { Check(true, "session capture honors cancellation"); }
            }

            var capture = new PhotoSessionCapture(new[] { unicode, second, first, first.ToUpperInvariant(), missing },
                new[] { "An older window could not be captured." });
            var saved = await PhotoSessionWriter.SaveAsync(capture, output, timestamp);
            Check(Path.GetFileName(saved.Directory) == "2026-10-21 22-35-08 - 4 Photos",
                "session preserves organizer timestamp and unique photo-count naming");
            Check(saved.PathCount == 4 && saved.ShortcutCount == 3 && saved.MissingCount == 1
                && saved.ShortcutErrorCount == 0 && saved.CaptureWarningCount == 1,
                "session reports shortcut, missing and incomplete capture counts");
            var paths = File.ReadAllLines(Path.Combine(saved.Directory, "0 - paths.txt"));
            Check(paths.Length == 4 && paths.Contains(missing) && paths.SequenceEqual(paths.Order(StringComparer.CurrentCultureIgnoreCase)),
                "session manifest is sorted, unique and retains missing files");
            Check(File.ReadAllLines(Path.Combine(saved.Directory, "0 - missing-files.txt")).SequenceEqual(new[] { missing })
                && File.ReadAllText(Path.Combine(saved.Directory, "0 - capture-errors.txt")).Contains("older window"),
                "session writes prefixed missing and capture reports");
            var links = Directory.GetFiles(saved.Directory, "*.lnk");
            Check(links.Length == 3 && links.Any(p => Path.GetFileName(p) == "portrait.lnk")
                && links.Any(p => Path.GetFileName(p) == "portrait (2).lnk"), "session handles duplicate basenames without overwriting");
            var targets = links.Select(FileShortcutApi.GetTargetPathFromShortcut).ToArray();
            Check(targets.Order().SequenceEqual(new[] { first, second, unicode }.Order()),
                "session shortcut targets preserve Unicode and literal bracket paths" +
                (targets.Order().SequenceEqual(new[] { first, second, unicode }.Order()) ? "" : ": " + System.Text.Json.JsonSerializer.Serialize(targets)));
            Check(Directory.GetFiles(saved.Directory).All(p => Path.GetExtension(p) is ".lnk" or ".txt"),
                "session saves shortcuts and reports without copying photos");
            var collisions = await Task.WhenAll(PhotoSessionWriter.SaveAsync(capture, output, timestamp),
                PhotoSessionWriter.SaveAsync(capture, output, timestamp));
            Check(collisions.Select(r => r.Directory).Distinct().Count() == 2
                && collisions.All(r => r.Directory != saved.Directory && Directory.GetFiles(r.Directory, "*.lnk").Length == 3),
                "concurrent saves use separate numbered session directories");
            var emptyRoot = Path.Combine(directory, "empty");
            try { await PhotoSessionWriter.SaveAsync(new([], []), emptyRoot, timestamp); Check(false, "empty session should fail"); }
            catch (InvalidOperationException) { Check(!Directory.Exists(emptyRoot), "empty capture creates no session directory"); }
            var longTarget = Path.Combine(directory, new string('x', 253));
            File.WriteAllText(longTarget, "target");
            var failure = await PhotoSessionWriter.SaveAsync(new(new[] { longTarget }, []), output, timestamp);
            Check(failure.ShortcutErrorCount == 1 && File.Exists(Path.Combine(failure.Directory, "0 - shortcut-errors.txt")),
                "session records shell shortcut failures and preserves its manifest");
            Check(PhotoSessionWriter.DefaultRoot == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Documents", "ImageCrystal Sessions"), "session default uses the requested literal profile Documents folder");
        }
        finally
        {
            if (child != null)
            {
                if (!child.HasExited)
                {
                    await child.StandardInput.WriteLineAsync("EXIT");
                    await child.StandardInput.FlushAsync();
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try { await child.WaitForExitAsync(timeout.Token); }
                    catch (OperationCanceledException) { child.Kill(); }
                }
                child.Dispose();
            }
            endpoint.Dispose();
            await endpoint.Completion;
            Directory.Delete(directory, recursive: true);
        }
        Check(endpoint.Completion.IsCompletedSuccessfully, "session listener stops cleanly on disposal");
    }
}
