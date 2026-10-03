namespace System.Drawing.Drawing2D;

/// <summary>Defines a blend pattern for gradient brushes.</summary>
public sealed class Blend
{
    public Blend()
        : this(1)
    {
    }

    public Blend(int count)
    {
        Factors = new float[count];
        Positions = new float[count];
    }

    public float[] Factors { get; set; }

    public float[] Positions { get; set; }
}

/// <summary>Defines arrays of colors and positions used for multicolor gradients.</summary>
public sealed class ColorBlend
{
    public ColorBlend()
        : this(1)
    {
    }

    public ColorBlend(int count)
    {
        Colors = new Color[count];
        Positions = new float[count];
    }

    public Color[] Colors { get; set; }

    public float[] Positions { get; set; }
}

/// <summary>Opaque snapshot returned by <see cref="Graphics.Save"/>.</summary>
public sealed class GraphicsState
{
    internal GraphicsState(object snapshot)
    {
        Snapshot = snapshot;
    }

    internal object Snapshot { get; }
}
