using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing.Imaging;

/// <summary>A 5x5 color transform applied to row vectors [R G B A 1] with components in 0..1.</summary>
public sealed class ColorMatrix
{
    private readonly float[][] matrix;

    public ColorMatrix()
    {
        matrix = new float[5][];
        for (int i = 0; i < 5; i++)
        {
            matrix[i] = new float[5];
            matrix[i][i] = 1;
        }
    }

    public ColorMatrix(float[][] newColorMatrix)
        : this()
    {
        ArgumentNullException.ThrowIfNull(newColorMatrix);
        for (int row = 0; row < Math.Min(5, newColorMatrix.Length); row++)
        {
            for (int col = 0; col < Math.Min(5, newColorMatrix[row].Length); col++)
                matrix[row][col] = newColorMatrix[row][col];
        }
    }

    public float this[int row, int column]
    {
        get => matrix[row][column];
        set => matrix[row][column] = value;
    }

    public float Matrix00 { get => matrix[0][0]; set => matrix[0][0] = value; }
    public float Matrix01 { get => matrix[0][1]; set => matrix[0][1] = value; }
    public float Matrix02 { get => matrix[0][2]; set => matrix[0][2] = value; }
    public float Matrix03 { get => matrix[0][3]; set => matrix[0][3] = value; }
    public float Matrix04 { get => matrix[0][4]; set => matrix[0][4] = value; }
    public float Matrix10 { get => matrix[1][0]; set => matrix[1][0] = value; }
    public float Matrix11 { get => matrix[1][1]; set => matrix[1][1] = value; }
    public float Matrix12 { get => matrix[1][2]; set => matrix[1][2] = value; }
    public float Matrix13 { get => matrix[1][3]; set => matrix[1][3] = value; }
    public float Matrix14 { get => matrix[1][4]; set => matrix[1][4] = value; }
    public float Matrix20 { get => matrix[2][0]; set => matrix[2][0] = value; }
    public float Matrix21 { get => matrix[2][1]; set => matrix[2][1] = value; }
    public float Matrix22 { get => matrix[2][2]; set => matrix[2][2] = value; }
    public float Matrix23 { get => matrix[2][3]; set => matrix[2][3] = value; }
    public float Matrix24 { get => matrix[2][4]; set => matrix[2][4] = value; }
    public float Matrix30 { get => matrix[3][0]; set => matrix[3][0] = value; }
    public float Matrix31 { get => matrix[3][1]; set => matrix[3][1] = value; }
    public float Matrix32 { get => matrix[3][2]; set => matrix[3][2] = value; }
    public float Matrix33 { get => matrix[3][3]; set => matrix[3][3] = value; }
    public float Matrix34 { get => matrix[3][4]; set => matrix[3][4] = value; }
    public float Matrix40 { get => matrix[4][0]; set => matrix[4][0] = value; }
    public float Matrix41 { get => matrix[4][1]; set => matrix[4][1] = value; }
    public float Matrix42 { get => matrix[4][2]; set => matrix[4][2] = value; }
    public float Matrix43 { get => matrix[4][3]; set => matrix[4][3] = value; }
    public float Matrix44 { get => matrix[4][4]; set => matrix[4][4] = value; }

    /// <summary>
    /// Converts to Skia's row-major 4x5 layout (column vectors). Translation stays normalized to 0..1,
    /// which is the convention of both GDI+ and Skia's color matrix filter.
    /// </summary>
    internal float[] ToSkia()
    {
        var result = new float[20];
        for (int output = 0; output < 4; output++)
        {
            for (int input = 0; input < 4; input++)
                result[output * 5 + input] = matrix[input][output];
            result[output * 5 + 4] = matrix[4][output];
        }
        return result;
    }
}

/// <summary>Color adjustments applied when drawing images.</summary>
/// <remarks>Supports color matrices, gamma and color keys; wrap modes are accepted but ignored.</remarks>
public sealed class ImageAttributes : ICloneable, IDisposable
{
    private ColorMatrix? colorMatrix;
    private float? gamma;
    private (Color Low, Color High)? colorKey;

    public void SetColorMatrix(ColorMatrix newColorMatrix) =>
        SetColorMatrix(newColorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Default);

    public void SetColorMatrix(ColorMatrix newColorMatrix, ColorMatrixFlag flags) =>
        SetColorMatrix(newColorMatrix, flags, ColorAdjustType.Default);

    public void SetColorMatrix(ColorMatrix newColorMatrix, ColorMatrixFlag mode, ColorAdjustType type)
    {
        ArgumentNullException.ThrowIfNull(newColorMatrix);
        colorMatrix = newColorMatrix;
    }

    public void ClearColorMatrix() => colorMatrix = null;

    public void ClearColorMatrix(ColorAdjustType type) => colorMatrix = null;

    public void SetGamma(float gamma) => this.gamma = gamma;

    public void SetGamma(float gamma, ColorAdjustType type) => this.gamma = gamma;

    public void ClearGamma() => gamma = null;

    public void ClearGamma(ColorAdjustType type) => gamma = null;

    public void SetColorKey(Color colorLow, Color colorHigh) => colorKey = (colorLow, colorHigh);

    public void SetColorKey(Color colorLow, Color colorHigh, ColorAdjustType type) => colorKey = (colorLow, colorHigh);

    public void ClearColorKey() => colorKey = null;

    public void ClearColorKey(ColorAdjustType type) => colorKey = null;

    public void SetWrapMode(WrapMode mode)
    {
    }

    public void SetWrapMode(WrapMode mode, Color color)
    {
    }

    public void SetWrapMode(WrapMode mode, Color color, bool clamp)
    {
    }

    public object Clone() => MemberwiseClone();

    public void Dispose()
    {
    }

    internal SKColorFilter? CreateColorFilter()
    {
        SKColorFilter? filter = colorMatrix == null ? null : SKColorFilter.CreateColorMatrix(colorMatrix.ToSkia());

        if (gamma is float g && g > 0 && g != 1)
        {
            var identity = new byte[256];
            var table = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                identity[i] = (byte)i;
                table[i] = (byte)Math.Clamp(Math.Round(Math.Pow(i / 255.0, g) * 255), 0, 255);
            }

            var gammaFilter = SKColorFilter.CreateTable(identity, table, table, table);
            if (filter == null)
                return gammaFilter;

            var composed = SKColorFilter.CreateCompose(gammaFilter, filter);
            filter.Dispose();
            gammaFilter.Dispose();
            return composed;
        }

        return filter;
    }

    /// <summary>Returns a copy of <paramref name="source"/> with color-keyed pixels made transparent, or null if no key is set.</summary>
    internal SKImage? ApplyColorKey(SKImage source)
    {
        if (colorKey is not var (low, high))
            return null;

        var info = new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var pixels = new byte[info.BytesSize];
        unsafe
        {
            fixed (byte* ptr = pixels)
                source.ReadPixels(info, (IntPtr)ptr, info.RowBytes, 0, 0);
        }

        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
            if (r >= low.R && r <= high.R && g >= low.G && g <= high.G && b >= low.B && b <= high.B)
            {
                pixels[i] = pixels[i + 1] = pixels[i + 2] = pixels[i + 3] = 0;
            }
        }

        return SKImage.FromPixelCopy(info, pixels, info.RowBytes);
    }
}
