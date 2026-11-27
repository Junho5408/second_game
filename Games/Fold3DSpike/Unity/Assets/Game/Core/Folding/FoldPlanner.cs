using System;
using System.Collections.Generic;

namespace Fold3DSpike.Core.Folding
{
    /// <summary>
    /// A crease on the table plane: the perpendicular bisector of grab point and drop point.
    /// <see cref="Normal"/> points from the moving side (where the grab point is) to the static side.
    /// </summary>
    public readonly struct FoldLine
    {
        public readonly Vec2 Point;
        public readonly Vec2 Normal;

        public FoldLine(Vec2 point, Vec2 normal)
        {
            Point = point;
            Normal = normal.Normalized;
        }

        /// <summary>Unit direction along the crease.</summary>
        public Vec2 Direction => Normal.Perp;

        /// <summary>Signed distance: negative on the moving side, positive on the static side.</summary>
        public double Side(Vec2 p) => Vec2.Dot(p - Point, Normal);

        /// <summary>Mirror image of <paramref name="p"/> across the crease.</summary>
        public Vec2 Reflect(Vec2 p) => p - Normal * (2.0 * Side(p));

        /// <summary>Unsigned distance of <paramref name="p"/> from the (infinite) crease line.</summary>
        public double Distance(Vec2 p) => Math.Abs(Side(p));

        public static FoldLine FromGrabDrop(Vec2 grab, Vec2 drop) => new FoldLine((grab + drop) * 0.5, drop - grab);
    }

    /// <summary>Result of planning one fold: the split pieces before and after the fold.</summary>
    public sealed class FoldPlan
    {
        public bool Valid;
        /// <summary>Why the plan is invalid (Korean, for UI); null if valid.</summary>
        public string Reason;
        public FoldLine Line;
        public bool Mountain;
        public Vec2 Grab;
        public Vec2 Drop;
        /// <summary>Pieces that stay where they are (clones, layers unchanged).</summary>
        public readonly List<Facet> Static = new List<Facet>();
        /// <summary>Pieces that move, at their pre-fold position and layer.</summary>
        public readonly List<Facet> Moving = new List<Facet>();
        /// <summary>The moving pieces after the fold (these objects are also in <see cref="Result"/>).</summary>
        public readonly List<Facet> Moved = new List<Facet>();
        /// <summary>The paper after the fold (only meaningful if <see cref="Valid"/>).</summary>
        public PaperState Result;
    }

    /// <summary>
    /// Flat-fold model: every fold is a 180° rotation of everything on the grab side of the crease (all layers,
    /// like real paper held flat on a table). Valley folds put the flap on top of the stack, mountain folds under it.
    /// </summary>
    public static class FoldPlanner
    {
        /// <summary>Drags shorter than this (paper units) are not a fold.</summary>
        public const double MinDrag = 0.03;
        private const double Eps = 1e-9;
        private const double MinArea = 1e-7;

        public static FoldPlan Plan(PaperState state, Vec2 grab, Vec2 drop, bool mountain)
        {
            var plan = new FoldPlan { Mountain = mountain, Grab = grab, Drop = drop };
            if (state == null || state.Facets.Count == 0)
            {
                plan.Reason = "종이가 없어요";
                return plan;
            }
            if (Vec2.Distance(grab, drop) < MinDrag)
            {
                plan.Reason = "조금 더 끌어 주세요";
                return plan;
            }

            var line = FoldLine.FromGrabDrop(grab, drop);
            plan.Line = line;

            foreach (var f in state.Facets)
            {
                Split(f, line, out var moving, out var stat);
                if (moving != null) plan.Moving.Add(moving);
                if (stat != null) plan.Static.Add(stat);
            }

            if (plan.Moving.Count == 0)
            {
                plan.Reason = "접히는 부분이 없어요";
                return plan;
            }
            if (plan.Static.Count == 0)
            {
                plan.Reason = "접는 선이 종이 밖이에요";
                return plan;
            }

            int maxAll = int.MinValue, minAll = int.MaxValue, maxMov = int.MinValue, minMov = int.MaxValue;
            foreach (var f in plan.Static)
            {
                maxAll = Math.Max(maxAll, f.Layer);
                minAll = Math.Min(minAll, f.Layer);
            }
            foreach (var f in plan.Moving)
            {
                maxAll = Math.Max(maxAll, f.Layer);
                minAll = Math.Min(minAll, f.Layer);
                maxMov = Math.Max(maxMov, f.Layer);
                minMov = Math.Min(minMov, f.Layer);
            }

            var result = new PaperState();
            foreach (var f in plan.Static) result.Facets.Add(f.Clone());
            foreach (var f in plan.Moving)
            {
                var world = new List<Vec2>(f.World.Count);
                var paper = new List<Vec2>(f.Paper.Count);
                // Reflection flips orientation: reverse to stay counter-clockwise.
                for (int i = f.World.Count - 1; i >= 0; i--)
                {
                    world.Add(line.Reflect(f.World[i]));
                    paper.Add(f.Paper[i]);
                }
                int layer = mountain
                    ? minAll - 1 - (f.Layer - minMov)
                    : maxAll + 1 + (maxMov - f.Layer);
                var moved = new Facet(world, paper, layer, !f.FrontUp);
                result.Facets.Add(moved);
                plan.Moved.Add(moved);
            }
            result.CompactLayers();
            plan.Result = result;
            plan.Valid = true;
            return plan;
        }

        /// <summary>Splits a convex facet by the crease into its moving (side &lt; 0) and static (side &gt; 0) parts.</summary>
        public static void Split(Facet f, FoldLine line, out Facet moving, out Facet stat)
        {
            int n = f.World.Count;
            var s = new double[n];
            bool anyNeg = false, anyPos = false;
            for (int i = 0; i < n; i++)
            {
                double v = line.Side(f.World[i]);
                if (Math.Abs(v) < Eps) v = 0;
                s[i] = v;
                if (v < 0) anyNeg = true;
                if (v > 0) anyPos = true;
            }

            if (!anyNeg && !anyPos)
            {
                moving = null;
                stat = null;
                return;
            }
            if (!anyPos)
            {
                moving = f.Clone();
                stat = null;
                return;
            }
            if (!anyNeg)
            {
                moving = null;
                stat = f.Clone();
                return;
            }

            var mw = new List<Vec2>();
            var mp = new List<Vec2>();
            var sw = new List<Vec2>();
            var sp = new List<Vec2>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double si = s[i], sj = s[j];
                if (si <= 0)
                {
                    mw.Add(f.World[i]);
                    mp.Add(f.Paper[i]);
                }
                if (si >= 0)
                {
                    sw.Add(f.World[i]);
                    sp.Add(f.Paper[i]);
                }
                if ((si < 0 && sj > 0) || (si > 0 && sj < 0))
                {
                    double t = si / (si - sj);
                    var w = Vec2.Lerp(f.World[i], f.World[j], t);
                    var p = Vec2.Lerp(f.Paper[i], f.Paper[j], t);
                    mw.Add(w);
                    mp.Add(p);
                    sw.Add(w);
                    sp.Add(p);
                }
            }
            moving = MakeIfValid(mw, mp, f);
            stat = MakeIfValid(sw, sp, f);
        }

        private static Facet MakeIfValid(List<Vec2> world, List<Vec2> paper, Facet src)
        {
            if (world.Count < 3) return null;
            var f = new Facet(world, paper, src.Layer, src.FrontUp);
            return Math.Abs(f.SignedArea()) < MinArea ? null : f;
        }
    }
}
