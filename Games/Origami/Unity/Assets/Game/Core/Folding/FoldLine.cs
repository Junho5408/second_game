using Origami.Core.Geometry;

namespace Origami.Core.Folding
{
    /// <summary>
    /// Fold line = perpendicular bisector of grab–drop (C-103, Python <c>Line</c>). <see cref="Normal"/> points from the
    /// moving side (grab, negative) to the staying side (positive).
    /// </summary>
    public readonly struct FoldLine
    {
        /// <param name="normal">Stored normalized ((0,0) stays (0,0)).</param>
        public FoldLine(Vec2 point, Vec2 normal)
        {
            Point = point;
            Normal = normal.Normalized;
        }

        public Vec2 Point { get; }
        public Vec2 Normal { get; }

        /// <summary>Line direction = <see cref="Normal"/>.Perp.</summary>
        public Vec2 Direction => Normal.Perp;

        /// <summary>Dot(p − Point, Normal); values with |v| &lt; <see cref="FoldPlanner.SideEps"/> become 0 (Python <c>side</c>).</summary>
        public double Side(Vec2 p)
        {
            double v = RawSide(p);
            return System.Math.Abs(v) < FoldPlanner.SideEps ? 0.0 : v;
        }

        /// <summary>Dot(p − Point, Normal) without the zero clamp.</summary>
        public double RawSide(Vec2 p) => Vec2.Dot(p - Point, Normal);

        /// <summary>Mirror image of p across the line (Python <c>refl</c>).</summary>
        public Vec2 Reflect(Vec2 p)
        {
            double s = RawSide(p);
            return new Vec2(p.X - 2 * s * Normal.X, p.Y - 2 * s * Normal.Y);
        }

        /// <summary>|<see cref="RawSide"/>|.</summary>
        public double Distance(Vec2 p) => System.Math.Abs(RawSide(p));

        /// <summary>
        /// Point = (g + d) / 2, Normal = (d − g) normalized. If grab == drop the normal is (0,0) (degenerate line; callers
        /// reject such drags first — <see cref="FoldPlanner"/> with DragTooShort, <see cref="LineOnPaper.TryGet"/> with false).
        /// </summary>
        public static FoldLine FromGrabDrop(Vec2 grab, Vec2 drop) =>
            new FoldLine(new Vec2((grab.X + drop.X) / 2, (grab.Y + drop.Y) / 2), drop - grab);
    }
}
