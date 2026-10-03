namespace System.Drawing.Printing;

/// <summary>Printer duplex setting. Printing itself is not available in the Skia backend.</summary>
public enum Duplex
{
    Default = -1,
    Simplex = 1,
    Vertical = 2,
    Horizontal = 3,
}
