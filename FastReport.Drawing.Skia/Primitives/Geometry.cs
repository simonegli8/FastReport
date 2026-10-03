using System.Globalization;

namespace System.Drawing;

// Integer and floating-point geometry primitives with the .NET (System.Drawing.Primitives) API.
// On .NET Framework these types live in System.Drawing.dll next to GDI+, so the Skia build provides its own.

[Serializable]
public partial struct Point : IEquatable<Point>
{
    public static readonly Point Empty = default;

    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public Point(Size sz)
    {
        X = sz.Width;
        Y = sz.Height;
    }

    public Point(int dw)
    {
        X = unchecked((short)(dw & 0xFFFF));
        Y = unchecked((short)((dw >> 16) & 0xFFFF));
    }

    public int X { readonly get; set; }

    public int Y { readonly get; set; }

    public readonly bool IsEmpty => X == 0 && Y == 0;

    public static implicit operator PointF(Point p) => new(p.X, p.Y);

    public static explicit operator Size(Point p) => new(p.X, p.Y);

    public static Point operator +(Point pt, Size sz) => Add(pt, sz);

    public static Point operator -(Point pt, Size sz) => Subtract(pt, sz);

    public static bool operator ==(Point left, Point right) => left.X == right.X && left.Y == right.Y;

    public static bool operator !=(Point left, Point right) => !(left == right);

    public static Point Add(Point pt, Size sz) => new(unchecked(pt.X + sz.Width), unchecked(pt.Y + sz.Height));

    public static Point Subtract(Point pt, Size sz) => new(unchecked(pt.X - sz.Width), unchecked(pt.Y - sz.Height));

    public static Point Ceiling(PointF value) => new(unchecked((int)Math.Ceiling(value.X)), unchecked((int)Math.Ceiling(value.Y)));

    public static Point Truncate(PointF value) => new(unchecked((int)value.X), unchecked((int)value.Y));

    public static Point Round(PointF value) => new(unchecked((int)Math.Round(value.X)), unchecked((int)Math.Round(value.Y)));

    public void Offset(int dx, int dy)
    {
        unchecked
        {
            X += dx;
            Y += dy;
        }
    }

    public void Offset(Point p) => Offset(p.X, p.Y);

    public override readonly bool Equals(object? obj) => obj is Point other && Equals(other);

    public readonly bool Equals(Point other) => this == other;

    public override readonly int GetHashCode() => HashCode.Combine(X, Y);

    public override readonly string ToString() => $"{{X={X},Y={Y}}}";
}

[Serializable]
public struct PointF : IEquatable<PointF>
{
    public static readonly PointF Empty = default;

    public PointF(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { readonly get; set; }

    public float Y { readonly get; set; }

    public readonly bool IsEmpty => X == 0f && Y == 0f;

    public static PointF operator +(PointF pt, Size sz) => Add(pt, sz);

    public static PointF operator -(PointF pt, Size sz) => Subtract(pt, sz);

    public static PointF operator +(PointF pt, SizeF sz) => Add(pt, sz);

    public static PointF operator -(PointF pt, SizeF sz) => Subtract(pt, sz);

    public static bool operator ==(PointF left, PointF right) => left.X == right.X && left.Y == right.Y;

    public static bool operator !=(PointF left, PointF right) => !(left == right);

    public static PointF Add(PointF pt, Size sz) => new(pt.X + sz.Width, pt.Y + sz.Height);

    public static PointF Subtract(PointF pt, Size sz) => new(pt.X - sz.Width, pt.Y - sz.Height);

    public static PointF Add(PointF pt, SizeF sz) => new(pt.X + sz.Width, pt.Y + sz.Height);

    public static PointF Subtract(PointF pt, SizeF sz) => new(pt.X - sz.Width, pt.Y - sz.Height);

    public override readonly bool Equals(object? obj) => obj is PointF other && Equals(other);

    public readonly bool Equals(PointF other) => this == other;

    public override readonly int GetHashCode() => HashCode.Combine(X.GetHashCode(), Y.GetHashCode());

    public override readonly string ToString() => $"{{X={X}, Y={Y}}}";
}

[Serializable]
public partial struct Size : IEquatable<Size>
{
    public static readonly Size Empty = default;

