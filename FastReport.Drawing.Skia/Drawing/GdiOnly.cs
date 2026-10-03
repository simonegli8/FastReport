using System.Drawing.Imaging;
using SkiaSharp;

namespace System.Drawing
{
    /// <summary>Fonts for user-interface elements. Without a desktop to ask, these use GDI+'s Windows defaults.</summary>
    public static class SystemFonts
    {
        public static Font DefaultFont => new(FontFamily.GenericSansSerif, 8.25f);

        public static Font DialogFont => new(FontFamily.GenericSansSerif, 8.25f);

        public static Font MessageBoxFont => new(FontFamily.GenericSansSerif, 9f);

        public static Font MenuFont => new(FontFamily.GenericSansSerif, 9f);

        public static Font CaptionFont => new(FontFamily.GenericSansSerif, 9f);

        public static Font SmallCaptionFont => new(FontFamily.GenericSansSerif, 9f);

        public static Font StatusFont => new(FontFamily.GenericSansSerif, 9f);

        public static Font IconTitleFont => new(FontFamily.GenericSansSerif, 9f);

        public static Font? GetFontByName(string systemFontName) => systemFontName switch
        {
            nameof(DefaultFont) => DefaultFont,
            nameof(DialogFont) => DialogFont,
            nameof(MessageBoxFont) => MessageBoxFont,
            nameof(MenuFont) => MenuFont,
            nameof(CaptionFont) => CaptionFont,
            nameof(SmallCaptionFont) => SmallCaptionFont,
            nameof(StatusFont) => StatusFont,
            nameof(IconTitleFont) => IconTitleFont,
            _ => null,
        };
    }

    public sealed partial class Graphics
    {
        /// <summary>Windows device contexts do not exist in the Skia backend.</summary>
        /// <exception cref="PlatformNotSupportedException">Always.</exception>
        public IntPtr GetHdc() =>
            throw new PlatformNotSupportedException("Device contexts (HDC) are not available in the Skia backend.");

        public void ReleaseHdc(IntPtr hdc)
        {
        }

        public void ReleaseHdc()
        {
        }
    }

    public abstract partial class Image
    {
        /// <summary>Multi-frame (TIFF) encoding is not available in the Skia backend.</summary>
        /// <exception cref="PlatformNotSupportedException">Always.</exception>
        public void SaveAdd(EncoderParameters? encoderParams) =>
            throw new PlatformNotSupportedException("Multi-frame images are not supported by the Skia backend.");

        /// <inheritdoc cref="SaveAdd(EncoderParameters?)"/>
        public void SaveAdd(Image image, EncoderParameters? encoderParams) =>
            throw new PlatformNotSupportedException("Multi-frame images are not supported by the Skia backend.");
    }
}

namespace System.Drawing.Imaging
{
    /// <summary>
    /// EMF/WMF vector images. Skia cannot read or record Windows metafiles, so this type only exists to keep
    /// GDI+ code compiling: images are never decoded as metafiles, and the constructors always throw.
    /// </summary>
    public sealed class Metafile : Image
    {
        private Metafile()
        {
        }

        public Metafile(Stream stream, IntPtr referenceHdc) => throw NotSupported();

        public Metafile(string fileName, IntPtr referenceHdc) => throw NotSupported();

        public Metafile(Stream stream) => throw NotSupported();

        public Metafile(string filename) => throw NotSupported();

        public override int Width => throw NotSupported();

        public override int Height => throw NotSupported();

        public override float HorizontalResolution => throw NotSupported();

        public override float VerticalResolution => throw NotSupported();

        public override PixelFormat PixelFormat => throw NotSupported();

        public override void RotateFlip(RotateFlipType rotateFlipType) => throw NotSupported();

        public override object Clone() => throw NotSupported();

        internal override SKImage GetSKImage() => throw NotSupported();

        internal override SKBitmap GetSKBitmap() => throw NotSupported();

        private static PlatformNotSupportedException NotSupported() =>
            new("Windows metafiles (EMF/WMF) are not supported by the Skia backend.");
    }
}
