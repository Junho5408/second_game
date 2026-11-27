using System;
using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>The fold line clipped to the current paper outline — diagram and guide line (C-105, Python <c>line_on_paper</c>).</summary>
    public static class LineOnPaper
    {
        private const double ZeroDrag = 1e-9;
        private const double ParamRange = 10.0;
        private const double ParallelEps = 1e-12;
        private const double OutsideEps = 1e-9;
        private const double MinSpan = 1e-9;

        /// <summary>
        /// End points a (min parameter) and b (max parameter) of the fold line of grab→drop over all facets. Not rounded.
        /// False when no facet is crossed, or when Distance(grab, drop) &lt; 1e-9 (no line; QA-2 of ORIGAMI-008).
        /// </summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public static bool TryGet(PaperState state, Vec2 grab, Vec2 drop, out Vec2 a, out Vec2 b)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            a = default;
            b = default;
            if (Vec2.Distance(grab, drop) < ZeroDrag) return false;
            var L = FoldLine.FromGrabDrop(grab, drop);
            var d = new Vec2(-L.Normal.Y, L.Normal.X);
            bool any = false;
            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var f in state.FacetArray)
            {
                var w = Polygon.Ccw(f.World);
                double t0 = -ParamRange, t1 = ParamRange;
                bool ok = true;
                for (int i = 0; i < w.Count; i++)
                {
                    Vec2 P = w[i], Q = w[(i + 1) % w.Count];
                    var e = Q - P;
                    double num = Vec2.Cross(e, L.Point - P), den = Vec2.Cross(e, d);
                    if (Math.Abs(den) < ParallelEps)
                    {
                        if (num < -OutsideEps)
                        {
                            ok = false;
                            break;
                        }
                        continue;
                    }
                    double t = -num / den;
                    if (den > 0) t0 = Math.Max(t0, t);
                    else t1 = Math.Min(t1, t);
                }
                if (ok && t1 > t0 + MinSpan)
                {
                    any = true;
                    lo = Math.Min(lo, Math.Min(t0, t1));
                    hi = Math.Max(hi, Math.Max(t0, t1));
                }
            }
            if (!any) return false;
            a = new Vec2(L.Point.X + d.X * lo, L.Point.Y + d.Y * lo);
            b = new Vec2(L.Point.X + d.X * hi, L.Point.Y + d.Y * hi);
            return true;
        }
    }
}