    public Size(Point pt)
    {
        Width = pt.X;
        Height = pt.Y;
    }

    public Size(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { readonly get; set; }

    public int Height { readonly get; set; }

    public readonly bool IsEmpty => Width == 0 && Height == 0;

    public static implicit operator SizeF(Size p) => new(p.Width, p.Height);

    public static explicit operator Point(Size size) => new(size.Width, size.Height);

    public static Size operator +(Size sz1, Size sz2) => Add(sz1, sz2);

    public static Size operator -(Size sz1, Size sz2) => Subtract(sz1, sz2);

    public static Size operator *(int left, Size right) => new(unchecked(left * right.Width), unchecked(left * right.Height));

    public static Size operator *(Size left, int right) => right * left;

    public static Size operator /(Size left, int right) => new(unchecked(left.Width / right), unchecked(left.Height / right));

    public static SizeF operator *(float left, Size right) => new(left * right.Width, left * right.Height);

    public static SizeF operator *(Size left, float right) => right * left;

    public static SizeF operator /(Size left, float right) => new(left.Width / right, left.Height / right);

    public static bool operator ==(Size sz1, Size sz2) => sz1.Width == sz2.Width && sz1.Height == sz2.Height;

    public static bool operator !=(Size sz1, Size sz2) => !(sz1 == sz2);

    public static Size Add(Size sz1, Size sz2) => new(unchecked(sz1.Width + sz2.Width), unchecked(sz1.Height + sz2.Height));

    public static Size Subtract(Size sz1, Size sz2) => new(unchecked(sz1.Width - sz2.Width), unchecked(sz1.Height - sz2.Height));

    public static Size Ceiling(SizeF value) => new(unchecked((int)Math.Ceiling(value.Width)), unchecked((int)Math.Ceiling(value.Height)));

    public static Size Truncate(SizeF value) => new(unchecked((int)value.Width), unchecked((int)value.Height));

    public static Size Round(SizeF value) => new(unchecked((int)Math.Round(value.Width)), unchecked((int)Math.Round(value.Height)));

    public override readonly bool Equals(object? obj) => obj is Size other && Equals(other);

    public readonly bool Equals(Size other) => this == other;

    public override readonly int GetHashCode() => HashCode.Combine(Width, Height);

    public override readonly string ToString() => $"{{Width={Width}, Height={Height}}}";
}

[Serializable]
public partial struct SizeF : IEquatable<SizeF>
{
    public static readonly SizeF Empty = default;

    public SizeF(SizeF size)
    {
        Width = size.Width;
        Height = size.Height;
    }

    public SizeF(PointF pt)
    {
        Width = pt.X;
        Height = pt.Y;
    }

    public SizeF(float width, float height)
    {
        Width = width;
        Height = height;
    }

    public float Width { readonly get; set; }

    public float Height { readonly get; set; }

    public readonly bool IsEmpty => Width == 0 && Height == 0;

    public static explicit operator PointF(SizeF size) => new(size.Width, size.Height);

    public static SizeF operator +(SizeF sz1, SizeF sz2) => Add(sz1, sz2);

    public static SizeF operator -(SizeF sz1, SizeF sz2) => Subtract(sz1, sz2);

    public static SizeF operator *(float left, SizeF right) => new(left * right.Width, left * right.Height);

    public static SizeF operator *(SizeF left, float right) => right * left;

    public static SizeF operator /(SizeF left, float right) => new(left.Width / right, left.Height / right);

    public static bool operator ==(SizeF sz1, SizeF sz2) => sz1.Width == sz2.Width && sz1.Height == sz2.Height;

    public static bool operator !=(SizeF sz1, SizeF sz2) => !(sz1 == sz2);

    public static SizeF Add(SizeF sz1, SizeF sz2) => new(sz1.Width + sz2.Width, sz1.Height + sz2.Height);

    public static SizeF Subtract(SizeF sz1, SizeF sz2) => new(sz1.Width - sz2.Width, sz1.Height - sz2.Height);

    public readonly PointF ToPointF() => (PointF)this;

    public readonly Size ToSize() => Size.Truncate(this);

    public override readonly bool Equals(object? obj) => obj is SizeF other && Equals(other);

