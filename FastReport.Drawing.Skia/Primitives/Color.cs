namespace System.Drawing;

/// <summary>
/// An ARGB color. On .NET Framework, System.Drawing.dll owns this type (next to GDI+), so the Skia build
/// provides its own implementation with the .NET (System.Drawing.Primitives) semantics.
/// </summary>
[Serializable]
public readonly partial struct Color : IEquatable<Color>
{
    public static readonly Color Empty = default;

    private const short StateKnownColorValid = 0x0001;
    private const short StateValueMask = 0x0002;
    private const short StateNameValid = 0x0008;

    private readonly string? name;
    private readonly long value;
    private readonly short knownColor;
    private readonly short state;

    internal Color(KnownColor knownColor)
    {
        value = 0;
        state = StateKnownColorValid;
        name = null;
        this.knownColor = unchecked((short)knownColor);
    }

    private Color(long value, short state, string? name, KnownColor knownColor)
    {
        this.value = value;
        this.state = state;
        this.name = name;
        this.knownColor = unchecked((short)knownColor);
    }

    public byte R => unchecked((byte)(Value >> 16));

    public byte G => unchecked((byte)(Value >> 8));

    public byte B => unchecked((byte)Value);

    public byte A => unchecked((byte)(Value >> 24));

    public bool IsKnownColor => (state & StateKnownColorValid) != 0;

    public bool IsEmpty => state == 0;

    public bool IsNamedColor => (state & StateNameValid) != 0 || IsKnownColor;

    public bool IsSystemColor => IsKnownColor && IsKnownColorSystem((KnownColor)knownColor);

    public string Name
    {
        get
        {
            if ((state & StateNameValid) != 0)
                return name!;
            if (IsKnownColor)
                return ((KnownColor)knownColor).ToString();
            return value.ToString("x");
        }
    }

    private long Value
    {
        get
        {
            if ((state & StateValueMask) != 0)
                return value;
            if (IsKnownColor)
                return KnownColorTable.Argb[knownColor];
            return 0;
        }
    }

    public static Color FromArgb(int argb) => FromArgb(unchecked((uint)argb));

    public static Color FromArgb(int alpha, int red, int green, int blue)
    {
        CheckByte(alpha, nameof(alpha));
        CheckByte(red, nameof(red));
        CheckByte(green, nameof(green));
        CheckByte(blue, nameof(blue));
        return FromArgb((uint)alpha << 24 | (uint)red << 16 | (uint)green << 8 | (uint)blue);
    }

    public static Color FromArgb(int alpha, Color baseColor)
    {
        CheckByte(alpha, nameof(alpha));
        return FromArgb((uint)alpha << 24 | (uint)baseColor.Value & 0x00FFFFFF);
    }

    public static Color FromArgb(int red, int green, int blue) => FromArgb(255, red, green, blue);

    public static Color FromKnownColor(KnownColor color) =>
        color <= 0 || color > KnownColor.RebeccaPurple ? FromName(color.ToString()) : new Color(color);

    public static Color FromName(string name)
    {
        if (Enum.TryParse<KnownColor>(name, ignoreCase: true, out var known) && Enum.IsDefined(typeof(KnownColor), known)
            && !int.TryParse(name, out _))
            return new Color(known);
        return new Color(0, StateNameValid, name, 0);
    }

    public int ToArgb() => unchecked((int)Value);

    public KnownColor ToKnownColor() => (KnownColor)knownColor;

    public float GetBrightness()
    {
        GetRgbValues(out int r, out int g, out int b);
        int min = Math.Min(Math.Min(r, g), b);
        int max = Math.Max(Math.Max(r, g), b);
        return (max + min) / (byte.MaxValue * 2f);
    }

    public float GetHue()
    {
        GetRgbValues(out int r, out int g, out int b);
        if (r == g && g == b)
            return 0f;

        int min = Math.Min(Math.Min(r, g), b);
        int max = Math.Max(Math.Max(r, g), b);
        float delta = max - min;
        float hue;
        if (r == max)
            hue = (g - b) / delta;
        else if (g == max)
            hue = (b - r) / delta + 2f;
        else
            hue = (r - g) / delta + 4f;

        hue *= 60f;
        if (hue < 0f)
            hue += 360f;
        return hue;
    }

    public float GetSaturation()
    {
        GetRgbValues(out int r, out int g, out int b);
        if (r == g && g == b)
            return 0f;

        int min = Math.Min(Math.Min(r, g), b);
        int max = Math.Max(Math.Max(r, g), b);
        int div = max + min;
        if (div > byte.MaxValue)
            div = byte.MaxValue * 2 - max - min;
        return (max - min) / (float)div;
    }

    public override string ToString() =>
        IsNamedColor ? $"Color [{Name}]"
        : (state & StateValueMask) != 0 ? $"Color [A={A}, R={R}, G={G}, B={B}]"
        : "Color [Empty]";

    public static bool operator ==(Color left, Color right) =>
        left.value == right.value && left.state == right.state && left.knownColor == right.knownColor && left.name == right.name;

    public static bool operator !=(Color left, Color right) => !(left == right);

    public override bool Equals(object? obj) => obj is Color other && this == other;

    public bool Equals(Color other) => this == other;

    public override int GetHashCode()
    {
        if (name != null && !IsKnownColor)
            return name.GetHashCode();
        return HashCode.Combine(value, state, knownColor);
    }

    internal static bool IsKnownColorSystem(KnownColor knownColor) =>
        knownColor is >= KnownColor.ActiveBorder and <= KnownColor.WindowText
        or >= KnownColor.ButtonFace and <= KnownColor.MenuHighlight;

    private static Color FromArgb(uint argb) => new(argb, StateValueMask, null, 0);

    private void GetRgbValues(out int r, out int g, out int b)
    {
        uint argb = (uint)Value;
        r = (int)(argb >> 16) & 0xFF;
        g = (int)(argb >> 8) & 0xFF;
        b = (int)argb & 0xFF;
    }

    private static void CheckByte(int value, string name)
    {
        if (unchecked((uint)value) > byte.MaxValue)
            throw new ArgumentException($"Value of '{value}' is not valid for '{name}'. '{name}' should be greater than or equal to 0 and less than or equal to 255.", name);
    }
}
