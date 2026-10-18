// ImageCrystal modifications by xenon, 2026-10-18; see NOTICE.ImageCrystal.txt.
using DirectN;
using ImageGlass.Base.Diagnostics;
using ImageMagick;
using System.Runtime.InteropServices;
using WicNet;

namespace ImageGlass.Base;

public partial class BHelper
{
    // Keep WPF type references out of the normal converter's JIT compilation.
    // This path is used only by the opt-in diagnostic comparison.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WicBitmapSource? ToWicBitmapSourceLegacy(IMagickImage<float> source, bool hasAlpha)
        => ToWicBitmapSource(source.ToBitmapSource(), hasAlpha);

    /// <summary>
    /// Exports Magick's already-processed pixels into WIC, without a full-image
    /// managed array or WPF bitmap. Block arrays stay below the large object heap.
    /// </summary>
    public static WicBitmapSource? ToWicBitmapSource(IMagickImage<float>? source, bool? outputHasAlpha = null)
    {
        if (source == null) return null;
        using var memoryPhase = MemoryProbe.Begin("magick.to-wic-blocks");

        // Diagnostic A/B switch only; preserves the old path for comparisons.
        if (Environment.GetEnvironmentVariable("IMAGECRYSTAL_LEGACY_BITMAP_BRIDGE") == "1")
            return ToWicBitmapSourceLegacy(source, outputHasAlpha ?? source.HasAlpha);

        MagickImage? converted = null;
        WicBitmapSource? bitmap = null;
        try
        {
            var image = source;
            var mapping = PixelMapping.RGB;
            var format = WicPixelFormat.GUID_WICPixelFormat24bppRGB;
            var components = 3;

            // Match Magick.NET.SystemWindowsMedia's ToBitmapSource semantics.
            if (image.ColorSpace == ColorSpace.CMYK && !image.HasAlpha)
            {
                mapping = PixelMapping.CMYK;
                format = WicPixelFormat.GUID_WICPixelFormat32bppCMYK;
                components = 4;
            }
            else
            {
                if (image.ColorSpace != ColorSpace.sRGB)
                {
                    converted = (MagickImage)image.Clone();
                    converted.ColorSpace = ColorSpace.sRGB;
                    image = converted;
                }
                if (image.HasAlpha)
                {
                    mapping = PixelMapping.BGRA;
                    format = WicPixelFormat.GUID_WICPixelFormat32bppBGRA;
                    components = 4;
                }
            }

            var width = checked((int)image.Width);
            var height = checked((int)image.Height);
            bitmap = new WicBitmapSource(width, height, format);
            using (var nativeBitmap = bitmap.AsBitmap())
                nativeBitmap.Object.SetResolution(96, 96).ThrowOnError();

            using var pixels = image.GetPixelsUnsafe();
            const int maxBlockBytes = 64 * 1024;
            var blockWidth = Math.Min(width, maxBlockBytes / components);
            var blockHeight = Math.Max(1, maxBlockBytes / checked(blockWidth * components));
            MemoryProbe.Capture("magick.pixel-block-layout", new { width, height, components, blockWidth, blockHeight, maxBlockBytes });
            bitmap.WithLock(WICBitmapLockFlags.WICBitmapLockWrite, locked =>
            {
                for (var y = 0; y < height; y += blockHeight)
                {
                    var rows = Math.Min(blockHeight, height - y);
                    for (var x = 0; x < width; x += blockWidth)
                    {
                        var columns = Math.Min(blockWidth, width - x);
                        var rowBytes = checked(columns * components);
                        var bytes = pixels.ToByteArray(x, y, (uint)columns, (uint)rows, mapping)
                            ?? throw new InvalidOperationException("ImageMagick did not export the pixel block.");
                        if (bytes.Length != checked(rowBytes * rows))
                            throw new InvalidOperationException("Unexpected ImageMagick pixel block size.");

                        for (var row = 0; row < rows; row++)
                        {
                            var offset = checked((y + row) * locked.Stride + x * components);
                            Marshal.Copy(bytes, row * rowBytes, IntPtr.Add(locked.DataPointer, offset), rowBytes);
                        }
                    }
                }
            });

            if (MemoryProbe.Enabled)
                MemoryProbe.Track(bitmap, "magick.direct-wic", (long)bitmap.DefaultStride * height,
                    "Native WIC pixel buffer; 32bpp output conversion may be lazy");

            bitmap.ConvertTo((outputHasAlpha ?? source.HasAlpha)
                ? WicPixelFormat.GUID_WICPixelFormat32bppPBGRA
                : WicPixelFormat.GUID_WICPixelFormat32bppBGR);
            return bitmap;
        }
        catch
        {
            MemoryProbe.MarkDisposed(bitmap);
            bitmap?.Dispose();
            throw;
        }
        finally { converted?.Dispose(); }
    }
}
