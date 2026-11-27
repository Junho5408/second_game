using System;
using System.Collections.Generic;

namespace Fold3DSpike.Core.Folding
{
    /// <summary>
    /// One flat convex piece of the folded sheet. <see cref="World"/> is its current position on the table plane
    /// (always counter-clockwise), <see cref="Paper"/> the matching points on the unfolded sheet (same index).
    /// <see cref="Layer"/> is the global stacking order (0 = lowest), <see cref="FrontUp"/> whether the coloured
    /// front side faces the camera/up.
    /// </summary>
    public sealed class Facet
    {
        public readonly List<Vec2> World;
        public readonly List<Vec2> Paper;
        public int Layer;
        public bool FrontUp;

        public Facet(List<Vec2> world, List<Vec2> paper, int layer, bool frontUp)
        {
            if (world == null || paper == null || world.Count != paper.Count || world.Count < 3)
                throw new ArgumentException("facet needs >= 3 matching world/paper points");
            World = world;
            Paper = paper;
            Layer = layer;
            FrontUp = frontUp;
        }

        public Facet Clone() => new Facet(new List<Vec2>(World), new List<Vec2>(Paper), Layer, FrontUp);

        /// <summary>Signed area of the world polygon (positive = counter-clockwise).</summary>
        public double SignedArea()
        {
            double a = 0;
            for (int i = 0; i < World.Count; i++)
            {
                var p = World[i];
                var q = World[(i + 1) % World.Count];
                a += Vec2.Cross(p, q);
            }
            return a * 0.5;
        }

        /// <summary>True if <paramref name="p"/> lies inside (or on the border of) this convex CCW polygon.</summary>
        public bool Contains(Vec2 p, double eps = 1e-9)
        {
            for (int i = 0; i < World.Count; i++)
            {
                var a = World[i];
                var b = World[(i + 1) % World.Count];
                if (Vec2.Cross(b - a, p - a) < -eps) return false;
            }
            return true;
        }
    }

    /// <summary>The whole (possibly folded) sheet: a flat stack of facets on the table plane.</summary>
    public sealed class PaperState
    {
        public readonly List<Facet> Facets = new List<Facet>();

        /// <summary>Unfolded square sheet centred on the origin, side = 2 * <paramref name="half"/>.</summary>
        public static PaperState CreateSquare(double half, bool frontUp)
        {
            var s = new PaperState();
            var pts = new List<Vec2>
            {
                new Vec2(-half, -half), new Vec2(half, -half), new Vec2(half, half), new Vec2(-half, half),
            };
            s.Facets.Add(new Facet(new List<Vec2>(pts), new List<Vec2>(pts), 0, frontUp));
            return s;
        }

        public PaperState Clone()
        {
            var s = new PaperState();
            foreach (var f in Facets) s.Facets.Add(f.Clone());
            return s;
        }

        public int MaxLayer()
        {
            int m = int.MinValue;
            foreach (var f in Facets) if (f.Layer > m) m = f.Layer;
            return Facets.Count == 0 ? 0 : m;
        }

        /// <summary>Total visible paper area of all facets (sum of absolute areas). Constant under folding.</summary>
        public double TotalArea()
        {
            double a = 0;
            foreach (var f in Facets) a += Math.Abs(f.SignedArea());
            return a;
        }

        /// <summary>Index of the top-most facet containing <paramref name="p"/>, or -1.</summary>
        public int TopFacetAt(Vec2 p)
        {
            int best = -1;
            for (int i = 0; i < Facets.Count; i++)
            {
                if (!Facets[i].Contains(p, 1e-6)) continue;
                if (best < 0 || Facets[i].Layer > Facets[best].Layer) best = i;
            }
            return best;
        }

        /// <summary>
        /// Grab-point snapping: nearest facet vertex within <paramref name="cornerRadius"/>, else nearest point
        /// on any facet edge within <paramref name="edgeRadius"/>, else <paramref name="p"/> itself if it lies on the
        /// paper. Returns false if nothing can be grabbed there.
        /// </summary>
        public bool TrySnapGrab(Vec2 p, double cornerRadius, double edgeRadius, out Vec2 grab, out bool isCorner)
        {
            grab = p;
            isCorner = false;
            double best = cornerRadius;
            bool found = false;
            foreach (var f in Facets)
            {
                foreach (var v in f.World)
                {
                    double d = Vec2.Distance(v, p);
                    if (d <= best)
                    {
                        best = d;
                        grab = v;
                        found = true;
                    }
                }
            }
            if (found)
            {
                isCorner = true;
                return true;
            }

            best = edgeRadius;
            foreach (var f in Facets)
            {
                for (int i = 0; i < f.World.Count; i++)
                {
                    var c = Vec2.ClosestOnSegment(p, f.World[i], f.World[(i + 1) % f.World.Count]);
                    double d = Vec2.Distance(c, p);
                    if (d <= best)
                    {
                        best = d;
                        grab = c;
                        found = true;
                    }
                }
            }
            if (found) return true;

            if (TopFacetAt(p) >= 0)
            {
                grab = p;
                return true;
            }
            return false;
        }

        /// <summary>Nearest facet vertex within <paramref name="radius"/> of <paramref name="p"/>.</summary>
        public bool TryNearestVertex(Vec2 p, double radius, out Vec2 vertex)
        {
            vertex = p;
            double best = radius;
            bool found = false;
            foreach (var f in Facets)
            {
                foreach (var v in f.World)
                {
                    double d = Vec2.Distance(v, p);
                    if (d <= best)
                    {
                        best = d;
                        vertex = v;
                        found = true;
                    }
                }
            }
            return found;
        }

        /// <summary>Re-numbers layers densely (0..k) keeping their order.</summary>
        public void CompactLayers()
        {
            var distinct = new SortedSet<int>();
            foreach (var f in Facets) distinct.Add(f.Layer);
            var rank = new Dictionary<int, int>();
            int r = 0;
            foreach (var l in distinct) rank[l] = r++;
            foreach (var f in Facets) f.Layer = rank[f.Layer];
        }
    }
}
