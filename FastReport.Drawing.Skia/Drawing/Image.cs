using System.Drawing.Imaging;
using SkiaSharp;

namespace System.Drawing;

/// <summary>Base class for raster images.</summary>
/// <remarks>
/// Vector formats (EMF/WMF) and TIFF/GIF encoding are not available in Skia: decoding them throws
/// <see cref="ArgumentException"/>, and saving to them throws <see cref="NotSupportedException"/>.
/// </remarks>
public abstract class Image : ICloneable, IDisposable
{
    private const int DefaultJpegQuality = 75;

    private protected Image()
    {
    }

    public delegate bool GetThumbnailImageAbort();

    public abstract int Width { get; }

    public abstract int Height { get; }

    public Size Size => new(Width, Height);

    public SizeF PhysicalDimension => new(Width, Height);

    public abstract float HorizontalResolution { get; }

    public abstract float VerticalResolution { get; }

    public abstract PixelFormat PixelFormat { get; }

    public ImageFormat RawFormat { get; private protected set; } = ImageFormat.MemoryBmp;

    public int Flags => 0;

    public object? Tag { get; set; }

    public static Image FromFile(string filename) => new Bitmap(filename);

    public static Image FromFile(string filename, bool useEmbeddedColorManagement) => new Bitmap(filename);

    public static Image FromStream(Stream stream) => new Bitmap(stream);

    public static Image FromStream(Stream stream, bool useEmbeddedColorManagement) => new Bitmap(stream);

    public static Image FromStream(Stream stream, bool useEmbeddedColorManagement, bool validateImageData) => new Bitmap(stream);

    public static int GetPixelFormatSize(PixelFormat pixfmt) => ((int)pixfmt >> 8) & 0xFF;

    public static bool IsAlphaPixelFormat(PixelFormat pixfmt) => (pixfmt & PixelFormat.Alpha) != 0;

    public static bool IsCanonicalPixelFormat(PixelFormat pixfmt) => (pixfmt & PixelFormat.Canonical) != 0;

    public static bool IsExtendedPixelFormat(PixelFormat pixfmt) => (pixfmt & PixelFormat.Extended) != 0;

    public RectangleF GetBounds(ref GraphicsUnit pageUnit)
    {
        pageUnit = GraphicsUnit.Pixel;
        return new RectangleF(0, 0, Width, Height);
    }

    public Image GetThumbnailImage(int thumbWidth, int thumbHeight, GetThumbnailImageAbort? callback, IntPtr callbackData) =>
        new Bitmap(this, thumbWidth, thumbHeight);

    public abstract void RotateFlip(RotateFlipType rotateFlipType);

    public void Save(string filename) => Save(filename, FormatFromExtension(filename));

    public void Save(string filename, ImageFormat format)
    {
        using var stream = File.Create(filename);
        Save(stream, format);
    }

    public void Save(string filename, ImageCodecInfo encoder, EncoderParameters? encoderParams)
    {
        using var stream = File.Create(filename);
        Save(stream, encoder, encoderParams);
    }

    public void Save(Stream stream, ImageFormat format)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(format);
        Encode(stream, format, DefaultJpegQuality);
    }

    public void Save(Stream stream, ImageCodecInfo encoder, EncoderParameters? encoderParams)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(encoder);

        int quality = DefaultJpegQuality;
        foreach (var parameter in encoderParams?.Param ?? [])
        {
            if (parameter?.Encoder.Guid == Imaging.Encoder.Quality.Guid && parameter.NumberOfValues > 0)
                quality = (int)Math.Clamp(parameter.Values[0], 0, 100);
        }

        Encode(stream, new ImageFormat(encoder.FormatID), quality);
    }

    public abstract object Clone();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    /// <summary>Returns an immutable snapshot of the pixels for drawing. The image owns the result.</summary>
    internal abstract SKImage GetSKImage();

    internal abstract SKBitmap GetSKBitmap();

    private void Encode(Stream stream, ImageFormat format, int quality)
    {
        var bitmap = GetSKBitmap();
        using var pixmap = bitmap.PeekPixels();

        if (format.Equals(ImageFormat.Png))
            Check(pixmap.Encode(stream, SKEncodedImageFormat.Png, 100));
        else if (format.Equals(ImageFormat.Jpeg))
            Check(pixmap.Encode(stream, SKEncodedImageFormat.Jpeg, quality));
        else if (format.Equals(ImageFormat.Webp))
            Check(pixmap.Encode(stream, SKEncodedImageFormat.Webp, quality));
        else if (format.Equals(ImageFormat.Bmp) || format.Equals(ImageFormat.MemoryBmp))
            WriteBmp(stream, pixmap);
        else
            throw new NotSupportedException($"Saving images as {format} is not supported by the Skia backend.");

        static void Check(bool success)
        {
            if (!success)
                throw new InvalidOperationException("Image encoding failed.");
        }
    }

    /// <summary>Writes a bottom-up 32-bit BGRA BMP (Skia has no BMP encoder).</summary>
    private void WriteBmp(Stream stream, SKPixmap pixmap)
    {
        int width = pixmap.Width, height = pixmap.Height;
        int rowBytes = width * 4;
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var pixels = new byte[rowBytes * height];
        unsafe
        {
            fixed (byte* ptr = pixels)
                pixmap.ReadPixels(info, (IntPtr)ptr, rowBytes, 0, 0);
        }

        const int headerSize = 14 + 40;
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(headerSize + pixels.Length);
        writer.Write(0);
        writer.Write(headerSize);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)32);
        writer.Write(0); // BI_RGB
        writer.Write(pixels.Length);
        writer.Write((int)Math.Round(HorizontalResolution / 0.0254));
        writer.Write((int)Math.Round(VerticalResolution / 0.0254));
        writer.Write(0);
        writer.Write(0);
        for (int y = height - 1; y >= 0; y--)
            writer.Write(pixels, y * rowBytes, rowBytes);
    }

    private static ImageFormat FormatFromExtension(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".jpe" or ".jfif" => ImageFormat.Jpeg,
            ".bmp" or ".dib" => ImageFormat.Bmp,
            ".webp" => ImageFormat.Webp,
            ".gif" => ImageFormat.Gif,
            ".tif" or ".tiff" => ImageFormat.Tiff,
            _ => ImageFormat.Png,
        };
}
