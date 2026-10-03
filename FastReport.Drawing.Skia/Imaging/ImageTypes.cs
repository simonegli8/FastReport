namespace System.Drawing.Imaging;

public enum PixelFormat
{
    Indexed = 0x00010000,
    Gdi = 0x00020000,
    Alpha = 0x00040000,
    PAlpha = 0x00080000,
    Extended = 0x00100000,
    Canonical = 0x00200000,
    Undefined = 0,
    DontCare = 0,
    Format1bppIndexed = 1 | (1 << 8) | Indexed | Gdi,
    Format4bppIndexed = 2 | (4 << 8) | Indexed | Gdi,
    Format8bppIndexed = 3 | (8 << 8) | Indexed | Gdi,
    Format16bppGrayScale = 4 | (16 << 8) | Extended,
    Format16bppRgb555 = 5 | (16 << 8) | Gdi,
    Format16bppRgb565 = 6 | (16 << 8) | Gdi,
    Format16bppArgb1555 = 7 | (16 << 8) | Alpha | Gdi,
    Format24bppRgb = 8 | (24 << 8) | Gdi,
    Format32bppRgb = 9 | (32 << 8) | Gdi,
    Format32bppArgb = 10 | (32 << 8) | Alpha | Gdi | Canonical,
    Format32bppPArgb = 11 | (32 << 8) | Alpha | PAlpha | Gdi,
    Format48bppRgb = 12 | (48 << 8) | Extended,
    Format64bppArgb = 13 | (64 << 8) | Alpha | Canonical | Extended,
    Format64bppPArgb = 14 | (64 << 8) | Alpha | PAlpha | Extended,
    Max = 15,
}

public enum ImageLockMode
{
    ReadOnly = 1,
    WriteOnly = 2,
    ReadWrite = ReadOnly | WriteOnly,
    UserInputBuffer = 4,
}

public enum ColorMatrixFlag
{
    Default = 0,
    SkipGrays = 1,
    AltGrays = 2,
}

public enum ColorAdjustType
{
    Default = 0,
    Bitmap = 1,
    Brush = 2,
    Pen = 3,
    Text = 4,
    Count = 5,
    Any = 6,
}

public enum EncoderValue
{
    ColorTypeCMYK = 0,
    ColorTypeYCCK = 1,
    CompressionLZW = 2,
    CompressionCCITT3 = 3,
    CompressionCCITT4 = 4,
    CompressionRle = 5,
    CompressionNone = 6,
    ScanMethodInterlaced = 7,
    ScanMethodNonInterlaced = 8,
    VersionGif87 = 9,
    VersionGif89 = 10,
    RenderProgressive = 11,
    RenderNonProgressive = 12,
    TransformRotate90 = 13,
    TransformRotate180 = 14,
    TransformRotate270 = 15,
    TransformFlipHorizontal = 16,
    TransformFlipVertical = 17,
    MultiFrame = 18,
    LastFrame = 19,
    Flush = 20,
    FrameDimensionTime = 21,
    FrameDimensionResolution = 22,
    FrameDimensionPage = 23,
    ColorTypeGray = 24,
    ColorTypeRGB = 25,
}

/// <summary>Identifies an image file format by the GDI+ format GUID.</summary>
public sealed class ImageFormat
{
    private static readonly ImageFormat memoryBmp = new(new Guid("b96b3caa-0728-11d3-9d7b-0000f81ef32e"), "MemoryBMP");
    private static readonly ImageFormat bmp = new(new Guid("b96b3cab-0728-11d3-9d7b-0000f81ef32e"), "Bmp");
    private static readonly ImageFormat emf = new(new Guid("b96b3cac-0728-11d3-9d7b-0000f81ef32e"), "Emf");
    private static readonly ImageFormat wmf = new(new Guid("b96b3cad-0728-11d3-9d7b-0000f81ef32e"), "Wmf");
    private static readonly ImageFormat jpeg = new(new Guid("b96b3cae-0728-11d3-9d7b-0000f81ef32e"), "Jpeg");
    private static readonly ImageFormat png = new(new Guid("b96b3caf-0728-11d3-9d7b-0000f81ef32e"), "Png");
    private static readonly ImageFormat gif = new(new Guid("b96b3cb0-0728-11d3-9d7b-0000f81ef32e"), "Gif");
    private static readonly ImageFormat tiff = new(new Guid("b96b3cb1-0728-11d3-9d7b-0000f81ef32e"), "Tiff");
    private static readonly ImageFormat exif = new(new Guid("b96b3cb2-0728-11d3-9d7b-0000f81ef32e"), "Exif");
    private static readonly ImageFormat icon = new(new Guid("b96b3cb5-0728-11d3-9d7b-0000f81ef32e"), "Icon");
    private static readonly ImageFormat heif = new(new Guid("b96b3cb6-0728-11d3-9d7b-0000f81ef32e"), "Heif");
    private static readonly ImageFormat webp = new(new Guid("b96b3cb7-0728-11d3-9d7b-0000f81ef32e"), "Webp");

    private readonly string? name;

    public ImageFormat(Guid guid)
    {
        Guid = guid;
    }

