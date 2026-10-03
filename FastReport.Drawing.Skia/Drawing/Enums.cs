namespace System.Drawing;

public enum GraphicsUnit
{
    World = 0,
    Display = 1,
    Pixel = 2,
    Point = 3,
    Inch = 4,
    Document = 5,
    Millimeter = 6,
}

[Flags]
public enum FontStyle
{
    Regular = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4,
    Strikeout = 8,
}

public enum StringAlignment
{
    Near = 0,
    Center = 1,
    Far = 2,
}

[Flags]
public enum StringFormatFlags
{
    DirectionRightToLeft = 0x0001,
    DirectionVertical = 0x0002,
    FitBlackBox = 0x0004,
    DisplayFormatControl = 0x0020,
    NoFontFallback = 0x0400,
    MeasureTrailingSpaces = 0x0800,
    NoWrap = 0x1000,
    LineLimit = 0x2000,
    NoClip = 0x4000,
}

public enum StringTrimming
{
    None = 0,
    Character = 1,
    Word = 2,
    EllipsisCharacter = 3,
    EllipsisWord = 4,
    EllipsisPath = 5,
}

public enum StringDigitSubstitute
{
    User = 0,
    None = 1,
    National = 2,
    Traditional = 3,
}

public enum RotateFlipType
{
    RotateNoneFlipNone = 0,
    Rotate90FlipNone = 1,
    Rotate180FlipNone = 2,
    Rotate270FlipNone = 3,
    RotateNoneFlipX = 4,
    Rotate90FlipX = 5,
    Rotate180FlipX = 6,
    Rotate270FlipX = 7,
    RotateNoneFlipY = Rotate180FlipX,
    Rotate90FlipY = Rotate270FlipX,
    Rotate180FlipY = RotateNoneFlipX,
    Rotate270FlipY = Rotate90FlipX,
    RotateNoneFlipXY = Rotate180FlipNone,
    Rotate90FlipXY = Rotate270FlipNone,
    Rotate180FlipXY = RotateNoneFlipNone,
    Rotate270FlipXY = Rotate90FlipNone,
}

public struct CharacterRange : IEquatable<CharacterRange>
{
    public CharacterRange(int first, int length)
    {
        First = first;
        Length = length;
    }

    public int First { get; set; }

    public int Length { get; set; }

    public readonly bool Equals(CharacterRange other) => First == other.First && Length == other.Length;

    public override readonly bool Equals(object? obj) => obj is CharacterRange other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(First, Length);

    public static bool operator ==(CharacterRange cr1, CharacterRange cr2) => cr1.Equals(cr2);

    public static bool operator !=(CharacterRange cr1, CharacterRange cr2) => !cr1.Equals(cr2);
}