    public readonly bool Equals(SizeF other) => this == other;

    public override readonly int GetHashCode() => HashCode.Combine(Width.GetHashCode(), Height.GetHashCode());

    public override readonly string ToString() => $"{{Width={Width}, Height={Height}}}";
}

[Serializable]
public partial struct Rectangle : IEquatable<Rectangle>
{
    public static readonly Rectangle Empty = default;

    public Rectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Rectangle(Point location, Size size)
    {
        X = location.X;
        Y = location.Y;
        Width = size.Width;
        Height = size.Height;
    }

    public int X { readonly get; set; }

    public int Y { readonly get; set; }

    public int Width { readonly get; set; }

    public int Height { readonly get; set; }

    public Point Location
    {
        readonly get => new(X, Y);
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }

    public Size Size
    {
        readonly get => new(Width, Height);
        set
        {
            Width = value.Width;
            Height = value.Height;
        }
    }

    public readonly int Left => X;

    public readonly int Top => Y;

    public readonly int Right => unchecked(X + Width);

    public readonly int Bottom => unchecked(Y + Height);

    public readonly bool IsEmpty => Height == 0 && Width == 0 && X == 0 && Y == 0;

    public static Rectangle FromLTRB(int left, int top, int right, int bottom) =>
        new(left, top, unchecked(right - left), unchecked(bottom - top));

    public static bool operator ==(Rectangle left, Rectangle right) =>
        left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height;

    public static bool operator !=(Rectangle left, Rectangle right) => !(left == right);

    public static Rectangle Ceiling(RectangleF value) => new(
        unchecked((int)Math.Ceiling(value.X)), unchecked((int)Math.Ceiling(value.Y)),
        unchecked((int)Math.Ceiling(value.Width)), unchecked((int)Math.Ceiling(value.Height)));

    public static Rectangle Truncate(RectangleF value) => new(
        unchecked((int)value.X), unchecked((int)value.Y), unchecked((int)value.Width), unchecked((int)value.Height));

    public static Rectangle Round(RectangleF value) => new(
        unchecked((int)Math.Round(value.X)), unchecked((int)Math.Round(value.Y)),
        unchecked((int)Math.Round(value.Width)), unchecked((int)Math.Round(value.Height)));

    public static Rectangle Inflate(Rectangle rect, int x, int y)
    {
        var r = rect;
        r.Inflate(x, y);
        return r;
    }

    public static Rectangle Intersect(Rectangle a, Rectangle b)
    {
        int x1 = Math.Max(a.X, b.X);
        int x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        int y1 = Math.Max(a.Y, b.Y);
        int y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return x2 >= x1 && y2 >= y1 ? new Rectangle(x1, y1, x2 - x1, y2 - y1) : Empty;
    }

    public static Rectangle Union(Rectangle a, Rectangle b)
    {
        int x1 = Math.Min(a.X, b.X);
        int x2 = Math.Max(a.X + a.Width, b.X + b.Width);
        int y1 = Math.Min(a.Y, b.Y);
        int y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return new Rectangle(x1, y1, x2 - x1, y2 - y1);
    }

    public readonly bool Contains(int x, int y) => X <= x && x < X + Width && Y <= y && y < Y + Height;

    public readonly bool Contains(Point pt) => Contains(pt.X, pt.Y);

    public readonly bool Contains(Rectangle rect) =>
        X <= rect.X && rect.X + rect.Width <= X + Width && Y <= rect.Y && rect.Y + rect.Height <= Y + Height;

    public void Inflate(int width, int height)
    {
        unchecked
        {
            X -= width;
            Y -= height;
            Width += 2 * width;
            Height += 2 * height;
        }
    }

    public void Inflate(Size size) => Inflate(size.Width, size.Height);

    public void Intersect(Rectangle rect) => this = Intersect(rect, this);

    public readonly bool IntersectsWith(Rectangle rect) =>
        rect.X < X + Width && X < rect.X + rect.Width && rect.Y < Y + Height && Y < rect.Y + rect.Height;

    public void Offset(Point pos) => Offset(pos.X, pos.Y);

    public void Offset(int x, int y)
    {
        unchecked
        {
            X += x;
            Y += y;
        }
    }

