using ImageGlass;
using ImageGlass.Base;
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

internal static class SmokeTests
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
            var dir = Path.GetFullPath(args[0]);
            Directory.CreateDirectory(dir);
            RunAsync(dir).GetAwaiter().GetResult();
            TestNavigation(dir);
            Console.WriteLine($"SUCCESS: {_checks} checks passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
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
        using (var thumbnail = await PhotoCodec.GetThumbnailAsync(files[0], 32, 32))
            Check(thumbnail != null && thumbnail.Width <= 32, "Thumbnail remains valid after temporary decoder disposal");
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
