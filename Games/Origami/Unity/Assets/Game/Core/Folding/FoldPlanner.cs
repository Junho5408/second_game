using System;
using System.Collections.Generic;
using System.Linq;
using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>
    /// Fold engine (C-103, R-007). A line-by-line port of <c>balance_check.py</c> <c>_split</c>, <c>plan</c>, <c>fold</c>,
    /// <c>crease</c>, <c>flip</c>, <c>_hinged</c>, <c>_overlap_len</c>, <c>_overlap_area</c>: same order, constants and
    /// comparison operators (CODING_CONVENTION "참조 구현 동등성"). Pure functions; invalid folds are not exceptions
    /// (<see cref="FoldPlan.Valid"/> false). Only <see cref="Apply"/> / <see cref="ApplyAll"/> throw on invalid steps.
    /// </summary>
    public static class FoldPlanner
    {
        public const double MinDrag = 0.03;
        public const double HingeMin = 0.02;
        public const double SideEps = 1e-9;
        public const double MinArea = 1e-7;
        public const double ContainsEps = 1e-6;

        // Python _overlap_len tolerances (named here so the reference constants are visible in one place).
        private const double OverlapMinLength = 1e-9;
        private const double OverlapCollinear = 1e-6;

        /// <summary>Plans (and computes the result of) a fold. layers 0 = all layers, n ≥ 1 = top n layers.</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public static FoldPlan PlanFold(PaperState state, Vec2 grab, Vec2 drop, bool mountain, int layers)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var p = Plan(state, grab, drop, layers, out FoldError error);
            if (error != FoldError.None) return FoldPlan.Invalid(error, false, grab, drop, mountain, layers);
            return FoldResult(state, p, grab, drop, mountain, layers);
        }

        /// <summary>Plans a crease (fold and unfold): same moving set as a fold, shape unchanged, marks added.</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public static FoldPlan PlanCrease(PaperState state, Vec2 grab, Vec2 drop, int layers)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var p = Plan(state, grab, drop, layers, out FoldError error);
            if (error != FoldError.None) return FoldPlan.Invalid(error, true, grab, drop, false, layers);
            var L = p.Line;
            var marks = new List<CreaseSegment>();
            foreach (var f in p.Moving)
            {
                var w = f.WorldArray;
                var pp = f.PaperArray;
                int n = w.Length;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    if (L.Side(w[i]) == 0 && L.Side(w[j]) == 0) marks.Add(new CreaseSegment(pp[i], pp[j]));
                }
            }
            var creases = new List<CreaseSegment>(state.CreaseArray);
            creases.AddRange(marks);
            var result = new PaperState(state.FacetArray, creases);
            return FoldPlan.Create(true, grab, drop, false, layers, L, p.Partial, p.Moving, p.Static, new List<Facet>(), marks,
                result);
        }

        /// <summary>Turns the sheet over left–right around x = <see cref="PaperState.FlipAxisX"/> (Python <c>flip</c>).</summary>
        /// <exception cref="ArgumentNullException">state is null.</exception>
        public static PaperState Flip(PaperState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            double cx = state.FlipAxisX();
            int mx = state.MaxLayer();
            var facets = new List<Facet>(state.Facets.Count);
            foreach (var f in state.FacetArray)
            {
                var w = f.WorldArray;
                var pp = f.PaperArray;
                int n = w.Length;
                var nw = new Vec2[n];
                var np = new Vec2[n];
                for (int k = 0; k < n; k++)
                {
                    var q = w[n - 1 - k];
                    nw[k] = new Vec2(2 * cx - q.X, q.Y);
                    np[k] = pp[n - 1 - k];
                }
                facets.Add(new Facet(nw, np, mx - f.Layer, !f.FrontUp));
            }
            return new PaperState(facets, state.CreaseArray);
        }

        /// <summary>Applies one step; an invalid fold/crease throws (reference sequences must be valid).</summary>
        /// <exception cref="ArgumentNullException">state or step is null.</exception>
        /// <exception cref="InvalidOperationException">the fold/crease is invalid (message contains the FoldError).</exception>
        public static PaperState Apply(PaperState state, FoldStep step)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (step == null) throw new ArgumentNullException(nameof(step));
            FoldPlan plan;
            switch (step.Kind)
            {
                case FoldStepKind.Fold:
                    plan = PlanFold(state, step.Grab, step.Drop, step.Mountain, step.Layers);
                    break;
                case FoldStepKind.Crease:
                    plan = PlanCrease(state, step.Grab, step.Drop, step.Layers);
                    break;
                case FoldStepKind.Flip:
                    return Flip(state);
                default:
                    throw new ArgumentException("unknown step kind " + step.Kind, nameof(step));
            }
            if (!plan.Valid) throw new InvalidOperationException("invalid " + step.Kind + ": " + plan.Error);
            return plan.Result;
        }

        /// <summary>Applies all steps in order starting from <paramref name="start"/> (null = <see cref="PaperState.Initial"/>).</summary>
        /// <exception cref="ArgumentNullException">steps (or one of them) is null.</exception>
        /// <exception cref="InvalidOperationException">a step is invalid.</exception>
        public static PaperState ApplyAll(IEnumerable<FoldStep> steps, PaperState start = null)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));
            var st = start ?? PaperState.Initial;
            foreach (var s in steps) st = Apply(st, s);
            return st;
        }

        // ------------------------------------------------------------------ Python plan

        private sealed class Planned
        {
            public FoldLine Line;
            public List<Facet> Moving;
            public List<Facet> Static;
            public bool Partial;
        }

        private static Planned Plan(PaperState st, Vec2 grab, Vec2 drop, int layers, out FoldError error)
        {
            error = FoldError.None;
            if (layers < 0)
            {
                error = FoldError.InvalidLayers;
                return null;
            }
            if (Vec2.Distance(grab, drop) < MinDrag)
            {
                error = FoldError.DragTooShort;
                return null;
            }
            var L = FoldLine.FromGrabDrop(grab, drop);
            var facets = st.FacetArray;
            int count = facets.Length;
            var mParts = new Facet[count];
            var sParts = new Facet[count];
            for (int i = 0; i < count; i++) Split(facets[i], L, out mParts[i], out sParts[i]);

            var grabSide = new List<int>();
            for (int i = 0; i < count; i++) if (mParts[i] != null) grabSide.Add(i);
            var inMv = new bool[count];
            foreach (int i in grabSide) inMv[i] = true;
            int mvCount = grabSide.Count;

            if (layers >= 1)
            {
                // Python: sorted(contains(grab), key=-layer) — stable, so equal layers keep index order.
                var stack = Enumerable.Range(0, count)
                    .Where(i => facets[i].Contains(grab, ContainsEps))
                    .OrderByDescending(i => facets[i].Layer)
                    .ToList();
                if (stack.Count == 0)
                {
                    error = FoldError.NothingGrabbed;
                    return null;
                }
                if (layers < stack.Count)
                {
                    Array.Clear(inMv, 0, count);
                    var mv = new List<int>();
                    for (int k = 0; k < layers; k++)
                    {
                        int i = stack[k];
                        if (mParts[i] != null && !inMv[i])
                        {
                            inMv[i] = true;
                            mv.Add(i);
                        }
                    }
                    bool changed = true;
                    while (changed)
                    {
                        changed = false;
                        foreach (int j in grabSide)
                        {
                            if (inMv[j]) continue;
                            var mj = mParts[j];
                            for (int k = 0; k < mv.Count; k++)
                            {
                                var mi = mParts[mv[k]];
                                if (Hinged(mi, mj, L) || (mj.Layer > mi.Layer && OverlapArea(mi, mj) > MinArea))
                                {
                                    inMv[j] = true;
                                    mv.Add(j);
                                    changed = true;
                                    break;
                                }
                            }
                        }
                    }
                    mvCount = mv.Count;
                }
            }
            if (mvCount == 0)
            {
                error = FoldError.NothingToFold;
                return null;
            }
            var moving = new List<Facet>();
            var stat = new List<Facet>();
            for (int i = 0; i < count; i++)
            {
                if (inMv[i])
                {
                    moving.Add(mParts[i]);
                    if (sParts[i] != null) stat.Add(sParts[i]);
                }
                else
                {
                    stat.Add(facets[i]);
                }
            }
            if (stat.Count == 0)
            {
                error = FoldError.LineOutsidePaper;
                return null;
            }
            return new Planned { Line = L, Moving = moving, Static = stat, Partial = mvCount < grabSide.Count };
        }

        /// <summary>Python <c>_split</c>: (moving part or null, staying part or null).</summary>
        private static void Split(Facet f, FoldLine L, out Facet moving, out Facet staying)
        {
            var w = f.WorldArray;
            var p = f.PaperArray;
            int n = w.Length;
            var s = new double[n];
            bool anyNeg = false, anyPos = false;
            for (int i = 0; i < n; i++)
            {
                s[i] = L.Side(w[i]);
                if (s[i] < 0) anyNeg = true;
                if (s[i] > 0) anyPos = true;
            }
            if (!anyNeg)
            {
                moving = null;
                staying = f;
                return;
            }
            if (!anyPos)
            {
                moving = f;
                staying = null;
                return;
            }
            var mw = new List<Vec2>();
            var mp = new List<Vec2>();
            var sw = new List<Vec2>();
            var sp = new List<Vec2>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (s[i] <= 0)
                {
                    mw.Add(w[i]);
                    mp.Add(p[i]);
                }
                if (s[i] >= 0)
                {
                    sw.Add(w[i]);
                    sp.Add(p[i]);
                }
                if ((s[i] < 0 && 0 < s[j]) || (s[i] > 0 && 0 > s[j]))
                {
                    double t = s[i] / (s[i] - s[j]);
                    var qw = new Vec2(w[i].X + (w[j].X - w[i].X) * t, w[i].Y + (w[j].Y - w[i].Y) * t);
                    var qp = new Vec2(p[i].X + (p[j].X - p[i].X) * t, p[i].Y + (p[j].Y - p[i].Y) * t);
                    mw.Add(qw);
                    mp.Add(qp);
                    sw.Add(qw);
                    sp.Add(qp);
                }
            }
            var m = mw.Count >= 3 && Math.Abs(Polygon.SignedArea(mw)) > MinArea ? new Facet(mw, mp, f.Layer, f.FrontUp) : null;
            var st = sw.Count >= 3 && Math.Abs(Polygon.SignedArea(sw)) > MinArea ? new Facet(sw, sp, f.Layer, f.FrontUp) : null;
            if (m == null)
            {
                moving = null;
                staying = f;
                return;
            }
            if (st == null)
            {
                moving = f;
                staying = null;
                return;
            }
            moving = m;
            staying = st;
        }

        /// <summary>Python <c>_overlap_len</c>: length of the collinear overlap of segment c–d along a–b.</summary>
        internal static double OverlapLen(Vec2 a, Vec2 b, Vec2 c, Vec2 d)
        {
            var u = b - a;
            double lu = u.Length;
            if (lu < OverlapMinLength || Math.Abs(Vec2.Cross(u, c - a)) / lu > OverlapCollinear ||
                Math.Abs(Vec2.Cross(u, d - a)) / lu > OverlapCollinear)
                return 0;
            double t1 = Vec2.Dot(c - a, u) / lu, t2 = Vec2.Dot(d - a, u) / lu;
            return Math.Min(lu, Math.Max(t1, t2)) - Math.Max(0, Math.Min(t1, t2));
        }

        /// <summary>Python <c>_hinged</c>: a and b share a paper edge ≥ HingeMin that is not on the fold line.</summary>
        internal static bool Hinged(Facet a, Facet b, FoldLine L)
        {
            var aw = a.WorldArray;
            var ap = a.PaperArray;
            var bp = b.PaperArray;
            int na = ap.Length, nb = bp.Length;
            for (int i = 0; i < na; i++)
            {
                if (L.Side(aw[i]) == 0 && L.Side(aw[(i + 1) % na]) == 0) continue;
                for (int j = 0; j < nb; j++)
                {
                    if (OverlapLen(ap[i], ap[(i + 1) % na], bp[j], bp[(j + 1) % nb]) >= HingeMin) return true;
                }
            }
            return false;
        }

        /// <summary>Python <c>_overlap_area</c>: Sutherland–Hodgman intersection area of the two world polygons.</summary>
        internal static double OverlapArea(Facet a, Facet b)
        {
            var poly = Polygon.Ccw(a.World);
            var bw = Polygon.Ccw(b.World);
            for (int i = 0; i < bw.Count; i++)
            {
                poly = Clip(poly, bw[i], bw[(i + 1) % bw.Count]);
                if (poly.Count < 3) return 0;
            }
            return Math.Abs(Polygon.SignedArea(poly));
        }

        /// <summary>Python <c>_clip</c>: keep the part of poly left of a→b (Cross ≥ 0).</summary>
        private static List<Vec2> Clip(List<Vec2> poly, Vec2 a, Vec2 b)
        {
            var output = new List<Vec2>();
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vec2 P = poly[i], Q = poly[(i + 1) % n];
                double sp = Vec2.Cross(b - a, P - a), sq = Vec2.Cross(b - a, Q - a);
                if (sp >= 0) output.Add(P);
                if ((sp > 0 && 0 > sq) || (sp < 0 && 0 < sq))
                {
                    double t = sp / (sp - sq);
                    output.Add(new Vec2(P.X + (Q.X - P.X) * t, P.Y + (Q.Y - P.Y) * t));
                }
            }
            return output;
        }

        // ------------------------------------------------------------------ Python fold

        private static FoldPlan FoldResult(PaperState st, Planned p, Vec2 grab, Vec2 drop, bool mountain, int layers)
        {
            int maxA = int.MinValue, minA = int.MaxValue, maxM = int.MinValue, minM = int.MaxValue;
            foreach (var f in p.Moving)
            {
                maxA = Math.Max(maxA, f.Layer);
                minA = Math.Min(minA, f.Layer);
                maxM = Math.Max(maxM, f.Layer);
                minM = Math.Min(minM, f.Layer);
            }
            foreach (var f in p.Static)
            {
                maxA = Math.Max(maxA, f.Layer);
                minA = Math.Min(minA, f.Layer);
            }

            int total = p.Static.Count + p.Moving.Count;
            var raw = new int[total];
            int idx = 0;
            foreach (var f in p.Static)
            {
                int layer = f.Layer;
                if (mountain && p.Partial && layer >= minM) layer += maxM - minM + 1;
                raw[idx++] = layer;
            }
            foreach (var f in p.Moving)
            {
                int layer;
                if (!mountain) layer = maxA + 1 + (maxM - f.Layer);
                else if (p.Partial) layer = minM + (maxM - f.Layer);
                else layer = minA - 1 - (f.Layer - minM);
                raw[idx++] = layer;
            }

            // Python rank: distinct layer values ascending → 0, 1, 2, …
            var distinct = new SortedSet<int>(raw);
            var rank = new Dictionary<int, int>();
            int r = 0;
            foreach (int v in distinct) rank[v] = r++;

            var output = new List<Facet>(total);
            var moved = new List<Facet>(p.Moving.Count);
            idx = 0;
            foreach (var f in p.Static)
            {
                output.Add(new Facet(f.WorldArray, f.PaperArray, rank[raw[idx++]], f.FrontUp));
            }
            var L = p.Line;
            foreach (var f in p.Moving)
            {
                var w = f.WorldArray;
                var pp = f.PaperArray;
                int n = w.Length;
                var nw = new Vec2[n];
                var np = new Vec2[n];
                for (int k = 0; k < n; k++)
                {
                    nw[k] = L.Reflect(w[n - 1 - k]);
                    np[k] = pp[n - 1 - k];
                }
                var mf = new Facet(nw, np, rank[raw[idx++]], !f.FrontUp);
                output.Add(mf);
                moved.Add(mf);
            }
            var result = new PaperState(output, st.CreaseArray);
            return FoldPlan.Create(false, grab, drop, mountain, layers, L, p.Partial, p.Moving, p.Static, moved,
                new List<CreaseSegment>(), result);
        }
    }
}
