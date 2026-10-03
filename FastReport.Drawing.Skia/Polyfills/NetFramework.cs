// Polyfills for .NET Framework 4.8 (compiled only for that target). Language-level polyfills
// (Index/Range, init, nullable and caller-argument attributes) come from the PolySharp source generator.
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System
{
    internal static class MathF
    {
        public const float PI = (float)Math.PI;

        public static float Abs(float x) => Math.Abs(x);

        public static float Ceiling(float x) => (float)Math.Ceiling(x);

        public static float Floor(float x) => (float)Math.Floor(x);

        public static float Round(float x) => (float)Math.Round(x);

        public static float Sqrt(float x) => (float)Math.Sqrt(x);

        public static float Sin(float x) => (float)Math.Sin(x);

        public static float Cos(float x) => (float)Math.Cos(x);

        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);

        public static float Max(float x, float y) => Math.Max(x, y);

        public static float Min(float x, float y) => Math.Min(x, y);
    }

    internal static class NetFrameworkPolyfills
    {
        extension(ArgumentNullException)
        {
            public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
            {
                if (argument is null)
                    throw new ArgumentNullException(paramName);
            }
        }

        extension(Math)
        {
            public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

            public static long Clamp(long value, long min, long max) => value < min ? min : value > max ? max : value;

            public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

            public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
        }

        extension(Enum)
        {
            public static TEnum[] GetValues<TEnum>() where TEnum : struct, Enum => (TEnum[])Enum.GetValues(typeof(TEnum));
        }

        public static IOrderedEnumerable<T> Order<T>(this IEnumerable<T> source, IComparer<T>? comparer = null) =>
            source.OrderBy(item => item, comparer);
    }
}

namespace System.Collections.Generic
{
    internal sealed class ReferenceEqualityComparer : IEqualityComparer<object?>, System.Collections.IEqualityComparer
    {
        public static ReferenceEqualityComparer Instance { get; } = new();

        private ReferenceEqualityComparer()
        {
        }

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object? obj) => RuntimeHelpers.GetHashCode(obj!);
    }
}
