using System.Collections.Generic;
using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>Internal polygon helpers shared by the engine (Python <c>_area</c>, <c>_clip</c>).</summary>
    internal static class Polygon
    {
        /// <summary>Python <c>_area</c>: 0.5 · Σ Cross(p[i], p[i+1]).</summary>
        public static double SignedArea(IReadOnlyList<Vec2> p)
        {
            double s = 0;
            int n = p.Count;
            for (int i = 0; i < n; i++) s += Vec2.Cross(p[i], p[(i + 1) % n]);
            return 0.5 * s;
        }

        /// <summary>Returns the points counter-clockwise (reversed copy when the signed area is not positive).</summary>
        public static List<Vec2> Ccw(IReadOnlyList<Vec2> p)
        {
            var list = new List<Vec2>(p);
            if (!(SignedArea(p) > 0)) list.Reverse();
            return list;
        }
    }
}
