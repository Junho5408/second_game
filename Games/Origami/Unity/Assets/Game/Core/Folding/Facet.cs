using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>
    /// One flat convex piece of the (possibly folded) sheet (C-102, D-101). Immutable.
    /// <see cref="World"/> = current position on the table, <see cref="Paper"/> = the same points on the unfolded sheet
    /// (same index). Engine output is always counter-clockwise in world; hand-made test states may use any orientation.
    /// </summary>
    public sealed class Facet
    {
        private readonly Vec2[] _world;
        private readonly Vec2[] _paper;

        /// <exception cref="ArgumentNullException">world or paper is null.</exception>
        /// <exception cref="ArgumentException">fewer than 3 points or different counts.</exception>
        public Facet(IEnumerable<Vec2> world, IEnumerable<Vec2> paper, int layer, bool frontUp)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (paper == null) throw new ArgumentNullException(nameof(paper));
            _world = new List<Vec2>(world).ToArray();
            _paper = new List<Vec2>(paper).ToArray();
            if (_world.Length < 3) throw new ArgumentException("facet needs at least 3 points", nameof(world));
            if (_world.Length != _paper.Length) throw new ArgumentException("world/paper point counts differ", nameof(paper));
            World = new ReadOnlyCollection<Vec2>(_world);
            Paper = new ReadOnlyCollection<Vec2>(_paper);
            Layer = layer;
            FrontUp = frontUp;
            Area = Math.Abs(SignedArea());
        }

        /// <summary>Position on the table (paper units).</summary>
        public IReadOnlyList<Vec2> World { get; }

        /// <summary>The matching points on the unfolded sheet.</summary>
        public IReadOnlyList<Vec2> Paper { get; }

        /// <summary>Global stacking order, 0 = bottom.</summary>
        public int Layer { get; }

        /// <summary>Coloured (front) side faces up. The initial sheet is false (white side up).</summary>
        public bool FrontUp { get; }

        /// <summary>|<see cref="SignedArea"/>|.</summary>
        public double Area { get; }

        /// <summary>Signed area of the world polygon (counter-clockwise positive). Python <c>_area(f.w)</c>.</summary>
        public double SignedArea() => Polygon.SignedArea(_world);

        /// <summary>Python <c>Facet.contains</c>: every edge has Cross(b − a, p − a) ≥ −eps (assumes CCW world).</summary>
        public bool Contains(Vec2 p, double eps = 1e-6)
        {
            int n = _world.Length;
            for (int i = 0; i < n; i++)
            {
                var a = _world[i];
                var b = _world[(i + 1) % n];
                if (!(Vec2.Cross(b - a, p - a) >= -eps)) return false;
            }
            return true;
        }

        /// <summary>Python <c>Facet.strictly</c>: every edge has Cross(b − a, p − a) / |b − a| &gt; eps.</summary>
        public bool StrictlyContains(Vec2 p, double eps = 1e-6)
        {
            int n = _world.Length;
            for (int i = 0; i < n; i++)
            {
                var a = _world[i];
                var b = _world[(i + 1) % n];
                if (!(Vec2.Cross(b - a, p - a) / Vec2.Distance(a, b) > eps)) return false;
            }
            return true;
        }

        /// <summary>Python <c>to_world</c>: affine map defined by the first three vertices (paper → world).</summary>
        public Vec2 PaperToWorld(Vec2 paperPoint)
        {
            Vec2 a = _paper[0], b = _paper[1], c = _paper[2];
            Vec2 A = _world[0], B = _world[1], C = _world[2];
            var e1 = b - a;
            var e2 = c - a;
            double det = Vec2.Cross(e1, e2);
            var d = paperPoint - a;
            double s = Vec2.Cross(d, e2) / det;
            double t = Vec2.Cross(e1, d) / det;
            return new Vec2(A.X + s * (B.X - A.X) + t * (C.X - A.X), A.Y + s * (B.Y - A.Y) + t * (C.Y - A.Y));
        }

        internal Vec2[] WorldArray => _world;
        internal Vec2[] PaperArray => _paper;
    }
}