    private ImageFormat(Guid guid, string name)
    {
        Guid = guid;
        this.name = name;
    }

    public Guid Guid { get; }

    public static ImageFormat MemoryBmp => memoryBmp;

    public static ImageFormat Bmp => bmp;

    public static ImageFormat Emf => emf;

    public static ImageFormat Wmf => wmf;

    public static ImageFormat Jpeg => jpeg;

    public static ImageFormat Png => png;

    public static ImageFormat Gif => gif;

    public static ImageFormat Tiff => tiff;

    public static ImageFormat Exif => exif;

    public static ImageFormat Icon => icon;

    public static ImageFormat Heif => heif;

    public static ImageFormat Webp => webp;

    public override bool Equals(object? obj) => obj is ImageFormat other && other.Guid == Guid;

    public override int GetHashCode() => Guid.GetHashCode();

    public override string ToString() => name ?? $"[ImageFormat: {Guid}]";
}

/// <summary>Pixel data exposed by <see cref="Bitmap.LockBits(Rectangle, ImageLockMode, PixelFormat)"/>.</summary>
public sealed class BitmapData
{
    public int Width { get; set; }

    public int Height { get; set; }

    public int Stride { get; set; }

    public PixelFormat PixelFormat { get; set; }

    public IntPtr Scan0 { get; set; }

    public int Reserved { get; set; }

    internal Rectangle LockedRect { get; set; }

    internal ImageLockMode LockMode { get; set; }

    internal bool OwnsBuffer { get; set; }
}

public sealed class Encoder
{
    public static readonly Encoder Compression = new(new Guid("e09d739d-ccd4-44ee-8eba-3fbf8be4fc58"));
    public static readonly Encoder ColorDepth = new(new Guid("66087055-ad66-4c7c-9a18-38a2310b8337"));
    public static readonly Encoder Quality = new(new Guid("1d5be4b5-fa4a-452d-9cdd-5db35105e7eb"));
    public static readonly Encoder SaveFlag = new(new Guid("292266fc-ac40-47bf-8cfc-a85b89a655de"));

    public Encoder(Guid guid)
    {
        Guid = guid;
    }

    public Guid Guid { get; }
}

public sealed class EncoderParameter : IDisposable
{
    public EncoderParameter(Encoder encoder, long value)
        : this(encoder, [value])
    {
    }

    public EncoderParameter(Encoder encoder, byte value)
        : this(encoder, [(long)value])
    {
    }

    public EncoderParameter(Encoder encoder, short value)
        : this(encoder, [(long)value])
    {
    }

    public EncoderParameter(Encoder encoder, long[] value)
    {
        ArgumentNullException.ThrowIfNull(encoder);
        ArgumentNullException.ThrowIfNull(value);
        Encoder = encoder;
        Values = (long[])value.Clone();
    }

    public Encoder Encoder { get; set; }

    public int NumberOfValues => Values.Length;

    internal long[] Values { get; }

    public void Dispose()
    {
    }
}

public sealed class EncoderParameters : IDisposable
{
    public EncoderParameters()
        : this(1)
    {
    }

    public EncoderParameters(int count)
    {
        Param = new EncoderParameter[count];
    }

    public EncoderParameter[] Param { get; set; }

    public void Dispose()
    {
    }
}

/// <summary>Describes the encoders available through Skia (BMP, JPEG, PNG and WebP).</summary>
public sealed class ImageCodecInfo
{
    private static readonly ImageCodecInfo[] encoders =
    [
        new(new Guid("557cf400-1a04-11d3-9a73-0000f81ef32e"), ImageFormat.Bmp, "Built-in BMP Codec", "BMP", "*.BMP;*.DIB;*.RLE", "image/bmp"),
        new(new Guid("557cf401-1a04-11d3-9a73-0000f81ef32e"), ImageFormat.Jpeg, "Built-in JPEG Codec", "JPEG", "*.JPG;*.JPEG;*.JPE;*.JFIF", "image/jpeg"),
        new(new Guid("557cf406-1a04-11d3-9a73-0000f81ef32e"), ImageFormat.Png, "Built-in PNG Codec", "PNG", "*.PNG", "image/png"),
        new(ImageFormat.Webp.Guid, ImageFormat.Webp, "Skia WebP Codec", "WEBP", "*.WEBP", "image/webp"),
    ];

    private ImageCodecInfo(Guid clsid, ImageFormat format, string codecName, string description, string extension, string mimeType)
    {
        Clsid = clsid;
        FormatID = format.Guid;
        CodecName = codecName;
        FormatDescription = description;
        FilenameExtension = extension;
        MimeType = mimeType;
    }

    public Guid Clsid { get; set; }

    public Guid FormatID { get; set; }

    public string CodecName { get; set; }

    public string? DllName { get; set; }

    public string FormatDescription { get; set; }

    public string FilenameExtension { get; set; }

    public string MimeType { get; set; }

    public int Version { get; set; } = 1;

    public static ImageCodecInfo[] GetImageEncoders() => (ImageCodecInfo[])encoders.Clone();

    public static ImageCodecInfo[] GetImageDecoders() => (ImageCodecInfo[])encoders.Clone();
}
