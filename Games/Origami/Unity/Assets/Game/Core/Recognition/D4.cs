using System;
using Origami.Core.Geometry;

namespace Origami.Core.Recognition
{
    /// <summary>The 8 symmetries of the square (C-110), applied to vertex coordinates.</summary>
    public static class D4
    {
        public const int Count = 8;

        /// <summary>g0 (x,y) g1 (−y,x) g2 (−x,−y) g3 (y,−x) g4 (−x,y) g5 (−y,−x) g6 (x,−y) g7 (y,x).</summary>
        /// <exception cref="ArgumentOutOfRangeException">g is not 0..7.</exception>
        public static Vec2 Apply(int g, Vec2 p)
        {
            switch (g)
            {
                case 0: return new Vec2(p.X, p.Y);
                case 1: return new Vec2(-p.Y, p.X);
                case 2: return new Vec2(-p.X, -p.Y);
                case 3: return new Vec2(p.Y, -p.X);
                case 4: return new Vec2(-p.X, p.Y);
                case 5: return new Vec2(-p.Y, -p.X);
                case 6: return new Vec2(p.X, -p.Y);
                case 7: return new Vec2(p.Y, p.X);
                default: throw new ArgumentOutOfRangeException(nameof(g));
            }
        }
    }
}
