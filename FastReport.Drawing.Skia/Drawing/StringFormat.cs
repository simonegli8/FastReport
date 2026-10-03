using System.Drawing.Text;

namespace System.Drawing;

/// <summary>Text layout information (alignment, wrapping, trimming, tab stops).</summary>
public sealed class StringFormat : ICloneable, IDisposable
{
    private float firstTabOffset;
    private float[] tabStops = [];
    private CharacterRange[] measurableCharacterRanges = [];

    public StringFormat()
        : this(0, 0)
    {
    }

    public StringFormat(StringFormatFlags options)
        : this(options, 0)
    {
    }

    public StringFormat(StringFormatFlags options, int language)
    {
        FormatFlags = options;
        DigitSubstitutionLanguage = language;
    }

    public StringFormat(StringFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        Alignment = format.Alignment;
        LineAlignment = format.LineAlignment;
        FormatFlags = format.FormatFlags;
        Trimming = format.Trimming;
        HotkeyPrefix = format.HotkeyPrefix;
        DigitSubstitutionLanguage = format.DigitSubstitutionLanguage;
        DigitSubstitutionMethod = format.DigitSubstitutionMethod;
        IsTypographic = format.IsTypographic;
        firstTabOffset = format.firstTabOffset;
        tabStops = (float[])format.tabStops.Clone();
        measurableCharacterRanges = (CharacterRange[])format.measurableCharacterRanges.Clone();
    }

    /// <summary>
    /// Returns a new generic default format. Like GDI+, text laid out with it is padded by 1/6 em on the left and right.
    /// </summary>
    public static StringFormat GenericDefault => new();

    /// <summary>
    /// Returns a new generic typographic format: no padding, <see cref="StringFormatFlags.LineLimit"/>,
    /// <see cref="StringFormatFlags.NoClip"/> and no trimming.
    /// </summary>
    public static StringFormat GenericTypographic => new(StringFormatFlags.FitBlackBox | StringFormatFlags.LineLimit | StringFormatFlags.NoClip)
    {
        Trimming = StringTrimming.None,
        IsTypographic = true,
    };

    public StringAlignment Alignment { get; set; }

    public StringAlignment LineAlignment { get; set; }

    public StringFormatFlags FormatFlags { get; set; }

    public StringTrimming Trimming { get; set; } = StringTrimming.Character;

    public HotkeyPrefix HotkeyPrefix { get; set; }

    public int DigitSubstitutionLanguage { get; private set; }

    public StringDigitSubstitute DigitSubstitutionMethod { get; private set; }

    /// <summary>
    /// GDI+ keeps a hidden "typographic" flag on formats derived from <see cref="GenericTypographic"/>
    /// that disables the 1/6 em padding. It survives cloning, so it is tracked separately from the flags.
    /// </summary>
    internal bool IsTypographic { get; set; }

    internal CharacterRange[] MeasurableCharacterRanges => measurableCharacterRanges;

    public void SetMeasurableCharacterRanges(CharacterRange[] ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        measurableCharacterRanges = (CharacterRange[])ranges.Clone();
    }

    public void SetTabStops(float firstTabOffset, float[] tabStops)
    {
        ArgumentNullException.ThrowIfNull(tabStops);
        this.firstTabOffset = firstTabOffset;
        this.tabStops = (float[])tabStops.Clone();
    }

    public float[] GetTabStops(out float firstTabOffset)
    {
        firstTabOffset = this.firstTabOffset;
        return (float[])tabStops.Clone();
    }

    public void SetDigitSubstitution(int language, StringDigitSubstitute substitute)
    {
        DigitSubstitutionLanguage = language;
        DigitSubstitutionMethod = substitute;
    }

    public object Clone() => new StringFormat(this);

    public void Dispose()
    {
    }

    public override string ToString() => $"[StringFormat, FormatFlags={FormatFlags}]";
}
