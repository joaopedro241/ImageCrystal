// ImageCrystal modifications by xenon, 2026-10-21; see NOTICE.ImageCrystal.txt.
using ImageGlass;
using ImageGlass.Base;
using ImageGlass.Base.Diagnostics;
using ImageGlass.Base.Photoing.Codecs;
using ImageGlass.Base.Services;
using ImageGlass.Settings;
using ImageMagick;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

internal static partial class SmokeTests
{
    private static int _checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        _checks++;
        Console.WriteLine("PASS " + message);
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--photo-session-host") return RunPhotoSessionHost();
            var dir = Path.GetFullPath(args[0]);
            Directory.CreateDirectory(dir);
            RunAsync(dir).GetAwaiter().GetResult();
            TestLiveVideoAsync(dir).GetAwaiter().GetResult();
            TestBitmapConversion();
            TestAlphaOutput();
            TestNavigation(dir);
            TestMemoryProbe();
            TestHeaderReuseAsync(dir).GetAwaiter().GetResult();
            TestMislabeledFilesAsync(dir).GetAwaiter().GetResult();
            TestToolbarIcons(dir);
            TestPhotoSessionViewer(dir);
            TestPhotoSessionsAsync(dir).GetAwaiter().GetResult();
            Console.WriteLine($"SUCCESS: {_checks} checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static async Task TestHeaderReuseAsync(string dir)
    {
        var headerFlag = typeof(IgMetadata).GetProperty("HeaderReadSucceeded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var options = new CodecReadOptions
        {
            ColorProfileName = "sRGB", ApplyColorProfileForAll = true,
            UseEmbeddedThumbnailRawFormats = true, UseEmbeddedThumbnailOtherFormats = false,
        };
        foreach (var extension in new[] { ".jpg", ".png" })
        foreach (var withExif in new[] { false, true })
        {
            var filename = Path.Combine(dir, "header-reuse-" + withExif + extension);
            using (var image = new MagickImage("gradient:#113355-#ccee88", 91, 63))
            {
                image.Depth = 16;
                image.SetProfile(ColorProfiles.AdobeRGB1998);
                if (extension == ".png") image.Alpha(AlphaOption.Set);
                if (withExif)
                {
                    var exif = new ExifProfile();
                    exif.SetValue(ExifTag.Orientation, (ushort)6);
                    exif.SetValue(ExifTag.DateTimeOriginal, "2025:01:02 03:04:05");
                    image.SetProfile(exif);
                    image.Orientation = OrientationType.RightTop;
                }
                image.Write(filename);
            }
            var metadata = PhotoCodec.LoadMetadata(filename, options)!;
            Check((bool)headerFlag.GetValue(metadata)!, extension + " metadata enables header reuse");
            using var fast = await PhotoCodec.LoadAsync(filename, options, metadata: metadata);
            headerFlag.SetValue(metadata, false); // Exercise the existing Ping path on the same file.
            using var original = await PhotoCodec.LoadAsync(filename, options, metadata: metadata);
            Check(fast.Image.Width == original.Image.Width && fast.Image.Height == original.Image.Height
                && fast.FrameCount == original.FrameCount && fast.HasAlpha == original.HasAlpha,
                extension + " reused header preserves dimensions/orientation/frames/alpha (EXIF=" + withExif + ")");
            using var fastBitmap = BHelper.ToGdiPlusBitmap(fast.Image);
            using var oldBitmap = BHelper.ToGdiPlusBitmap(original.Image);
            var equal = true;
            for (var y = 0; y < fastBitmap.Height; y++)
                for (var x = 0; x < fastBitmap.Width; x++)
                    equal &= fastBitmap.GetPixel(x, y).ToArgb() == oldBitmap.GetPixel(x, y).ToArgb();
            Check(equal, extension + " reused header matches all ICC-converted pixels (EXIF=" + withExif + ")");

            headerFlag.SetValue(metadata, true);
            using var fastPreview = PhotoCodec.GetEmbeddedThumbnail(filename, metadata: metadata);
            using var oldPreview = PhotoCodec.GetEmbeddedThumbnail(filename);
            Check((fastPreview == null) == (oldPreview == null), extension + " optional preview result is preserved");
            if (withExif)
                Check(fast.Image.Width == 63 && fast.Image.Height == 91, extension + " EXIF rotation is applied");
            File.Delete(filename);
        }

        var changedFile = Path.Combine(dir, "header-replaced.png");
        using (var image = new MagickImage(MagickColors.Red, 12, 10)) image.Write(changedFile);
        var oldMetadata = PhotoCodec.LoadMetadata(changedFile, options)!;
        using (var image = new MagickImage(new MagickColor("#408080"), 50, 46))
        {
            image.SetProfile(ColorProfiles.AdobeRGB1998);
            image.Write(changedFile);
        }
        File.SetLastWriteTime(changedFile, DateTime.Now.AddSeconds(3));
        var thresholdOptions = options with { MinDimensionToUseWIC = 40 };
        using (var reused = await PhotoCodec.LoadAsync(changedFile, thresholdOptions, metadata: oldMetadata))
        using (var fresh = await PhotoCodec.LoadAsync(changedFile, thresholdOptions))
        using (var reusedBitmap = BHelper.ToGdiPlusBitmap(reused.Image))
        using (var freshBitmap = BHelper.ToGdiPlusBitmap(fresh.Image))
            Check(reusedBitmap.Width == 50 && reusedBitmap.Height == 46
                && reusedBitmap.GetPixel(20, 20).ToArgb() == freshBitmap.GetPixel(20, 20).ToArgb(),
                "Replaced PNG rejects stale header and preserves the existing WIC threshold path");
        File.Delete(changedFile);
    }

    private static async Task TestMislabeledFilesAsync(string dir)
    {
        var headerFlag = typeof(IgMetadata).GetProperty("HeaderReadSucceeded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var pair in new[] { (MagickFormat.Jpeg, ".png"), (MagickFormat.Png, ".jpg"), (MagickFormat.Jpeg, ".heic") })
        {
            var filename = Path.Combine(dir, "mislabeled" + pair.Item2);
            using (var image = new MagickImage(MagickColors.Red, 64, 48)) image.Write(filename, pair.Item1);
            var metadata = PhotoCodec.LoadMetadata(filename)!;
            foreach (var reuse in pair.Item2 == ".heic" ? new[] { false } : new[] { true, false })
            {
                headerFlag.SetValue(metadata, reuse);
                using var decoded = await PhotoCodec.LoadAsync(filename, metadata: metadata);
                using var bitmap = BHelper.ToGdiPlusBitmap(decoded.Image);
                Check(bitmap.Width == 64 && bitmap.Height == 48 && bitmap.GetPixel(20, 20).R > 200,
                    "Actual " + pair.Item1 + " loads with " + pair.Item2 + " extension (header reuse=" + reuse + ")");
            }
            File.Delete(filename);
        }
    }

    private static void TestToolbarIcons(string dir)
    {
        static void Pump(Task task)
        {
            var timeout = Stopwatch.StartNew();
            while (!task.IsCompleted && timeout.ElapsedMilliseconds < 15000)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(1);
            }
            if (!task.IsCompleted) throw new TimeoutException("Toolbar update did not finish");
            task.GetAwaiter().GetResult();
        }
        static bool ImageDisposed(System.Drawing.Image image)
        {
            try { _ = image.Width; return false; } catch (ArgumentException) { return true; }
        }
        var svg = Path.Combine(dir, "toolbar-test.svg");
        File.WriteAllText(svg, "<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24'><rect width='24' height='24' fill='#1188cc'/></svg>");
        using var theme = new ImageGlass.UI.IgTheme(Path.Combine(App.StartUpDir(), "Themes", "Kobe"));
        using var otherTheme = new ImageGlass.UI.IgTheme(Path.Combine(App.StartUpDir(), "Themes", "Kobe-Light"));
        using var toolbar = new ImageGlass.UI.ModernToolbar { Theme = theme, IconHeight = 24 };
        toolbar.AddItem(new ToolbarItemModel { Id = "TestIcon", Image = svg });
        var button = toolbar.GetItem<System.Windows.Forms.ToolStripButton>("TestIcon")!;
        var first = button.Image!;
        Pump(toolbar.UpdateThemeAsync(24));
        Check(ReferenceEquals(first, button.Image), "Toolbar reuses initial icon at unchanged size/theme");
        Pump(Task.WhenAll(toolbar.UpdateThemeAsync(24), toolbar.UpdateThemeAsync(24)));
        Check(ReferenceEquals(first, button.Image), "Overlapping toolbar refreshes reuse the same bitmap");
        Pump(toolbar.UpdateThemeAsync(36));
        Check(button.Image!.Height == toolbar.IconHeight && !ReferenceEquals(first, button.Image) && ImageDisposed(first),
            "Toolbar size/DPI update replaces and disposes the preceding icon");
        var sized = button.Image!;
        toolbar.Theme = otherTheme;
        Pump(toolbar.UpdateThemeAsync(36));
        Check(!ReferenceEquals(sized, button.Image) && ImageDisposed(sized), "Theme change replaces and disposes the old icon");
        var dark = button.Image!;
        otherTheme.Settings.IsDarkMode = !otherTheme.Settings.IsDarkMode;
        Pump(toolbar.UpdateThemeAsync(36));
        Check(!ReferenceEquals(dark, button.Image) && ImageDisposed(dark), "Dark/light change invalidates icon reuse");
        var last = button.Image!;
        toolbar.ClearItems();
        Check(ImageDisposed(last), "Clearing toolbar releases rendered icons");
        otherTheme.ToolbarIcons.OpenFile = new System.Drawing.Bitmap(20, 20);
        toolbar.AddItem(new ToolbarItemModel { Id = "BorrowedIcon", Image = "OpenFile" });
        var borrowed = toolbar.GetItem<System.Windows.Forms.ToolStripButton>("BorrowedIcon")!.Image!;
        var pending = toolbar.UpdateThemeAsync(40);
        toolbar.Dispose();
        Pump(pending);
        Check(borrowed.Width == 20, "Toolbar disposal preserves borrowed theme bitmap ownership");
        File.Delete(svg);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<object> CreateWeakTrackedResource()
    {
        var pixels = new byte[1024 * 1024];
        MemoryProbe.Track(pixels, "test.collectible-buffer", pixels.Length, "Managed test buffer");
        return new WeakReference<object>(pixels);
    }

    private static void TestMemoryProbe()
    {
        if (!MemoryProbe.Enabled)
        {
            MemoryProbe.Capture("test.disabled");
            Check(MemoryProbe.LogPath == null, "Ordinary launches do not create a diagnostic log");
            return;
        }
        var weak = CreateWeakTrackedResource();
        MemoryProbe.Capture("test.weak-reference.before");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Check(!weak.TryGetTarget(out _), "Memory tracking does not keep image buffers alive");
        MemoryProbe.Capture("test.weak-reference.after");
        var rows = File.ReadAllLines(MemoryProbe.LogPath!).Select(x => System.Text.Json.JsonDocument.Parse(x)).ToArray();
        try
        {
            Check(rows.Length > 0 && rows.All(x => x.RootElement.GetProperty("privateCommitBytes").GetInt64() > 0),
                "Enabled probes produce parseable snapshots with real process counters");
            var stages = rows.Select(x => x.RootElement.GetProperty("stage").GetString()).ToArray();
            Check(stages.Contains("magick.decode.after") && stages.Contains("magick.to-wic-blocks.after") && stages.Contains("magick.intermediates-disposed"),
                "Decode, conversion and native disposal stages are recorded");
        }
        finally { foreach (var row in rows) row.Dispose(); }
    }
    private static async Task TestLiveVideoAsync(string dir)
    {
        var plain = Path.Combine(dir, "plain-motion-scan.jpg");
        using (var output = File.Create(plain)) output.SetLength(12 * 1024 * 1024);
        await BHelper.GetLiveVideoAsync(plain); // warm up async/file machinery
        var before = GC.GetTotalAllocatedBytes(true);
        Check(await BHelper.GetLiveVideoAsync(plain) == null, "Ordinary JPEG scan returns no motion video");
        var allocated = GC.GetTotalAllocatedBytes(true) - before;
        Check(allocated < 1024 * 1024, "Scanning a 12 MiB JPEG allocates less than 1 MiB (bounded buffer)");
        var payload = new byte[] { 0, 0, 0, 20, 0x66, 0x74, 0x79, 0x70, 1, 2, 3, 4, 0x66, 0x74, 0x79, 0x70 };
        foreach (var offset in new[] { 0, 1, 65529, 65530, 65531, 65532, 65533, 65534, 65535, 65536, 131065 })
        {
            var data = new byte[offset + payload.Length];
            payload.CopyTo(data, offset);
            var file = Path.Combine(dir, "motion-boundary.jpg");
            await File.WriteAllBytesAsync(file, data);
            var extracted = await BHelper.GetLiveVideoAsync(file);
            Check(extracted != null && extracted.SequenceEqual(payload), "Motion header/first match preserved at offset " + offset);
        }
        await File.WriteAllBytesAsync(plain, new byte[] { 0, 0, 0, 4, 0x66, 0x74, 0x79 });
        Check(await BHelper.GetLiveVideoAsync(plain) == null, "Truncated motion header is ignored");
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Check(await BHelper.GetLiveVideoAsync(plain, cancelled.Token) == null, "Cancelled motion detection does not interrupt viewing");
        Check(await BHelper.GetLiveVideoAsync(Path.Combine(dir, "missing-motion.jpg")) == null,
            "Missing JPEG does not interrupt optional motion detection");
        File.Delete(plain);
        File.Delete(Path.Combine(dir, "motion-boundary.jpg"));
    }

    private static async Task RunAsync(string dir)
    {
        var files = Enumerable.Range(0, 5).Select(x => Path.Combine(dir, $"{x}.jpg")).ToArray();
        foreach (var file in files)
        {
            using var fixture = new MagickImage(MagickColors.Red, 160, 120);
            fixture.SetProfile(ColorProfiles.SRGB);
            fixture.Write(file);
        }
        var options = new CodecReadOptions { UseEmbeddedThumbnailRawFormats = false, UseEmbeddedThumbnailOtherFormats = false };
        using (var data = await PhotoCodec.LoadAsync(files[0], options))
        using (var bitmap = BHelper.ToGdiPlusBitmap(data.Image))
            Check(bitmap != null && bitmap.Width == 160 && bitmap.GetPixel(40, 40).R > 200,
                "JPEG pixels remain readable after decoder intermediates are disposed (ICC enabled)");
        using (var data = await PhotoCodec.LoadAsync(files[0], options with { MinDimensionToUseWIC = 0 }))
        using (var bitmap = BHelper.ToGdiPlusBitmap(data.Image))
            Check(bitmap != null && bitmap.Height == 120, "WIC ownership transfer survives intermediate disposal");
        var png = Path.Combine(dir, "alpha.png");
        using (var fixture = new MagickImage(new MagickColor("#2288aa80"), 40, 30)) fixture.Write(png);
        using (var data = await PhotoCodec.LoadAsync(png, options))
        using (var bitmap = BHelper.ToGdiPlusBitmap(data.Image))
            Check(bitmap != null && bitmap.GetPixel(10, 10).A is >= 125 and <= 131, "PNG transparency is preserved");
        using (var decoded = BHelper.ToWicBitmapSource("data:image/jpeg;base64," + Convert.ToBase64String(File.ReadAllBytes(files[0]))))
        using (var bitmap = BHelper.ToGdiPlusBitmap(decoded))
            Check(bitmap != null && bitmap.Width == 160 && bitmap.GetPixel(40, 40).R > 200,
                "Base64 JPEG uses bounded conversion and remains readable after decoder disposal");
        using (var decoded = BHelper.ToWicBitmapSource("data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(png))))
        using (var bitmap = BHelper.ToGdiPlusBitmap(decoded))
            Check(bitmap != null && bitmap.GetPixel(10, 10).A is >= 125 and <= 131,
                "Base64 PNG bounded conversion preserves transparency");
        using (var thumbnail = await PhotoCodec.GetThumbnailAsync(files[0], 32, 32))
            Check(thumbnail != null && thumbnail.Width <= 32, "Thumbnail remains valid after temporary decoder disposal");
        using (var source = await PhotoCodec.LoadAsync(png, options))
        using (var resized = await BHelper.ResizeImageAsync(source.Image, 20, 15))
        using (var bitmap = BHelper.ToGdiPlusBitmap(resized))
            Check(bitmap != null && bitmap.Width == 20 && bitmap.Height == 15 && bitmap.GetPixel(10, 7).A is >= 125 and <= 131,
                "Resize output retains pixels/alpha after decoder and encoded streams are disposed");
        var base64File = Path.Combine(dir, "resize.b64");
        await File.WriteAllTextAsync(base64File, "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(png)));
        using (var data = await PhotoCodec.LoadAsync(base64File, options with { Width = 20, Height = 15 }))
        using (var bitmap = BHelper.ToGdiPlusBitmap(data.Image))
            Check(bitmap != null && bitmap.Width <= 20 && bitmap.Height <= 15 && bitmap.GetPixel(10, 7).A is >= 125 and <= 131,
                "Sized base64 load retains alpha and owns only its resized result");
        File.Delete(base64File);
        var gif = Path.Combine(dir, "animation.gif");
        var tiff = Path.Combine(dir, "pages.tiff");
        using (var frames = new MagickImageCollection())
        {
            frames.Add(new MagickImage(MagickColors.Red, 40, 30) { AnimationDelay = 10 });
            frames.Add(new MagickImage(MagickColors.Blue, 40, 30) { AnimationDelay = 10 });
            frames.Write(gif);
            frames.Write(tiff);
        }
        using (var data = await PhotoCodec.LoadAsync(gif, options with { FirstFrameOnly = false }))
        {
            Check(data.CanAnimate && data.FrameCount == 2 && data.Source is System.Drawing.Bitmap,
                "Native GIF decoding retains both frames");
        }
        // Exercise Magick's animation fallback and intermediate lifetime explicitly.
        using (var data = new IgMagickReadData
        {
            MultiFrameImage = new MagickImageCollection(gif), CanAnimate = true, FrameCount = 2,
        })
        using (var converted = new IgImgData(data))
        {
            data.Dispose();
            var animation = converted.Source as AnimatedImg;
            Check(animation?.FrameCount == 2, "Magick animation fallback retains both frames after decoder disposal");
            var first = animation!.GetFrame(0)!;
            var second = animation.GetFrame(1)!;
            // Repeated access must return the same materialized frame, not allocate it again.
            Check(ReferenceEquals(first, animation.GetFrame(0)), "Animation frames are materialized exactly once");
            Check(first != second, "Animation frames remain individually accessible after decoder disposal");
        }
        var rgbTiff = Path.Combine(dir, "pages-rgb.tiff");
        using (var rgbFrames = new MagickImageCollection())
        {
            rgbFrames.Add(new MagickImage(MagickColors.Red, 40, 30) { ColorType = ColorType.TrueColor, Depth = 8 });
            rgbFrames.Add(new MagickImage(MagickColors.Blue, 40, 30) { ColorType = ColorType.TrueColor, Depth = 8 });
            rgbFrames.Write(rgbTiff);
        }
        using (var decoder = WicNet.WicBitmapDecoder.Load(rgbTiff))
        {
            for (var index = 0; index < 2; index++)
            {
                using var frame = decoder.GetFrame(index);
                frame.ConvertTo(WicNet.WicPixelFormat.GUID_WICPixelFormat32bppPBGRA);
                using var bitmap = BHelper.ToGdiPlusBitmap(frame);
                var color = bitmap!.GetPixel(20, 15);
                Check(index == 0 ? color.R > 200 && color.B < 20 : color.B > 200 && color.R < 20,
                    "True-color TIFF frame " + index + " retains its actual native WIC pixels");
            }
        }
        using (var data = await PhotoCodec.LoadAsync(tiff, options with { FirstFrameOnly = false }))
            Check(data.Source is WicNet.WicBitmapDecoder && data.FrameCount == 2, "Multipage TIFF decoder survives intermediate disposal");

        using (var cache = new NeighbourImageCache(files) { ReadOptions = options })
        {
            Check(cache.MaxQueue == 0, "Neighbour preloading defaults to zero");
            await cache.GetAsync(2);
            await cache.WaitForPreloadingAsync();
            Check(cache.IsCached(2) && Enumerable.Range(0, 5).Where(i => i != 2).All(i => !cache.IsCached(i)),
                "Zero preloads only the requested image");
            cache.StartCaching(2, false);
            Check(cache.WaitForPreloadingAsync().IsCompleted, "Zero StartCaching has no pending worker");
            cache.StartCaching(1, true);
            await cache.WaitForPreloadingAsync();
            Check(!cache.IsCached(1), "Zero also suppresses the list-loaded current-image preload request");
            await cache.GetAsync(3);
            Check(!cache.IsCached(2) && cache.IsCached(3), "Zero releases the previous full-resolution image");
            cache.MaxQueue = 1;
            cache.StartCaching(3, false);
            await cache.WaitForPreloadingAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Check(cache.IsCached(2) && cache.IsCached(3) && cache.IsCached(4) && !cache.IsCached(0) && !cache.IsCached(1),
                "One preloads exactly one neighbour in each direction");
            cache.MaxQueue = 0;
            Check(cache.IsCached(3) && !cache.IsCached(2) && !cache.IsCached(4), "Changing to zero evicts neighbouring bitmaps");
            cache.MaxQueue = 10;
            cache.StartCaching(3, false);
            cache.Remove(1);
            cache.Add(files[1], 1);
            await cache.GetAsync(0);
            await cache.WaitForPreloadingAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Check(cache.GetFilePath(0) == files[0] && cache.IsCached(0), "Preload cancellation tolerates insertion and removal without stale indices");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try { await cache.GetAsync(1, tokenSrc: cancelled); throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Check(true, "Cancelled foreground load terminates without IsDone polling"); }
            cache.MaxQueue = 0;
            await cache.GetAsync(1);
            Check(cache.IsCached(1), "Cache remains usable after cancellation");
        }
        using (var cache = new NeighbourImageCache([files[0]]) { ReadOptions = options, MaxQueue = 10 })
        {
            await cache.GetAsync(0);
            await cache.WaitForPreloadingAsync();
            Check(cache.IsCached(0), "Single-image folder does not queue duplicates or loop forever");
        }
        var disposable = Path.Combine(dir, "delete-fixture.txt");
        File.WriteAllText(disposable, "Smoke test owns this file.");
        Check(BHelper.DeleteFile(disposable, false) && !File.Exists(disposable), "Permanent deletion returns success only after deleting the test file");
        using var idle = new NeighbourImageCache(files);
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        await Task.Delay(1200);
        process.Refresh();
        Console.WriteLine($"Idle smoke-test process CPU over 1.2 s: {(process.TotalProcessorTime - cpu).TotalMilliseconds:F1} ms (includes runtime).");
    }

    private static void TestBitmapConversion()
    {
        foreach (var withAlpha in new[] { false, true })
        {
            const uint width = 127, height = 521;
            var channels = withAlpha ? 4 : 3;
            var samples = new byte[width * height * channels];
            for (var i = 0; i < samples.Length; i++) samples[i] = (byte)((i * 31 + i / 7) % 256);
            using var image = new MagickImage();
            image.ReadPixels(samples, new PixelReadSettings(width, height, StorageType.Char,
                withAlpha ? PixelMapping.RGBA : PixelMapping.RGB));
            image.ColorSpace = ColorSpace.sRGB;
            CheckConversion(image, withAlpha ? "RGBA gradient including transparent pixels" : "RGB gradient and padded stride");

            image.ColorSpace = ColorSpace.RGB;
            CheckConversion(image, withAlpha ? "Linear RGB with alpha" : "Linear RGB conversion");
            image.ColorSpace = ColorSpace.Gray;
            CheckConversion(image, withAlpha ? "Grayscale with alpha" : "Grayscale conversion");
            image.ColorSpace = ColorSpace.CMYK;
            CheckConversion(image, withAlpha ? "CMYK with alpha" : "CMYK conversion");
        }
        using (var small = new MagickImage(MagickColors.Red, 1, 1)) CheckConversion(small, "Single pixel");
        using (var wide = new MagickImage(MagickColors.Blue, 23001, 2)) CheckConversion(wide, "Horizontal block boundaries");

        // ImageMagick's high-precision quantization and ICC processing remain in use.
        using var precision = new MagickImage(MagickColors.Red, 193, 177);
        using (var pixels = precision.GetPixels())
        {
            var values = new float[193 * 177 * 3];
            for (var i = 0; i < values.Length; i++) values[i] = (i * 131.25f) % 65535;
            pixels.SetPixels(values);
        }
        CheckConversion(precision, "HDRI fractional quantum values");
        precision.SetProfile(ColorProfiles.AdobeRGB1998);
        precision.TransformColorSpace(ColorProfiles.AdobeRGB1998, ColorProfiles.SRGB);
        CheckConversion(precision, "ICC-transformed pixels");
    }

    private static void CheckConversion(MagickImage image, string description)
    {
        var originalColorSpace = image.ColorSpace;
        WicNet.WicBitmapSource? reference;
        try { reference = BHelper.ToWicBitmapSource(image.ToBitmapSource(), image.HasAlpha); }
        catch (WicNet.WicNetException legacyError)
        {
            Exception? directError = null;
            try { using var unused = BHelper.ToWicBitmapSource(image); }
            catch (Exception ex) { directError = ex; }
            Check(directError?.GetType() == legacyError.GetType(), description + ": unsupported raw WIC conversion behaves as before");
            return;
        }
        using var referenceLifetime = reference;
        using var result = BHelper.ToWicBitmapSource(image)!;
        Check(reference != null && result.Width == reference.Width && result.Height == reference.Height && result.PixelFormat == reference.PixelFormat,
            description + ": dimensions and pixel format match the old bridge");
        // Compare rendered channels exactly; 32bpp BGR's unused byte is unspecified.
        var actualPixels = result.CopyPixels();
        var expectedPixels = reference!.CopyPixels();
        if (!image.HasAlpha)
            for (var i = 3; i < actualPixels.Length; i += 4) { actualPixels[i] = 0; expectedPixels[i] = 0; }
        Check(actualPixels.SequenceEqual(expectedPixels), description + ": rendered channels match the old bridge byte-for-byte");
        Check(result.DpiX == reference.DpiX && result.DpiY == reference.DpiY && image.ColorSpace == originalColorSpace,
            description + ": resolution and source color space are preserved");
    }
    private static void TestAlphaOutput()
    {
        using var image = new MagickImage(MagickColors.Red, 31, 23);
        using var reference = BHelper.ToWicBitmapSource(image.ToBitmapSource(), true);
        using var result = BHelper.ToWicBitmapSource(image, outputHasAlpha: true);
        Check(result != null && reference != null && result.PixelFormat == reference.PixelFormat
            && result.CopyPixels().SequenceEqual(reference.CopyPixels()),
            "Opaque forced-PBGRA export matches the old JPEG XR bridge");
    }

    private static void TestNavigation(string dir)
    {
        Config.Load();
        Check(Config.NeighbourPreloadCount == 0 && !Config.EnableRealTimeFileUpdate, "App defaults disable preloading and live folder monitoring");
        using var form = new FrmMain();
        Local.FrmMain = form;
        var resolve = typeof(FrmMain).GetMethod("ResolveExistingImageIndex", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var a = Path.Combine(dir, "0.jpg");
        var b = Path.Combine(dir, "1.jpg");
        var c = Path.Combine(dir, "2.jpg");
        var missing = Path.Combine(dir, "missing.jpg");
        void Snapshot(params string[] paths)
        {
            Local.InitImageList(paths);
            form.Gallery.Items.Clear();
            foreach (var path in paths) form.Gallery.Items.Add(path);
            Local.CurrentIndex = 0;
        }
        int Resolve(int index, int direction) => (int)resolve.Invoke(form, [index, direction])!;
        Config.EnableLoopBackNavigation = true;
        Snapshot(a, missing, c);
        Check(Resolve(1, 1) == 1 && Local.Images.GetFilePath(1) == c && Local.Images.Length == 2,
            "Next skips and removes a missing snapshot entry");
        Snapshot(a, missing, c);
        Local.CurrentIndex = 2;
        Check(Resolve(1, -1) == 0 && Local.Images.GetFilePath(0) == a, "Previous skips a missing entry in the same direction");
        Snapshot(a, missing, missing + "2");
        Check(Resolve(1, 1) == 0 && Local.Images.Length == 1, "Missing entries at the end wrap without errors");
        Snapshot(missing, missing + "2");
        Check(Resolve(0, 1) == -1 && Local.Images.Length == 0 && Local.CurrentIndex == -1, "An all-missing folder terminates and clears the image");
        Snapshot(c, a, b);
        Check(Resolve(1, 1) == 1 && Local.Images.FilePaths.SequenceEqual(new[] { c, a, b }), "Navigation preserves supplied Explorer snapshot order");
        Config.EnableLoopBackNavigation = false;
        Snapshot(a, missing);
        Check(Resolve(1, 1) == -1 && Local.Images.Length == 1, "Missing entry at the end honours disabled wrapping");
        Local.Images.Dispose();
    }
}
