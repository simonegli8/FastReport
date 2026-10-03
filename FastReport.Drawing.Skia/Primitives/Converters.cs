using System.ComponentModel;
using System.Globalization;

namespace System.Drawing;

// TypeDescriptor support for the .NET Framework primitives. On .NET 10 the in-box types carry these attributes and
// System.ComponentModel.TypeConverter provides the converters; GDI+'s System.Drawing.dll versions don't apply here.
// Color uses FastReport.Compat's System.Drawing.ColorConverter (referenced by name, as Compat depends on this assembly).

[TypeConverter("System.Drawing.ColorConverter, FastReport.Compat")]
public readonly partial struct Color
{
}

[TypeConverter(typeof(PointConverter))]
public partial struct Point
{
}

[TypeConverter(typeof(SizeConverter))]
public partial struct Size
{
}

[TypeConverter(typeof(RectangleConverter))]
public partial struct Rectangle
{
}

[TypeConverter(typeof(SizeFConverter))]
public partial struct SizeF
{
}

public class PointConverter : NumberListConverter<Point, int>
{
    public PointConverter() : base(2, v => new Point(v[0], v[1]), p => [p.X, p.Y]) { }
}

public class SizeConverter : NumberListConverter<Size, int>
{
    public SizeConverter() : base(2, v => new Size(v[0], v[1]), s => [s.Width, s.Height]) { }
}

public class RectangleConverter : NumberListConverter<Rectangle, int>
{
    public RectangleConverter() : base(4, v => new Rectangle(v[0], v[1], v[2], v[3]), r => [r.X, r.Y, r.Width, r.Height]) { }
}

public class SizeFConverter : NumberListConverter<SizeF, float>
{
    public SizeFConverter() : base(2, v => new SizeF(v[0], v[1]), s => [s.Width, s.Height]) { }
}

/// <summary>Converts a struct to and from a culture list-separated list of numbers, e.g. "10, 20".</summary>
public abstract class NumberListConverter<T, TNumber> : TypeConverter
    where T : struct
    where TNumber : struct, IConvertible
{
    private readonly int count;
    private readonly Func<TNumber[], T> create;
    private readonly Func<T, TNumber[]> deconstruct;

    private protected NumberListConverter(int count, Func<TNumber[], T> create, Func<T, TNumber[]> deconstruct)
    {
        this.count = count;
        this.create = create;
        this.deconstruct = deconstruct;
    }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text)
            return base.ConvertFrom(context, culture, value);

        text = text.Trim();
        if (text.Length == 0)
            return null;

        culture ??= CultureInfo.CurrentCulture;
        var parts = text.Split(culture.TextInfo.ListSeparator[0]);
        if (parts.Length != count)
            throw new ArgumentException($"Text \"{text}\" cannot be parsed. The expected text format is a list of {count} numbers.");

        var numbers = new TNumber[count];
        for (int i = 0; i < count; i++)
            numbers[i] = (TNumber)Convert.ChangeType(parts[i].Trim(), typeof(TNumber), culture);
        return create(numbers);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is T typed)
        {
            culture ??= CultureInfo.CurrentCulture;
            string separator = culture.TextInfo.ListSeparator + " ";
            return string.Join(separator, Array.ConvertAll(deconstruct(typed), n => Convert.ToString(n, culture)));
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
