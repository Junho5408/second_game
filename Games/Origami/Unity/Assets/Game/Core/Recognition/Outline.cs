using System;
using System.Collections.Generic;
using Origami.Core.Folding;
using Origami.Core.Geometry;

namespace Origami.Core.Recognition
{
    /// <summary>
    /// Union of convex polygons as a row scan (C-110, D-105, ARCHITECTURE §9 / A2-3): rows y_k = (k + 0.5)·h,
    /// h = <see cref="RowStep"/>, each row a sorted list of disjoint [lo, hi] intervals. x is exact, y is midpoint
    /// integration. Immutable and deterministic.
    /// </summary>
    public sealed class Outline
    {
        public const double RowStep = 0.001;

        private readonly int[] _rows;    // row index k, ascending
        private readonly int[] _start;   // _start[r] .. _start[r + 1] − 1 = intervals of row r
        private readonly double[] _lo;
        private readonly double[] _hi;

        private Outline(int[] rows, int[] start, double[] lo, double[] hi)
        {
            _rows = rows;
            _start = start;
            _lo = lo;
            _hi = hi;
            double h = RowStep, len = 0, sx = 0, sy = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                double y = (rows[r] + 0.5) * h;
                for (int i = start[r]; i < start[r + 1]; i++)
                {
                    double w = hi[i] - lo[i];
                    len += w;
                    sx += (hi[i] * hi[i] - lo[i] * lo[i]) / 2;
                    sy += w * y;
                }
            }
            Area = h * len;
            Centroid = Area > 0 ? new Vec2(h * sx / Area, h * sy / Area) : new Vec2(0, 0);
        }

        /// <summary>Area of the union.</summary>
        public double Area { get; }

        /// <summary>Area centroid of the union ((0,0) when empty).</summary>
        public Vec2 Centroid { get; }

        /// <summary>Scans the union of the polygons (any orientation, convex expected).</summary>
        /// <exception cref="ArgumentNullException">polygons is null.</exception>
        /// <exception cref="ArgumentException">a polygon is null.</exception>
        public static Outline FromPolygons(IEnumerable<IReadOnlyList<Vec2>> polygons)
        {
            if (polygons == null) throw new ArgumentNullException(nameof(polygons));
            double h = RowStep;
            var rows = new SortedDictionary<int, List<KeyValuePair<double, double>>>();
            var xs = new List<double>();
            foreach (var poly in polygons)
            {
                if (poly == null) throw new ArgumentException("null polygon", nameof(polygons));
                int n = poly.Count;
                if (n < 2) continue;
                double minY = double.MaxValue, maxY = double.MinValue;
                for (int i = 0; i < n; i++)
                {
                    minY = Math.Min(minY, poly[i].Y);
                    maxY = Math.Max(maxY, poly[i].Y);
                }
                int k0 = (int)Math.Floor(minY / h - 0.5), k1 = (int)Math.Ceiling(maxY / h - 0.5);
                for (int k = k0; k <= k1; k++)
                {
                    double y = (k + 0.5) * h;
                    xs.Clear();
                    for (int i = 0; i < n; i++)
                    {
                        var a = poly[i];
                        var b = poly[(i + 1) % n];
                        if ((a.Y <= y && y < b.Y) || (b.Y <= y && y < a.Y))
                            xs.Add(a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                    }
                    if (xs.Count < 2) continue;
                    double lo = double.MaxValue, hi = double.MinValue;
                    foreach (double x in xs)
                    {
                        lo = Math.Min(lo, x);
                        hi = Math.Max(hi, x);
                    }
                    if (!(hi > lo)) continue;
                    if (!rows.TryGetValue(k, out var list))
                    {
                        list = new List<KeyValuePair<double, double>>();
                        rows.Add(k, list);
                    }
                    list.Add(new KeyValuePair<double, double>(lo, hi));
                }
            }

            var rowKeys = new List<int>(rows.Count);
            var start = new List<int>(rows.Count + 1);
            var los = new List<double>();
            var his = new List<double>();
            foreach (var kv in rows)
            {
                var list = kv.Value;
                list.Sort((p, q) => p.Key != q.Key ? p.Key.CompareTo(q.Key) : p.Value.CompareTo(q.Value));
                rowKeys.Add(kv.Key);
                start.Add(los.Count);
                double curLo = list[0].Key, curHi = list[0].Value;
                for (int i = 1; i < list.Count; i++)
                {
                    if (list[i].Key <= curHi)
                    {
                        if (list[i].Value > curHi) curHi = list[i].Value;
                    }
                    else
                    {
                        los.Add(curLo);
                        his.Add(curHi);
                        curLo = list[i].Key;
                        curHi = list[i].Value;
                    }
                }
                los.Add(curLo);
                his.Add(curHi);
            }
            start.Add(los.Count);
            return new Outline(rowKeys.ToArray(), start.ToArray(), los.ToArray(), his.ToArray());
        }

        /// <summary>Outline of all facet world polygons.</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public static Outline FromState(PaperState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return FromPolygons(WorldPolygons(state));
        }

        /// <summary>Area of the intersection of two outlines (same rows merged).</summary>
        /// <exception cref="ArgumentNullException">a or b is null.</exception>
        public static double IntersectionArea(Outline a, Outline b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            double sum = 0;
            int ra = 0, rb = 0;
            while (ra < a._rows.Length && rb < b._rows.Length)
            {
                int ka = a._rows[ra], kb = b._rows[rb];
                if (ka < kb)
                {
                    ra++;
                    continue;
                }
                if (kb < ka)
                {
                    rb++;
                    continue;
                }
                int i = a._start[ra], iEnd = a._start[ra + 1];
                int j = b._start[rb], jEnd = b._start[rb + 1];
                while (i < iEnd && j < jEnd)
                {
                    double lo = Math.Max(a._lo[i], b._lo[j]);
                    double hi = Math.Min(a._hi[i], b._hi[j]);
                    if (hi > lo) sum += hi - lo;
                    if (a._hi[i] < b._hi[j]) i++;
                    else j++;
                }
                ra++;
                rb++;
            }
            return sum * RowStep;
        }

        /// <summary>IoU = I / (A + B − I); 0 when both are empty.</summary>
        internal static double IoU(Outline a, Outline b)
        {
            double inter = IntersectionArea(a, b);
            double union = a.Area + b.Area - inter;
            return union > 0 ? inter / union : 0;
        }

        internal static List<IReadOnlyList<Vec2>> WorldPolygons(PaperState state)
        {
            var polys = new List<IReadOnlyList<Vec2>>(state.Facets.Count);
            foreach (var f in state.Facets) polys.Add(f.World);
            return polys;
        }

        /// <summary>Polygons translated so the outline centroid is at the origin, then mapped by D4 element g.</summary>
        internal static List<IReadOnlyList<Vec2>> Centered(List<IReadOnlyList<Vec2>> polys, int g)
        {
            var c = FromPolygons(polys).Centroid;
            var output = new List<IReadOnlyList<Vec2>>(polys.Count);
            foreach (var poly in polys)
            {
                var q = new Vec2[poly.Count];
                for (int i = 0; i < q.Length; i++) q[i] = D4.Apply(g, new Vec2(poly[i].X - c.X, poly[i].Y - c.Y));
                output.Add(q);
            }
            return output;
        }
    }
}
