using System;

namespace Fold3DSpike.Core.Folding
{
    /// <summary>Immutable 2D vector on the table plane (x = world X, y = world Z). Double precision.</summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly double X;
        public readonly double Y;

        public Vec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, double s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(double s, Vec2 a) => new Vec2(a.X * s, a.Y * s);

        public double Length => Math.Sqrt(X * X + Y * Y);
        public double LengthSq => X * X + Y * Y;

        /// <summary>Unit vector; (0,0) stays (0,0).</summary>
        public Vec2 Normalized
        {
            get
            {
                double l = Length;
                return l < 1e-12 ? new Vec2(0, 0) : new Vec2(X / l, Y / l);
            }
        }

        /// <summary>Counter-clockwise perpendicular (-y, x).</summary>
        public Vec2 Perp => new Vec2(-Y, X);

        public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static double Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;
        public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        /// <summary>Closest point to <paramref name="p"/> on segment a-b.</summary>
        public static Vec2 ClosestOnSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            var ab = b - a;
            double len = ab.LengthSq;
            if (len < 1e-18) return a;
            double t = Dot(p - a, ab) / len;
            if (t < 0) t = 0;
            else if (t > 1) t = 1;
            return a + ab * t;
        }

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}
