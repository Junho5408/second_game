using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>
    /// Immutable sheet state: flat convex facets + crease marks (C-102, D-101). Every engine operation returns a new
    /// instance, so snapshots, previews and undo history can share states safely. Facet order is part of the state.
    /// </summary>
    public sealed class PaperState
    {
        private static readonly PaperState InitialState = CreateInitial();

        private readonly Facet[] _facets;
        private readonly CreaseSegment[] _creases;

        /// <exception cref="ArgumentNullException">facets is null.</exception>
        /// <exception cref="ArgumentException">no facets, or a null facet.</exception>
        public PaperState(IEnumerable<Facet> facets, IEnumerable<CreaseSegment> creases)
        {
            if (facets == null) throw new ArgumentNullException(nameof(facets));
            _facets = new List<Facet>(facets).ToArray();
            if (_facets.Length == 0) throw new ArgumentException("a paper state needs at least one facet", nameof(facets));
            foreach (var f in _facets)
                if (f == null) throw new ArgumentException("null facet", nameof(facets));
            _creases = creases == null ? new CreaseSegment[0] : new List<CreaseSegment>(creases).ToArray();
            Facets = new ReadOnlyCollection<Facet>(_facets);
            Creases = new ReadOnlyCollection<CreaseSegment>(_creases);
        }

        /// <summary>The unfolded sheet [−1,1]², white side up, no creases. Always the same instance.</summary>
        public static PaperState Initial => InitialState;

        public IReadOnlyList<Facet> Facets { get; }
        public IReadOnlyList<CreaseSegment> Creases { get; }

        /// <summary>Largest facet layer.</summary>
        public int MaxLayer()
        {
            int m = int.MinValue;
            foreach (var f in _facets) if (f.Layer > m) m = f.Layer;
            return m;
        }

        /// <summary>Σ |facet area| (4.0 for any fold of the initial sheet).</summary>
        public double TotalArea()
        {
            double a = 0;
            foreach (var f in _facets) a += f.Area;
            return a;
        }

        /// <summary>Number of facets that contain <paramref name="p"/> (border included, 1e-6) = stack count M.</summary>
        public int StackCountAt(Vec2 p)
        {
            int n = 0;
            foreach (var f in _facets) if (f.Contains(p, FoldPlanner.ContainsEps)) n++;
            return n;
        }

        /// <summary>Index of the highest-layer facet containing <paramref name="p"/> (1e-6), or −1.</summary>
        public int TopFacetAt(Vec2 p)
        {
            int best = -1;
            for (int i = 0; i < _facets.Length; i++)
            {
                if (!_facets[i].Contains(p, FoldPlanner.ContainsEps)) continue;
                if (best < 0 || _facets[i].Layer > _facets[best].Layer) best = i;
            }
            return best;
        }

        /// <summary>
        /// Grab snapping (Fold3DSpike): nearest facet vertex within <paramref name="cornerRadius"/> (equal distance → later
        /// one), else nearest point on any facet edge within <paramref name="edgeRadius"/>, else <paramref name="p"/> if it
        /// lies on the paper. Returns false if nothing can be grabbed.
        /// </summary>
        public bool TrySnapGrab(Vec2 p, double cornerRadius, double edgeRadius, out Vec2 grab, out bool isCorner)
        {
            grab = p;
            isCorner = false;
            double best = cornerRadius;
            bool found = false;
            foreach (var f in _facets)
            {
                foreach (var v in f.WorldArray)
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
            foreach (var f in _facets)
            {
                var w = f.WorldArray;
                for (int i = 0; i < w.Length; i++)
                {
                    var c = Vec2.ClosestOnSegment(p, w[i], w[(i + 1) % w.Length]);
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

        /// <summary>(min + max) / 2 of all world vertex x values — the vertical axis <see cref="FoldPlanner.Flip"/> uses.</summary>
        public double FlipAxisX()
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            foreach (var f in _facets)
            foreach (var v in f.WorldArray)
            {
                if (v.X < minX) minX = v.X;
                if (v.X > maxX) maxX = v.X;
            }
            return 0.5 * (minX + maxX);
        }

        internal Facet[] FacetArray => _facets;
        internal CreaseSegment[] CreaseArray => _creases;

        private static PaperState CreateInitial()
        {
            var sq = new[] { new Vec2(-1, -1), new Vec2(1, -1), new Vec2(1, 1), new Vec2(-1, 1) };
            return new PaperState(new[] { new Facet(sq, sq, 0, false) }, null);
        }
    }
}