    public override readonly bool Equals(object? obj) => obj is Rectangle other && Equals(other);

    public readonly bool Equals(Rectangle other) => this == other;

    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    public override readonly string ToString() => $"{{X={X},Y={Y},Width={Width},Height={Height}}}";
}

[Serializable]
public struct RectangleF : IEquatable<RectangleF>
{
    public static readonly RectangleF Empty = default;

    public RectangleF(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public RectangleF(PointF location, SizeF size)
    {
        X = location.X;
        Y = location.Y;
        Width = size.Width;
        Height = size.Height;
    }

    public float X { readonly get; set; }

    public float Y { readonly get; set; }

    public float Width { readonly get; set; }

    public float Height { readonly get; set; }

    public PointF Location
    {
        readonly get => new(X, Y);
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }

    public SizeF Size
    {
        readonly get => new(Width, Height);
        set
        {
            Width = value.Width;
            Height = value.Height;
        }
    }

    public readonly float Left => X;

    public readonly float Top => Y;

    public readonly float Right => X + Width;

    public readonly float Bottom => Y + Height;

    public readonly bool IsEmpty => !(Width > 0) || !(Height > 0);

    public static RectangleF FromLTRB(float left, float top, float right, float bottom) =>
        new(left, top, right - left, bottom - top);

    public static implicit operator RectangleF(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

    public static bool operator ==(RectangleF left, RectangleF right) =>
        left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height;

    public static bool operator !=(RectangleF left, RectangleF right) => !(left == right);

    public static RectangleF Inflate(RectangleF rect, float x, float y)
    {
        var r = rect;
        r.Inflate(x, y);
        return r;
    }

    public static RectangleF Intersect(RectangleF a, RectangleF b)
    {
        float x1 = Math.Max(a.X, b.X);
        float x2 = Math.Min(a.X + a.Width, b.X + b.Width);
        float y1 = Math.Max(a.Y, b.Y);
        float y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return x2 >= x1 && y2 >= y1 ? new RectangleF(x1, y1, x2 - x1, y2 - y1) : Empty;
    }

    public static RectangleF Union(RectangleF a, RectangleF b)
    {
        float x1 = Math.Min(a.X, b.X);
        float x2 = Math.Max(a.X + a.Width, b.X + b.Width);
        float y1 = Math.Min(a.Y, b.Y);
        float y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);
        return new RectangleF(x1, y1, x2 - x1, y2 - y1);
    }

    public readonly bool Contains(float x, float y) => X <= x && x < X + Width && Y <= y && y < Y + Height;

    public readonly bool Contains(PointF pt) => Contains(pt.X, pt.Y);

    public readonly bool Contains(RectangleF rect) =>
        X <= rect.X && rect.X + rect.Width <= X + Width && Y <= rect.Y && rect.Y + rect.Height <= Y + Height;

    public void Inflate(float x, float y)
    {
        X -= x;
        Y -= y;
        Width += 2 * x;
        Height += 2 * y;
    }

    public void Inflate(SizeF size) => Inflate(size.Width, size.Height);

    public void Intersect(RectangleF rect) => this = Intersect(rect, this);

    public readonly bool IntersectsWith(RectangleF rect) =>
        rect.X < X + Width && X < rect.X + rect.Width && rect.Y < Y + Height && Y < rect.Y + rect.Height;

    public void Offset(PointF pos) => Offset(pos.X, pos.Y);

    public void Offset(float x, float y)
    {
        X += x;
        Y += y;
    }

    public override readonly bool Equals(object? obj) => obj is RectangleF other && Equals(other);

    public readonly bool Equals(RectangleF other) => this == other;

    public override readonly int GetHashCode() =>
        HashCode.Combine(X.GetHashCode(), Y.GetHashCode(), Width.GetHashCode(), Height.GetHashCode());

    public override readonly string ToString() =>
        "{X=" + X.ToString(CultureInfo.CurrentCulture) + ",Y=" + Y.ToString(CultureInfo.CurrentCulture)
        + ",Width=" + Width.ToString(CultureInfo.CurrentCulture) + ",Height=" + Height.ToString(CultureInfo.CurrentCulture) + "}";
}
