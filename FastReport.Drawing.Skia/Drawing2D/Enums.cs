namespace System.Drawing.Drawing2D;

public enum MatrixOrder
{
    Prepend = 0,
    Append = 1,
}

public enum SmoothingMode
{
    Invalid = -1,
    Default = 0,
    HighSpeed = 1,
    HighQuality = 2,
    None = 3,
    AntiAlias = 4,
}

public enum InterpolationMode
{
    Invalid = -1,
    Default = 0,
    Low = 1,
    High = 2,
    Bilinear = 3,
    Bicubic = 4,
    NearestNeighbor = 5,
    HighQualityBilinear = 6,
    HighQualityBicubic = 7,
}

public enum CompositingQuality
{
    Invalid = -1,
    Default = 0,
    HighSpeed = 1,
    HighQuality = 2,
    GammaCorrected = 3,
    AssumeLinear = 4,
}

public enum CompositingMode
{
    SourceOver = 0,
    SourceCopy = 1,
}

public enum PixelOffsetMode
{
    Invalid = -1,
    Default = 0,
    HighSpeed = 1,
    HighQuality = 2,
    None = 3,
    Half = 4,
}

public enum CombineMode
{
    Replace = 0,
    Intersect = 1,
    Union = 2,
    Xor = 3,
    Exclude = 4,
    Complement = 5,
}

public enum CoordinateSpace
{
    World = 0,
    Page = 1,
    Device = 2,
}

public enum FillMode
{
    Alternate = 0,
    Winding = 1,
}

public enum DashStyle
{
    Solid = 0,
    Dash = 1,
    Dot = 2,
    DashDot = 3,
    DashDotDot = 4,
    Custom = 5,
}

public enum DashCap
{
    Flat = 0,
    Round = 2,
    Triangle = 3,
}

public enum LineCap
{
    Flat = 0,
    Square = 1,
    Round = 2,
    Triangle = 3,
    NoAnchor = 0x10,
    SquareAnchor = 0x11,
    RoundAnchor = 0x12,
    DiamondAnchor = 0x13,
    ArrowAnchor = 0x14,
    AnchorMask = 0xf0,
    Custom = 0xff,
}

public enum LineJoin
{
    Miter = 0,
    Bevel = 1,
    Round = 2,
    MiterClipped = 3,
}

public enum PenAlignment
{
    Center = 0,
    Inset = 1,
    Outset = 2,
    Left = 3,
    Right = 4,
}

public enum PenType
{
    SolidColor = 0,
    HatchFill = 1,
    TextureFill = 2,
    PathGradient = 3,
    LinearGradient = 4,
}

public enum WrapMode
{
    Tile = 0,
    TileFlipX = 1,
    TileFlipY = 2,
    TileFlipXY = 3,
    Clamp = 4,
}

public enum LinearGradientMode
{
    Horizontal = 0,
    Vertical = 1,
    ForwardDiagonal = 2,
    BackwardDiagonal = 3,
}

public enum PathPointType
{
    Start = 0,
    Line = 1,
    Bezier = 3,
    Bezier3 = 3,
    PathTypeMask = 0x07,
    DashMode = 0x10,
    PathMarker = 0x20,
    CloseSubpath = 0x80,
}

public enum HatchStyle
{
    Horizontal = 0,
    Vertical = 1,
    ForwardDiagonal = 2,
    BackwardDiagonal = 3,
    Cross = 4,
    DiagonalCross = 5,
    Percent05 = 6,
    Percent10 = 7,
    Percent20 = 8,
    Percent25 = 9,
    Percent30 = 10,
    Percent40 = 11,
    Percent50 = 12,
    Percent60 = 13,
    Percent70 = 14,
    Percent75 = 15,
    Percent80 = 16,
    Percent90 = 17,
    LightDownwardDiagonal = 18,
    LightUpwardDiagonal = 19,
    DarkDownwardDiagonal = 20,
    DarkUpwardDiagonal = 21,
    WideDownwardDiagonal = 22,
    WideUpwardDiagonal = 23,
    LightVertical = 24,
    LightHorizontal = 25,
    NarrowVertical = 26,
    NarrowHorizontal = 27,
    DarkVertical = 28,
    DarkHorizontal = 29,
    DashedDownwardDiagonal = 30,
    DashedUpwardDiagonal = 31,
    DashedHorizontal = 32,
    DashedVertical = 33,
    SmallConfetti = 34,
    LargeConfetti = 35,
    ZigZag = 36,
    Wave = 37,
    DiagonalBrick = 38,
    HorizontalBrick = 39,
    Weave = 40,
    Plaid = 41,
    Divot = 42,
    DottedGrid = 43,
    DottedDiamond = 44,
    Shingle = 45,
    Trellis = 46,
    Sphere = 47,
    SmallGrid = 48,
    SmallCheckerBoard = 49,
    LargeCheckerBoard = 50,
    OutlinedDiamond = 51,
    SolidDiamond = 52,
    LargeGrid = Cross,
    Min = Horizontal,
    Max = Cross,
}
