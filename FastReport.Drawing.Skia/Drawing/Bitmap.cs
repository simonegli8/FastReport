using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace System.Drawing;

/// <summary>A raster image stored as a premultiplied BGRA <see cref="SKBitmap"/>.</summary>
public sealed class Bitmap : Image
{
    private SKBitmap bitmap;
    private SKImage? cachedImage;
    private PixelFormat pixelFormat;
    private float dpiX = 96;
    private float dpiY = 96;

    public Bitmap(int width, int height)
        : this(width, height, PixelFormat.Format32bppArgb)
    {
    }

    public Bitmap(int width, int height, PixelFormat format)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Parameter is not valid.");

        bool hasAlpha = IsAlphaPixelFormat(format) || (format & PixelFormat.Indexed) != 0;
        bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, hasAlpha ? SKAlphaType.Premul : SKAlphaType.Opaque));
        bitmap.Erase(hasAlpha ? SKColors.Transparent : SKColors.Black);
        pixelFormat = format;
    }

    public Bitmap(int width, int height, Graphics g)
        : this(width, height)
    {
        ArgumentNullException.ThrowIfNull(g);
        dpiX = g.DpiX;
        dpiY = g.DpiY;
    }

    public Bitmap(Image original)
        : this(original, original.Width, original.Height)
    {
    }

    public Bitmap(Image original, Size newSize)
        : this(original, newSize.Width, newSize.Height)
    {
    }

    public Bitmap(Image original, int width, int height)
        : this(width, height)
    {
        ArgumentNullException.ThrowIfNull(original);
        using var canvas = new SKCanvas(bitmap);
        var sampling = width == original.Width && height == original.Height
            ? new SKSamplingOptions(SKFilterMode.Nearest)
            : new SKSamplingOptions(SKCubicResampler.Mitchell);
        canvas.DrawImage(original.GetSKImage(), new SKRect(0, 0, width, height), sampling);
    }

    public Bitmap(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        (bitmap, pixelFormat) = Decode(memory.ToArray(), out var format);
        RawFormat = format;
    }

    public Bitmap(Stream stream, bool useIcm)
        : this(stream)
    {
    }

    public Bitmap(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        (bitmap, pixelFormat) = Decode(File.ReadAllBytes(filename), out var format);
        RawFormat = format;
    }

    public Bitmap(string filename, bool useIcm)
        : this(filename)
    {
    }

    /// <summary>Wraps a Skia bitmap. Bitmaps not in premultiplied BGRA are converted; the instance takes ownership.</summary>
    public Bitmap(SKBitmap skBitmap)
    {
        ArgumentNullException.ThrowIfNull(skBitmap);
        if (skBitmap.ColorType == SKColorType.Bgra8888 && skBitmap.AlphaType is SKAlphaType.Premul or SKAlphaType.Opaque)
        {
            bitmap = skBitmap;
        }
        else
        {
            bitmap = new SKBitmap(new SKImageInfo(skBitmap.Width, skBitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(bitmap))
                canvas.DrawBitmap(skBitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
            skBitmap.Dispose();
        }
        pixelFormat = bitmap.AlphaType == SKAlphaType.Opaque ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb;
    }

    public override int Width => bitmap.Width;

    public override int Height => bitmap.Height;

    public override float HorizontalResolution => dpiX;

    public override float VerticalResolution => dpiY;

    public override PixelFormat PixelFormat => pixelFormat;

    /// <summary>
    /// The underlying Skia bitmap. Accessing it invalidates the cached snapshot used for drawing,
    /// so pixel changes made through it are picked up.
    /// </summary>
    public SKBitmap SKBitmap
    {
        get
        {
            InvalidateCache();
            return bitmap;
        }
    }

    public void SetResolution(float xDpi, float yDpi)
    {
        if (xDpi <= 0 || yDpi <= 0)
            throw new ArgumentException("Parameter is not valid.");
        dpiX = xDpi;
        dpiY = yDpi;
    }

    public Color GetPixel(int x, int y)
    {
        CheckPixel(x, y);
        return bitmap.GetPixel(x, y).ToColor();
    }

    public void SetPixel(int x, int y, Color color)
    {
        CheckPixel(x, y);
        bitmap.SetPixel(x, y, color.ToSKColor());
        InvalidateCache();
    }

    /// <summary>Makes the color of the lower-left pixel transparent, as GDI+ does.</summary>
    public void MakeTransparent() => MakeTransparent(GetPixel(0, Height - 1));

    public void MakeTransparent(Color transparentColor)
    {
        EnsureAlpha();
        var key = transparentColor.ToSKColor();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (bitmap.GetPixel(x, y) == key)
                    bitmap.SetPixel(x, y, SKColors.Transparent);
            }
        }
        InvalidateCache();
    }

    public Bitmap Clone(Rectangle rect, PixelFormat format)
    {
        if (rect.Width <= 0 || rect.Height <= 0 || !new Rectangle(0, 0, Width, Height).Contains(rect))
            throw new ArgumentException("Out of memory.", nameof(rect));

        var copy = new SKBitmap(new SKImageInfo(rect.Width, rect.Height, SKColorType.Bgra8888, bitmap.AlphaType));
        using (var canvas = new SKCanvas(copy))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(bitmap, rect.ToSKRect(), new SKRect(0, 0, rect.Width, rect.Height), new SKSamplingOptions(SKFilterMode.Nearest), null);
        }

        return new Bitmap(copy) { pixelFormat = format == PixelFormat.DontCare ? pixelFormat : format, dpiX = dpiX, dpiY = dpiY, RawFormat = RawFormat };
    }

    public Bitmap Clone(RectangleF rect, PixelFormat format) => Clone(Rectangle.Round(rect), format);

    public override object Clone() => Clone(new Rectangle(0, 0, Width, Height), pixelFormat);

    public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format) =>
        LockBits(rect, flags, format, new BitmapData());

    /// <summary>
    /// Copies pixels into an unmanaged buffer in the requested format. Supported formats are
    /// 32bppArgb, 32bppPArgb, 32bppRgb and 24bppRgb.
    /// </summary>
    public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format, BitmapData bitmapData)
    {
        ArgumentNullException.ThrowIfNull(bitmapData);
        if (!new Rectangle(0, 0, Width, Height).Contains(rect) || rect.Width <= 0 || rect.Height <= 0)
            throw new ArgumentException("Parameter is not valid.", nameof(rect));
        if (format is not (PixelFormat.Format32bppArgb or PixelFormat.Format32bppPArgb or PixelFormat.Format32bppRgb or PixelFormat.Format24bppRgb))
            throw new NotSupportedException($"LockBits with {format} is not supported by the Skia backend.");

        int bpp = GetPixelFormatSize(format);
        int stride = (rect.Width * bpp + 31) / 32 * 4;
        var buffer = Marshal.AllocHGlobal(stride * rect.Height);

        bitmapData.Width = rect.Width;
        bitmapData.Height = rect.Height;
        bitmapData.Stride = stride;
        bitmapData.PixelFormat = format;
        bitmapData.Scan0 = buffer;
        bitmapData.LockedRect = rect;
        bitmapData.LockMode = flags;
        bitmapData.OwnsBuffer = true;

        if ((flags & ImageLockMode.ReadOnly) != 0)
            CopyOut(rect, format, buffer, stride);

        return bitmapData;
    }

    public void UnlockBits(BitmapData bitmapdata)
    {
        ArgumentNullException.ThrowIfNull(bitmapdata);
        if (!bitmapdata.OwnsBuffer)
            return;

        try
        {
            if ((bitmapdata.LockMode & ImageLockMode.WriteOnly) != 0)
            {
                CopyIn(bitmapdata.LockedRect, bitmapdata.PixelFormat, bitmapdata.Scan0, bitmapdata.Stride);
                InvalidateCache();
            }
        }
        finally
        {
            Marshal.FreeHGlobal(bitmapdata.Scan0);
            bitmapdata.Scan0 = IntPtr.Zero;
            bitmapdata.OwnsBuffer = false;
        }
    }

    public override void RotateFlip(RotateFlipType rotateFlipType)
    {
        int type = (int)rotateFlipType;
        int rotation = type & 3;
        bool flipX = type >= 4;
        if (rotation == 0 && !flipX)
            return;

        int w = Width, h = Height;
        int newWidth = rotation % 2 == 0 ? w : h;
        int newHeight = rotation % 2 == 0 ? h : w;

        // Maps source pixel coordinates to the rotated (clockwise) and then horizontally flipped result.
        var matrix = rotation switch
        {
            1 => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            2 => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            3 => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity,
        };
        if (flipX)
            matrix = SKMatrix.Concat(new SKMatrix(-1, 0, newWidth, 0, 1, 0, 0, 0, 1), matrix);

        var result = new SKBitmap(new SKImageInfo(newWidth, newHeight, SKColorType.Bgra8888, bitmap.AlphaType));
        using (var canvas = new SKCanvas(result))
        using (var image = SKImage.FromBitmap(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.SetMatrix(matrix);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        }

        bitmap.Dispose();
        bitmap = result;
        if (rotation % 2 == 1)
            (dpiX, dpiY) = (dpiY, dpiX);
        InvalidateCache();
    }

    internal override SKImage GetSKImage() => cachedImage ??= SKImage.FromBitmap(bitmap);

    internal override SKBitmap GetSKBitmap() => bitmap;

    /// <summary>Called whenever the pixels change so that the next draw takes a fresh snapshot.</summary>
    internal void InvalidateCache()
    {
        cachedImage?.Dispose();
        cachedImage = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            InvalidateCache();
            bitmap.Dispose();
        }
        base.Dispose(disposing);
    }

    private static (SKBitmap Bitmap, PixelFormat Format) Decode(byte[] data, out ImageFormat rawFormat)
    {
        using var skData = SKData.CreateCopy(data);
        using var codec = SKCodec.Create(skData) ?? throw new ArgumentException("Parameter is not valid.");

        bool opaque = codec.Info.AlphaType == SKAlphaType.Opaque;
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, opaque ? SKAlphaType.Opaque : SKAlphaType.Premul);
        var decoded = SKBitmap.Decode(codec, info) ?? throw new ArgumentException("Parameter is not valid.");

        rawFormat = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png => ImageFormat.Png,
            SKEncodedImageFormat.Jpeg => ImageFormat.Jpeg,
            SKEncodedImageFormat.Gif => ImageFormat.Gif,
            SKEncodedImageFormat.Bmp => ImageFormat.Bmp,
            SKEncodedImageFormat.Ico => ImageFormat.Icon,
            SKEncodedImageFormat.Webp => ImageFormat.Webp,
            SKEncodedImageFormat.Heif => ImageFormat.Heif,
            _ => ImageFormat.MemoryBmp,
        };
        return (decoded, opaque ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb);
    }

    private void CopyOut(Rectangle rect, PixelFormat format, IntPtr buffer, int stride)
    {
        using var pixmap = bitmap.PeekPixels();
        if (format == PixelFormat.Format24bppRgb)
        {
            var info = new SKImageInfo(rect.Width, rect.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var temp = new byte[info.BytesSize];
            unsafe
            {
                fixed (byte* src = temp)
                {
                    pixmap.ReadPixels(info, (IntPtr)src, info.RowBytes, rect.X, rect.Y);
                    byte* dst = (byte*)buffer;
                    for (int y = 0; y < rect.Height; y++)
                    {
                        byte* srcRow = src + y * info.RowBytes;
                        byte* dstRow = dst + y * stride;
                        for (int x = 0; x < rect.Width; x++)
                        {
                            dstRow[x * 3] = srcRow[x * 4];
                            dstRow[x * 3 + 1] = srcRow[x * 4 + 1];
                            dstRow[x * 3 + 2] = srcRow[x * 4 + 2];
                        }
                    }
                }
            }
            return;
        }

        var alphaType = format == PixelFormat.Format32bppPArgb ? SKAlphaType.Premul : SKAlphaType.Unpremul;
        pixmap.ReadPixels(new SKImageInfo(rect.Width, rect.Height, SKColorType.Bgra8888, alphaType), buffer, stride, rect.X, rect.Y);
    }

    private void CopyIn(Rectangle rect, PixelFormat format, IntPtr buffer, int stride)
    {
        var dstInfo = new SKImageInfo(rect.Width, rect.Height, SKColorType.Bgra8888, bitmap.AlphaType);
        var dst = bitmap.GetAddress(rect.X, rect.Y);

        if (format is PixelFormat.Format24bppRgb or PixelFormat.Format32bppRgb)
        {
            // Expand to opaque BGRA first.
            var info = new SKImageInfo(rect.Width, rect.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var temp = new byte[info.BytesSize];
            int bytesPerPixel = format == PixelFormat.Format24bppRgb ? 3 : 4;
            unsafe
            {
                byte* src = (byte*)buffer;
                fixed (byte* tmp = temp)
                {
                    for (int y = 0; y < rect.Height; y++)
                    {
                        byte* srcRow = src + y * stride;
                        byte* tmpRow = tmp + y * info.RowBytes;
                        for (int x = 0; x < rect.Width; x++)
                        {
                            tmpRow[x * 4] = srcRow[x * bytesPerPixel];
                            tmpRow[x * 4 + 1] = srcRow[x * bytesPerPixel + 1];
                            tmpRow[x * 4 + 2] = srcRow[x * bytesPerPixel + 2];
                            tmpRow[x * 4 + 3] = 255;
                        }
                    }

                    using var source = new SKPixmap(info, (IntPtr)tmp, info.RowBytes);
                    source.ReadPixels(dstInfo, dst, bitmap.RowBytes);
                }
            }
            return;
        }

        var alphaType = format == PixelFormat.Format32bppPArgb ? SKAlphaType.Premul : SKAlphaType.Unpremul;
        using var pixmap = new SKPixmap(new SKImageInfo(rect.Width, rect.Height, SKColorType.Bgra8888, alphaType), buffer, stride);
        pixmap.ReadPixels(dstInfo, dst, bitmap.RowBytes);
    }

    private void EnsureAlpha()
    {
        if (bitmap.AlphaType != SKAlphaType.Opaque)
            return;

        var withAlpha = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(withAlpha))
            canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest), null);
        bitmap.Dispose();
        bitmap = withAlpha;
        pixelFormat = PixelFormat.Format32bppArgb;
    }

    private void CheckPixel(int x, int y)
    {
        if (x < 0 || x >= Width)
            throw new ArgumentOutOfRangeException(nameof(x), "Parameter must be positive and < Width.");
        if (y < 0 || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(y), "Parameter must be positive and < Height.");
    }
}
