using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>
    /// Drop magnets (C-106, R-021): port of <c>balance_check.py</c> <c>mark_pieces</c>, <c>visible</c>, <c>landmarks</c>,
    /// <c>snap</c>. Pure and deterministic; ties are broken with <see cref="Key"/> (Python <c>round(v, 9)</c>) and then the
    /// raw coordinates, exactly like Python tuple <c>min</c>. Core commands never apply magnets — only input does.
    /// </summary>
    public static class DropSnap
    {
        public const double SnapPoint = 0.06, SnapLine = 0.04, SnapMark = 0.04, LinePairMin = 0.2;
        public const double GuideSnap = 0.08;

        private const double ParallelEps = 1e-12;
        private const double OutsideEps = 1e-9;
        private const double MinMarkSpan = 1e-6;
        private const double VisibleEps = 1e-6;
        private const double CrossParallel = 1e-9;
        private const double ParamSlack = 1e-9;
        private const double SamePoint = 1e-6;
        private const double ZeroDrag = 1e-9;

        /// <summary>Tie-break key: Math.Round(v, 9, ToEven) (Python <c>round(v, 9)</c>).</summary>
        public static double Key(double v) => Math.Round(v, 9, MidpointRounding.ToEven);

        /// <summary>Crease marks clipped to each facet (facet order × crease order), in world coordinates.</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public static IReadOnlyList<MarkPiece> MarkPieces(PaperState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new ReadOnlyCollection<MarkPiece>(ComputeMarkPieces(state).ToArray());
        }

        /// <summary>True when no other facet with a higher layer strictly contains q (1e-6).</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">facetIndex is not a facet index.</exception>
        public static bool IsVisible(PaperState state, Vec2 q, int facetIndex)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (facetIndex < 0 || facetIndex >= state.FacetArray.Length) throw new ArgumentOutOfRangeException(nameof(facetIndex));
            return Visible(state, q, facetIndex);
        }

        /// <summary>
        /// Landmarks: all facet world vertices, visible mark-piece ends, visible mark crossings; points within 1e-6 of an
        /// earlier one are dropped (first kept).
        /// </summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public static IReadOnlyList<Vec2> Landmarks(PaperState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new ReadOnlyCollection<Vec2>(ComputeLandmarks(state, ComputeMarkPieces(state)).ToArray());
        }

        /// <summary>R-021 drop magnet (first match of P → L → M, else free).</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public static SnapResult Snap(PaperState state, Vec2 grab, Vec2 drop)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return SnapCore(state, grab, drop);
        }

        /// <summary>Same as <see cref="Snap(PaperState, Vec2, Vec2)"/> with the tutorial target magnet first (0.08 → Guide).</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public static SnapResult Snap(PaperState state, Vec2 grab, Vec2 drop, Vec2 guideDrop)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (Vec2.Distance(drop, guideDrop) <= GuideSnap)
                return new SnapResult(guideDrop, SnapKind.Guide, 1, guideDrop, default);
            return SnapCore(state, grab, drop);
        }

        // ------------------------------------------------------------------ implementation

        private static List<MarkPiece> ComputeMarkPieces(PaperState st)
        {
            var output = new List<MarkPiece>();
            var facets = st.FacetArray;
            var creases = st.CreaseArray;
            for (int fi = 0; fi < facets.Length; fi++)
            {
                var f = facets[fi];
                var pp = Polygon.Ccw(f.Paper);
                foreach (var c in creases)
                {
                    Vec2 a = c.A, b = c.B;
                    double t0 = 0.0, t1 = 1.0;
                    var d = b - a;
                    bool ok = true;
                    for (int i = 0; i < pp.Count; i++)
                    {
                        Vec2 P = pp[i], Q = pp[(i + 1) % pp.Count];
                        var e = Q - P;
                        double num = Vec2.Cross(e, a - P), den = Vec2.Cross(e, d);
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
                    if (ok && t1 - t0 > MinMarkSpan)
                    {
                        output.Add(new MarkPiece(
                            f.PaperToWorld(new Vec2(a.X + d.X * t0, a.Y + d.Y * t0)),
                            f.PaperToWorld(new Vec2(a.X + d.X * t1, a.Y + d.Y * t1)), fi));
                    }
                }
            }
            return output;
        }

        private static bool Visible(PaperState st, Vec2 q, int fi)
        {
            var facets = st.FacetArray;
            var f = facets[fi];
            foreach (var g in facets)
            {
                if (!ReferenceEquals(g, f) && g.Layer > f.Layer && g.StrictlyContains(q, VisibleEps)) return false;
            }
            return true;
        }

        private static List<Vec2> ComputeLandmarks(PaperState st, List<MarkPiece> mp)
        {
            var pts = new List<Vec2>();
            foreach (var f in st.FacetArray) pts.AddRange(f.WorldArray);
            foreach (var m in mp)
            {
                if (Visible(st, m.A, m.FacetIndex)) pts.Add(m.A);
                if (Visible(st, m.B, m.FacetIndex)) pts.Add(m.B);
            }
            for (int i = 0; i < mp.Count; i++)
            {
                for (int j = i + 1; j < mp.Count; j++)
                {
                    Vec2 a = mp[i].A, b = mp[i].B, c = mp[j].A, d = mp[j].B;
                    double den = Vec2.Cross(b - a, d - c);
                    if (Math.Abs(den) < CrossParallel) continue;
                    double t = Vec2.Cross(c - a, d - c) / den, u = Vec2.Cross(c - a, b - a) / den;
                    if (-ParamSlack <= t && t <= 1 + ParamSlack && -ParamSlack <= u && u <= 1 + ParamSlack)
                    {
                        var q = new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
                        if (Visible(st, q, mp[i].FacetIndex) || Visible(st, q, mp[j].FacetIndex)) pts.Add(q);
                    }
                }
            }
            var res = new List<Vec2>();
            foreach (var q in pts)
            {
                bool far = true;
                foreach (var r in res)
                {
                    if (!(Vec2.Distance(q, r) > SamePoint))
                    {
                        far = false;
                        break;
                    }
                }
                if (far) res.Add(q);
            }
            return res;
        }

        private static SnapResult SnapCore(PaperState st, Vec2 grab, Vec2 drop)
        {
            var mp = ComputeMarkPieces(st);
            var lms = new List<Vec2>();
            foreach (var q in ComputeLandmarks(st, mp))
                if (Vec2.Distance(q, grab) > SamePoint) lms.Add(q);

            // 1. P: nearest landmark within SnapPoint, key (dist, x, y, raw x, raw y).
            double[] bestKey = null;
            Vec2 bestPoint = default;
            foreach (var q in lms)
            {
                double dist = Vec2.Distance(q, drop);
                if (!(dist <= SnapPoint)) continue;
                var key = new[] { Key(dist), Key(q.X), Key(q.Y), q.X, q.Y };
                if (bestKey == null || Less(key, bestKey))
                {
                    bestKey = key;
                    bestPoint = q;
                }
            }
            if (bestKey != null) return new SnapResult(bestPoint, SnapKind.Point, 1, bestPoint, default);

            // 2. Zero drag: nothing to align.
            if (Vec2.Distance(grab, drop) < ZeroDrag) return SnapResult.Free(drop);

            // 3. L: the fold line through two landmarks (pair key (Σdist, front.x, front.y, back.x, back.y, raw…)).
            var L = FoldLine.FromGrabDrop(grab, drop);
            var cand = new List<KeyValuePair<double, Vec2>>();
            foreach (var q in lms)
            {
                double dq = L.Distance(q);
                if (dq <= SnapLine) cand.Add(new KeyValuePair<double, Vec2>(dq, q));
            }
            bestKey = null;
            Vec2 front = default, back = default;
            for (int i = 0; i < cand.Count; i++)
            {
                for (int j = i + 1; j < cand.Count; j++)
                {
                    Vec2 a = cand[i].Value, b = cand[j].Value;
                    if (!(Vec2.Distance(a, b) >= LinePairMin)) continue;
                    if (Less(new[] { Key(b.X), Key(b.Y) }, new[] { Key(a.X), Key(a.Y) }))
                    {
                        var tmp = a;
                        a = b;
                        b = tmp;
                    }
                    var key = new[]
                    {
                        Key(cand[i].Key + cand[j].Key), Key(a.X), Key(a.Y), Key(b.X), Key(b.Y), a.X, a.Y, b.X, b.Y,
                    };
                    if (bestKey == null || Less(key, bestKey))
                    {
                        bestKey = key;
                        front = a;
                        back = b;
                    }
                }
            }
            if (bestKey != null) return new SnapResult(ReflectAcross(grab, front, back), SnapKind.Line, 2, front, back);

            // 4. M: nearest visible point on a mark piece within SnapMark, key (dist, x, y, raw x, raw y).
            bestKey = null;
            MarkPiece bestMark = default;
            foreach (var m in mp)
            {
                var ab = m.B - m.A;
                double t = Math.Max(0, Math.Min(1, Vec2.Dot(drop - m.A, ab) / Vec2.Dot(ab, ab)));
                var q = new Vec2(m.A.X + ab.X * t, m.A.Y + ab.Y * t);
                double dist = Vec2.Distance(q, drop);
                if (dist <= SnapMark && Visible(st, q, m.FacetIndex))
                {
                    var key = new[] { Key(dist), Key(q.X), Key(q.Y), q.X, q.Y };
                    if (bestKey == null || Less(key, bestKey))
                    {
                        bestKey = key;
                        bestPoint = q;
                        bestMark = m;
                    }
                }
            }
            if (bestKey != null) return new SnapResult(bestPoint, SnapKind.Mark, 2, bestMark.A, bestMark.B);

            return SnapResult.Free(drop);
        }

        /// <summary>Python <c>_refl_ab</c>: mirror p across the line a–b.</summary>
        private static Vec2 ReflectAcross(Vec2 p, Vec2 a, Vec2 b)
        {
            var d = b - a;
            double ln = d.Length;
            d = new Vec2(d.X / ln, d.Y / ln);
            double t = Vec2.Dot(p - a, d);
            var f = new Vec2(a.X + d.X * t, a.Y + d.Y * t);
            return new Vec2(2 * f.X - p.X, 2 * f.Y - p.Y);
        }

        /// <summary>Lexicographic "strictly less" (Python tuple comparison).</summary>
        private static bool Less(double[] x, double[] y)
        {
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] < y[i]) return true;
                if (x[i] > y[i]) return false;
            }
            return false;
        }
    }
}
